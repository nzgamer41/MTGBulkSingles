using MTGBulkSingles.Classes;
using MTGBulkSingles.Functions;
using Velopack;

namespace MTGBulkSingles
{
    internal class Program
    {
        static Settings programSettings;
        static void Main(string[] args)
        {
            VelopackApp.Build().Run();
            if (File.Exists("settings.json"))
            {
                programSettings = SettingsHandler.ReadFromJsonFile<Settings>("settings.json");
            }
            else
            {
                programSettings = new Settings();
                SettingsHandler.WriteToJsonFile("settings.json", programSettings);
            }
            Console.WriteLine("MTG Bulk Singles - 2025 nzgamer41");
            Console.WriteLine("Checking for updates...");
            try
            {
                Task.Run(() => UpdateApp()).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error checking for updates: " + ex.Message);
            }
            if (args.Length != 1 || !File.Exists(args[0]))
            {
                Console.WriteLine("Usage: MTGBulkSingles.exe <path to decklist>");
                Console.WriteLine("Ensure your deck list is in MTG Arena format!");
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
                return;
            }
            if (!programSettings.useDelay)
            {
                Console.Clear();
                Console.WriteLine("WARNING!");
                Console.WriteLine("You have chosen to not use a delay between requests. This may result in your IP being banned from the MTG Singles API.");
                Console.WriteLine("It is recommended to use a delay to avoid this. If you continue, you do so at your own risk. You can change this by editing settings.json in a text editor and changing useDelay to true.");
                Console.WriteLine("Press Enter to continue or Ctrl+C to exit.");
                Console.ReadLine();
                Console.Clear();
            }

            if (programSettings.printAllVersions)
            {
                Console.WriteLine("You have chosen to print all versions of the cards. This may result in a lot of output. If you only want the cheapest version, set printAllVersions to false in settings.json. Please note a list with store links will NOT be saved.");
            }
            else
            {
                Console.WriteLine("You have chosen to print only the cheapest version of the cards.");
            }

            var cardNames = DeckParser.ParseDecklist(args[0]);
            List<MTGSCardListing> foundCards = new List<MTGSCardListing>();
            var allListingsByCard = new Dictionary<string, List<MTGSCardListing>>();

            Console.WriteLine("Fetching Card Kingdom reference prices...");
            var ckApi = new CardKingdomApi();
            try
            {
                Task.Run(() => ckApi.FetchPriceListAsync()).GetAwaiter().GetResult();
                Console.WriteLine(ckApi.ExchangeRateLoaded
                    ? $"Card Kingdom prices loaded. Rate: 1 USD = {ckApi.UsdToNzdRate:F4} NZD"
                    : "Card Kingdom prices loaded. (Warning: exchange rate unavailable, CK prices shown in USD)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load Card Kingdom prices: {ex.Message}");
            }
            string ckCurrencyLabel = ckApi.ExchangeRateLoaded ? "NZD" : "USD";
            string ckPrefix = ckApi.ExchangeRateLoaded ? "$" : "US$";

            MTGSApi mtgs = new MTGSApi();
            foreach (var cardName in cardNames)
            {
                Console.WriteLine($"Fetching listings for: {cardName}");
                Console.WriteLine(programSettings.printAllVersions ? "Fetching all versions of the card..." : "Fetching only the cheapest version of the card...");

                var cardListings = Task.Run(() => mtgs.GetCardListingsAsync(cardName, programSettings.matchNameExactly, programSettings.includeArtCards)).GetAwaiter().GetResult();
                allListingsByCard[cardName] = cardListings;

                if (cardListings.Count == 0)
                {
                    Console.WriteLine($"No listings found for card: {cardName}. Please ensure the card name is spelled properly and in English.");
                }
                else if (programSettings.printAllVersions)
                {
                    var ckPrice = ckApi.GetPriceNzd(cardName);
                    var ckRef = ckPrice.HasValue ? $"CK: {ckPrefix}{ckPrice.Value:F2} {ckCurrencyLabel}" : "CK: N/A";
                    foreach (var listing in cardListings)
                        Console.WriteLine($"Store: {listing.Store}, Price: {listing.PriceRaw}, Set: {listing.SetName}, Title: {listing.Title}, {ckRef}, URL: {listing.Url}");
                }
                else
                {
                    var cheapest = cardListings.First();
                    var ckPrice = ckApi.GetPriceNzd(cardName);
                    var ckRef = ckPrice.HasValue ? $"CK: {ckPrefix}{ckPrice.Value:F2} {ckCurrencyLabel}" : "CK: N/A";
                    Console.WriteLine($"Store: {cheapest.Store}, Price: {cheapest.PriceRaw}, Set: {cheapest.SetName}, Title: {cheapest.Title}, {ckRef}, URL: {cheapest.Url}");
                    foundCards.Add(cheapest);
                }

                if (programSettings.useDelay)
                {
                    Console.WriteLine($"Waiting {programSettings.delayMilliseconds / 1000} seconds before next request...");
                    System.Threading.Thread.Sleep(programSettings.delayMilliseconds);
                }
                else
                {
                    Console.WriteLine("Skipping delay as per settings. USE AT YOUR OWN RISK! I TAKE NO RESPONSIBILITY FOR IP BANS ETC");
                }
            }
            if (foundCards.Count > 0)
            {
                //Write found cards to a CSV file with card name, price, and link to store, using the decklist file name as the file name but with _found appended to it.
                string outputFileName = Path.GetFileNameWithoutExtension(args[0]) + "_found.csv";
                using (StreamWriter writer = new StreamWriter(outputFileName))
                {
                    writer.WriteLine("Card Name,Price,Store,Set,Title,URL");
                    foreach (var card in foundCards)
                    {
                        writer.WriteLine($"{card.Title},{card.PriceRaw},{card.Store},{card.SetName},{card.Title},{card.Url}");
                    }
                }
                Console.WriteLine($"Found cards written to {outputFileName}");
            }
            var cardMarketAvg = allListingsByCard
                .Where(kvp => kvp.Value.Count > 0)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Average(l => l.Price));

