using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine;

public record FlashbackCost(
    ManaCost? ManaCost = null,
    int LifeCost = 0,
    bool SacrificeCreature = false,
    int ExileBlueCardsFromGraveyard = 0);
