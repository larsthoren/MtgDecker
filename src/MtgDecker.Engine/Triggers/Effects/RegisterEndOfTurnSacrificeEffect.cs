using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class RegisterEndOfTurnSacrificeEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var delayed = new DelayedTrigger(
            GameEvent.EndStep,
            new SacrificeSpecificCardEffect(context.Source.Id),
            context.Controller.Id);
        context.State.DelayedTriggers.Add(delayed);
        return Task.CompletedTask;
    }
}
