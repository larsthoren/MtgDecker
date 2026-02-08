# Phase 9: Maybeboard Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a Maybeboard zone to decks — a "considering" list of cards that don't count toward deck limits or totals.

**Architecture:** Add `Maybeboard` to the `DeckCategory` enum. Update domain rules so maybeboard cards skip copy-limit checks. Update the DeckBuilder UI, export handler, and stats handler to handle the new category. No database migration needed — EF stores enums as integers and a new enum value is handled automatically.

**Tech Stack:** .NET 10, EF Core 10, MudBlazor, xUnit + FluentAssertions

---

### Task 1: Add Maybeboard to DeckCategory Enum and Update Domain Rules

**Files:**
- Modify: `src/MtgDecker.Domain/Enums/DeckCategory.cs`
- Modify: `src/MtgDecker.Domain/Entities/Deck.cs`
- Modify: `tests/MtgDecker.Domain.Tests/Entities/DeckTests.cs`

**Step 1: Write the failing tests**

Add to `tests/MtgDecker.Domain.Tests/Entities/DeckTests.cs`:

```csharp
[Fact]
public void AddCard_ToMaybeboard_SkipsCopyLimit()
{
    var deck = CreateDeck(Format.Modern);
    var card = CreateCard("Lightning Bolt");

    deck.AddCard(card, 10, DeckCategory.Maybeboard);

    deck.Entries.Should().HaveCount(1);
    deck.Entries[0].Quantity.Should().Be(10);
    deck.Entries[0].Category.Should().Be(DeckCategory.Maybeboard);
}

[Fact]
public void AddCard_ToMaybeboard_DoesNotCountInMainDeck()
{
    var deck = CreateDeck(Format.Modern);
    deck.AddCard(CreateCard("Card A"), 4, DeckCategory.MainDeck);
    deck.AddCard(CreateCard("Card B"), 3, DeckCategory.Maybeboard);

    deck.TotalMainDeckCount.Should().Be(4);
    deck.TotalMaybeboardCount.Should().Be(3);
}

[Fact]
public void AddCard_ToMaybeboard_InCommanderFormat_Succeeds()
{
    var deck = CreateDeck(Format.Commander);
    var card = CreateCard("Sol Ring");

    deck.AddCard(card, 5, DeckCategory.Maybeboard);

    deck.Entries.Should().HaveCount(1);
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/MtgDecker.Domain.Tests --filter "DeckTests" --verbosity quiet`
Expected: FAIL — `DeckCategory.Maybeboard` does not exist

**Step 3: Add Maybeboard to enum**

Modify `src/MtgDecker.Domain/Enums/DeckCategory.cs`:

```csharp
namespace MtgDecker.Domain.Enums;

public enum DeckCategory
{
    MainDeck,
    Sideboard,
    Maybeboard
}
```

**Step 4: Add TotalMaybeboardCount to Deck**

In `src/MtgDecker.Domain/Entities/Deck.cs`, add after `TotalSideboardCount`:

```csharp
public int TotalMaybeboardCount => Entries
    .Where(e => e.Category == DeckCategory.Maybeboard)
    .Sum(e => e.Quantity);
```

**Step 5: Update AddCard to skip limits for Maybeboard**

In `Deck.AddCard`, update the copy-limit logic. The sideboard check already exists. Add maybeboard exemption. Replace the method body:

```csharp
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
```

**Step 6: Run tests to verify they pass**

Run: `dotnet test tests/MtgDecker.Domain.Tests --filter "DeckTests" --verbosity quiet`
Expected: All tests PASS

**Step 7: Commit**

```bash
git add src/MtgDecker.Domain/Enums/DeckCategory.cs src/MtgDecker.Domain/Entities/Deck.cs tests/MtgDecker.Domain.Tests/Entities/DeckTests.cs
git commit -m "feat: add Maybeboard to DeckCategory with domain rules"
```

---

### Task 2: Update DeckBuilder UI for Maybeboard

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/DeckBuilder.razor`

**Step 1: Add maybeboard entries computed property**

In the `@code` block, add:

```csharp
private IEnumerable<MtgDecker.Domain.Entities.DeckEntry> _maybeboardEntries =>
    _deck?.Entries.Where(e => e.Category == MtgDecker.Domain.Enums.DeckCategory.Maybeboard) ?? Enumerable.Empty<MtgDecker.Domain.Entities.DeckEntry>();
```

**Step 2: Add Maybeboard button to search results**

In the search results card list (around line 87-95), add a maybeboard button after the sideboard button:

```razor
<MudIconButton Icon="@Icons.Material.Filled.Bookmark" Size="Size.Small"
               Color="Color.Info" Title="Add to Maybeboard"
               OnClick="() => AddCardToDeck(card, MtgDecker.Domain.Enums.DeckCategory.Maybeboard)" />
