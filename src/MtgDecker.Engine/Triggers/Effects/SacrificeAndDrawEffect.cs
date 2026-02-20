namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Effect for Clue token ability: draws a card for the controller.
/// The sacrifice cost is handled by the ActivateAbility handler in GameEngine
/// (via ActivatedAbilityCost.SacrificeSelf), so this effect only draws.
/// </summary>
public class SacrificeAndDrawEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var drawn = context.Controller.Library.DrawFromTop();
        if (drawn != null)
        {
            context.Controller.Hand.Add(drawn);
            context.State.Log($"{context.Controller.Name} draws a card.");
        }

        return Task.CompletedTask;
    }
}
