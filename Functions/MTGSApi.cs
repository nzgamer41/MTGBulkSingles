using MTGBulkSingles.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MTGBulkSingles.Functions
{
    internal class MTGSApi
    {
        public async Task<MTGSCardListing> GetCardListingAsync(string cardName, bool matchName = true, bool artCards = false)
        {
            string url = $"https://api.mtgsingles.co.nz/MtgSingle?query={Uri.EscapeDataString(cardName)}&page=1&pageSize=20&Country=1";

            using var http = new HttpClient();
            var json = await FetchJsonAsync(http, url);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var listings = JsonSerializer.Deserialize<List<MTGSCardListing>>(json, options)?
                                         .Where(c =>
                                         (!c.Title.Contains("Art Card", StringComparison.OrdinalIgnoreCase) || artCards) &&
                                             (
                                                 !matchName ||
                                                 IsExactCardMatch(c.Title, cardName)
                                             )
                                         )
                                         .OrderBy(c => c.Price) // sort by price ascending
                                         .ToList() ?? new();

            if (listings.Count > 0)
            return listings.First();
            else
            {
                Console.WriteLine("No listings found for card: " + cardName + ". Please ensure the card name is spelled properly and in English. ");
                return new MTGSCardListing
                {
                    Store = "No listings found",
                    Price = 0,
                    SetName = "N/A",
                    Title = cardName,
                    Url = "N/A",
                    ImageUrl = "N/A",
                    Features = new List<string>()
                };
            }
        }

        public async Task<List<MTGSCardListing>> GetCardListingsAsync(string cardName, bool matchName = true, bool artCards = false)
        {
            string url = $"https://api.mtgsingles.co.nz/MtgSingle?query={Uri.EscapeDataString(cardName)}&page=1&pageSize=20&Country=1";

            using var http = new HttpClient();
            var json = await FetchJsonAsync(http, url);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var listings = JsonSerializer.Deserialize<List<MTGSCardListing>>(json, options)?
                                         .Where(c => (!c.Title.Contains("Art Card", StringComparison.OrdinalIgnoreCase) || artCards) && (!matchName || IsExactCardMatch(c.Title, cardName)))
                                         .OrderBy(c => c.Price) // sort by price ascending
                                         .ToList() ?? new();

            return listings;
        }

        private static async Task<string> FetchJsonAsync(HttpClient http, string url)
        {
            http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
            http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
            http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-NZ,en;q=0.9");
            http.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://www.mtgsingles.co.nz/");

            Console.WriteLine($"[DEBUG] Request URL: {url}");
            Console.WriteLine($"[DEBUG] Request headers:");
            foreach (var header in http.DefaultRequestHeaders)
                Console.WriteLine($"[DEBUG]   {header.Key}: {string.Join(", ", header.Value)}");

            var response = await http.GetAsync(url);

            Console.WriteLine($"[DEBUG] Response status: {(int)response.StatusCode} {response.ReasonPhrase}");
            Console.WriteLine($"[DEBUG] Response headers:");
            foreach (var header in response.Headers)
                Console.WriteLine($"[DEBUG]   {header.Key}: {string.Join(", ", header.Value)}");

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[DEBUG] Response body: {body}");
                response.EnsureSuccessStatusCode();
            }

            return string.IsNullOrWhiteSpace(body) ? "[]" : body;
        }

        private static bool IsExactCardMatch(string title, string cardName)
        {
            if (title.Equals(cardName, StringComparison.OrdinalIgnoreCase))
                return true;

            if (title.StartsWith(cardName + " ", StringComparison.OrdinalIgnoreCase))
            {
                var suffix = title.Substring(cardName.Length).TrimStart();

                // Check if it starts with a single pair of brackets, like (Foil), (Alternate Art), etc.
                return suffix.StartsWith("(") && suffix.EndsWith(")");
            }

            return false;
        }
    }
}
