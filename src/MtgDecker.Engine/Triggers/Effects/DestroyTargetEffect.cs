namespace MtgDecker.Engine.Triggers.Effects;

public class DestroyTargetEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        if (context.Target == null) return Task.CompletedTask;

        var target = context.Target;
        var owner = context.State.GetCardController(target.Id);
        if (owner == null) return Task.CompletedTask;

        // Track who caused land destruction for Sacred Ground
        if (target.IsLand)
            context.State.LastLandDestroyedByPlayerId = context.Controller.Id;

        owner.Battlefield.RemoveById(target.Id);
        if (!target.IsToken)
            owner.Graveyard.Add(target);
        context.State.Log($"{target.Name} is destroyed.");

        return Task.CompletedTask;
    }
}