            var flatListings = allListingsByCard
                .SelectMany(kvp => kvp.Value.Select(l => (CardName: kvp.Key, Listing: l)))
                .ToList();

            var storeSummary = flatListings
                .GroupBy(x => x.Listing.Store)
                .Select(g =>
                {
                    var cardGroups = g.GroupBy(x => x.CardName).ToList();
                    var minSpendTotal = cardGroups.Sum(cg => cg.Min(x => x.Listing.Price));
                    var priceIndex = cardGroups
                        .Where(cg => cardMarketAvg.ContainsKey(cg.Key))
                        .Average(cg => cg.Min(x => x.Listing.Price) / cardMarketAvg[cg.Key]);
                    var ckEquiv = cardGroups.Sum(cg => ckApi.GetPriceNzd(cg.Key) ?? 0m);
                    return new
                    {
                        Store = g.Key,
                        CardCount = cardGroups.Count,
                        MinSpendTotal = minSpendTotal,
                        PriceIndex = priceIndex,
                        CkEquiv = ckEquiv
                    };
                })
                .OrderByDescending(x => x.CardCount)
                .ThenBy(x => x.MinSpendTotal)
                .ToList();

            string baseName = Path.GetFileNameWithoutExtension(args[0]);

            if (flatListings.Count > 0)
            {
                string allListingsFile = baseName + "_all_listings.csv";
                using (var w = new StreamWriter(allListingsFile))
                {
                    w.WriteLine("Card Name,Store,Price,Set,URL");
                    foreach (var (cardName, listing) in flatListings.OrderBy(x => x.CardName).ThenBy(x => x.Listing.Price))
                        w.WriteLine($"{CsvEscape(cardName)},{CsvEscape(listing.Store)},{listing.Price:F2},{CsvEscape(listing.SetName)},{CsvEscape(listing.Url)}");
                }
                Console.WriteLine($"All listings written to {allListingsFile}");
            }

