namespace MtgDecker.Engine.Effects;

public class IntuitionEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var caster = state.GetPlayer(spell.ControllerId);
        var opponent = state.GetOpponent(caster);
        var library = caster.Library.Cards.ToList();

        if (library.Count == 0)
        {
            state.Log($"{caster.Name}'s library is empty — Intuition finds nothing.");
            return;
        }

        // Caster picks up to 3 cards from library
        var chosen = new List<GameCard>();
        var maxPicks = Math.Min(3, library.Count);

        for (int i = 0; i < maxPicks; i++)
        {
            var available = caster.Library.Cards
                .Where(c => !chosen.Contains(c))
                .ToList();

            if (available.Count == 0) break;

            var pick = await caster.DecisionHandler.ChooseCard(
                available,
                $"Search: choose card {i + 1} of {maxPicks} (Intuition)",
                optional: i > 0,
                ct);

            if (!pick.HasValue) break;

            var card = available.FirstOrDefault(c => c.Id == pick.Value);
            if (card != null)
                chosen.Add(card);
        }

        if (chosen.Count == 0)
        {
            state.Log("Intuition finds no cards.");
            return;
        }

        // Opponent chooses 1 to go to caster's hand
        Guid? opponentChoice = null;
        if (chosen.Count == 1)
        {
            // Only 1 card — it goes to hand automatically
            opponentChoice = chosen[0].Id;
        }
        else
        {
            opponentChoice = await opponent.DecisionHandler.ChooseCard(
                chosen, "Choose one card for opponent's hand (rest go to graveyard)",
                optional: false, ct);
        }

        foreach (var card in chosen)
        {
            caster.Library.Remove(card);

            if (card.Id == opponentChoice)
            {
                caster.Hand.Add(card);
                state.Log($"{card.Name} goes to {caster.Name}'s hand (chosen by {opponent.Name}).");
            }
            else
            {
                caster.Graveyard.Add(card);
                state.Log($"{card.Name} goes to {caster.Name}'s graveyard.");
            }
        }

        caster.Library.Shuffle();
    }
}
