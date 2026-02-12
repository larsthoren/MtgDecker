using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public enum ContinuousEffectType
{
    ModifyPowerToughness,
    GrantKeyword,
    ModifyCost,
    ExtraLandDrop,
    SkipDraw,
    GrantPlayerShroud,
    PreventDamageToPlayer,
}

public record ContinuousEffect(
    Guid SourceId,
    ContinuousEffectType Type,
    Func<GameCard, Player, bool> Applies,
    int PowerMod = 0,
    int ToughnessMod = 0,
    bool UntilEndOfTurn = false,
    Keyword? GrantedKeyword = null,
    int CostMod = 0,
    Func<GameCard, bool>? CostApplies = null,
    int ExtraLandDrops = 0,
    bool CostAppliesToOpponent = false,
    bool ExcludeSelf = false,
    bool ControllerOnly = false);
