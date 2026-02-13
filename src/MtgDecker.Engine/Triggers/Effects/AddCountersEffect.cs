using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class AddCountersEffect(CounterType counterType, int count) : IEffect
{
    public CounterType CounterType { get; } = counterType;
    public int Count { get; } = count;

    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        context.Source.AddCounters(CounterType, Count);
        context.State.Log($"{context.Source.Name} enters with {Count} {CounterType} counter(s).");
        return Task.CompletedTask;
    }
}
