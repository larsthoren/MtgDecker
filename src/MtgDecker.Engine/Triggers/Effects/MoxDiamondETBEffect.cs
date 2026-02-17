namespace MtgDecker.Engine.Triggers.Effects;

public class MoxDiamondETBEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var player = context.Controller;
        var mox = context.Source;

        // Check if player has lands in hand
        var landsInHand = player.Hand.Cards.Where(c => c.IsLand).ToList();

        if (landsInHand.Count == 0)
        {
            // No lands to discard -- sacrifice Mox Diamond
            player.Battlefield.RemoveById(mox.Id);
            player.Graveyard.Add(mox);
            context.State.Log($"{mox.Name} goes to graveyard (no land to discard).");
            return;
        }

        // Ask player to choose a land to discard
        var chosenId = await context.DecisionHandler.ChooseCard(
            landsInHand, "Discard a land for Mox Diamond", optional: false, ct);

        if (chosenId.HasValue)
        {
            var land = player.Hand.Cards.FirstOrDefault(c => c.Id == chosenId.Value);
            if (land != null)
            {
                player.Hand.RemoveById(land.Id);
                player.Graveyard.Add(land);
                context.State.Log($"{player.Name} discards {land.Name} for {mox.Name}.");
            }
        }
        else
        {
            // Player didn't choose -- sacrifice
            player.Battlefield.RemoveById(mox.Id);
            player.Graveyard.Add(mox);
            context.State.Log($"{mox.Name} goes to graveyard (no land discarded).");
        }
    }
}
