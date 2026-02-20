namespace MtgDecker.Engine.Triggers;

public enum TriggerCondition
{
    Self,
    AnyCreatureDies,
    ControllerCastsEnchantment,
    SelfDealsCombatDamage,
    SelfAttacks,
    Upkeep,
    AttachedPermanentTapped,
    SelfLeavesBattlefield,
    AnySpellCastCmc3OrLess,
    SelfInGraveyardDuringUpkeep,
    ControllerCastsNoncreature,
    AnyPlayerCastsSpell,
    AnyUpkeep,
    OpponentDrawsExceptFirst,
    ThirdDrawInTurn,
}
