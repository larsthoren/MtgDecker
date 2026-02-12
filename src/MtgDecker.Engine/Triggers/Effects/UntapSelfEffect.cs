namespace MtgDecker.Engine.Triggers.Effects;

public class UntapSelfEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        context.Source.IsTapped = false;
        context.State.Log($"{context.Source.Name} untaps.");
        return Task.CompletedTask;
    }
}
