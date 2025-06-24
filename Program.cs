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
            string header = " /$$      /$$ /$$$$$$$$/$$$$$$                            \r\n| $$$    /$$$|__  $$__/$$__  $$                           \r\n| $$$$  /$$$$   | $$ | $$  \\__/                           \r\n| $$ $$/$$ $$   | $$ | $$ /$$$$                           \r\n| $$  $$$| $$   | $$ | $$|_  $$                           \r\n| $$\\  $ | $$   | $$ | $$  \\ $$                           \r\n| $$ \\/  | $$   | $$ |  $$$$$$/                           \r\n|__/     |__/   |__/  \\______/                            \r\n /$$$$$$$            /$$ /$$                              \r\n| $$__  $$          | $$| $$                              \r\n| $$  \\ $$ /$$   /$$| $$| $$   /$$                        \r\n| $$$$$$$ | $$  | $$| $$| $$  /$$/                        \r\n| $$__  $$| $$  | $$| $$| $$$$$$/                         \r\n| $$  \\ $$| $$  | $$| $$| $$_  $$                         \r\n| $$$$$$$/|  $$$$$$/| $$| $$ \\  $$                        \r\n|_______/  \\______/ |__/|__/  \\__/                        \r\n  /$$$$$$  /$$                     /$$                    \r\n /$$__  $$|__/                    | $$                    \r\n| $$  \\__/ /$$ /$$$$$$$   /$$$$$$ | $$  /$$$$$$   /$$$$$$$\r\n|  $$$$$$ | $$| $$__  $$ /$$__  $$| $$ /$$__  $$ /$$_____/\r\n \\____  $$| $$| $$  \\ $$| $$  \\ $$| $$| $$$$$$$$|  $$$$$$ \r\n /$$  \\ $$| $$| $$  | $$| $$  | $$| $$| $$_____/ \\____  $$\r\n|  $$$$$$/| $$| $$  | $$|  $$$$$$$| $$|  $$$$$$$ /$$$$$$$/\r\n \\______/ |__/|__/  |__/ \\____  $$|__/ \\_______/|_______/ \r\n                         /$$  \\ $$                        \r\n                        |  $$$$$$/                        \r\n                         \\______/  ";
            Console.Write(header);
            Console.WriteLine("2025 nzgamer41");
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
            Console.WriteLine("Decklist loaded! Press Enter to begin.");
            Console.ReadLine();

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

            MTGSApi mtgs = new MTGSApi();
            foreach (var cardName in cardNames)
            {
                Console.WriteLine($"Fetching listings for: {cardName}");

                switch (programSettings.printAllVersions)
                {
                    case true:
                        Console.WriteLine("Fetching all versions of the card...");
                        var cardListings = Task.Run(() => mtgs.GetCardListingsAsync(cardName, programSettings.matchNameExactly, programSettings.includeArtCards)).GetAwaiter().GetResult();
                        foreach (MTGSCardListing cardListing in cardListings)
                        {
                            Console.WriteLine($"Store: {cardListing.Store}, Price: {cardListing.PriceRaw}, Set: {cardListing.SetName}, Title: {cardListing.Title}, URL: {cardListing.Url}");
                        }
                        break;
                    case false:
                        Console.WriteLine("Fetching only the cheapest version of the card...");
                        var card = Task.Run(() => mtgs.GetCardListingAsync(cardName, programSettings.matchNameExactly, programSettings.includeArtCards)).GetAwaiter().GetResult();
                        Console.WriteLine($"Store: {card.Store}, Price: {card.PriceRaw}, Set: {card.SetName}, Title: {card.Title}, URL: {card.Url}");
                        foundCards.Add(card);
                        break;
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
            Console.WriteLine("All listings fetched! Press Enter to exit.");
            Console.ReadLine();
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
