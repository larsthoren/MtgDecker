# Phase 7: Card Pricing Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Display card prices from Scryfall throughout the app — on individual cards, deck totals, and collection value.

**Architecture:** Scryfall bulk data already includes a `prices` object per card with `usd`, `usd_foil`, `eur`, `eur_foil`, and `tix` fields (all nullable strings representing decimals). We add 5 nullable `decimal?` properties to the `Card` domain entity, parse them in the Scryfall mapper, persist via EF Core, and display them in CardDetailDialog, CardSearch, DeckBuilder/DeckStats, and MyCollection.

**Tech Stack:** .NET 10, EF Core 10, MudBlazor, xUnit + FluentAssertions + NSubstitute

---

### Task 1: Add Price Properties to Domain Card Entity

**Files:**
- Modify: `src/MtgDecker.Domain/Entities/Card.cs`

**Step 1: Add price properties to Card.cs**

Add after the `Layout` property (line 26):

```csharp
public decimal? PriceUsd { get; set; }
public decimal? PriceUsdFoil { get; set; }
public decimal? PriceEur { get; set; }
public decimal? PriceEurFoil { get; set; }
public decimal? PriceTix { get; set; }
```

**Step 2: Run all existing tests to confirm nothing breaks**

Run: `dotnet test tests/ --verbosity quiet`
Expected: All 141 tests PASS (no existing tests reference prices)

**Step 3: Commit**

```bash
git add src/MtgDecker.Domain/Entities/Card.cs
git commit -m "feat: add price properties to Card entity"
```

---

### Task 2: Add Scryfall Price Parsing

**Files:**
- Modify: `src/MtgDecker.Infrastructure/Scryfall/ScryfallCard.cs`
- Modify: `src/MtgDecker.Infrastructure/Scryfall/ScryfallCardMapper.cs`
- Modify: `tests/MtgDecker.Infrastructure.Tests/Scryfall/ScryfallCardMapperTests.cs`

**Step 1: Write the failing test**

Add to `ScryfallCardMapperTests.cs`:

```csharp
[Fact]
public void MapToCard_WithPrices_MapsPriceFields()
{
    var source = CreateMinimalCard();
    source.Prices = new ScryfallPrices
    {
        Usd = "1.50",
        UsdFoil = "3.25",
        Eur = "1.20",
        EurFoil = "2.80",
        Tix = "0.50"
    };

    var card = ScryfallCardMapper.MapToCard(source);

    card.PriceUsd.Should().Be(1.50m);
    card.PriceUsdFoil.Should().Be(3.25m);
    card.PriceEur.Should().Be(1.20m);
    card.PriceEurFoil.Should().Be(2.80m);
    card.PriceTix.Should().Be(0.50m);
}

[Fact]
public void MapToCard_NullPrices_DefaultsToNull()
{
    var source = CreateMinimalCard();
    source.Prices = null;

    var card = ScryfallCardMapper.MapToCard(source);

    card.PriceUsd.Should().BeNull();
    card.PriceUsdFoil.Should().BeNull();
    card.PriceEur.Should().BeNull();
    card.PriceEurFoil.Should().BeNull();
    card.PriceTix.Should().BeNull();
}

[Fact]
public void MapToCard_PartialPrices_MapsAvailableOnes()
{
    var source = CreateMinimalCard();
    source.Prices = new ScryfallPrices
    {
        Usd = "0.25",
        UsdFoil = null,
        Eur = null,
        EurFoil = null,
        Tix = null
    };

    var card = ScryfallCardMapper.MapToCard(source);

    card.PriceUsd.Should().Be(0.25m);
    card.PriceUsdFoil.Should().BeNull();
    card.PriceEur.Should().BeNull();
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/MtgDecker.Infrastructure.Tests --filter "ScryfallCardMapperTests" --verbosity quiet`
Expected: FAIL — `ScryfallPrices` class does not exist

**Step 3: Add ScryfallPrices class to ScryfallCard.cs**

Add at the bottom of `src/MtgDecker.Infrastructure/Scryfall/ScryfallCard.cs` (before closing of file), and add the `Prices` property to `ScryfallCard`:

Add to `ScryfallCard` class after the `CardFaces` property:

```csharp
[JsonPropertyName("prices")]
public ScryfallPrices? Prices { get; set; }
```

Add new class after `ScryfallCardFace`:

