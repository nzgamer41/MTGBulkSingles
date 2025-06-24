using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MTGBulkSingles.Classes
{
    internal class MTGSCardListing
    {
        public string Store { get; set; }
        public string TcgType { get; set; }
        public string SetName { get; set; }
        public string Title { get; set; }
        [JsonIgnore] // Ignore this during deserialization
        public decimal Price { get; set; }

        [JsonPropertyName("price")]
        public string PriceRaw
        {
            get => Price.ToString("C");
            set
            {
                if (decimal.TryParse(value?.Replace("$", ""), out var parsed))
                    Price = parsed;
                else
                    Price = 0;
            }
        }

        public string Url { get; set; }
        public string ImageUrl { get; set; }
        public List<string> Features { get; set; }
    }
}