            if (storeSummary.Count > 0)
            {
                string storeBreakdownFile = baseName + "_store_breakdown.csv";
                using (var w = new StreamWriter(storeBreakdownFile))
                {
                    w.WriteLine($"Store,Card Name,Price,Set,URL,vs Avg,CK ({ckCurrencyLabel}),vs CK");
                    foreach (var s in storeSummary)
                    {
                        var storeCardListings = flatListings
                            .Where(x => x.Listing.Store == s.Store)
                            .GroupBy(x => x.CardName)
                            .Select(cg => (CardName: cg.Key, Listing: cg.OrderBy(x => x.Listing.Price).First().Listing))
                            .OrderBy(x => x.CardName);
                        foreach (var (cardName, listing) in storeCardListings)
                        {
                            var vsAvgPct = cardMarketAvg.TryGetValue(cardName, out var avg) && avg > 0
                                ? $"{(listing.Price / avg - 1m) * 100m:+0.#;-0.#}%"
                                : "";
                            var ckCsvPrice = ckApi.GetPriceNzd(cardName);
                            var ckCsvStr = ckCsvPrice.HasValue ? ckCsvPrice.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "";
                            var vsCkCsvStr = ckCsvPrice.HasValue && ckApi.ExchangeRateLoaded
                                ? $"{(listing.Price / ckCsvPrice.Value - 1m) * 100m:+0.#;-0.#}%"
                                : "";
                            w.WriteLine($"{CsvEscape(s.Store)},{CsvEscape(cardName)},{listing.Price:F2},{CsvEscape(listing.SetName)},{CsvEscape(listing.Url)},{vsAvgPct},{ckCsvStr},{vsCkCsvStr}");
                        }
                    }
                }
                Console.WriteLine($"Store breakdown written to {storeBreakdownFile}");

                Console.WriteLine();
                var ckEquivHeader = $"CK Equiv ({ckCurrencyLabel})";
                Console.WriteLine($"=== Store Summary ({cardNames.Count} cards searched) ===");
                Console.WriteLine($"{"Store",-30} {"Coverage",10}   {"Min Spend",10}   {"vs Avg",8}   {ckEquivHeader,-16}   {"vs CK",7}");
                Console.WriteLine(new string('-', 96));
                foreach (var s in storeSummary)
                {
                    var vsAvg = (s.PriceIndex - 1m) * 100m;
                    var vsAvgStr = vsAvg >= 0m ? $"+{vsAvg:F0}%" : $"{vsAvg:F0}%";
                    var ckStr = s.CkEquiv > 0 ? $"{ckPrefix}{s.CkEquiv:F2}" : "N/A";
                    var vsCkStr = s.CkEquiv > 0 && ckApi.ExchangeRateLoaded
                        ? $"{(s.MinSpendTotal / s.CkEquiv - 1m) * 100m:+0;-0}%"
                        : "N/A";
                    Console.WriteLine($"{s.Store,-30} {s.CardCount,4}/{cardNames.Count,-5}   {s.MinSpendTotal,8:C}   {vsAvgStr,7}   {ckStr,-16}   {vsCkStr,7}");
                }
            }

