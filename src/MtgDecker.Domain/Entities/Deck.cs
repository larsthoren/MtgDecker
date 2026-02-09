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
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
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

    public void AddCard(Card card, int quantity, DeckCategory category, DateTime utcNow)
    {
        if (quantity < 1)
            throw new DomainException("Quantity must be at least 1.");

        if (category == DeckCategory.Sideboard && !FormatRules.HasSideboard(Format))
            throw new DomainException($"{Format} does not allow a sideboard.");

        var existing = Entries.FirstOrDefault(e => e.CardId == card.Id && e.Category == category);
        if (existing != null)
        {
            var newQuantity = existing.Quantity + quantity;
            ValidateMaxCopies(card, category, newQuantity);

            existing.Quantity = newQuantity;
        }
        else
        {
            ValidateMaxCopies(card, category, quantity);

            Entries.Add(new DeckEntry
            {
                Id = Guid.NewGuid(),
                DeckId = Id,
                CardId = card.Id,
                Quantity = quantity,
                Category = category
            });
        }

        UpdatedAt = utcNow;
    }

    public void UpdateCardQuantity(Card card, DeckCategory category, int quantity, DateTime utcNow)
    {
        var entry = Entries.FirstOrDefault(e => e.CardId == card.Id && e.Category == category)
            ?? throw new DomainException("Card not found in deck.");

        if (quantity < 1)
            throw new DomainException("Quantity must be at least 1.");

        ValidateMaxCopies(card, category, quantity);

        entry.Quantity = quantity;
        UpdatedAt = utcNow;
    }

    public void RemoveCard(Guid cardId, DeckCategory category, DateTime utcNow)
    {
        var entry = Entries.FirstOrDefault(e => e.CardId == cardId && e.Category == category)
            ?? throw new DomainException("Card not found in deck.");

        Entries.Remove(entry);
        UpdatedAt = utcNow;
    }

    public void MoveCardCategory(Card card, DeckCategory from, DeckCategory to, DateTime utcNow)
    {
        var entry = Entries.FirstOrDefault(e => e.CardId == card.Id && e.Category == from)
            ?? throw new DomainException("Card not found in deck.");

        var quantity = entry.Quantity;

        // Validate target category constraints before removing
        if (to == DeckCategory.Sideboard && !FormatRules.HasSideboard(Format))
            throw new DomainException($"{Format} does not allow a sideboard.");

        ValidateMaxCopies(card, to, quantity);

        // Check if card already exists in target category
        var existingInTarget = Entries.FirstOrDefault(e => e.CardId == card.Id && e.Category == to);
        if (existingInTarget != null)
        {
            var newQuantity = existingInTarget.Quantity + quantity;
            ValidateMaxCopies(card, to, newQuantity);

            existingInTarget.Quantity = newQuantity;
        }
        else
        {
            Entries.Add(new DeckEntry
            {
                Id = Guid.NewGuid(),
                DeckId = Id,
                CardId = card.Id,
                Quantity = quantity,
                Category = to
            });
        }

        Entries.Remove(entry);
        UpdatedAt = utcNow;
    }

    private void ValidateMaxCopies(Card card, DeckCategory category, int quantity)
    {
        if (category != DeckCategory.Maybeboard && !card.IsBasicLand && quantity > FormatRules.GetMaxCopies(Format))
            throw new DomainException(
                $"A deck cannot exceed {FormatRules.GetMaxCopies(Format)} copies of {card.Name}.");
    }
}
