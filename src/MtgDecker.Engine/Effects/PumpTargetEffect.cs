namespace MtgDecker.Engine.Effects;

public class PumpTargetEffect(int powerMod, int toughnessMod) : SpellEffect
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

        var effect = new ContinuousEffect(
            spell.Card.Id,
            ContinuousEffectType.ModifyPowerToughness,
            (card, _) => card.Id == creature.Id,
            PowerMod: powerMod,
            ToughnessMod: toughnessMod,
            UntilEndOfTurn: true);
        state.ActiveEffects.Add(effect);

        state.Log($"{creature.Name} gets {FormatMod(powerMod)}/{FormatMod(toughnessMod)} until end of turn.");
    }

    private static string FormatMod(int mod) => mod >= 0 ? $"+{mod}" : $"{mod}";
}