```csharp
public class ScryfallPrices
{
    [JsonPropertyName("usd")]
    public string? Usd { get; set; }

    [JsonPropertyName("usd_foil")]
    public string? UsdFoil { get; set; }

    [JsonPropertyName("eur")]
    public string? Eur { get; set; }

    [JsonPropertyName("eur_foil")]
    public string? EurFoil { get; set; }

    [JsonPropertyName("tix")]
    public string? Tix { get; set; }
}
```

**Step 4: Update ScryfallCardMapper.MapToCard to map prices**

Add after the `ImageUriArtCrop` mapping (line 30) in the object initializer:

```csharp
PriceUsd = ParsePrice(source.Prices?.Usd),
PriceUsdFoil = ParsePrice(source.Prices?.UsdFoil),
PriceEur = ParsePrice(source.Prices?.Eur),
PriceEurFoil = ParsePrice(source.Prices?.EurFoil),
PriceTix = ParsePrice(source.Prices?.Tix)
```

Add helper method to `ScryfallCardMapper`:

```csharp
private static decimal? ParsePrice(string? value)
    => decimal.TryParse(value, System.Globalization.NumberStyles.Any,
        System.Globalization.CultureInfo.InvariantCulture, out var result)
        ? result : null;
```

**Step 5: Run tests to verify they pass**

Run: `dotnet test tests/MtgDecker.Infrastructure.Tests --filter "ScryfallCardMapperTests" --verbosity quiet`
Expected: All mapper tests PASS

**Step 6: Commit**

```bash
git add src/MtgDecker.Infrastructure/Scryfall/ScryfallCard.cs src/MtgDecker.Infrastructure/Scryfall/ScryfallCardMapper.cs tests/MtgDecker.Infrastructure.Tests/Scryfall/ScryfallCardMapperTests.cs
git commit -m "feat: parse Scryfall price data in card mapper"
```

---

### Task 3: EF Core Configuration and Migration

**Files:**
- Modify: `src/MtgDecker.Infrastructure/Data/Configurations/CardConfiguration.cs`
- Modify: `src/MtgDecker.Infrastructure/Data/Repositories/CardRepository.cs` (UpsertBatchAsync)
- Create: new migration file (auto-generated)

**Step 1: Add price column config to CardConfiguration.cs**

Add after the `Layout` property config (line 28), before the `HasIndex` lines:

```csharp
builder.Property(c => c.PriceUsd).HasColumnType("decimal(10,2)");
builder.Property(c => c.PriceUsdFoil).HasColumnType("decimal(10,2)");
builder.Property(c => c.PriceEur).HasColumnType("decimal(10,2)");
builder.Property(c => c.PriceEurFoil).HasColumnType("decimal(10,2)");
builder.Property(c => c.PriceTix).HasColumnType("decimal(10,2)");
```

**Step 2: Update UpsertBatchAsync to include price fields**

In `CardRepository.cs`, add to the `else` block of `UpsertBatchAsync` after `existing.Layout = card.Layout;` (around line 129):

```csharp
existing.PriceUsd = card.PriceUsd;
existing.PriceUsdFoil = card.PriceUsdFoil;
existing.PriceEur = card.PriceEur;
existing.PriceEurFoil = card.PriceEurFoil;
existing.PriceTix = card.PriceTix;
```

**Step 3: Generate migration**

Run:
```bash
dotnet ef migrations add AddCardPrices --project src/MtgDecker.Infrastructure --startup-project src/MtgDecker.Web
```

**Step 4: Apply migration**

Run:
```bash
dotnet ef database update --project src/MtgDecker.Infrastructure --startup-project src/MtgDecker.Web
```

**Step 5: Run all tests**

Run: `dotnet test tests/ --verbosity quiet`
Expected: All tests PASS

**Step 6: Commit**

```bash
git add src/MtgDecker.Infrastructure/Data/Configurations/CardConfiguration.cs src/MtgDecker.Infrastructure/Data/Repositories/CardRepository.cs src/MtgDecker.Infrastructure/Data/Migrations/
git commit -m "feat: add price columns to database with migration"
```

---

