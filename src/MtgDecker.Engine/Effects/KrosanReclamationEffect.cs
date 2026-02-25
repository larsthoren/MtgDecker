namespace MtgDecker.Engine.Effects;

/// <summary>
/// Krosan Reclamation - Target player shuffles up to two target cards
/// from their graveyard into their library.
/// </summary>
public class KrosanReclamationEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        // Targets[0] is the target player
        if (spell.Targets.Count == 0) return;

        var targetPlayerId = spell.Targets[0].PlayerId;
        var targetPlayer = state.GetPlayer(targetPlayerId);

        if (targetPlayer.Graveyard.Count == 0)
        {
            state.Log($"{targetPlayer.Name} has no cards in graveyard (Krosan Reclamation).");
            return;
        }

        var shuffledCards = new List<GameCard>();

        // Choose up to 2 cards from target player's graveyard
        for (int i = 0; i < 2; i++)
        {
            var remaining = targetPlayer.Graveyard.Cards.ToList();
            if (remaining.Count == 0) break;

            var chosenId = await handler.ChooseCard(remaining,
                $"Krosan Reclamation: Choose a card from {targetPlayer.Name}'s graveyard to shuffle into library ({i + 1}/2, optional).",
                optional: true, ct);

            if (!chosenId.HasValue) break;

            var card = targetPlayer.Graveyard.RemoveById(chosenId.Value);
            if (card != null)
            {
                targetPlayer.Library.Add(card);
                shuffledCards.Add(card);
            }
        }

        if (shuffledCards.Count > 0)
        {
            targetPlayer.Library.Shuffle();
            var names = string.Join(", ", shuffledCards.Select(c => c.Name));
            state.Log($"{names} shuffled into {targetPlayer.Name}'s library (Krosan Reclamation).");
        }
        else
        {
            state.Log($"No cards chosen to shuffle (Krosan Reclamation).");
        }
    }
}
