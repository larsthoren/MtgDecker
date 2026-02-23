namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Bounce land ETB effect — When this land enters, return a non-lair land you control to hand.
/// If you can't, sacrifice this land.
/// Used by Darigaaz's Caldera, Treva's Ruins, and similar Lair lands.
/// </summary>
public class BounceLandETBEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var lands = context.Controller.Battlefield.Cards
            .Where(c => c.IsLand && c.Id != context.Source.Id)
            .ToList();

        if (lands.Count == 0)
        {
            // Must sacrifice this land if can't bounce
            context.Controller.Battlefield.RemoveById(context.Source.Id);
            context.Controller.Graveyard.Add(context.Source);
            context.State.Log($"{context.Source.Name} is sacrificed (no land to return).");
            return;
        }

        var chosenId = await context.DecisionHandler.ChooseCard(
            lands, "Choose a land to return to hand", optional: false, ct);

        if (chosenId.HasValue)
        {
            var land = lands.FirstOrDefault(c => c.Id == chosenId.Value);
            if (land != null)
            {
                context.Controller.Battlefield.RemoveById(land.Id);
                context.Controller.Hand.Add(land);
                context.State.Log($"{land.Name} is returned to {context.Controller.Name}'s hand.");
            }
        }
    }
}
