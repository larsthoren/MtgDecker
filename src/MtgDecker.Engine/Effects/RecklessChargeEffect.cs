using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Effects;

public class RecklessChargeEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        if (spell.Targets.Count == 0) return;
        var target = spell.Targets[0];

        var controller = state.GetPlayer(spell.ControllerId);
        var opponent = state.GetOpponent(controller);

        // Find target creature on battlefield
        var creature = controller.Battlefield.Cards.FirstOrDefault(c => c.Id == target.CardId)
            ?? opponent.Battlefield.Cards.FirstOrDefault(c => c.Id == target.CardId);

        if (creature == null || !creature.IsCreature) return;

        // Apply +3/+0 until end of turn
        var ptEffect = new ContinuousEffect(
            spell.Card.Id,
            ContinuousEffectType.ModifyPowerToughness,
            (card, _) => card.Id == creature.Id,
            PowerMod: 3, ToughnessMod: 0,
            UntilEndOfTurn: true);
        state.ActiveEffects.Add(ptEffect);

        // Grant haste until end of turn
        var hasteEffect = new ContinuousEffect(
            spell.Card.Id,
            ContinuousEffectType.GrantKeyword,
            (card, _) => card.Id == creature.Id,
            GrantedKeyword: Keyword.Haste,
            UntilEndOfTurn: true);
        state.ActiveEffects.Add(hasteEffect);

        state.Log($"{creature.Name} gets +3/+0 and haste until end of turn.");
    }
}
