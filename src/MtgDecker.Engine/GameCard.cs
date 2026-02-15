using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Triggers;

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
    public CardType CardTypes { get; set; } = CardType.None;
    public IReadOnlyList<string> Subtypes { get; init; } = [];
    public IReadOnlyList<Trigger> Triggers { get; init; } = [];
    public bool IsToken { get; init; }
    public bool IsLegendary { get; init; }
    public bool EntersTapped { get; init; }
    public FetchAbility? FetchAbility { get; init; }

    // Base power/toughness from the card definition
    public int? BasePower { get; set; }
    public int? BaseToughness { get; set; }

    // Effective overrides set by continuous effects (RecalculateState)
    public int? EffectivePower { get; set; }
    public int? EffectiveToughness { get; set; }

    // Computed: returns effective value if set, otherwise base value.
    // Setter writes to base for backward compatibility.
    public int? Power
    {
        get => EffectivePower ?? BasePower;
        set => BasePower = value;
    }

    public int? Toughness
    {
        get => EffectiveToughness ?? BaseToughness;
        set => BaseToughness = value;
    }

    // Type-changing effects (e.g., Opalescence makes enchantments into creatures)
    public CardType? EffectiveCardTypes { get; set; }

    // Keywords granted by continuous effects or intrinsic abilities
    public HashSet<Keyword> ActiveKeywords { get; } = new();

    // Aura attachment
    public Guid? AttachedTo { get; set; }

    // Counter tracking
    public Dictionary<CounterType, int> Counters { get; } = new();

    public void AddCounters(CounterType type, int count)
    {
        Counters.TryGetValue(type, out var current);
        Counters[type] = current + count;
    }

    public bool RemoveCounter(CounterType type)
    {
        if (!Counters.TryGetValue(type, out var current) || current <= 0)
            return false;
        Counters[type] = current - 1;
        return true;
    }

    public int GetCounters(CounterType type) =>
        Counters.TryGetValue(type, out var count) ? count : 0;

    // Per-source exile tracking (e.g., Parallax Wave)
    public List<Guid> ExiledCardIds { get; } = new();

    // Combat tracking
    public int? TurnEnteredBattlefield { get; set; }
    public int DamageMarked { get; set; }

    public bool HasSummoningSickness(int currentTurn) =>
        IsCreature
        && TurnEnteredBattlefield.HasValue
        && TurnEnteredBattlefield.Value >= currentTurn
        && !ActiveKeywords.Contains(Keyword.Haste);

    private static readonly HashSet<string> BasicLandNames = new(StringComparer.OrdinalIgnoreCase)
        { "Plains", "Island", "Swamp", "Mountain", "Forest" };

    public bool IsBasicLand =>
        IsLand && BasicLandNames.Contains(Name);

    // Backward-compatible: check both CardTypes flags and TypeLine
    public bool IsLand =>
        (EffectiveCardTypes ?? CardTypes).HasFlag(CardType.Land) ||
        TypeLine.Contains("Land", StringComparison.OrdinalIgnoreCase);

    public bool IsCreature =>
        (EffectiveCardTypes ?? CardTypes).HasFlag(CardType.Creature) ||
        TypeLine.Contains("Creature", StringComparison.OrdinalIgnoreCase);

    /// <summary>Original factory: uses CardDefinitions registry only.</summary>
    public static GameCard Create(string name, string typeLine = "", string? imageUrl = null)
    {
        if (CardDefinitions.TryGet(name, out var def))
        {
            return new GameCard
            {
                Name = name,
                TypeLine = typeLine,
                ImageUrl = imageUrl,
                ManaCost = def.ManaCost,
                ManaAbility = def.ManaAbility,
                BasePower = def.Power,
                BaseToughness = def.Toughness,
                CardTypes = def.CardTypes,
                Subtypes = def.Subtypes,
                Triggers = def.Triggers,
                IsLegendary = def.IsLegendary,
                EntersTapped = def.EntersTapped,
                FetchAbility = def.FetchAbility,
            };
        }
        return new GameCard
        {
            Name = name,
            TypeLine = typeLine,
            ImageUrl = imageUrl,
            IsLegendary = typeLine.Contains("Legendary", StringComparison.OrdinalIgnoreCase),
        };
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
            return new GameCard
            {
                Name = name,
                TypeLine = typeLine,
                ImageUrl = imageUrl,
                ManaCost = def.ManaCost,
                ManaAbility = def.ManaAbility,
                BasePower = def.Power,
                BaseToughness = def.Toughness,
                CardTypes = def.CardTypes,
                Subtypes = def.Subtypes,
                Triggers = def.Triggers,
                IsLegendary = def.IsLegendary,
                EntersTapped = def.EntersTapped,
                FetchAbility = def.FetchAbility,
            };
        }

        // Auto-parse from raw data
        var parsed = CardTypeParser.ParseFull(typeLine);
        var autoCard = new GameCard
        {
            Name = name,
            TypeLine = typeLine,
            ImageUrl = imageUrl,
            CardTypes = parsed.Types,
            Subtypes = parsed.Subtypes,
            IsLegendary = typeLine.Contains("Legendary", StringComparison.OrdinalIgnoreCase),
        };

        if (!string.IsNullOrWhiteSpace(manaCost))
            autoCard.ManaCost = ManaCost.Parse(manaCost);

        if (int.TryParse(power, out var p))
            autoCard.BasePower = p;
        if (int.TryParse(toughness, out var t))
            autoCard.BaseToughness = t;

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
