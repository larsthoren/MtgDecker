namespace MtgDecker.Engine.Triggers.Effects;

public class TapTargetEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        if (context.Target == null) return Task.CompletedTask;

        context.Target.IsTapped = true;
        context.State.Log($"{context.Target.Name} is tapped.");

        return Task.CompletedTask;
    }
}
