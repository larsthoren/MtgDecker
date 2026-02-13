namespace MtgDecker.Engine.Effects;

public class PreordainEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var player = spell.ControllerId == state.Player1.Id ? state.Player1 : state.Player2;
        var top2 = player.Library.PeekTop(2).ToList();

        if (top2.Count == 0)
        {
            state.Log($"{player.Name} scries 0 (Preordain) — library empty.");
            return;
        }

        // Remove from library temporarily
        foreach (var card in top2)
            player.Library.RemoveById(card.Id);

        var keptOnTop = new List<GameCard>();

        // For each card: choose to keep on top or send to bottom
        foreach (var card in top2)
        {
            // optional=true: choosing the card = keep on top, null/skip = send to bottom
            var choice = await handler.ChooseCard(
                new[] { card } as IReadOnlyList<GameCard>,
                $"Keep {card.Name} on top of library? (Choose = top, Skip = bottom)",
                optional: true, ct);

            if (choice.HasValue)
                keptOnTop.Add(card);
            else
                player.Library.AddToBottom(card);
        }

        // Put kept cards back on top (in order they were kept)
        foreach (var card in keptOnTop)
            player.Library.AddToTop(card);

        state.Log($"{player.Name} scries {top2.Count} (Preordain).");

        // Draw 1
        var drawn = player.Library.DrawFromTop();
        if (drawn != null)
        {
            player.Hand.Add(drawn);
            state.Log($"{player.Name} draws a card.");
        }
    }
}
