using eft_dma_radar.Silk.DMA.ScatterAPI;

namespace eft_dma_radar.Silk.Tarkov.GameWorld.Exits
{
    /// <summary>
    /// Manages exfiltration points and transit points — reads from the ExfilController
    /// and TransitController, refreshes exfil status via scatter reads.
    /// Initialized lazily by <see cref="LocalGameWorld"/> on the registration worker thread.
    /// </summary>
    internal sealed class ExfilManager
    {
        private readonly ulong _lgw;
        private readonly string _mapId;
        private readonly bool _isPmc;
        private volatile IReadOnlyList<Exfil> _exfils = [];
        private volatile IReadOnlyList<TransitPoint> _transits = [];
        private int _initAttempts;
        private const int MaxInitAttempts = 20;
        private int _transitInitAttempts;
        private const int MaxTransitInitAttempts = 40;
        private DateTime _lastRefresh;
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(3);

        /// <summary>Current exfil snapshot (thread-safe read).</summary>
        public IReadOnlyList<Exfil> Exfils => _exfils;

        /// <summary>Current transit point snapshot (thread-safe read).</summary>
        public IReadOnlyList<TransitPoint> Transits => _transits;

        public ExfilManager(ulong localGameWorld, string mapId, bool isPmc)
        {
            _lgw = localGameWorld;
            _mapId = mapId;
            _isPmc = isPmc;
        }

        /// <summary>
        /// Refreshes exfil status via scatter reads. Initializes on first call (with retry).
        /// Called from the registration worker thread.
        /// </summary>
        public void Refresh()
        {
            var now = DateTime.UtcNow;
            if (now - _lastRefresh < RefreshInterval)
                return;
            _lastRefresh = now;

            var exfils = _exfils;

            // Initialize or retry if empty (ExfilController may not be ready immediately)
            if (exfils.Count == 0 && _initAttempts < MaxInitAttempts)
            {
                _initAttempts++;
                Init();
                exfils = _exfils;

                if (exfils.Count > 0)
                    Log.WriteLine($"[ExfilManager] Initialized {exfils.Count} exfils, {_transits.Count} transits on attempt {_initAttempts}");
            }

            // Transit points can come online later than exfils. Retry independently.
            if (_transits.Count == 0 && _transitInitAttempts < MaxTransitInitAttempts)
            {
                _transitInitAttempts++;
                try
                {
                    var transitList = new List<TransitPoint>();
                    ReadTransits(transitList);
                    if (transitList.Count > 0)
                    {
                        _transits = transitList;
                        Log.WriteLine($"[ExfilManager] Initialized {transitList.Count} transits on attempt {_transitInitAttempts}");
                    }
                }
                catch (Exception ex)
                {
                    Log.Write(AppLogLevel.Debug, $"[ExfilManager] Transit read error: {ex.Message}");
                }
            }

            if (exfils.Count == 0)
                return;

            // Scatter-read status for all exfils in a single DMA round-trip
            using var map = ScatterReadMap.Get();
            var round1 = map.AddRound();

            for (int ix = 0; ix < exfils.Count; ix++)
            {
                int i = ix;
                var exfil = exfils[i];
                round1[i].AddEntry<int>(0, exfil.StatusAddr);
                round1[i].Callbacks += index =>
                {
                    if (index.TryGetResult<int>(0, out var status))
                        exfil.Update(status);
                };
            }

            map.Execute();
        }

