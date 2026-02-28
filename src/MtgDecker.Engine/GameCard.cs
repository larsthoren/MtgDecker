using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Triggers;

namespace MtgDecker.Engine;

public class GameCard
{
    public Guid Id { get; init; } = Guid.NewGuid();

    // Backing fields for transform-aware properties
    private string _frontName = string.Empty;
    private CardType _frontCardTypes = CardType.None;
    private int? _frontBasePower;
    private int? _frontBaseToughness;

    // Transform state
    public bool IsTransformed { get; set; }
    public CardDefinition? BackFaceDefinition { get; set; }

    /// <summary>Always returns the front face name, regardless of transform state.</summary>
    public string FrontName => _frontName;

    /// <summary>
    /// Returns back face name when transformed (with a BackFaceDefinition), otherwise front face name.
    /// Setter always writes to the front face.
    /// </summary>
    public string Name
    {
        get => IsTransformed && BackFaceDefinition != null ? BackFaceDefinition.Name : _frontName;
        set => _frontName = value;
    }

    public string TypeLine { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public bool IsTapped { get; set; }

    // Resolved from CardDefinitions registry or auto-parsed
    private ManaCost? _manaCost;
    public ManaCost? ManaCost
    {
        get => _manaCost;
        set
        {
            _manaCost = value;
            // Auto-populate Colors from ManaCost when set (if Colors not already populated)
            if (value != null && Colors.Count == 0)
            {
                foreach (var color in value.ColorRequirements.Keys)
                {
                    if (color != ManaColor.Colorless)
                        Colors.Add(color);
                }
            }
        }
    }

    // BaseManaAbility stores the original mana ability (before continuous effects).
    // ManaAbility is the "effective" value used by the engine.
    public ManaAbility? BaseManaAbility { get; set; }
    public ManaAbility? ManaAbility { get; set; }

    /// <summary>
    /// Returns back face card types when transformed, otherwise front face card types.
    /// Setter always writes to the front face.
    /// </summary>
    public CardType CardTypes
    {
        get => IsTransformed && BackFaceDefinition != null ? BackFaceDefinition.CardTypes : _frontCardTypes;
        set => _frontCardTypes = value;
    }

    public IReadOnlyList<string> Subtypes { get; init; } = [];
    public IReadOnlyList<Trigger> Triggers { get; init; } = [];

    /// <summary>
    /// Returns card instance triggers if any, otherwise falls back to CardDefinitions registry.
    /// Uses FrontName for the lookup since CardDefinitions registers cards by front name.
    /// </summary>
    public IReadOnlyList<Trigger> EffectiveTriggers =>
        Triggers.Count > 0
            ? Triggers
            : (CardDefinitions.TryGet(FrontName, out var def) ? def.Triggers : []);
    public bool IsToken { get; init; }
    public bool IsLegendary { get; init; }
    public bool EntersTapped { get; init; }
    public FetchAbility? FetchAbility { get; init; }
    public ActivatedAbility? TokenActivatedAbility { get; set; }
    public bool EchoPaid { get; set; } = true;

    // Base power/toughness from the card definition
    /// <summary>
    /// Returns back face power when transformed, otherwise front face power.
    /// Setter always writes to the front face.
    /// </summary>
    public int? BasePower
    {
        get => IsTransformed && BackFaceDefinition != null ? BackFaceDefinition.Power : _frontBasePower;
        set => _frontBasePower = value;
    }

    /// <summary>
    /// Returns back face toughness when transformed, otherwise front face toughness.
    /// Setter always writes to the front face.
    /// </summary>
    public int? BaseToughness
    {
        get => IsTransformed && BackFaceDefinition != null ? BackFaceDefinition.Toughness : _frontBaseToughness;
        set => _frontBaseToughness = value;
    }

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

    // Card colors — initialized from ManaCost, can be overridden (e.g., tokens, color-changing effects)
    public HashSet<ManaColor> Colors { get; set; } = new();

    // Adventure state — true when in exile after adventure half resolved
    public bool IsOnAdventure { get; set; }

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

    /// <summary>Current loyalty (reads from loyalty counters).</summary>
    public int Loyalty => GetCounters(CounterType.Loyalty);

    // Per-source exile tracking (e.g., Parallax Wave)
    public List<Guid> ExiledCardIds { get; } = new();

    // Chosen type/name for cards like Engineered Plague / Meddling Mage
    public string? ChosenType { get; set; }
    public string? ChosenName { get; set; }

    // Kicker tracking
    public bool WasKicked { get; set; }

    // Regeneration shields
    public int RegenerationShields { get; set; }

    // Carpet of Flowers once-per-turn tracking
    public bool CarpetUsedThisTurn { get; set; }

    // Once-per-turn activated ability tracking (e.g. Basking Rootwalla)
    public HashSet<int> AbilitiesActivatedThisTurn { get; } = new();

    // Combat tracking
    public int? TurnEnteredBattlefield { get; set; }
    public int DamageMarked { get; set; }
    public bool AbilitiesRemoved { get; set; }

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

    public bool IsPlaneswalker =>
        (EffectiveCardTypes ?? CardTypes).HasFlag(CardType.Planeswalker);

    /// <summary>Original factory: uses CardDefinitions registry only.</summary>
    public static GameCard Create(string name, string typeLine = "", string? imageUrl = null)
    {
        if (CardDefinitions.TryGet(name, out var def))
            return CreateFromDefinition(def, name, typeLine, imageUrl);

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
            return CreateFromDefinition(def, name, typeLine, imageUrl);

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
        var detectedAbility = DetectBasicLandManaAbility(typeLine);
        autoCard.BaseManaAbility = detectedAbility;
        autoCard.ManaAbility = detectedAbility;

        return autoCard;
    }

    private static GameCard CreateFromDefinition(CardDefinition def, string name, string typeLine, string? imageUrl) => new()
    {
        Name = name,
        TypeLine = typeLine,
        ImageUrl = imageUrl,
        ManaCost = def.ManaCost,
        BaseManaAbility = def.ManaAbility,
        ManaAbility = def.ManaAbility,
        BasePower = def.Power,
        BaseToughness = def.Toughness,
        CardTypes = def.CardTypes,
        Subtypes = def.Subtypes,
        Triggers = def.Triggers,
        IsLegendary = def.IsLegendary,
        EntersTapped = def.EntersTapped,
        FetchAbility = def.FetchAbility,
        EchoPaid = def.EchoCost == null,
        BackFaceDefinition = def.TransformInto,
    };

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
