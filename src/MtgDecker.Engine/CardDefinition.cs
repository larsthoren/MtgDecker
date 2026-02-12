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
    public IReadOnlyList<string> Subtypes { get; init; } = [];
    public IReadOnlyList<Trigger> Triggers { get; init; } = [];
}
