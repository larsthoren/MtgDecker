using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Triggers;

namespace MtgDecker.Engine;

public record ActivatedAbilityCost(
    bool TapSelf = false,
    bool SacrificeSelf = false,
    string? SacrificeSubtype = null,
    ManaCost? ManaCost = null,
    CounterType? RemoveCounterType = null,
    CardType? SacrificeCardType = null,
    CardType? DiscardCardType = null,
    int PayLife = 0,
    int ExileFromGraveyardCount = 0,
    bool DiscardAny = false,
    int DiscardCount = 0,
    bool ReturnSelfToHand = false);

public record ActivatedAbility(
    ActivatedAbilityCost Cost,
    IEffect Effect,
    Func<GameCard, bool>? TargetFilter = null,
    bool CanTargetPlayer = false,
    Func<Player, bool>? Condition = null,
    bool TargetOwnOnly = false,
    bool OncePerTurn = false);
