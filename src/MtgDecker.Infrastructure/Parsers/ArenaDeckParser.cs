using System.Text.RegularExpressions;
using MtgDecker.Application.Interfaces;

namespace MtgDecker.Infrastructure.Parsers;

public partial class ArenaDeckParser : IDeckParser
{
    public string FormatName => "Arena";

    // Matches: "4 Lightning Bolt (LEA) 161" or "4 Lightning Bolt"
    [GeneratedRegex(@"^(\d+)\s+(.+?)(?:\s+\(([A-Za-z0-9]+)\)\s+(\S+))?$", RegexOptions.Compiled)]
    private static partial Regex LinePattern();

    public ParsedDeck Parse(string deckText)
    {
        var deck = new ParsedDeck();

        if (string.IsNullOrWhiteSpace(deckText))
            return deck;

        var lines = deckText.Split('\n', StringSplitOptions.TrimEntries);
        var inSideboard = false;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.Equals("Sideboard", StringComparison.OrdinalIgnoreCase))
            {
                inSideboard = true;
                continue;
            }

            // Also support "Companion" and "Commander" sections as sideboard
            if (line.Equals("Companion", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("Commander", StringComparison.OrdinalIgnoreCase))
            {
                inSideboard = true;
                continue;
            }

            // "Deck" header resets to main deck
            if (line.Equals("Deck", StringComparison.OrdinalIgnoreCase))
            {
                inSideboard = false;
                continue;
            }

            var match = LinePattern().Match(line);
            if (!match.Success) continue;

            var entry = new ParsedDeckEntry
            {
                Quantity = int.Parse(match.Groups[1].Value),
                CardName = match.Groups[2].Value.Trim(),
                SetCode = match.Groups[3].Success ? match.Groups[3].Value.ToLowerInvariant() : null,
                CollectorNumber = match.Groups[4].Success ? match.Groups[4].Value : null
            };

            if (inSideboard)
                deck.Sideboard.Add(entry);
            else
                deck.MainDeck.Add(entry);
        }

        return deck;
    }
}
