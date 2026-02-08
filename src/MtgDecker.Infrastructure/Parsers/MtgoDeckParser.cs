using System.Text.RegularExpressions;
using MtgDecker.Application.Interfaces;

namespace MtgDecker.Infrastructure.Parsers;

public partial class MtgoDeckParser : IDeckParser
{
    public string FormatName => "MTGO";

    // Matches: "4 Lightning Bolt" or "SB: 4 Lightning Bolt" or "SB:  4 Lightning Bolt"
    [GeneratedRegex(@"^(?:SB:\s*)?(\d+)\s+(.+)$", RegexOptions.Compiled)]
    private static partial Regex LinePattern();

    [GeneratedRegex(@"^SB:\s*", RegexOptions.Compiled)]
    private static partial Regex SideboardPrefix();

    public ParsedDeck Parse(string deckText)
    {
        var deck = new ParsedDeck();

        if (string.IsNullOrWhiteSpace(deckText))
            return deck;

        var lines = deckText.Split('\n', StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var isSideboard = SideboardPrefix().IsMatch(line);
            var match = LinePattern().Match(line);

            if (!match.Success) continue;

            var entry = new ParsedDeckEntry
            {
                Quantity = int.Parse(match.Groups[1].Value),
                CardName = match.Groups[2].Value.Trim()
            };

            if (isSideboard)
                deck.Sideboard.Add(entry);
            else
                deck.MainDeck.Add(entry);
        }

        return deck;
    }
}