### Task 4: Display Price in Card Detail Dialog

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/CardDetailDialog.razor`

**Step 1: Add price display section**

In `CardDetailDialog.razor`, add after the `Colors` display block (after line 104) and before the legalities `@if` block:

```razor
@if (Card.PriceUsd.HasValue || Card.PriceEur.HasValue)
{
    <MudStack Row="true" Spacing="4">
        @if (Card.PriceUsd.HasValue)
        {
            <MudText Typo="Typo.body2">
                <b>USD:</b> $@Card.PriceUsd.Value.ToString("F2")
                @if (Card.PriceUsdFoil.HasValue)
                {
                    <span class="ml-2">(Foil: $@Card.PriceUsdFoil.Value.ToString("F2"))</span>
                }
            </MudText>
        }
        @if (Card.PriceEur.HasValue)
        {
            <MudText Typo="Typo.body2">
                <b>EUR:</b> @("\u20AC")@Card.PriceEur.Value.ToString("F2")
                @if (Card.PriceEurFoil.HasValue)
                {
                    <span class="ml-2">(Foil: @("\u20AC")@Card.PriceEurFoil.Value.ToString("F2"))</span>
                }
            </MudText>
        }
    </MudStack>
}
```

**Step 2: Run app and verify visually**

Run: `dotnet run --project src/MtgDecker.Web`
Navigate to Card Search, click a card, verify price displays.

**Step 3: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/CardDetailDialog.razor
git commit -m "feat: show card price in detail dialog"
```

---

### Task 5: Display Price in Card Search Results

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/CardSearch.razor`

**Step 1: Add price column to the data grid (list view)**

In `CardSearch.razor`, add a new column after the `SetCode` column (line 114):

```razor
<PropertyColumn Property="x => x.PriceUsd" Title="Price" Format="$0.00" />
```

**Step 2: Add price overlay to grid view**

In the grid view section, add a price label under each card image. Replace the card grid item content (lines 89-101) with:

```razor
<div class="cursor-pointer" @onclick="() => ShowCardDetail(card)">
    @if (!string.IsNullOrEmpty(card.ImageUri))
    {
        <img src="@card.ImageUri" alt="@card.Name" loading="lazy"
             style="width: 100%; border-radius: 8px; display: block;" />
    }
    else
    {
        <MudPaper Style="aspect-ratio: 5/7;" Class="d-flex align-center justify-center rounded-lg">
            <MudText Typo="Typo.body2" Align="Align.Center">@card.Name</MudText>
        </MudPaper>
    }
    @if (card.PriceUsd.HasValue)
    {
        <MudText Typo="Typo.caption" Align="Align.Center" Class="mt-1">
            $@card.PriceUsd.Value.ToString("F2")
        </MudText>
    }
</div>
```

**Step 3: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/CardSearch.razor
git commit -m "feat: show card prices in search results"
```

---

### Task 6: Add Deck Price Total to DeckStats

**Files:**
- Modify: `src/MtgDecker.Application/Stats/GetDeckStatsQuery.cs`
- Modify: `tests/MtgDecker.Application.Tests/Stats/GetDeckStatsQueryTests.cs` (if exists, otherwise create)

**Step 1: Write the failing test**

Check if `tests/MtgDecker.Application.Tests/Stats/` exists. If not, create the test file.

Create/modify `tests/MtgDecker.Application.Tests/Stats/GetDeckStatsTests.cs`:

