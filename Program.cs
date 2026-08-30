using MTGBulkSingles.Api;
using MTGBulkSingles.Classes;
using MTGBulkSingles.Functions;
using MTGBulkSingles.Reporting;
using Velopack;

namespace MTGBulkSingles
{
    internal class Program
    {
        static Settings programSettings = new();
        static async Task Main(string[] args)
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
                await UpdateApp();
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
                await ckApi.FetchPriceListAsync();
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

                var cardListings = await mtgs.GetCardListingsAsync(cardName, programSettings.matchNameExactly, programSettings.includeArtCards);
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
                    await Task.Delay(programSettings.delayMilliseconds);
                }
                else
                {
                    Console.WriteLine("Skipping delay as per settings. USE AT YOUR OWN RISK! I TAKE NO RESPONSIBILITY FOR IP BANS ETC");
                }
            }

            string baseName = Path.GetFileNameWithoutExtension(args[0]);
            ReportWriter.GenerateReports(cardNames, allListingsByCard, foundCards, ckApi, baseName);
        }

        private static async Task UpdateApp()
        {
            // TODO: empty feed URL below means update checks currently no-op; set the real release feed URL once known.
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
