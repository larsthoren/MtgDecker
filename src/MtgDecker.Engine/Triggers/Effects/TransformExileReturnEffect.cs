using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class TransformExileReturnEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var card = context.Source;
        var controller = context.Controller;
        var state = context.State;

        // Remove from battlefield (exile)
        controller.Battlefield.Remove(card);

        // Transform
        card.IsTransformed = true;

        // Return to battlefield
        controller.Battlefield.Add(card);

        // If back face is a planeswalker with starting loyalty, add loyalty counters
        if (card.BackFaceDefinition?.StartingLoyalty is int loyalty)
        {
            card.AddCounters(CounterType.Loyalty, loyalty);
        }

        state.Log($"{card.Name} transforms!");

        return Task.CompletedTask;
    }
}
