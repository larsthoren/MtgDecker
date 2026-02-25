using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Wild Mongrel: Discard a card: Wild Mongrel gets +1/+1 and becomes the color
/// of your choice until end of turn. The pump is handled as a ContinuousEffect,
/// and the color change is applied directly with a delayed trigger to restore at EOT.
/// </summary>
public class WildMongrelEffect : IEffect
{
    private static readonly IReadOnlyList<ManaColor> AllColors =
        [ManaColor.White, ManaColor.Blue, ManaColor.Black, ManaColor.Red, ManaColor.Green];

    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var sourceId = context.Source.Id;

        // Pump +1/+1 until end of turn
        var pumpEffect = new ContinuousEffect(
            sourceId,
            ContinuousEffectType.ModifyPowerToughness,
            (card, _) => card.Id == sourceId,
            PowerMod: 1,
            ToughnessMod: 1,
            UntilEndOfTurn: true);
        context.State.ActiveEffects.Add(pumpEffect);

        // Ask player to choose a color
        var chosenColor = await context.DecisionHandler.ChooseManaColor(AllColors, ct);

        // Save original colors for restoration
        var originalColors = new HashSet<ManaColor>(context.Source.Colors);

        // Change color
        context.Source.Colors.Clear();
        context.Source.Colors.Add(chosenColor);

        // Register delayed trigger to restore colors at end of turn
        context.State.DelayedTriggers.Add(new DelayedTrigger(
            GameEvent.EndStep,
            new RestoreColorsEffect(sourceId, originalColors),
            context.Controller.Id));

        context.State.Log($"{context.Source.Name} gets +1/+1 and becomes {chosenColor} until end of turn.");
    }
}

/// <summary>
/// Restores a card's Colors to original values at end of turn.
/// </summary>
internal class RestoreColorsEffect(Guid cardId, HashSet<ManaColor> originalColors) : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        // Find the card on the battlefield
        var card = context.Controller.Battlefield.Cards.FirstOrDefault(c => c.Id == cardId);
        if (card != null)
        {
            card.Colors.Clear();
            foreach (var color in originalColors)
                card.Colors.Add(color);
        }
        return Task.CompletedTask;
    }
}
