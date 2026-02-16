namespace MtgDecker.Engine.Effects;

/// <summary>
/// Skeletal Scrying — As an additional cost, exile X cards from your graveyard.
/// Draw X cards and lose X life.
/// Since X-costs aren't fully supported yet, the player chooses cards to exile
/// one at a time during resolution, then draws that many and loses that much life.
/// </summary>
public class SkeletalScryingEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var player = state.GetPlayer(spell.ControllerId);

        if (player.Graveyard.Count == 0)
        {
            state.Log($"{player.Name} has no cards in graveyard to exile (Skeletal Scrying).");
            return;
        }

        // Choose cards to exile one at a time
        var exiled = new List<GameCard>();
        while (true)
        {
            var remaining = player.Graveyard.Cards.ToList(); // fresh snapshot each iteration
            if (remaining.Count == 0) break;

            var chosenId = await handler.ChooseCard(remaining,
                "Skeletal Scrying: Choose a card from your graveyard to exile (or skip to stop).",
                optional: true, ct);
            if (!chosenId.HasValue) break;

            var card = player.Graveyard.RemoveById(chosenId.Value);
            if (card != null)
            {
                player.Exile.Add(card);
                exiled.Add(card);
            }
        }

        var x = exiled.Count;
        if (x == 0)
        {
            state.Log($"{player.Name} chose not to exile any cards (Skeletal Scrying).");
            return;
        }

        state.Log($"{player.Name} exiles {x} card(s) from graveyard (Skeletal Scrying).");

        // Draw X cards
        var drawn = 0;
        for (int i = 0; i < x; i++)
        {
            var card = player.Library.DrawFromTop();
            if (card == null) break;
            player.Hand.Add(card);
            drawn++;
        }

        // Lose X life
        player.AdjustLife(-x);
        state.Log($"{player.Name} draws {drawn} card(s) and loses {x} life (Skeletal Scrying). Life: {player.Life}.");
    }
}
