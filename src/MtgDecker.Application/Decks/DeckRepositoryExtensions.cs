using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;

namespace MtgDecker.Application.Decks;

public static class DeckRepositoryExtensions
{
    public static async Task<Deck> GetMutableDeckAsync(
        this IDeckRepository repository, Guid deckId, CancellationToken ct = default)
    {
        var deck = await repository.GetByIdAsync(deckId, ct)
            ?? throw new KeyNotFoundException($"Deck {deckId} not found.");

        if (deck.IsSystemDeck)
            throw new InvalidOperationException("System decks cannot be modified.");

        return deck;
    }
}
