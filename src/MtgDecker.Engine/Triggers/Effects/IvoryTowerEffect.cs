namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Ivory Tower: At the beginning of your upkeep, you gain X life,
/// where X is the number of cards in your hand minus 4.
/// </summary>
public class IvoryTowerEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var handSize = context.Controller.Hand.Cards.Count;
        var lifeGain = Math.Max(0, handSize - 4);

        if (lifeGain > 0)
        {
            context.Controller.AdjustLife(lifeGain);
            context.State.Log($"{context.Source.Name} gains {context.Controller.Name} {lifeGain} life (hand size {handSize}). ({context.Controller.Life} life)");
        }
        else
        {
            context.State.Log($"{context.Source.Name}: {context.Controller.Name} has {handSize} cards in hand (no life gained).");
        }

        return Task.CompletedTask;
    }
}
