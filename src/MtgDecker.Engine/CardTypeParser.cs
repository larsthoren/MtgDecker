using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public static class CardTypeParser
{
    public static CardType Parse(string typeLine)
    {
        if (string.IsNullOrWhiteSpace(typeLine))
            return CardType.None;

        // Only check the part before the em dash (supertypes + types, not subtypes)
        var mainPart = typeLine.Contains('—')
            ? typeLine[..typeLine.IndexOf('—')]
            : typeLine;

        var result = CardType.None;

        if (mainPart.Contains("Creature", StringComparison.OrdinalIgnoreCase))
            result |= CardType.Creature;
        if (mainPart.Contains("Land", StringComparison.OrdinalIgnoreCase))
            result |= CardType.Land;
        if (mainPart.Contains("Enchantment", StringComparison.OrdinalIgnoreCase))
            result |= CardType.Enchantment;
        if (mainPart.Contains("Instant", StringComparison.OrdinalIgnoreCase))
            result |= CardType.Instant;
        if (mainPart.Contains("Sorcery", StringComparison.OrdinalIgnoreCase))
            result |= CardType.Sorcery;
        if (mainPart.Contains("Artifact", StringComparison.OrdinalIgnoreCase))
            result |= CardType.Artifact;

        return result;
    }
}
