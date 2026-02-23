using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class FadingUpkeepEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var fadeCount = context.Source.GetCounters(CounterType.Fade);
        if (fadeCount > 0)
        {
            context.Source.RemoveCounter(CounterType.Fade);
            context.State.Log($"Removed a fade counter from {context.Source.Name} ({fadeCount - 1} remaining).");
        }
        else
        {
            // No fade counters — sacrifice
            context.Controller.Battlefield.RemoveById(context.Source.Id);
            context.Controller.Graveyard.Add(context.Source);
            context.State.Log($"{context.Source.Name} is sacrificed (no fade counters).");
        }
        return Task.CompletedTask;
    }
}
