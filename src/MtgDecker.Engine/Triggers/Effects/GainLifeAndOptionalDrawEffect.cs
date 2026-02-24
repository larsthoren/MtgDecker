namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Spiritual Focus (simplified): Whenever you discard a card, you gain 2 life
/// and you may draw a card.
/// </summary>
public class GainLifeAndOptionalDrawEffect(int lifeGain) : IEffect
{
    public int LifeGain { get; } = lifeGain;

    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        // Gain life
        context.Controller.AdjustLife(LifeGain);
        context.State.Log($"{context.Source.Name}: {context.Controller.Name} gains {LifeGain} life. ({context.Controller.Life} life)");

        // May draw a card (optional)
        var drawChoice = await context.DecisionHandler.ChooseCard(
            context.Controller.Hand.Cards,
            $"Draw a card from {context.Source.Name}?",
            optional: true, ct);

        // If they chose anything (non-null), draw. If null, they declined.
        // Since we're asking "may draw", we use null = decline
        if (drawChoice.HasValue || context.Controller.Hand.Cards.Count == 0)
        {
            // Draw a card
            if (context.Controller.Library.Count > 0)
            {
                var drawn = context.Controller.Library.DrawFromTop();
                if (drawn != null)
                {
                    context.Controller.Hand.Add(drawn);
                    context.State.Log($"{context.Controller.Name} draws a card from {context.Source.Name}.");
                }
            }
        }
        else
        {
            context.State.Log($"{context.Controller.Name} declines to draw from {context.Source.Name}.");
        }
    }
}
