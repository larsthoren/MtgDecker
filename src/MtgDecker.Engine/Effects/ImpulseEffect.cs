namespace MtgDecker.Engine.Effects;

/// <summary>
/// Impulse — Look at the top 4 cards of your library. Put one of them into
/// your hand and the rest on the bottom of your library in any order.
/// </summary>
public class ImpulseEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var player = state.GetPlayer(spell.ControllerId);

        var top = player.Library.PeekTop(4).ToList();
        if (top.Count == 0)
        {
            state.Log($"{player.Name} looks at the top of their library but it is empty (Impulse).");
            return;
        }

        state.Log($"{player.Name} looks at the top {top.Count} card(s) of their library (Impulse).");

        // Player chooses one card to put into hand
        var chosenId = await handler.ChooseCard(top,
            "Impulse: Choose a card to put into your hand.",
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
        state.Log($"{player.Name} puts {chosen.Name} into their hand (Impulse).");

        // Put the rest on the bottom of the library
        foreach (var card in top.Where(c => c.Id != chosen.Id))
            player.Library.AddToBottom(card);
    }
}