```csharp
using FluentAssertions;
using NSubstitute;
using MtgDecker.Application.Interfaces;
using MtgDecker.Application.Stats;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Tests.Stats;

public class GetDeckStatsTests
{
    private readonly IDeckRepository _deckRepo = Substitute.For<IDeckRepository>();
    private readonly ICardRepository _cardRepo = Substitute.For<ICardRepository>();

    [Fact]
    public async Task Handle_CalculatesTotalPrice()
    {
        var cardA = new Card { Id = Guid.NewGuid(), Name = "Bolt", TypeLine = "Instant", PriceUsd = 1.50m, Rarity = "common", SetCode = "lea", SetName = "Alpha", ScryfallId = "a", OracleId = "a" };
        var cardB = new Card { Id = Guid.NewGuid(), Name = "Force", TypeLine = "Instant", PriceUsd = 80.00m, Rarity = "rare", SetCode = "all", SetName = "Alliances", ScryfallId = "b", OracleId = "b" };
        var deck = new Deck
        {
            Id = Guid.NewGuid(), Name = "Test", Format = Format.Legacy, UserId = Guid.NewGuid(),
            Entries = new List<DeckEntry>
            {
                new() { Id = Guid.NewGuid(), CardId = cardA.Id, Quantity = 4, Category = DeckCategory.MainDeck },
                new() { Id = Guid.NewGuid(), CardId = cardB.Id, Quantity = 2, Category = DeckCategory.MainDeck }
            }
        };

        _deckRepo.GetByIdAsync(deck.Id, Arg.Any<CancellationToken>()).Returns(deck);
        _cardRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { cardA, cardB });

        var handler = new GetDeckStatsHandler(_deckRepo, _cardRepo);
        var result = await handler.Handle(new GetDeckStatsQuery(deck.Id), CancellationToken.None);

        // 4 * 1.50 + 2 * 80.00 = 166.00
        result.TotalPriceUsd.Should().Be(166.00m);
    }

    [Fact]
    public async Task Handle_CardsWithNoPrices_TotalPriceIsZero()
    {
        var card = new Card { Id = Guid.NewGuid(), Name = "Bolt", TypeLine = "Instant", PriceUsd = null, Rarity = "common", SetCode = "lea", SetName = "Alpha", ScryfallId = "a", OracleId = "a" };
        var deck = new Deck
        {
            Id = Guid.NewGuid(), Name = "Test", Format = Format.Legacy, UserId = Guid.NewGuid(),
            Entries = new List<DeckEntry>
            {
                new() { Id = Guid.NewGuid(), CardId = card.Id, Quantity = 4, Category = DeckCategory.MainDeck }
            }
        };

        _deckRepo.GetByIdAsync(deck.Id, Arg.Any<CancellationToken>()).Returns(deck);
        _cardRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { card });

        var handler = new GetDeckStatsHandler(_deckRepo, _cardRepo);
        var result = await handler.Handle(new GetDeckStatsQuery(deck.Id), CancellationToken.None);

        result.TotalPriceUsd.Should().Be(0m);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MtgDecker.Application.Tests --filter "GetDeckStatsTests" --verbosity quiet`
Expected: FAIL — `DeckStats` has no `TotalPriceUsd` property

**Step 3: Update DeckStats record and handler**

In `src/MtgDecker.Application/Stats/GetDeckStatsQuery.cs`, update the `DeckStats` record:

```csharp
public record DeckStats(
    int TotalCards,
    int MainDeckCount,
    int SideboardCount,
    Dictionary<int, int> ManaCurve,
    Dictionary<string, int> ColorDistribution,
    Dictionary<string, int> TypeBreakdown,
    decimal TotalPriceUsd);
```

In the handler's `Handle` method, add price calculation before the return statement:

```csharp
var totalPrice = 0m;
foreach (var entry in deck.Entries)
{
    if (cards.TryGetValue(entry.CardId, out var priceCard) && priceCard.PriceUsd.HasValue)
        totalPrice += priceCard.PriceUsd.Value * entry.Quantity;
}
```

Update the return statement to include the new field:

```csharp
return new DeckStats(
    deck.Entries.Sum(e => e.Quantity),
    deck.TotalMainDeckCount,
    deck.TotalSideboardCount,
    manaCurve,
    colorDist,
    typeBd,
    totalPrice);
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/MtgDecker.Application.Tests --filter "GetDeckStatsTests" --verbosity quiet`
Expected: PASS

**Step 5: Run all tests to confirm nothing broke**

Run: `dotnet test tests/ --verbosity quiet`
Expected: All tests PASS

**Step 6: Commit**

```bash
git add src/MtgDecker.Application/Stats/GetDeckStatsQuery.cs tests/MtgDecker.Application.Tests/Stats/GetDeckStatsTests.cs
git commit -m "feat: add total deck price to DeckStats"
```

---

### Task 7: Display Deck Price in DeckBuilder UI

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/DeckBuilder.razor`

**Step 1: Add price display to stats bar and deck header**

In `DeckBuilder.razor`, update the header area (around line 19-25) to show total price:

Replace the `MudText` showing main/SB counts:

```razor
<MudText Typo="Typo.body2">
    Main: @_deck.TotalMainDeckCount
    @if (_deck.TotalSideboardCount > 0)
    {
        <span> | SB: @_deck.TotalSideboardCount</span>
    }
    @if (_stats?.TotalPriceUsd > 0)
    {
        <span> | $@_stats.TotalPriceUsd.ToString("F2")</span>
    }
</MudText>
```

**Step 2: Add per-card price in the deck list**

In the main deck list entry (around line 114-115), update the card text to include price:

```razor
<MudText Typo="Typo.body2" Class="flex-grow-1">
    @entry.Quantity x @GetCardName(entry.CardId)
    @{ var price = GetCardPrice(entry.CardId); }
    @if (price.HasValue)
    {
        <span style="color: var(--mud-palette-text-secondary); font-size: 0.8em;">
            ($@((price.Value * entry.Quantity).ToString("F2")))
        </span>
    }
