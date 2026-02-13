namespace MtgDecker.Engine.Effects;

public class PonderEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var player = state.GetPlayer(spell.ControllerId);
        var top3 = player.Library.PeekTop(3).ToList();

        // Show the top cards to the player
        await handler.RevealCards(top3, top3, "Ponder: Look at the top 3 cards of your library", ct);

        // Ask: shuffle or keep order?
        // optional=true: null means "yes shuffle", choosing any card means "keep order"
        var decision = await handler.ChooseCard(
            Array.Empty<GameCard>(),
            "Shuffle your library? (Choose to shuffle, Skip to keep order)",
            optional: true, ct);

        if (decision == null)
        {
            player.Library.Shuffle();
            state.Log($"{player.Name} shuffles their library (Ponder).");
        }
        else
        {
            state.Log($"{player.Name} keeps the card order (Ponder).");
        }

        // Draw 1
        var drawn = player.Library.DrawFromTop();
        if (drawn != null)
        {
            player.Hand.Add(drawn);
            state.Log($"{player.Name} draws a card.");
        }
    }
}