            if (storeSummary.Count >= 2)
            {
                var storeNames = storeSummary.Select(s => s.Store).ToArray();
                var allTopCombos = new Dictionary<int, List<(string[] Stores, int Coverage, decimal TotalCost, List<(string CardName, string Store, decimal Price, string Url)> Cards)>>();

                string comboFile = baseName + "_combinations.csv";
                using (var w = new StreamWriter(comboFile))
                {
                    w.WriteLine($"Combination Size,Rank,Stores,Coverage,Total Cost,Card Name,Store,Price,URL,CK Price ({ckCurrencyLabel}),vs CK");
                    foreach (int size in new[] { 2, 3, 4 }.Where(s => s <= storeNames.Length))
                    {
                        var best = GetCombinations(storeNames, size)
                            .Select(combo =>
                            {
                                var comboSet = new HashSet<string>(combo);
                                decimal cost = 0;
                                var cards = new List<(string CardName, string Store, decimal Price, string Url)>();
                                foreach (var (card, listings) in allListingsByCard.Where(kvp => kvp.Value.Count > 0))
                                {
                                    var cheapest = listings.Where(l => comboSet.Contains(l.Store)).OrderBy(l => l.Price).FirstOrDefault();
                                    if (cheapest != null)
                                    {
                                        cost += cheapest.Price;
                                        cards.Add((card, cheapest.Store, cheapest.Price, cheapest.Url));
                                    }
                                }
                                return (Stores: combo, Coverage: cards.Count, TotalCost: cost, Cards: cards);
                            })
                            .OrderByDescending(x => x.Coverage)
                            .ThenBy(x => x.TotalCost)
                            .Take(5)
                            .ToList();

                        allTopCombos[size] = best.Take(3).Select(b => (b.Stores, b.Coverage, b.TotalCost, b.Cards)).ToList();

                        Console.WriteLine();
                        Console.WriteLine($"  Best {size}-store combinations:");
                        for (int i = 0; i < best.Count; i++)
                        {
                            var b = best[i];
                            Console.WriteLine($"  {i + 1}. {string.Join(" + ", b.Stores)}");
                            Console.WriteLine($"     Coverage: {b.Coverage}/{cardNames.Count}   Cost: {b.TotalCost:C}");
                            string storesCell = CsvEscape(string.Join("; ", b.Stores));
                            string coverageCell = $"{b.Coverage}/{cardNames.Count}";
                            foreach (var card in b.Cards.OrderBy(c => c.CardName))
                            {
                                var ckCsvPrice = ckApi.GetPriceNzd(card.CardName);
                                var ckCsvStr = ckCsvPrice.HasValue ? ckCsvPrice.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "";
                                var vsCkCsvStr = ckCsvPrice.HasValue && ckApi.ExchangeRateLoaded
                                    ? $"{(card.Price / ckCsvPrice.Value - 1m) * 100m:+0.#;-0.#}%"
                                    : "";
                                w.WriteLine($"{size},{i + 1},{storesCell},{coverageCell},{b.TotalCost:F2},{CsvEscape(card.CardName)},{CsvEscape(card.Store)},{card.Price:F2},{CsvEscape(card.Url)},{ckCsvStr},{vsCkCsvStr}");
                            }
                        }
                    }
                }
                Console.WriteLine($"Combinations written to {comboFile}");

                foreach (var (size, topCombos) in allTopCombos.OrderBy(kvp => kvp.Key))
                {
                    Console.WriteLine();
                    Console.WriteLine($"=== Top {topCombos.Count} Recommended {size}-Store Combinations ===");
                    for (int i = 0; i < topCombos.Count; i++)
                    {
                        var b = topCombos[i];
                        var storeCardCounts = b.Cards.GroupBy(c => c.Store).ToDictionary(g => g.Key, g => g.Count());
                        Console.WriteLine();
                        Console.WriteLine($"  #{i + 1}  {string.Join(" + ", b.Stores.Select(s => $"{s} ({storeCardCounts.GetValueOrDefault(s, 0)})"))}");
                        Console.WriteLine($"       Coverage: {b.Coverage}/{cardNames.Count}   Total: {b.TotalCost:C}");
                        Console.WriteLine();
                        var ckColHeader = $"CK ({ckCurrencyLabel})";
                        Console.WriteLine($"       {"Card",-36} {"Store",-28} {"Price",8}   {"vs Avg",7}   {ckColHeader,-12}   {"vs CK",7}");
                        Console.WriteLine($"       {new string('-', 108)}");
                        var comboStoreSet = new HashSet<string>(b.Stores);
                        foreach (var card in b.Cards.OrderBy(c => c.CardName))
                        {
                            var hasAvg = cardMarketAvg.TryGetValue(card.CardName, out var avg) && avg > 0;
                            var vsAvgPct = hasAvg ? (card.Price / avg - 1m) * 100m : 0m;
                            var vsAvgStr = hasAvg ? $"{vsAvgPct:+0;-0}%" : "";
                            var ckCardPrice = ckApi.GetPriceNzd(card.CardName);
                            var ckCardStr = ckCardPrice.HasValue ? $"{ckPrefix}{ckCardPrice.Value:F2}" : "N/A";
                            var vsCkStr = ckCardPrice.HasValue && ckApi.ExchangeRateLoaded
                                ? $"{(card.Price / ckCardPrice.Value - 1m) * 100m:+0;-0}%"
                                : "";
                            Console.WriteLine($"       {card.CardName,-36} {card.Store,-28} {card.Price,8:C}   {vsAvgStr,7}   {ckCardStr,-12}   {vsCkStr,7}");
                            if (vsAvgPct > 150m && allListingsByCard.TryGetValue(card.CardName, out var allCardListings))
                            {
                                var cheapestOutside = allCardListings.Where(l => !comboStoreSet.Contains(l.Store)).OrderBy(l => l.Price).FirstOrDefault();
                                if (cheapestOutside != null)
                                    Console.WriteLine($"         ^ cheapest outside this combo: {cheapestOutside.Store} at {cheapestOutside.Price:C} (saves {card.Price - cheapestOutside.Price:C})");
                            }
                        }
                        var ckComboTotal = b.Cards.Sum(c => ckApi.GetPriceNzd(c.CardName) ?? 0m);
                        if (ckComboTotal > 0)
                            Console.WriteLine($"\n       CK equivalent total: {ckPrefix}{ckComboTotal:F2} {ckCurrencyLabel}");

                        var coveredSet = new HashSet<string>(b.Cards.Select(c => c.CardName));
                        var notCovered = cardNames.Where(n => !coveredSet.Contains(n)).OrderBy(n => n).ToList();
                        if (notCovered.Count > 0)
                        {
                            Console.WriteLine();
                            Console.WriteLine($"       Not covered ({notCovered.Count}):");
                            foreach (var name in notCovered)
                            {
                                var availableElsewhere = allListingsByCard.TryGetValue(name, out var lst) && lst.Count > 0;
                                Console.WriteLine($"         - {name}{(availableElsewhere ? "" : "  (no listings found anywhere)")}");
                            }
                        }
                    }
                }
            }

