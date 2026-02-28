namespace MtgDecker.Engine.Triggers.Effects;

public class BounceTargetCreatureEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        if (context.Target == null) return Task.CompletedTask;

        var target = context.Target;
        var owner = context.State.GetCardController(target.Id);
        if (owner == null) return Task.CompletedTask;

        owner.Battlefield.RemoveById(target.Id);
        if (target.IsToken)
        {
            context.State.Log($"{target.Name} token ceases to exist.");
        }
        else
        {
            owner.Hand.Add(target);
            target.IsTapped = false;
            context.State.Log($"{target.Name} is returned to {owner.Name}'s hand.");
        }

        return Task.CompletedTask;
    }
}
