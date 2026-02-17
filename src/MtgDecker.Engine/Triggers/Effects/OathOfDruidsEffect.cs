namespace MtgDecker.Engine.Triggers.Effects;

public class OathOfDruidsEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var state = context.State;
        var activePlayer = state.ActivePlayer;
        var opponent = state.GetOpponent(activePlayer);

        // Only triggers if an opponent controls more creatures than the active player
        var playerCreatures = activePlayer.Battlefield.Cards.Count(c => c.IsCreature);
        var opponentCreatures = opponent.Battlefield.Cards.Count(c => c.IsCreature);

        if (opponentCreatures <= playerCreatures)
        {
            state.Log($"Oath of Druids: {opponent.Name} doesn't control more creatures. No effect.");
            return;
        }

        // Reveal cards from the top of the active player's library until a creature is found
        var revealed = new List<GameCard>();
        GameCard? creatureFound = null;

        while (activePlayer.Library.Count > 0)
        {
            var card = activePlayer.Library.DrawFromTop();
            if (card == null) break;

            revealed.Add(card);

            if (card.IsCreature)
            {
                creatureFound = card;
                break;
            }
        }

        if (revealed.Count == 0) return;

        // Show revealed cards
        var kept = creatureFound != null ? new List<GameCard> { creatureFound } : new List<GameCard>();
        await context.DecisionHandler.RevealCards(
            revealed, kept,
            $"Oath of Druids: Revealed {revealed.Count} cards" +
            (creatureFound != null ? $" — found {creatureFound.Name}" : " — no creature found"),
            ct);

        // Put creature onto the battlefield
        if (creatureFound != null)
        {
            activePlayer.Battlefield.Add(creatureFound);
            creatureFound.TurnEnteredBattlefield = state.TurnNumber;
            state.Log($"Oath of Druids puts {creatureFound.Name} onto the battlefield.");
        }

        // Put remaining revealed cards into graveyard
        foreach (var card in revealed.Where(c => c != creatureFound))
        {
            activePlayer.Graveyard.Add(card);
            state.Log($"{card.Name} is put into {activePlayer.Name}'s graveyard.");
        }
    }
}
