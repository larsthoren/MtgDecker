namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Activated ability effect: add a regeneration shield to the source creature.
/// {G}: Regenerate River Boa.
/// </summary>
public class RegenerateEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        context.Source.RegenerationShields++;
        context.State.Log($"{context.Source.Name} gains a regeneration shield ({context.Source.RegenerationShields} total).");
        return Task.CompletedTask;
    }
}
