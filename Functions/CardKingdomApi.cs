using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MTGBulkSingles.Functions
{
    internal class CardKingdomApi
    {
        private Dictionary<string, decimal> _prices = new();

        public decimal UsdToNzdRate { get; private set; } = 0m;
        public bool ExchangeRateLoaded => UsdToNzdRate > 0;

        public async Task FetchPriceListAsync()
        {
            try { await FetchExchangeRateAsync(); } catch { }

            using var http = new HttpClient();
            http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

            var response = await http.GetAsync("https://api.cardkingdom.com/api/pricelist");
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var data = JsonSerializer.Deserialize<CkResponse>(body, options);

            _prices = (data?.Data ?? new())
                .Where(d => d.IsFoil == "false")
                .Select(d => new { d.Name, Price = decimal.TryParse(d.PriceRetail, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) ? p : -1m })
                .Where(d => d.Price > 0)
                .GroupBy(d => d.Name.ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.Min(d => d.Price));
        }

        private async Task FetchExchangeRateAsync()
        {
            using var http = new HttpClient();
            var response = await http.GetAsync("https://open.er-api.com/v6/latest/USD");
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<ErApiResponse>(body, options);

            if (result?.Rates?.TryGetValue("NZD", out var rate) == true && rate > 0)
                UsdToNzdRate = rate;
        }

        public decimal? GetPrice(string cardName) =>
            _prices.TryGetValue(cardName.ToLowerInvariant(), out var p) ? p : null;

        public decimal? GetPriceNzd(string cardName)
        {
            var usdPrice = GetPrice(cardName);
            if (!usdPrice.HasValue) return null;
            return ExchangeRateLoaded ? usdPrice.Value * UsdToNzdRate : usdPrice.Value;
        }
    }

    internal class CkResponse
    {
        public List<CkEntry> Data { get; set; } = new();
    }

    internal class CkEntry
    {
        public string Name { get; set; } = "";

        [JsonPropertyName("is_foil")]
        public string IsFoil { get; set; } = "false";

        [JsonPropertyName("price_retail")]
        public string PriceRetail { get; set; } = "0";
    }

    internal class ErApiResponse
    {
        public Dictionary<string, decimal> Rates { get; set; } = new();
    }
}