            decimal ckRatioThreshold = ckApi.ExchangeRateLoaded ? 1.5m : 2.0m;
            var ckCandidates = allListingsByCard
                .Where(kvp => kvp.Value.Count > 0)
                .Select(kvp =>
                {
                    var cheapestLocal = kvp.Value.Min(l => l.Price);
                    var ckPrice = ckApi.GetPriceNzd(kvp.Key);
                    return new { CardName = kvp.Key, LocalPrice = cheapestLocal, CkPrice = ckPrice };
                })
                .Where(x => x.CkPrice.HasValue && x.CkPrice.Value > 0 && x.LocalPrice / x.CkPrice.Value > ckRatioThreshold)
                .OrderByDescending(x => x.LocalPrice / x.CkPrice!.Value)
                .ToList();

            if (ckCandidates.Count > 0)
            {
                var ckCkHeader = $"CK ({ckCurrencyLabel})";
                Console.WriteLine();
                Console.WriteLine($"=== Card Kingdom Value Analysis ===");
                Console.WriteLine($"The following cards may be worth ordering from Card Kingdom (local price is >{ckRatioThreshold:F1}x CK {ckCurrencyLabel} equivalent).");
                Console.WriteLine($"Note: factor in CK shipping costs before deciding.");
                Console.WriteLine();
                Console.WriteLine($"  {"Card",-36} {"Local (NZD)",12}   {ckCkHeader,12}   {"Ratio",6}");
                Console.WriteLine($"  {new string('-', 74)}");
                foreach (var c in ckCandidates)
                {
                    var ratio = c.LocalPrice / c.CkPrice!.Value;
                    Console.WriteLine($"  {c.CardName,-36} {c.LocalPrice,10:C}   {ckPrefix}{c.CkPrice.Value,9:F2}   {ratio,5:F1}x");
                }

                var ckCandidatesTotalLocal = ckCandidates.Sum(c => c.LocalPrice);
                var ckCandidatesTotalCk = ckCandidates.Sum(c => c.CkPrice!.Value);
                Console.WriteLine();
                Console.WriteLine($"  {ckCandidates.Count} card(s) flagged. Local total: {ckCandidatesTotalLocal:C}   CK total: {ckPrefix}{ckCandidatesTotalCk:F2} {ckCurrencyLabel}");
            }

