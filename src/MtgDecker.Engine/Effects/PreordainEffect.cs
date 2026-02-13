namespace MtgDecker.Engine.Effects;

public class PreordainEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var player = state.GetPlayer(spell.ControllerId);

        // Scry 2: look at top 2 cards
        var top2 = player.Library.PeekTop(2).ToList();
        var keptOnTop = new List<GameCard>();
        var sentToBottom = new List<GameCard>();

        foreach (var card in top2)
        {
            // Ask player to keep on top or send to bottom
            var choice = await handler.ChooseCard(
                new[] { card },
                $"Preordain: Keep {card.Name} on top? (Choose to keep, Skip to bottom)",
                optional: true, ct);

            if (choice != null)
                keptOnTop.Add(card);
            else
                sentToBottom.Add(card);
        }

        // Remove looked-at cards from library
        foreach (var card in top2)
            player.Library.RemoveById(card.Id);

        // Put kept cards back on top (in order chosen)
        foreach (var card in keptOnTop)
            player.Library.AddToTop(card);

        // Put bottom cards on bottom
        foreach (var card in sentToBottom)
            player.Library.AddToBottom(card);

        // Draw 1
        var drawn = player.Library.DrawFromTop();
        if (drawn != null)
        {
            player.Hand.Add(drawn);
            state.Log($"{player.Name} draws a card (Preordain).");
        }
    }
}
