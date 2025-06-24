# MTGBulkSingles

MTGBulkSingles is a .NET 8 console application for Magic: The Gathering players and collectors. It allows you to quickly fetch and compare prices for all cards in a decklist using the MTG Singles API. The tool supports both fetching the cheapest version of each card or all available versions, and outputs results to the console and CSV.

## Features

- Reads decklists in MTG Arena format.
- Fetches card prices and store links from the MTG Singles API.
- Optionally fetches all versions or only the cheapest version of each card.
- Outputs results to a CSV file for easy reference.
- Configurable settings via `settings.json` (created on first run).
- Optional delay between API requests to avoid rate limiting.

## Requirements

- .NET 8 SDK or newer
- Windows, Linux, or macOS

## Installation
          
1. Clone this repository:
    `git clone https://github.com/yourusername/MTGBulkSingles.git`
    `cd MTGBulkSingles`
    
2. Build the project:
    

    
## Usage

1. Prepare your decklist in MTG Arena format (plain text file).
2. Run the application from the command line:
    `MTGBulkSingles.exe <path to decklist>`

Example:
    `MTGBulkSingles.exe mydeck.txt`


3. On first run, a `settings.json` file will be created. You can edit this file to adjust options such as:
    - `printAllVersions`: `true` to print all versions, `false` for only the cheapest.
    - `useDelay`: `true` to add a delay between API requests (recommended).
    - `delayMilliseconds`: Number of milliseconds to wait between requests.
    - `matchNameExactly`: `true` to match card names exactly.
    - `includeArtCards`: `true` to include art cards in results.

4. The results will be displayed in the console and, if only the cheapest versions are fetched, saved as a CSV file named `<decklist>_found.csv`.

## Example settings.json

`{ "printAllVersions": false, "matchNameExactly": true, "useDelay": true, "includeArtCards": false, "delayMilliseconds": 2000, "settingsVersion": 1 }`

## Notes

- Disabling the delay (`useDelay: false`) may result in your IP being banned by the MTG Singles API. Use with caution.
- The application is intended for personal use and not for commercial scraping.

## License

2025 nzgamer41  
See [LICENSE](LICENSE) for details.





    