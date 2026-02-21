# System Decks Design

## Summary

Make `UserId` nullable on `Deck`. Decks with `UserId = null` are permanent system decks — visible to all users, cannot be edited or deleted by anyone. Users can clone system decks into their own collection.

## Domain Layer

- `Deck.UserId` changes from `Guid` to `Guid?`
- Add computed property `bool IsSystemDeck => UserId is null;`
- No guard methods on entity — access control enforced in Application layer

## Infrastructure Layer

- `DeckConfiguration`: `UserId` becomes `.IsRequired(false)`, index kept
- `IDeckRepository`: add `ListSystemDecksAsync(CancellationToken)`
- `DeckRepository`: implement as `Where(d => d.UserId == null)`
- EF Migration: `AlterColumn` UserId to nullable, `UPDATE Decks SET UserId = NULL` to convert all existing decks to system decks

## Application Layer

### Mutation Guards

Every handler that modifies a deck adds a check after loading:

```csharp
if (deck.IsSystemDeck)
    throw new InvalidOperationException("System decks cannot be modified.");
```

Applies to: `DeleteDeckCommand`, `AddCardToDeckCommand`, `RemoveCardFromDeckCommand`, `UpdateCardQuantityCommand`, `MoveCardCategoryCommand`, `UpdateDeckFormatCommand`.

### Queries

- `ListDecksQuery` — unchanged (filters by UserId, system decks excluded)
- New `ListSystemDecksQuery` — no parameters, calls `ListSystemDecksAsync()`

### New Commands

- `CloneDeckCommand(Guid SourceDeckId, Guid UserId)` — loads source deck with entries, creates new Deck with user's UserId, copies name/format/description and all entries

### Modified Commands

- `SeedPresetDecksCommand` — passes `UserId = null` instead of hardcoded user GUID; uses `ListSystemDecksAsync()` to check for existing system decks
- `CreateDeckCommand` — unchanged (UserId stays required for user decks)

## Web Layer

### MyDecks Page

Split into two sections:

1. **"Preset Decks"** — calls `ListSystemDecksQuery`. Cards show "Clone" button and "View" link. No edit/delete.
2. **"My Decks"** — unchanged, calls `ListDecksQuery(UserId)`. Edit/delete remain.

### DeckBuilder Page

When loading a system deck: read-only mode. Hide add/remove card controls, format change, delete button. Show "Clone to My Decks" button in toolbar. Deck detail (cards, stats, images) remains visible.

### CreateDeckDialog

No changes (always creates user decks).
