namespace MtgDecker.Engine.Effects;

public class BrainstormEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var player = spell.ControllerId == state.Player1.Id ? state.Player1 : state.Player2;

        // Draw 3
        var drawn = 0;
        for (int i = 0; i < 3; i++)
        {
            var card = player.Library.DrawFromTop();
            if (card == null) break;
            player.Hand.Add(card);
            drawn++;
        }
        state.Log($"{player.Name} draws {drawn} card(s) (Brainstorm).");

        // Put 2 back on top of library (one at a time)
        var putBack = Math.Min(2, player.Hand.Count);
        for (int i = 0; i < putBack; i++)
        {
            var cardId = await handler.ChooseCard(
                player.Hand.Cards,
                $"Put a card on top of your library ({i + 1}/{putBack})",
                optional: false, ct);
            if (cardId.HasValue)
            {
                var card = player.Hand.RemoveById(cardId.Value);
                if (card != null) player.Library.AddToTop(card);
            }
        }
        state.Log($"{player.Name} puts {putBack} card(s) on top of library.");
    }
}
