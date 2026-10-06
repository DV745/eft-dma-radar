using System.Net;
using System.Net.Http;
using eft_dma_radar.Silk.Config;

namespace eft_dma_radar.Silk.Tarkov
{
    /// <summary>
    /// Fetches player profiles from tarkov.dev and caches them per-session.
    /// Background worker thread polls for pending lookups.
    /// </summary>
    internal static class ProfileService
    {
        #region Constants

        private const string TarkovDevBaseUrl = "https://players.tarkov.dev/";
        private const int PollIntervalMs = 500;
        private const int RequestDelayMs = 1500;
        private const int RateLimitPauseMs = 60_000;

        #endregion

        #region Helpers

        /// <summary>
        /// Gets the profile API endpoint URL based on the configured game mode.
        /// </summary>
        private static string GetProfileUrl(string accountId)
        {
            var gameMode = SilkProgram.Config.TarkovPriceGameMode;
            var modePath = gameMode switch
            {
                TarkovGameMode.PVE => "pve",
                TarkovGameMode.Seasonal => "pvp-season",
                _ => "profile" // Default to Regular/PVP
            };
            return $"{TarkovDevBaseUrl}{modePath}/{accountId}.json";
        }

        #endregion

        #region State

        private static readonly HttpClient _http;
        private static readonly ConcurrentDictionary<string, ProfileData?> _profiles = new(StringComparer.OrdinalIgnoreCase);
        private static volatile bool _running;
        private static Thread? _worker;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        #endregion

        #region Init