</MudText>
```

Add same pattern to sideboard entries.

**Step 3: Add GetCardPrice helper and update card data loading**

In the `@code` block, change `_cardNames` to store full card references:

```csharp
private Dictionary<Guid, MtgDecker.Domain.Entities.Card> _cardData = new();
```

Update `LoadCardNames`:

```csharp
private async Task LoadCardNames()
{
    if (_deck == null) return;
    foreach (var entry in _deck.Entries)
    {
        if (!_cardData.ContainsKey(entry.CardId))
        {
            var card = await Mediator.Send(new MtgDecker.Application.Cards.GetCardByIdQuery(entry.CardId));
            if (card != null)
            {
                _cardData[entry.CardId] = card;
                _cardNames[entry.CardId] = card.Name;
            }
        }
    }
}
```

Add helper:

```csharp
private decimal? GetCardPrice(Guid cardId) =>
    _cardData.TryGetValue(cardId, out var card) ? card.PriceUsd : null;
```

**Step 4: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/DeckBuilder.razor
git commit -m "feat: show card prices and deck total in deck builder"
```

---

### Task 8: Display Collection Value on MyCollection Page

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/MyCollection.razor`

**Step 1: Add price column and collection total**

In `MyCollection.razor`, add `Price` and `TotalValue` to the `CollectionDisplayEntry` class:

```csharp
public decimal? Price { get; set; }
public string PriceDisplay => Price.HasValue ? $"${Price.Value:F2}" : "—";
public string TotalValueDisplay => Price.HasValue ? $"${(Price.Value * Quantity):F2}" : "—";
```

**Step 2: Update LoadCollection to populate price**

In `LoadCollection`, update the display entry creation to include card price:

```csharp
_displayEntries.Add(new CollectionDisplayEntry
{
    Id = entry.Id,
    CardId = entry.CardId,
    CardName = card?.Name ?? "Unknown",
    SetCode = card?.SetCode?.ToUpperInvariant() ?? "",
    Quantity = entry.Quantity,
    Foil = entry.IsFoil ? "Yes" : "No",
    Condition = entry.Condition.ToString(),
    Price = entry.IsFoil ? card?.PriceUsdFoil ?? card?.PriceUsd : card?.PriceUsd
});
```

**Step 3: Add price columns to data grid**

Add after the `Condition` column:

```razor
<PropertyColumn Property="x => x.PriceDisplay" Title="Price" />
<PropertyColumn Property="x => x.TotalValueDisplay" Title="Total" />
```

**Step 4: Add collection total value display**

Add before the data grid (after `else {` around line 39):

```razor
<MudPaper Class="pa-3 mb-3" Elevation="1">
    <MudStack Row="true" Spacing="4" AlignItems="AlignItems.Center">
        <MudText Typo="Typo.body1">
            <b>Total Cards:</b> @_displayEntries.Sum(e => e.Quantity)
        </MudText>
        <MudText Typo="Typo.body1">
            <b>Unique Cards:</b> @_displayEntries.Count
        </MudText>
        <MudText Typo="Typo.body1">
            <b>Collection Value:</b> $@(_displayEntries.Where(e => e.Price.HasValue).Sum(e => e.Price!.Value * e.Quantity).ToString("F2"))
        </MudText>
    </MudStack>
</MudPaper>
```

**Step 5: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/MyCollection.razor
git commit -m "feat: show prices and total collection value"
```

---

### Task 9: Final Integration Test and Cleanup

**Step 1: Run full test suite**

Run: `dotnet test tests/ --verbosity quiet`
Expected: All tests PASS

**Step 2: Run the app and verify end-to-end**

Run: `dotnet run --project src/MtgDecker.Web`

Verify:
- [ ] Import data at `/admin/import` (prices will be populated from Scryfall)
- [ ] Card Search grid view shows price under each card image
- [ ] Card Search list view shows price column
- [ ] Card Detail Dialog shows USD and EUR prices
- [ ] Deck Builder shows per-card prices and deck total
- [ ] My Collection shows price per entry and total collection value

**Step 3: Final commit if any cleanup needed**

```bash
git add -A
git commit -m "feat: complete card pricing integration"
git push
```
