namespace MtgDecker.Engine.Effects;

/// <summary>
/// Funeral Pyre — Exile target card from a graveyard. Its owner creates a
/// 1/1 white Spirit creature token with flying.
/// </summary>
public class FuneralPyreEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var allGraveyardCards = state.Player1.Graveyard.Cards
            .Concat(state.Player2.Graveyard.Cards)
            .ToList();

        if (allGraveyardCards.Count == 0)
        {
            state.Log("No cards in any graveyard for Funeral Pyre.");
            return;
        }

        var chosenId = await handler.ChooseCard(allGraveyardCards,
            "Choose a card from any graveyard to exile", optional: false, ct);

        if (chosenId.HasValue)
        {
            // Find which player owns the card
            var card = state.Player1.Graveyard.RemoveById(chosenId.Value);
            var owner = state.Player1;
            if (card == null)
            {
                card = state.Player2.Graveyard.RemoveById(chosenId.Value);
                owner = state.Player2;
            }

            if (card != null)
            {
                owner.Exile.Add(card);
                state.Log($"{card.Name} is exiled from {owner.Name}'s graveyard.");

                // Owner creates a 1/1 white Spirit with flying
                var token = new GameCard
                {
                    Name = "Spirit",
                    BasePower = 1,
                    BaseToughness = 1,
                    CardTypes = Enums.CardType.Creature,
                    Subtypes = ["Spirit"],
                    IsToken = true,
                    TurnEnteredBattlefield = state.TurnNumber,
                };
                owner.Battlefield.Add(token);
                state.Log($"{owner.Name} creates a 1/1 Spirit token with flying.");
            }
        }
    }
}
