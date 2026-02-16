namespace MtgDecker.Engine.Effects;

/// <summary>
/// Fact or Fiction — Reveal the top 5 cards of your library. An opponent
/// separates them into two piles. Put one pile into your hand and the other
/// into your graveyard.
/// </summary>
public class FactOrFictionEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var caster = state.GetPlayer(spell.ControllerId);
        var opponent = spell.ControllerId == state.Player1.Id ? state.Player2 : state.Player1;

        var revealed = caster.Library.PeekTop(5).ToList();
        if (revealed.Count == 0)
        {
            state.Log($"{caster.Name} reveals the top of their library but it is empty (Fact or Fiction).");
            return;
        }

        state.Log($"{caster.Name} reveals the top {revealed.Count} card(s) of their library (Fact or Fiction): {string.Join(", ", revealed.Select(c => c.Name))}.");

        // Remove all revealed cards from library upfront
        foreach (var card in revealed)
            caster.Library.RemoveById(card.Id);

        // Opponent splits into two piles
        var pile1 = new List<GameCard>();
        var remaining = new List<GameCard>(revealed);

        while (remaining.Count > 0)
        {
            var chosenId = await opponent.DecisionHandler.ChooseCard(
                remaining,
                $"Fact or Fiction: Choose a card for pile 1 ({pile1.Count} selected so far). Skip when done.",
                optional: true, ct);

            if (chosenId == null)
                break; // opponent is done splitting

            var chosen = remaining.FirstOrDefault(c => c.Id == chosenId);
            if (chosen != null)
            {
                pile1.Add(chosen);
                remaining.Remove(chosen);
            }
        }

        var pile2 = remaining; // everything not in pile 1

        state.Log($"{opponent.Name} splits into Pile 1 ({pile1.Count} card(s)) and Pile 2 ({pile2.Count} card(s)).");

        // Caster chooses which pile to take
        List<GameCard> chosenPile;
        List<GameCard> rejectedPile;

        if (pile1.Count == 0)
        {
            // Pile 1 is empty — caster automatically gets pile 2
            chosenPile = pile2;
            rejectedPile = pile1;
            state.Log($"{caster.Name} takes pile 2 ({pile2.Count} card(s)) (Fact or Fiction).");
        }
        else
        {
            // Ask caster: pick from pile 1 = take pile 1, skip (null) = take pile 2
            var pick = await handler.ChooseCard(
                pile1,
                "Fact or Fiction: Choose a card from Pile 1 to take that pile, or skip to take Pile 2.",
                optional: true, ct);

            if (pick != null)
            {
                chosenPile = pile1;
                rejectedPile = pile2;
                state.Log($"{caster.Name} takes pile 1 ({pile1.Count} card(s)) (Fact or Fiction).");
            }
            else
            {
                chosenPile = pile2;
                rejectedPile = pile1;
                state.Log($"{caster.Name} takes pile 2 ({pile2.Count} card(s)) (Fact or Fiction).");
            }
        }

        // Chosen pile → hand
        foreach (var card in chosenPile)
            caster.Hand.Add(card);

        // Rejected pile → graveyard
        foreach (var card in rejectedPile)
            caster.Graveyard.Add(card);
    }
}