        static ProfileService()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            };
            _http = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(15),
            };
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("eft-dma-radar/1.0");
        }

        /// <summary>
        /// Start the background worker. Called when the game process is found.
        /// </summary>
        public static void Start()
        {
            if (_running)
                return;
            _running = true;
            _worker = new Thread(Worker)
            {
                IsBackground = true,
                Name = "ProfileService",
                Priority = ThreadPriority.BelowNormal,
            };
            _worker.Start();
            Log.WriteLine("[ProfileService] Started.");
        }

        /// <summary>
        /// Stop the background worker. Called when the game process is lost.
        /// </summary>
        public static void Stop()
        {
            _running = false;
            _profiles.Clear();
            Log.WriteLine("[ProfileService] Stopped.");
        }

        #endregion

        #region Public API

        /// <summary>
        /// Register an account ID for profile lookup.
        /// If the account ID is already registered, this is a no-op.
        /// </summary>
        public static void Register(string accountId)
        {
            if (string.IsNullOrEmpty(accountId) || accountId == "0")
                return;
            // Add with null value = pending lookup
            _profiles.TryAdd(accountId, null);
        }

        /// <summary>
        /// Try to get a cached profile for the given account ID.
        /// </summary>
        public static bool TryGetProfile(string accountId, out ProfileData profile)
        {
            if (_profiles.TryGetValue(accountId, out var data) && data is not null)
            {
                profile = data;
                return true;
            }
            profile = null!;
            return false;
        }

        #endregion

        #region Worker

        private static void Worker()
        {
            while (_running)
            {
                try
                {
                    if (!SilkProgram.Config.ProfileLookups)
                    {
                        Thread.Sleep(PollIntervalMs);
                        continue;
                    }

                    string? pendingId = null;
                    foreach (var kvp in _profiles)
                    {
                        if (kvp.Value is null)
                        {
                            pendingId = kvp.Key;
                            break;
                        }
                    }

                    if (pendingId is null)
                    {
                        Thread.Sleep(PollIntervalMs);
                        continue;
                    }

                    var profile = FetchProfile(pendingId);
                    if (profile is not null)
                    {
                        _profiles[pendingId] = profile;
                        Log.WriteLine($"[ProfileService] Fetched profile for {pendingId}: {profile.Info?.Nickname ?? "?"}");
                    }
                    else
                    {
                        // Mark as empty so we don't retry indefinitely
                        _profiles[pendingId] = ProfileData.Empty;
                    }

                    Thread.Sleep(RequestDelayMs);
                }
                catch (Exception ex) when (ex is not ThreadInterruptedException)
                {
                    Log.WriteLine($"[ProfileService] Worker error: {ex.Message}");
                    Thread.Sleep(RequestDelayMs);
                }
            }
        }

        private static ProfileData? FetchProfile(string accountId)
        {
            try
            {
                var url = GetProfileUrl(accountId);
                using var response = _http.GetAsync(url).GetAwaiter().GetResult();

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    Log.WriteLine("[ProfileService] Rate limited (429), pausing...");
                    Thread.Sleep(RateLimitPauseMs);
                    return null;
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                    return null;

                response.EnsureSuccessStatusCode();

                var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var container = JsonSerializer.Deserialize<ProfileData>(json, _jsonOptions);
                return container;
            }
            catch (HttpRequestException ex)
            {
                Log.WriteLine($"[ProfileService] HTTP error for {accountId}: {ex.Message}");
                return null;
            }
            catch (TaskCanceledException)
            {
                Log.WriteLine($"[ProfileService] Timeout for {accountId}");
                return null;
            }
            catch (JsonException ex)
            {
                Log.WriteLine($"[ProfileService] JSON parse error for {accountId}: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region JSON Models

        /// <summary>
        /// Root profile data from tarkov.dev.
        /// </summary>
        public sealed class ProfileData
        {
            public static readonly ProfileData Empty = new();

            public ProfileInfo? Info { get; set; }
            public ProfileStats? PmcStats { get; set; }
            public ProfileStats? ScavStats { get; set; }
            public Dictionary<string, long>? Achievements { get; set; }

            /// <summary>Whether this profile has any meaningful data.</summary>
            [JsonIgnore]
            public bool HasData => Info is not null && Info.Nickname is not null;

            #region Computed Stats

            [JsonIgnore]
            public int Kills => GetOverallCounter(PmcStats, "Kills");

            [JsonIgnore]
            public int Deaths => GetOverallCounter(PmcStats, "Deaths");

            [JsonIgnore]
            public float KD => Deaths > 0 ? (float)Kills / Deaths : Kills;

            [JsonIgnore]
            public int Sessions => GetOverallCounter(PmcStats, "Sessions", "Pmc");

            [JsonIgnore]
            public int Survived => GetOverallCounter(PmcStats, "ExitStatus", "Survived", "Pmc");

            [JsonIgnore]
            public float SurvivedRate => Sessions > 0 ? (float)Survived / Sessions * 100f : 0f;

            [JsonIgnore]
            public int Hours => (int)((PmcStats?.Eft?.TotalInGameTime ?? 0) / 3600);

            [JsonIgnore]
            public int Level
            {
                get
                {
                    var experience = Info?.Experience ?? 0;

                    // XP lookup table - find the level based on cumulative experience
                    var xpTable = new[] 
                    {
                        (xp: 0, level: 1), (xp: 1000, level: 2), (xp: 4017, level: 3), (xp: 8432, level: 4),
                        (xp: 14256, level: 5), (xp: 21477, level: 6), (xp: 30023, level: 7), (xp: 39936, level: 8),
                        (xp: 51204, level: 9), (xp: 63723, level: 10), (xp: 77563, level: 11), (xp: 93279, level: 12),
                        (xp: 115302, level: 13), (xp: 143253, level: 14), (xp: 177337, level: 15), (xp: 217885, level: 16),
                        (xp: 264432, level: 17), (xp: 316851, level: 18), (xp: 374400, level: 19), (xp: 437465, level: 20),
                        (xp: 505161, level: 21), (xp: 577978, level: 22), (xp: 656347, level: 23), (xp: 741150, level: 24),
                        (xp: 836066, level: 25), (xp: 944133, level: 26), (xp: 1066259, level: 27), (xp: 1199423, level: 28),
                        (xp: 1343743, level: 29), (xp: 1499338, level: 30), (xp: 1666320, level: 31), (xp: 1846664, level: 32),
                        (xp: 2043349, level: 33), (xp: 2258436, level: 34), (xp: 2492126, level: 35), (xp: 2750217, level: 36),
                        (xp: 3032022, level: 37), (xp: 3337766, level: 38), (xp: 3663831, level: 39), (xp: 4010401, level: 40),
                        (xp: 4377662, level: 41), (xp: 4765799, level: 42), (xp: 5182399, level: 43), (xp: 5627732, level: 44),
                        (xp: 6102063, level: 45), (xp: 6630287, level: 46), (xp: 7189442, level: 47), (xp: 7779792, level: 48),
                        (xp: 8401607, level: 49), (xp: 9055144, level: 50), (xp: 9740666, level: 51), (xp: 10458431, level: 52),
                        (xp: 11219666, level: 53), (xp: 12024744, level: 54), (xp: 12874041, level: 55), (xp: 13767918, level: 56),
                        (xp: 14706741, level: 57), (xp: 15690872, level: 58), (xp: 16720667, level: 59), (xp: 17816442, level: 60),
                        (xp: 19041492, level: 61), (xp: 20360945, level: 62), (xp: 21792266, level: 63), (xp: 23350443, level: 64),
                        (xp: 25098462, level: 65), (xp: 27100775, level: 66), (xp: 29581231, level: 67), (xp: 33028574, level: 68),
                        (xp: 37953544, level: 69), (xp: 44260543, level: 70), (xp: 51901513, level: 71), (xp: 60887711, level: 72),
                        (xp: 71228846, level: 73), (xp: 82933459, level: 74), (xp: 96009180, level: 75), (xp: 110462910, level: 76),
                        (xp: 126300949, level: 77), (xp: 144924572, level: 78), (xp: 172016256, level: 79)
                    };

                    // Find the highest level where cumulative XP <= player experience
                    for (int i = xpTable.Length - 1; i >= 0; i--)
                    {
                        if (experience >= xpTable[i].xp)
                            return xpTable[i].level;
                    }

                    return 1; // Default to level 1
                }
            }

            [JsonIgnore]
            public int AchievementCount => Achievements?.Count ?? 0;

            [JsonIgnore]
            public string AccountType => Info?.MemberCategory switch
            {
                2 => "EOD",
                // Unheard: memberCategory 2 + prestige >= 1, OR dedicated UH flag
                _ when Info?.MemberCategory == 2 && (Info?.PrestigeLevel ?? 0) >= 1 => "UH",
                _ => "STD"
            };

            private static int GetOverallCounter(ProfileStats? stats, params string[] keyParts)
            {
                var items = stats?.Eft?.OverAllCounters?.Items;
                if (items is null)
                    return 0;

                foreach (var item in items)
                {
                    if (item.Key is null || item.Key.Count != keyParts.Length)
                        continue;

                    bool match = true;
                    for (int i = 0; i < keyParts.Length; i++)
                    {
                        if (!string.Equals(item.Key[i], keyParts[i], StringComparison.OrdinalIgnoreCase))
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match)
                        return item.Value;
                }
                return 0;
            }

            #endregion
        }

        public sealed class ProfileInfo
        {
            public string? Nickname { get; set; }
            public int Experience { get; set; }
            public int MemberCategory { get; set; }
            public int PrestigeLevel { get; set; }
        }

        public sealed class ProfileStats
        {
            public ProfileStatsEft? Eft { get; set; }
        }

        public sealed class ProfileStatsEft
        {
            public long TotalInGameTime { get; set; }
            public OverAllCountersContainer? OverAllCounters { get; set; }
        }

        public sealed class OverAllCountersContainer
        {
            public List<OverAllCounterItem>? Items { get; set; }
        }

        public sealed class OverAllCounterItem
        {
            public List<string>? Key { get; set; }
            public int Value { get; set; }
        }

        #endregion
    }
}
