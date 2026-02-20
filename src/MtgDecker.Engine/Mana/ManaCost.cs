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

    public static ManaCost Zero { get; } = new(new Dictionary<ManaColor, int>(), 0);

    public IReadOnlyDictionary<ManaColor, int> ColorRequirements { get; }
    public int GenericCost { get; }
    public int ConvertedManaCost { get; }
    public bool IsColored => ColorRequirements.Keys.Any(c => c != ManaColor.Colorless);

    private ManaCost(Dictionary<ManaColor, int> colorRequirements, int genericCost)
    {
        ColorRequirements = new ReadOnlyDictionary<ManaColor, int>(colorRequirements);
        GenericCost = genericCost;
        ConvertedManaCost = genericCost + colorRequirements.Values.Sum();
    }

    public static ManaCost Parse(string? manaCostString)
    {
        if (string.IsNullOrEmpty(manaCostString))
            return Zero;

        var colorRequirements = new Dictionary<ManaColor, int>();
        var genericCost = 0;

        foreach (Match match in ManaSymbolRegex().Matches(manaCostString))
        {
            var symbol = match.Groups[1].Value;

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

        return new ManaCost(colorRequirements, genericCost);
    }

    public ManaCost WithGenericReduction(int reduction)
    {
        var newGeneric = Math.Max(0, GenericCost - reduction);
        var colorReqs = new Dictionary<ManaColor, int>(ColorRequirements);
        return new ManaCost(colorReqs, newGeneric);
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
        return sb.Length > 0 ? sb.ToString() : "{0}";
    }

    [GeneratedRegex(@"\{([^}]+)\}")]
    private static partial Regex ManaSymbolRegex();
}
