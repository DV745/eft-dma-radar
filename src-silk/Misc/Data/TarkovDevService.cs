using System.Net.Http.Json;

namespace eft_dma_radar.Silk.Misc.Data
{
    /// <summary>
    /// Fetches live flea market prices from api.tarkov.dev and patches them into <see cref="EftDataManager"/>.
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

        private const string GraphQlUrl = "https://api.tarkov.dev/graphql";
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(20);

        private static readonly string _query =
            """
            {
              items {
                id
                sellFor {
                  vendor { name }
                  priceRUB
                }
              }
            }
            """;

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
        /// Fetches live prices from tarkov.dev and updates <see cref="EftDataManager.AllItems"/>.
        /// Runs once at startup on a background thread — failures are logged and silently ignored.
        /// </summary>
        public static async Task FetchAndApplyAsync()
        {
            try
            {
                Log.WriteLine("[TarkovDevService] Fetching live prices from api.tarkov.dev...");

                var payload = new { query = _query };
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                using var response = await _http.PostAsJsonAsync(GraphQlUrl, payload, cts.Token);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<TarkovDevResponse>(
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                    cts.Token);

                if (result?.Data?.Items is not { Count: > 0 } items)
                {
                    Log.WriteLine("[TarkovDevService] No items returned from API.");
                    return;
                }

                int updated = 0;
                foreach (var item in items)
                {
                    if (string.IsNullOrEmpty(item.Id))
                        continue;

                    if (!EftDataManager.AllItems.TryGetValue(item.Id, out var existing))
                        continue;

                    // Use the live flea listing price. If there is no flea entry the item
                    // is flea-banned — set to 0 so GetDisplayPrice falls back to trader price.
                    long fleaPrice = item.SellFor?
                        .Where(s => s.Vendor?.Name == "Flea Market" && s.PriceRub.HasValue)
                        .Select(s => s.PriceRub!.Value)
                        .FirstOrDefault() ?? 0;

                    long traderPrice = item.SellFor?
                        .Where(s => s.Vendor?.Name != null && s.Vendor.Name != "Flea Market" && s.PriceRub.HasValue)
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

        private sealed class TarkovDevResponse
        {
            public TarkovDevData? Data { get; set; }
        }

        private sealed class TarkovDevData
        {
            public List<TarkovDevItem>? Items { get; set; }
        }

        private sealed class TarkovDevItem
        {
            public string? Id { get; set; }
            public List<SellFor>? SellFor { get; set; }
        }

        private sealed class SellFor
        {
            public Vendor? Vendor { get; set; }
            public long? PriceRub { get; set; }
        }

        private sealed class Vendor
        {
            public string? Name { get; set; }
        }

        #endregion
    }
}
