namespace MtgDecker.Engine.Triggers.Effects;

public class DestroyTargetEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        if (context.Target == null) return Task.CompletedTask;

        var target = context.Target;
        var owner = context.State.Player1.Battlefield.Contains(target.Id)
            ? context.State.Player1
            : context.State.Player2;

        owner.Battlefield.RemoveById(target.Id);
        if (!target.IsToken)
            owner.Graveyard.Add(target);
        context.State.Log($"{target.Name} is destroyed.");

        return Task.CompletedTask;
    }
}
