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
            var json = await http.GetStringAsync(url);

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
            var json = await http.GetStringAsync(url);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var listings = JsonSerializer.Deserialize<List<MTGSCardListing>>(json, options)?
                                         .Where(c => (!c.Title.Contains("Art Card", StringComparison.OrdinalIgnoreCase) || artCards) && (c.Title == cardName || !matchName))
                                         .OrderBy(c => c.Price) // sort by price ascending
                                         .ToList() ?? new();

            return listings;
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