```

**Step 3: Add Maybeboard section to the right panel**

After the sideboard section (after line 158), add:

```razor
@if (_maybeboardEntries.Any())
{
    <MudDivider Class="my-3" />
    <MudText Typo="Typo.subtitle1" Class="mb-2">
        Maybeboard (@_deck.TotalMaybeboardCount)
    </MudText>
    <MudList T="MtgDecker.Domain.Entities.DeckEntry" Dense="true">
        @foreach (var entry in _maybeboardEntries)
        {
            <MudListItem>
                <MudStack Row="true" AlignItems="AlignItems.Center">
                    <MudText Typo="Typo.body2" Class="flex-grow-1">
                        @entry.Quantity x @GetCardName(entry.CardId)
                    </MudText>
                    <MudIconButton Icon="@Icons.Material.Filled.ArrowUpward" Size="Size.Small"
                                   Color="Color.Primary" Title="Move to Main Deck"
                                   OnClick="() => MoveToMainDeck(entry)" />
                    <MudIconButton Icon="@Icons.Material.Filled.Remove" Size="Size.Small"
                                   OnClick="() => ChangeQuantity(entry.CardId, entry.Quantity - 1)" />
                    <MudIconButton Icon="@Icons.Material.Filled.Delete" Size="Size.Small"
                                   Color="Color.Error"
                                   OnClick="() => RemoveCard(entry.CardId)" />
                </MudStack>
            </MudListItem>
        }
    </MudList>
}
```

**Step 4: Add MoveToMainDeck method**

```csharp
private async Task MoveToMainDeck(MtgDecker.Domain.Entities.DeckEntry entry)
{
    try
    {
        // Remove from maybeboard, add to main deck
        await Mediator.Send(new MtgDecker.Application.Decks.RemoveCardFromDeckCommand(DeckId, entry.CardId));
        var card = await Mediator.Send(new MtgDecker.Application.Cards.GetCardByIdQuery(entry.CardId));
        if (card != null)
        {
            _deck = await Mediator.Send(new MtgDecker.Application.Decks.AddCardToDeckCommand(
                DeckId, card.Id, entry.Quantity, MtgDecker.Domain.Enums.DeckCategory.MainDeck));
        }
        await LoadStats();
        Snackbar.Add($"Moved {GetCardName(entry.CardId)} to main deck", Severity.Success);
    }
    catch (Exception ex)
    {
        Snackbar.Add(ex.Message, Severity.Error);
    }
}
```

**Step 5: Update header to show maybeboard count**

Update the counts text to include maybeboard:

```razor
<MudText Typo="Typo.body2">
    Main: @_deck.TotalMainDeckCount
    @if (_deck.TotalSideboardCount > 0)
    {
        <span> | SB: @_deck.TotalSideboardCount</span>
    }
    @if (_deck.TotalMaybeboardCount > 0)
    {
        <span> | MB: @_deck.TotalMaybeboardCount</span>
    }
</MudText>
```

**Step 6: Run all tests**

Run: `dotnet test tests/ --verbosity quiet`
Expected: All tests PASS

**Step 7: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/DeckBuilder.razor
git commit -m "feat: add maybeboard section to deck builder UI"
```

---

### Task 3: Update Export and Stats to Handle Maybeboard

**Files:**
- Modify: `src/MtgDecker.Application/DeckExport/ExportDeckQuery.cs`
- Modify: `src/MtgDecker.Application/Stats/GetDeckStatsQuery.cs`

**Step 1: Update export to include maybeboard**

In `ExportDeckQuery.cs`, add maybeboard handling. After the sideboard section in both Arena and MTGO formats:

For Arena format, add:
```csharp
var maybeboard = deck.Entries.Where(e => e.Category == DeckCategory.Maybeboard);
if (maybeboard.Any())
{
    lines.Add("");
    lines.Add("Maybeboard");
    foreach (var entry in maybeboard)
    {
        if (cards.TryGetValue(entry.CardId, out var card))
        {
            var setCode = card.SetCode.ToUpperInvariant();
            lines.Add($"{entry.Quantity} {card.Name} ({setCode}) {card.CollectorNumber}");
        }
    }
}
```

For MTGO format, add:
```csharp
foreach (var entry in deck.Entries.Where(e => e.Category == DeckCategory.Maybeboard))
{
    if (cards.TryGetValue(entry.CardId, out var card))
        lines.Add($"MB: {entry.Quantity} {card.Name}");
}
```

**Step 2: Update stats to exclude maybeboard from counts**

In `GetDeckStatsQuery.cs`, the stats handler already filters `DeckCategory.MainDeck` for mana curve etc. — no changes needed for the main stats loop. But `TotalCards` includes all entries. Update to exclude maybeboard:

```csharp
var countableEntries = deck.Entries.Where(e => e.Category != DeckCategory.Maybeboard).ToList();

return new DeckStats(
    countableEntries.Sum(e => e.Quantity),
    deck.TotalMainDeckCount,
    deck.TotalSideboardCount,
    manaCurve,
    colorDist,
    typeBd,
    totalPrice);
```

**Step 3: Run all tests**

Run: `dotnet test tests/ --verbosity quiet`
Expected: All tests PASS

**Step 4: Commit**

```bash
git add src/MtgDecker.Application/DeckExport/ExportDeckQuery.cs src/MtgDecker.Application/Stats/GetDeckStatsQuery.cs
git commit -m "feat: handle maybeboard in export and stats"
git push
```
