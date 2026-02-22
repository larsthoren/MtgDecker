using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Mana;

public sealed partial class ManaCost
{
    private static readonly ReadOnlyDictionary<string, ManaColor> SymbolToColor = new(
        new Dictionary<string, ManaColor>
        {
            ["W"] = ManaColor.White,
            ["U"] = ManaColor.Blue,
            ["B"] = ManaColor.Black,
            ["R"] = ManaColor.Red,
            ["G"] = ManaColor.Green,
            ["C"] = ManaColor.Colorless
        });

    public static ManaCost Zero { get; } = new(new Dictionary<ManaColor, int>(), new Dictionary<ManaColor, int>(), 0);

    public IReadOnlyDictionary<ManaColor, int> ColorRequirements { get; }
    public IReadOnlyDictionary<ManaColor, int> PhyrexianRequirements { get; }
    public int GenericCost { get; }
    public int ConvertedManaCost { get; }
    public bool IsColored => ColorRequirements.Keys.Any(c => c != ManaColor.Colorless);
    public bool HasPhyrexianCost => PhyrexianRequirements.Count > 0;

    private ManaCost(Dictionary<ManaColor, int> colorRequirements, Dictionary<ManaColor, int> phyrexianRequirements, int genericCost)
    {
        ColorRequirements = new ReadOnlyDictionary<ManaColor, int>(colorRequirements);
        PhyrexianRequirements = new ReadOnlyDictionary<ManaColor, int>(phyrexianRequirements);
        GenericCost = genericCost;
        ConvertedManaCost = genericCost + colorRequirements.Values.Sum() + phyrexianRequirements.Values.Sum();
    }

    public static ManaCost Parse(string? manaCostString)
    {
        if (string.IsNullOrEmpty(manaCostString))
            return Zero;

        var colorRequirements = new Dictionary<ManaColor, int>();
        var phyrexianRequirements = new Dictionary<ManaColor, int>();
        var genericCost = 0;

        foreach (Match match in ManaSymbolRegex().Matches(manaCostString))
        {
            var symbol = match.Groups[1].Value;

            // Check for Phyrexian mana: {W/P}, {U/P}, {B/P}, {R/P}, {G/P}
            if (symbol.Length == 3 && symbol[1] == '/' && symbol[2] == 'P')
            {
                var colorChar = symbol[0..1];
                if (SymbolToColor.TryGetValue(colorChar, out var phyColor))
                {
                    phyrexianRequirements.TryGetValue(phyColor, out var current);
                    phyrexianRequirements[phyColor] = current + 1;
                }
                continue;
            }

            if (SymbolToColor.TryGetValue(symbol, out var color))
            {
                colorRequirements.TryGetValue(color, out var current);
                colorRequirements[color] = current + 1;
            }
            else if (int.TryParse(symbol, out var generic))
            {
                genericCost += generic;
            }
        }

        return new ManaCost(colorRequirements, phyrexianRequirements, genericCost);
    }

    public ManaCost WithGenericReduction(int reduction)
    {
        var newGeneric = Math.Max(0, GenericCost - reduction);
        var colorReqs = new Dictionary<ManaColor, int>(ColorRequirements);
        var phyrexianReqs = new Dictionary<ManaColor, int>(PhyrexianRequirements);
        return new ManaCost(colorReqs, phyrexianReqs, newGeneric);
    }

    private static readonly Dictionary<ManaColor, string> ColorToSymbol = new()
    {
        [ManaColor.White] = "W",
        [ManaColor.Blue] = "U",
        [ManaColor.Black] = "B",
        [ManaColor.Red] = "R",
        [ManaColor.Green] = "G",
        [ManaColor.Colorless] = "C",
    };

    public override string ToString()
    {
        var sb = new StringBuilder();
        if (GenericCost > 0)
            sb.Append($"{{{GenericCost}}}");
        // Use WUBRG ordering for deterministic output
        foreach (var color in new[] { ManaColor.White, ManaColor.Blue, ManaColor.Black, ManaColor.Red, ManaColor.Green, ManaColor.Colorless })
        {
            if (ColorRequirements.TryGetValue(color, out var count))
            {
                var symbol = ColorToSymbol[color];
                for (int i = 0; i < count; i++)
                    sb.Append($"{{{symbol}}}");
            }
        }
        // Phyrexian symbols in WUBRG order, after regular color requirements
        foreach (var color in new[] { ManaColor.White, ManaColor.Blue, ManaColor.Black, ManaColor.Red, ManaColor.Green })
        {
            if (PhyrexianRequirements.TryGetValue(color, out var count))
            {
                var symbol = ColorToSymbol[color];
                for (int i = 0; i < count; i++)
                    sb.Append($"{{{symbol}/P}}");
            }
        }
        return sb.Length > 0 ? sb.ToString() : "{0}";
    }

    [GeneratedRegex(@"\{([^}]+)\}")]
    private static partial Regex ManaSymbolRegex();
}
