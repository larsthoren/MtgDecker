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

        state.Log($"{caster.Name} reveals {revealed.Count} card(s): {string.Join(", ", revealed.Select(c => c.Name))}.");

        // Remove revealed cards from library
        foreach (var card in revealed)
            caster.Library.RemoveById(card.Id);

        // Opponent splits into two piles
        var pile1 = await opponent.DecisionHandler.SplitCards(
            revealed, "Fact or Fiction: Separate these cards into two piles.", ct);

        var pile1Set = new HashSet<Guid>(pile1.Select(c => c.Id));
        var pile2 = revealed.Where(c => !pile1Set.Contains(c.Id)).ToList();

        state.Log($"{opponent.Name} splits: Pile 1 ({pile1.Count}), Pile 2 ({pile2.Count}).");

        List<GameCard> chosenPile;
        List<GameCard> rejectedPile;

        if (pile1.Count == 0)
        {
            chosenPile = pile2;
            rejectedPile = pile1.ToList();
            state.Log($"{caster.Name} takes all {pile2.Count} card(s).");
        }
        else if (pile2.Count == 0)
        {
            chosenPile = pile1.ToList();
            rejectedPile = pile2;
            state.Log($"{caster.Name} takes all {pile1.Count} card(s).");
        }
        else
        {
            var choice = await handler.ChoosePile(
                pile1.ToList(), pile2,
                "Fact or Fiction: Choose which pile to put into your hand.", ct);

            if (choice == 1)
            {
                chosenPile = pile1.ToList();
                rejectedPile = pile2;
                state.Log($"{caster.Name} takes pile 1 ({pile1.Count} card(s)).");
            }
            else
            {
                chosenPile = pile2;
                rejectedPile = pile1.ToList();
                state.Log($"{caster.Name} takes pile 2 ({pile2.Count} card(s)).");
            }
        }

        foreach (var card in chosenPile)
            caster.Hand.Add(card);

        foreach (var card in rejectedPile)
            caster.Graveyard.Add(card);
    }
}
