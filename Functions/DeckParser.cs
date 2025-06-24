using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MTGBulkSingles.Functions
{
    internal class DeckParser
    {
        public static List<string> ParseDecklist(string path)
        {
            var lines = File.ReadAllLines(path);

            return lines
                .Select(line =>
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("//"))
                        return null;

                    // Split on first space
                    var parts = trimmed.Split(' ', 2);
                    return parts.Length == 2 ? parts[1].Trim() : trimmed;
                })
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();
        }
    }
}
