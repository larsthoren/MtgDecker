namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// When Rancor is put into a graveyard from the battlefield, return it to its owner's hand.
/// </summary>
public class RancorReturnEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        // Find the Rancor in the graveyard
        var rancor = context.Controller.Graveyard.Cards
            .FirstOrDefault(c => c.Id == context.Source.Id);

        if (rancor != null)
        {
            context.Controller.Graveyard.RemoveById(rancor.Id);
            context.Controller.Hand.Add(rancor);
            context.State.Log($"{rancor.Name} returns to {context.Controller.Name}'s hand.");
        }

        return Task.CompletedTask;
    }
}
