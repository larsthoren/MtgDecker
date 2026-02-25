namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// ETB effect that asks the controller to choose a creature type and stores it on the source card.
/// Used by Engineered Plague: all creatures of the chosen type get -1/-1.
/// The continuous effect is generated dynamically in RebuildActiveEffects based on ChosenType.
/// </summary>
public class ChooseCreatureTypeEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var chosenType = await context.DecisionHandler.ChooseCreatureType(
            $"Choose a creature type for {context.Source.Name}:", ct);

        context.Source.ChosenType = chosenType;
        context.State.Log($"{context.Controller.Name} chooses creature type: {chosenType}.");
    }
}
