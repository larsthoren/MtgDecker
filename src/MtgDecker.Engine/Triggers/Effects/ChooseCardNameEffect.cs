namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// ETB effect that asks the controller to choose a nonland card name and stores it on the source card.
/// Used by Meddling Mage: spells with the chosen name can't be cast.
/// The cast prevention is enforced in CastSpellHandler by checking for Meddling Mages on the battlefield.
/// </summary>
public class ChooseCardNameEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var chosenName = await context.DecisionHandler.ChooseCardName(
            $"Choose a nonland card name for {context.Source.Name}:", ct);

        context.Source.ChosenName = chosenName;
        context.State.Log($"{context.Controller.Name} names: {chosenName}.");
    }
}