            if (ckApi.ExchangeRateLoaded)
            {
                var buyLocalCards = allListingsByCard
                    .Select(kvp =>
                    {
                        var ckNzd = ckApi.GetPriceNzd(kvp.Key);
                        if (!ckNzd.HasValue || ckNzd.Value <= 2.0m) return null;

                        var cutoff = ckNzd.Value * 0.75m;
                        var qualifyingStores = kvp.Value
                            .GroupBy(l => l.Store)
                            .Select(g => (Store: g.Key, Price: g.Min(l => l.Price)))
                            .Where(s => s.Price < cutoff)
                            .OrderBy(s => s.Price)
                            .ToList();

                        if (qualifyingStores.Count == 0) return null;
                        return new { CardName = kvp.Key, CkNzd = ckNzd.Value, Stores = qualifyingStores };
                    })
                    .Where(x => x != null)
                    .OrderByDescending(x => x!.CkNzd)
                    .ToList();

                if (buyLocalCards.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine($"=== Buy Locally (CK >$2.00 NZD, NZ store >25% cheaper than CK) ===");
                    Console.WriteLine();
                    Console.WriteLine($"  {"Card",-36} {"CK (NZD)",10}   Qualifying NZ Stores");
                    Console.WriteLine($"  {new string('-', 80)}");
                    foreach (var c in buyLocalCards)
                    {
                        var storeStr = string.Join("   ", c!.Stores.Select(s => $"{s.Store}: {s.Price:C}"));
                        Console.WriteLine($"  {c.CardName,-36} {c.CkNzd,8:F2}   {storeStr}");
                    }
                }
            }

            Console.WriteLine("All listings fetched! Press Enter to exit.");
            Console.ReadLine();
        }

        private static IEnumerable<string[]> GetCombinations(string[] items, int size)
        {
            var indices = Enumerable.Range(0, size).ToArray();
            while (true)
            {
                yield return indices.Select(i => items[i]).ToArray();
                int pos = size - 1;
                while (pos >= 0 && indices[pos] == pos + items.Length - size) pos--;
                if (pos < 0) yield break;
                indices[pos]++;
                for (int j = pos + 1; j < size; j++) indices[j] = indices[j - 1] + 1;
            }
        }

        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }

        private static async Task UpdateApp()
        {
            var mgr = new UpdateManager("");
            // check for new version
            var newVersion = await mgr.CheckForUpdatesAsync();
            if (newVersion == null)
                return; // no update available

            // download new version
            await mgr.DownloadUpdatesAsync(newVersion);

            // install new version and restart app
            mgr.ApplyUpdatesAndRestart(newVersion);
        }
    }
}
