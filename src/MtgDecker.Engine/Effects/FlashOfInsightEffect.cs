namespace MtgDecker.Engine.Effects;

/// <summary>
/// Flash of Insight - Look at the top X cards of your library.
/// Put one of them into your hand and the rest on the bottom of your library in any order.
/// X is determined by remaining mana pool (normal cast) or XValue on stack (flashback with exiled blue cards).
/// </summary>
public class FlashOfInsightEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var player = state.GetPlayer(spell.ControllerId);

        // Determine X: use XValue if set (flashback), otherwise use remaining mana pool
        int x;
        if (spell.XValue.HasValue)
        {
            x = spell.XValue.Value;
        }
        else
        {
            x = player.ManaPool.Total;
            // Drain remaining mana
            foreach (var (color, amount) in player.ManaPool.Available.ToList())
                player.ManaPool.Deduct(color, amount);
        }

        if (x == 0)
        {
            state.Log($"{player.Name} casts Flash of Insight with X=0.");
            return;
        }

        var top = player.Library.PeekTop(x).ToList();
        if (top.Count == 0)
        {
            state.Log($"{player.Name} looks at the top of their library but it is empty (Flash of Insight).");
            return;
        }

        state.Log($"{player.Name} looks at the top {top.Count} card(s) of their library (Flash of Insight, X={x}).");

        // Player chooses one card to put into hand
        var chosenId = await handler.ChooseCard(top,
            "Flash of Insight: Choose a card to put into your hand.",
            optional: false, ct);

        GameCard? chosen = chosenId != null
            ? top.FirstOrDefault(c => c.Id == chosenId)
            : null;

        // Fallback: if the handler returned null or an invalid id, pick the first
        chosen ??= top[0];

        // Remove all looked-at cards from library
        foreach (var card in top)
            player.Library.RemoveById(card.Id);

        // Put the chosen card into hand
        player.Hand.Add(chosen);
        state.Log($"{player.Name} puts {chosen.Name} into their hand (Flash of Insight).");

        // Put the rest on the bottom of the library
        foreach (var card in top.Where(c => c.Id != chosen.Id))
            player.Library.AddToBottom(card);
    }
}