        /// <summary>
        /// Reads exfil arrays from the ExfilController — PMC/Scav array + Secret array.
        /// Also reads transit points from the TransitController dictionary.
        /// </summary>
        private void Init()
        {
            var list = new List<Exfil>();

            try
            {
                if (!Memory.TryReadPtr(_lgw + Offsets.ClientLocalGameWorld.ExfilController, out var exfilController, false)
                    || exfilController == 0)
                {
                    return;
                }

                // Read PMC or Scav exfil array
                var arrayOffset = _isPmc
                    ? Offsets.ExfilController.ExfiltrationPointArray
                    : Offsets.ExfilController.ScavExfiltrationPointArray;

                ReadExfilArray(exfilController + arrayOffset, _isPmc, list);

                // Read secret exfil array (always PMC-style)
                ReadExfilArray(exfilController + Offsets.ExfilController.SecretExfiltrationPointArray, true, list);

                _exfils = list;
            }
            catch (Exception ex)
            {
                Log.WriteLine($"[ExfilManager] Init failed: {ex.Message}");
                _exfils = list;
            }

            // Read transit points once (separate try-catch — transits are independent of exfils)
            if (_transits.Count == 0)
            {
                try
                {
                    var transitList = new List<TransitPoint>();
                    ReadTransits(transitList);
                    _transits = transitList;
                }
                catch (Exception ex)
                {
                    Log.Write(AppLogLevel.Debug, $"[ExfilManager] Transit read error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Reads an IL2CPP array of exfil pointers and creates <see cref="Exfil"/> objects.
        /// IL2CPP Array: [0x18] = count, [0x20..] = elements.
        /// </summary>
        private void ReadExfilArray(ulong arrayPtrAddr, bool isPmc, List<Exfil> list)
        {
            if (!Memory.TryReadPtr(arrayPtrAddr, out var arrayPtr, false) || arrayPtr == 0)
                return;

            var count = Memory.ReadValue<int>(arrayPtr + 0x18, false);
            if (count <= 0 || count > 64)
                return;

            for (int i = 0; i < count; i++)
            {
                try
                {
                    if (!Memory.TryReadPtr(arrayPtr + 0x20 + (ulong)(i * 8), out var exfilAddr, false)
                        || exfilAddr == 0)
                        continue;

                    var exfil = new Exfil(exfilAddr, isPmc, _mapId);

                    if (exfil.Position == Vector3.Zero)
                    {
                        Log.Write(AppLogLevel.Debug, $"[ExfilManager] Skipped exfil '{exfil.Name}' — zero position");
                        continue;
                    }

                    list.Add(exfil);
                    Log.Write(AppLogLevel.Debug, $"[ExfilManager] Loaded exfil: '{exfil.Name}' @ {exfil.Position}");
                }
                catch (Exception ex)
                {
                    Log.Write(AppLogLevel.Debug, $"[ExfilManager] Failed to read exfil[{i}]: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Reads transit points from the TransitController's IL2CPP dictionary.
        /// IL2CPP Dictionary layout:
        ///   +0x18: _entries (Entry[])
        ///   +0x20: _count (int)
        ///   Entry[] data starts at +0x20, each entry is 24 bytes.
        ///   Value pointer at offset 16 within each entry.
        /// </summary>
        private void ReadTransits(List<TransitPoint> list)
        {
            if (!Memory.TryReadPtr(_lgw + Offsets.ClientLocalGameWorld.TransitController, out var transitController, false)
                || transitController == 0)
            {
                Log.WriteLine($"[ExfilManager] TransitController is null (lgw=0x{_lgw:X}, offset=0x{Offsets.ClientLocalGameWorld.TransitController:X})");
                return;
            }

            // Locate the pointsById dictionary — scan if the hardcoded offset is stale
            if (!FindTransitDictionary(transitController, out var dictPtr, out var count))
                return;

            const uint IL2CPP_DICT_ENTRIES = 0x18;
            const uint IL2CPP_ENTRIES_START = 0x20;
            const int IL2CPP_ENTRY_SIZE = 24;
            const int IL2CPP_ENTRY_VALUE_OFFSET = 16;

            if (!Memory.TryReadPtr(dictPtr + IL2CPP_DICT_ENTRIES, out var entriesPtr, false) || entriesPtr == 0)
            {
                Log.WriteLine($"[ExfilManager] TransitPoints entries array is null (dict=0x{dictPtr:X})");
                return;
            }

            Log.WriteLine($"[ExfilManager] Reading {count} transit entries from dict=0x{dictPtr:X}");

            var entriesBase = entriesPtr + IL2CPP_ENTRIES_START;
            var staticSentinel = new Vector3(0f, -100f, 0f);

            for (int i = 0; i < count; i++)
            {
                try
                {
                    var entryAddr = entriesBase + (ulong)(i * IL2CPP_ENTRY_SIZE);
                    if (!Memory.TryReadPtr(entryAddr + IL2CPP_ENTRY_VALUE_OFFSET, out var transitAddr, false)
                        || transitAddr == 0)
                        continue;

                    var transit = new TransitPoint(transitAddr, _mapId);

                    // Skip transits with no resolved position (static data miss)
                    if (transit.Position == staticSentinel || transit.Position == Vector3.Zero)
                    {
                        Log.WriteLine($"[ExfilManager] Transit '{transit.Name}' has no resolved position — check DEFAULT_DATA.json for map '{_mapId}'");
                        continue;
                    }

                    list.Add(transit);
                    Log.WriteLine($"[ExfilManager] Loaded transit: '{transit.Name}' active={transit.IsActive} @ {transit.Position}");
                }
                catch (Exception ex)
                {
                    Log.WriteLine($"[ExfilManager] Failed to read transit[{i}]: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Tries the configured <see cref="Offsets.TransitController.TransitPoints"/> offset first.
        /// If that offset does not yield a valid IL2CPP Dictionary, scans every 8-byte aligned slot
        /// in the TransitController object (0x08…0x90) for the real dictionary pointer and logs
        /// the correct offset so the hardcoded constant can be updated.
        /// </summary>
        private static bool FindTransitDictionary(ulong transitController, out ulong dictPtr, out int count)
        {
            const uint IL2CPP_DICT_COUNT = 0x20;
            const uint IL2CPP_DICT_ENTRIES = 0x18;

            // Fast path — try the configured offset
            var configuredOffset = Offsets.TransitController.TransitPoints;
            if (Memory.TryReadPtr(transitController + configuredOffset, out dictPtr, false)
                && IsValidTransitDict(dictPtr, IL2CPP_DICT_COUNT, IL2CPP_DICT_ENTRIES, out count))
                return true;

            Log.WriteLine(
                $"[ExfilManager] Configured TransitPoints offset 0x{configuredOffset:X} invalid " +
                $"(ptr=0x{dictPtr:X}); scanning TransitController @ 0x{transitController:X}...");

            // Slow path — scan all aligned slots
            for (uint off = 0x08; off <= 0x90; off += 8)
            {
                if (off == configuredOffset) continue; // already tried
                if (!Memory.TryReadPtr(transitController + off, out var candidate, false)) continue;
                if (!IsValidTransitDict(candidate, IL2CPP_DICT_COUNT, IL2CPP_DICT_ENTRIES, out count)) continue;

                Log.WriteLine(
                    $"[ExfilManager] *** Found transit dict at offset 0x{off:X} " +
                    $"(ptr=0x{candidate:X}, count={count}). " +
                    $"Update Offsets.TransitController.TransitPoints = 0x{off:X}; ***");

                dictPtr = candidate;
                return true;
            }

            // Dump raw slots to help diagnose further
            count = 0;
            dictPtr = 0;
            var sb = new System.Text.StringBuilder();
            sb.Append($"[ExfilManager] TransitController scan — no valid dict found. Raw slots:");
            for (uint off = 0x08; off <= 0x90; off += 8)
            {
                if (Memory.TryReadValue<ulong>(transitController + off, out var raw, false))
                    sb.Append($"  +0x{off:X}=0x{raw:X}");
            }
            Log.WriteLine(sb.ToString());
            return false;
        }

        /// <summary>
        /// Returns true if <paramref name="ptr"/> looks like an initialised IL2CPP Dictionary
        /// with a plausible entry count (1..50) and a non-null entries array.
        /// </summary>
        private static bool IsValidTransitDict(ulong ptr, uint countOffset, uint entriesOffset, out int count)
        {
            count = 0;
            // Reject obvious sentinels / low addresses
            if (ptr < 0x10000 || ptr > 0x00007FFFFFFFFFFF) return false;
            if (!Memory.TryReadValue<int>(ptr + countOffset, out count, false)) return false;
            if (count <= 0 || count > 50) return false;
            if (!Memory.TryReadPtr(ptr + entriesOffset, out var entriesPtr, false) || entriesPtr == 0) return false;
            return true;
        }
    }
}
