using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

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
    BecomeCreature,
    SetBasePowerToughness,
    RemoveAbilities,
    OverrideLandType,
    PreventLifeGain,
    PreventCastFromGraveyard,
    PreventActivatedAbilities,
    PreventLethalDamage,
    PreventSpellCasting,
    PreventCreatureAttacks,
    PreventCreatureBlocking,
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
    bool ControllerOnly = false,
    bool SetPowerToughnessToCMC = false,
    ManaColor? ProtectionColor = null,
    EffectLayer? Layer = null,
    long Timestamp = 0,
    int? SetPower = null,
    int? SetToughness = null,
    bool ApplyToSelf = false,
    Func<GameState, bool>? StateCondition = null,
    int? ExpiresOnTurnNumber = null);
