namespace MtgDecker.Engine.Effects;

public class NaturalizeEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        if (spell.Targets.Count == 0) return;
        var target = spell.Targets[0];
        var owner = state.GetPlayer(target.PlayerId);
        var permanent = owner.Battlefield.RemoveById(target.CardId);
        if (permanent == null) return;
        owner.Graveyard.Add(permanent);
        state.Log($"{permanent.Name} is destroyed.");
    }
}
