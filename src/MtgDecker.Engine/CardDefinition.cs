using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Triggers;

namespace MtgDecker.Engine;

public record CardDefinition(
    ManaCost? ManaCost,
    ManaAbility? ManaAbility,
    int? Power,
    int? Toughness,
    CardType CardTypes,
    TargetFilter? TargetFilter = null,
    SpellEffect? Effect = null
)
{
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<string> Subtypes { get; init; } = [];
    public IReadOnlyList<Trigger> Triggers { get; init; } = [];
    public bool IsLegendary { get; init; }
    public FetchAbility? FetchAbility { get; init; }
    public IReadOnlyList<ContinuousEffect> ContinuousEffects { get; init; } = [];
    public IReadOnlyList<ContinuousEffect> GraveyardAbilities { get; init; } = [];
    public ActivatedAbility? ActivatedAbility { get; init; }
    public AuraTarget? AuraTarget { get; init; }
    public ManaCost? CyclingCost { get; init; }
    public IReadOnlyList<Trigger> CyclingTriggers { get; init; } = [];
    public FlashbackCost? FlashbackCost { get; init; }
    public ManaCost? EchoCost { get; init; }
    public bool EntersTapped { get; init; }
    public AlternateCost? AlternateCost { get; init; }
    public Func<GameState, int>? DynamicBasePower { get; init; }
    public Func<GameState, int>? DynamicBaseToughness { get; init; }
    public Dictionary<CounterType, int>? EntersWithCounters { get; init; }
    public int? StartingLoyalty { get; init; }
    public IReadOnlyList<LoyaltyAbility>? LoyaltyAbilities { get; init; }
    public bool HasFlash { get; init; }
    public ManaCost? NinjutsuCost { get; init; }
    public CardDefinition? TransformInto { get; init; }
    public AdventurePart? Adventure { get; init; }
}
