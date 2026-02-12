namespace MtgDecker.Engine.Triggers.Effects;

public class ExileCreatureEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var target = context.Target;
        if (target == null) return;

        Player? owner = null;
        if (context.State.Player1.Battlefield.Contains(target.Id))
            owner = context.State.Player1;
        else if (context.State.Player2.Battlefield.Contains(target.Id))
            owner = context.State.Player2;

        if (owner == null) return;

        if (context.FireLeaveBattlefieldTriggers != null)
            await context.FireLeaveBattlefieldTriggers(target);
        owner.Battlefield.RemoveById(target.Id);
        owner.Exile.Add(target);
        context.Source.ExiledCardIds.Add(target.Id);
        context.State.Log($"{context.Source.Name} exiles {target.Name}.");
    }
}
