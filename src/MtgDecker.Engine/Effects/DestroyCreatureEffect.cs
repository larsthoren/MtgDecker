namespace MtgDecker.Engine.Effects;

public class DestroyCreatureEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        if (spell.Targets.Count == 0) return;
        var target = spell.Targets[0];
        var owner = state.GetPlayer(target.PlayerId);
        var creature = owner.Battlefield.RemoveById(target.CardId);
        if (creature == null) return;
        owner.Graveyard.Add(creature);
        state.Log($"{spell.Card.Name} destroys {creature.Name}.");
    }
}
