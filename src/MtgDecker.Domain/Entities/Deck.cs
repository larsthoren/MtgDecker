using MtgDecker.Domain.Enums;
using MtgDecker.Domain.Exceptions;
using MtgDecker.Domain.Rules;

namespace MtgDecker.Domain.Entities;

public class Deck
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Format Format { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid UserId { get; set; }

    public List<DeckEntry> Entries { get; set; } = new();

    public int TotalMainDeckCount => Entries
        .Where(e => e.Category == DeckCategory.MainDeck)
        .Sum(e => e.Quantity);

    public int TotalSideboardCount => Entries
        .Where(e => e.Category == DeckCategory.Sideboard)
        .Sum(e => e.Quantity);

    public int TotalMaybeboardCount => Entries
        .Where(e => e.Category == DeckCategory.Maybeboard)
        .Sum(e => e.Quantity);

    public void AddCard(Card card, int quantity, DeckCategory category)
    {
        if (quantity < 1)
            throw new DomainException("Quantity must be at least 1.");

        if (category == DeckCategory.Sideboard && !FormatRules.HasSideboard(Format))
            throw new DomainException($"{Format} does not allow a sideboard.");

        var existing = Entries.FirstOrDefault(e => e.CardId == card.Id && e.Category == category);
        if (existing != null)
        {
            var newQuantity = existing.Quantity + quantity;
            if (category != DeckCategory.Maybeboard && !card.IsBasicLand && newQuantity > FormatRules.GetMaxCopies(Format))
                throw new DomainException(
                    $"A deck cannot exceed {FormatRules.GetMaxCopies(Format)} copies of {card.Name}.");

            existing.Quantity = newQuantity;
        }
        else
        {
            if (category != DeckCategory.Maybeboard && !card.IsBasicLand && quantity > FormatRules.GetMaxCopies(Format))
                throw new DomainException(
                    $"A deck cannot exceed {FormatRules.GetMaxCopies(Format)} copies of {card.Name}.");

            Entries.Add(new DeckEntry
            {
                Id = Guid.NewGuid(),
                DeckId = Id,
                CardId = card.Id,
                Quantity = quantity,
                Category = category
            });
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateCardQuantity(Guid cardId, int quantity)
    {
        var entry = Entries.FirstOrDefault(e => e.CardId == cardId)
            ?? throw new DomainException("Card not found in deck.");

        if (quantity < 1)
            throw new DomainException("Quantity must be at least 1.");

        entry.Quantity = quantity;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveCard(Guid cardId)
    {
        var entry = Entries.FirstOrDefault(e => e.CardId == cardId)
            ?? throw new DomainException("Card not found in deck.");

        Entries.Remove(entry);
        UpdatedAt = DateTime.UtcNow;
    }
}
