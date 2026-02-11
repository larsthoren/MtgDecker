using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine;

public class GameCard
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
    public string TypeLine { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public bool IsTapped { get; set; }

    // Resolved from CardDefinitions registry or auto-parsed
    public ManaCost? ManaCost { get; set; }
    public ManaAbility? ManaAbility { get; set; }
    public int? Power { get; set; }
    public int? Toughness { get; set; }
    public CardType CardTypes { get; set; } = CardType.None;
    public IReadOnlyList<string> Subtypes { get; init; } = [];
    public bool IsToken { get; init; }

    // Combat tracking
    public int? TurnEnteredBattlefield { get; set; }
    public int DamageMarked { get; set; }

    public bool HasSummoningSickness(int currentTurn) =>
        IsCreature && TurnEnteredBattlefield.HasValue && TurnEnteredBattlefield.Value >= currentTurn;

    // Backward-compatible: check both CardTypes flags and TypeLine
    public bool IsLand =>
        CardTypes.HasFlag(CardType.Land) ||
        TypeLine.Contains("Land", StringComparison.OrdinalIgnoreCase);

    public bool IsCreature =>
        CardTypes.HasFlag(CardType.Creature) ||
        TypeLine.Contains("Creature", StringComparison.OrdinalIgnoreCase);

    /// <summary>Original factory: uses CardDefinitions registry only.</summary>
    public static GameCard Create(string name, string typeLine = "", string? imageUrl = null)
    {
        var card = new GameCard { Name = name, TypeLine = typeLine, ImageUrl = imageUrl };
        if (CardDefinitions.TryGet(name, out var def))
        {
            card.ManaCost = def.ManaCost;
            card.ManaAbility = def.ManaAbility;
            card.Power = def.Power;
            card.Toughness = def.Toughness;
            card.CardTypes = def.CardTypes;
        }
        return card;
    }

    /// <summary>
    /// Enhanced factory: auto-parses card data from raw strings (Scryfall DB fields).
    /// Falls back to CardDefinitions for ManaAbility on non-basic lands.
    /// </summary>
    public static GameCard Create(string name, string typeLine, string? imageUrl,
        string? manaCost, string? power, string? toughness)
    {
        // CardDefinitions registry takes full precedence if the card is registered
        if (CardDefinitions.TryGet(name, out var def))
        {
            var card = new GameCard { Name = name, TypeLine = typeLine, ImageUrl = imageUrl };
            card.ManaCost = def.ManaCost;
            card.ManaAbility = def.ManaAbility;
            card.Power = def.Power;
            card.Toughness = def.Toughness;
            card.CardTypes = def.CardTypes;
            return card;
        }

        // Auto-parse from raw data
        var parsed = CardTypeParser.ParseFull(typeLine);
        var autoCard = new GameCard
        {
            Name = name,
            TypeLine = typeLine,
            ImageUrl = imageUrl,
            CardTypes = parsed.Types,
            Subtypes = parsed.Subtypes
        };

        if (!string.IsNullOrWhiteSpace(manaCost))
            autoCard.ManaCost = ManaCost.Parse(manaCost);

        if (int.TryParse(power, out var p))
            autoCard.Power = p;
        if (int.TryParse(toughness, out var t))
            autoCard.Toughness = t;

        // Auto-detect mana ability for basic lands
        autoCard.ManaAbility = DetectBasicLandManaAbility(typeLine);

        return autoCard;
    }

    private static ManaAbility? DetectBasicLandManaAbility(string typeLine)
    {
        if (!typeLine.Contains("Basic Land", StringComparison.OrdinalIgnoreCase))
            return null;

        if (typeLine.Contains("Plains", StringComparison.OrdinalIgnoreCase))
            return ManaAbility.Fixed(ManaColor.White);
        if (typeLine.Contains("Island", StringComparison.OrdinalIgnoreCase))
            return ManaAbility.Fixed(ManaColor.Blue);
        if (typeLine.Contains("Swamp", StringComparison.OrdinalIgnoreCase))
            return ManaAbility.Fixed(ManaColor.Black);
        if (typeLine.Contains("Mountain", StringComparison.OrdinalIgnoreCase))
            return ManaAbility.Fixed(ManaColor.Red);
        if (typeLine.Contains("Forest", StringComparison.OrdinalIgnoreCase))
            return ManaAbility.Fixed(ManaColor.Green);

        return null;
    }
}
