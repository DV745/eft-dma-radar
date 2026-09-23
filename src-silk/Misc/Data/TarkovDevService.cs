using System.Net.Http.Json;
using eft_dma_radar.Silk.Config;

namespace eft_dma_radar.Silk.Misc.Data
{
    /// <summary>
    /// Fetches live flea market prices from json.tarkov.dev and patches them into <see cref="EftDataManager"/>.
    /// Refreshes every <see cref="RefreshInterval"/> while the application is running.
    /// </summary>
    internal static class TarkovDevService
    {
        private static readonly HttpClient _http = new(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        })
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        // Static JSON dump of the tarkov.dev item database. Updated periodically server-side;
        // no query body needed, this is a plain GET.
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(20);

        /// <summary>
        /// Gets the items endpoint URL based on the configured game mode.
        /// </summary>
        private static string GetItemsUrl()
        {
            var gameMode = SilkProgram.Config.TarkovPriceGameMode;
            return gameMode switch
            {
                TarkovGameMode.PVE => "https://json.tarkov.dev/pve/items",
                TarkovGameMode.Seasonal => "https://json.tarkov.dev/pvp-season/items",
                _ => "https://json.tarkov.dev/regular/items" // Default to Regular/PVP
            };
        }

        /// <summary>
        /// Starts a background loop that fetches live prices immediately, then repeats
        /// every <see cref="RefreshInterval"/>. Runs for the lifetime of the application.
        /// </summary>
        public static void Start()
        {
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await FetchAndApplyAsync();
                    await Task.Delay(RefreshInterval);
                }
            });
        }

        /// <summary>
        /// Fetches live prices from json.tarkov.dev and updates <see cref="EftDataManager.AllItems"/>.
        /// Runs once at startup on a background thread — failures are logged and silently ignored.
        /// </summary>
        public static async Task FetchAndApplyAsync()
        {
            try
            {
                var itemsUrl = GetItemsUrl();
                var gameModeStr = SilkProgram.Config.TarkovPriceGameMode switch
                {
                    TarkovGameMode.PVE => "PVE",
                    TarkovGameMode.Seasonal => "Seasonal",
                    _ => "Regular/PVP"
                };

                Log.WriteLine($"[TarkovDevService] Fetching live prices ({gameModeStr}) from json.tarkov.dev...");

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                using var response = await _http.GetAsync(itemsUrl, cts.Token);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<RegularItemsResponse>(
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                    cts.Token);

                if (result?.Data?.Items is not { Count: > 0 } items)
                {
                    Log.WriteLine("[TarkovDevService] No items returned from API.");
                    return;
                }

                int updated = 0;
                foreach (var (id, item) in items)
                {
                    if (string.IsNullOrEmpty(id))
                        continue;

                    if (!EftDataManager.AllItems.TryGetValue(id, out var existing))
                        continue;

                    // Use 24hr average for stability. If the item is flea-banned
                    // (types contains "noFlea"), set to 0 so GetDisplayPrice
                    // falls back to trader price.
                    bool isFleaBanned = item.Types?.Contains("noFlea") == true;
                    long fleaPrice = isFleaBanned ? 0 : (item.Avg24hPrice ?? 0);

                    // sellToTrader on this endpoint only ever contains actual traders
                    // (no "Flea Market" entry to filter out like the old GraphQL sellFor did).
                    long traderPrice = item.SellToTrader?
                        .Where(s => s.PriceRub.HasValue)
                        .Select(s => s.PriceRub!.Value)
                        .DefaultIfEmpty(0)
                        .Max() ?? 0;

                    existing.UpdateLivePrices(fleaPrice, traderPrice);
                    updated++;
                }

                Log.WriteLine($"[TarkovDevService] Updated prices for {updated} items.");
            }
            catch (Exception ex)
            {
                Log.WriteLine($"[TarkovDevService] Failed to fetch live prices: {ex.Message}");
            }
        }

        #region Response Models

        private sealed class RegularItemsResponse
        {
            public RegularItemsData? Data { get; set; }
        }

        private sealed class RegularItemsData
        {
            public Dictionary<string, RegularItem>? Items { get; set; }
        }

        private sealed class RegularItem
        {
            public long? Avg24hPrice { get; set; }
            public List<string>? Types { get; set; }
            public List<SellToTrader>? SellToTrader { get; set; }
        }

        private sealed class SellToTrader
        {
            public string? Trader { get; set; }
            public long? PriceRub { get; set; }
        }

        #endregion
    }
}