using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine;

public record AdventurePart(
    string Name,
    ManaCost Cost,
    TargetFilter? Filter = null,
    SpellEffect? Effect = null);
