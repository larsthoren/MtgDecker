namespace MtgDecker.Engine.Effects;

public class SwordsToPlowsharesEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        if (spell.Targets.Count == 0) return;
        var target = spell.Targets[0];
        var owner = state.GetPlayer(target.PlayerId);
        var creature = owner.Battlefield.RemoveById(target.CardId);
        if (creature == null) return;
        var power = creature.Power ?? 0;
        owner.AdjustLife(power);
        owner.Exile.Add(creature);
        state.Log($"{creature.Name} is exiled. {owner.Name} gains {power} life ({owner.Life}).");
    }
}
