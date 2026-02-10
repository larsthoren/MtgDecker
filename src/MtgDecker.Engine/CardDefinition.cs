using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine;

public record CardDefinition(
    ManaCost? ManaCost,
    ManaAbility? ManaAbility,
    int? Power,
    int? Toughness,
    CardType CardTypes
);
