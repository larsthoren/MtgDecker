namespace MtgDecker.Engine.Triggers;

public enum TriggerCondition
{
    Self,
    AnyCreatureDies,
    ControllerCasts,
    ControllerCastsEnchantment,
    SelfDealsCombatDamage,
    SelfAttacks,
    Upkeep,
}
