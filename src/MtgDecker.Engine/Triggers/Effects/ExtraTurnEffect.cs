namespace MtgDecker.Engine.Triggers.Effects;

public class ExtraTurnEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        context.State.ExtraTurns.Enqueue(context.Controller.Id);
        context.State.Log($"{context.Controller.Name} will take an extra turn.");
        return Task.CompletedTask;
    }
}
