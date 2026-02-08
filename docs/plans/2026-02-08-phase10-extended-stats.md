# Phase 10: Extended Deck Statistics Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Extend deck statistics with average CMC, land-to-spell ratio, mana source analysis, and rarity breakdown.

**Architecture:** Extend the existing `DeckStats` record and `GetDeckStatsHandler` in the Application layer. Add new computed fields. Update the DeckBuilder UI to display the new stats. All calculation is done in-memory from existing card data.

**Tech Stack:** .NET 10, MudBlazor, xUnit + FluentAssertions + NSubstitute

---

### Task 1: Extend DeckStats Record and Handler

**Files:**
- Modify: `src/MtgDecker.Application/Stats/GetDeckStatsQuery.cs`
- Create: `tests/MtgDecker.Application.Tests/Stats/GetDeckStatsTests.cs` (extend from Phase 7 if already exists)

**Step 1: Write the failing tests**

Add to `tests/MtgDecker.Application.Tests/Stats/GetDeckStatsTests.cs`:

```csharp
[Fact]
public async Task Handle_CalculatesAverageCmc()
{
    var cardA = CreateTestCard("Bolt", cmc: 1, typeLine: "Instant");
    var cardB = CreateTestCard("Jace", cmc: 4, typeLine: "Planeswalker");
    var deck = CreateTestDeck(
        (cardA.Id, 4, DeckCategory.MainDeck),
        (cardB.Id, 2, DeckCategory.MainDeck));

    SetupMocks(deck, cardA, cardB);
    var handler = new GetDeckStatsHandler(_deckRepo, _cardRepo);
    var result = await handler.Handle(new GetDeckStatsQuery(deck.Id), CancellationToken.None);

    // (4*1 + 2*4) / 6 = 12/6 = 2.0
    result.AverageCmc.Should().BeApproximately(2.0, 0.01);
}

[Fact]
public async Task Handle_AverageCmc_ExcludesLands()
{
    var bolt = CreateTestCard("Bolt", cmc: 1, typeLine: "Instant");
    var mountain = CreateTestCard("Mountain", cmc: 0, typeLine: "Basic Land — Mountain");
    var deck = CreateTestDeck(
        (bolt.Id, 4, DeckCategory.MainDeck),
        (mountain.Id, 20, DeckCategory.MainDeck));

    SetupMocks(deck, bolt, mountain);
    var handler = new GetDeckStatsHandler(_deckRepo, _cardRepo);
    var result = await handler.Handle(new GetDeckStatsQuery(deck.Id), CancellationToken.None);

    // Only bolt: 4*1 / 4 = 1.0 (lands excluded)
    result.AverageCmc.Should().BeApproximately(1.0, 0.01);
}

[Fact]
public async Task Handle_CalculatesLandToSpellRatio()
{
    var bolt = CreateTestCard("Bolt", cmc: 1, typeLine: "Instant");
    var mountain = CreateTestCard("Mountain", cmc: 0, typeLine: "Basic Land — Mountain");
    var deck = CreateTestDeck(
        (bolt.Id, 36, DeckCategory.MainDeck),
        (mountain.Id, 24, DeckCategory.MainDeck));

    SetupMocks(deck, bolt, mountain);
    var handler = new GetDeckStatsHandler(_deckRepo, _cardRepo);
    var result = await handler.Handle(new GetDeckStatsQuery(deck.Id), CancellationToken.None);

    result.LandCount.Should().Be(24);
    result.SpellCount.Should().Be(36);
}

[Fact]
public async Task Handle_CalculatesRarityBreakdown()
{
    var common = CreateTestCard("Bolt", typeLine: "Instant", rarity: "common");
    var rare = CreateTestCard("Jace", typeLine: "Planeswalker", rarity: "rare");
    var deck = CreateTestDeck(
        (common.Id, 4, DeckCategory.MainDeck),
        (rare.Id, 2, DeckCategory.MainDeck));

    SetupMocks(deck, common, rare);
    var handler = new GetDeckStatsHandler(_deckRepo, _cardRepo);
    var result = await handler.Handle(new GetDeckStatsQuery(deck.Id), CancellationToken.None);

    result.RarityBreakdown["common"].Should().Be(4);
    result.RarityBreakdown["rare"].Should().Be(2);
}

// Helper methods for this test class:
private Card CreateTestCard(string name, double cmc = 1, string typeLine = "Instant", string rarity = "common")
{
    return new Card
    {
        Id = Guid.NewGuid(), Name = name, TypeLine = typeLine, Cmc = cmc,
        Rarity = rarity, SetCode = "tst", SetName = "Test", ScryfallId = Guid.NewGuid().ToString(),
        OracleId = Guid.NewGuid().ToString()
    };
}

private Deck CreateTestDeck(params (Guid CardId, int Qty, DeckCategory Cat)[] entries)
{
    var deck = new Deck
    {
        Id = Guid.NewGuid(), Name = "Test", Format = Format.Modern, UserId = Guid.NewGuid(),
        Entries = entries.Select(e => new DeckEntry
        {
            Id = Guid.NewGuid(), CardId = e.CardId, Quantity = e.Qty, Category = e.Cat
        }).ToList()
    };
    return deck;
}

private void SetupMocks(Deck deck, params Card[] cards)
{
    _deckRepo.GetByIdAsync(deck.Id, Arg.Any<CancellationToken>()).Returns(deck);
    _cardRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
        .Returns(cards.ToList());
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/MtgDecker.Application.Tests --filter "GetDeckStatsTests" --verbosity quiet`
Expected: FAIL — `DeckStats` missing `AverageCmc`, `LandCount`, `SpellCount`, `RarityBreakdown`

**Step 3: Extend DeckStats record**

Update `src/MtgDecker.Application/Stats/GetDeckStatsQuery.cs`:

```csharp
public record DeckStats(
    int TotalCards,
    int MainDeckCount,
    int SideboardCount,
    Dictionary<int, int> ManaCurve,
    Dictionary<string, int> ColorDistribution,
    Dictionary<string, int> TypeBreakdown,
    decimal TotalPriceUsd,
    double AverageCmc,
    int LandCount,
    int SpellCount,
    Dictionary<string, int> RarityBreakdown);
```

**Step 4: Update handler to compute new stats**

In the handler's `Handle` method, add tracking variables alongside the existing ones:

```csharp
double totalCmc = 0;
int nonLandCardCount = 0;
int landCount = 0;
int spellCount = 0;
var rarityBd = new Dictionary<string, int>();
```

Inside the existing foreach loop, add:

```csharp
// Land vs spell count
bool isLand = card.TypeLine.Contains("Land", StringComparison.OrdinalIgnoreCase);
if (isLand)
    landCount += entry.Quantity;
else
    spellCount += entry.Quantity;

// Average CMC (exclude lands)
if (!isLand)
{
    totalCmc += card.Cmc * entry.Quantity;
    nonLandCardCount += entry.Quantity;
}

// Rarity breakdown
var rarity = card.Rarity.ToLowerInvariant();
rarityBd[rarity] = rarityBd.GetValueOrDefault(rarity) + entry.Quantity;
```

Update the return:

```csharp
return new DeckStats(
    deck.Entries.Sum(e => e.Quantity),
    deck.TotalMainDeckCount,
    deck.TotalSideboardCount,
    manaCurve,
    colorDist,
    typeBd,
    totalPrice,
    nonLandCardCount > 0 ? totalCmc / nonLandCardCount : 0,
    landCount,
    spellCount,
    rarityBd);
```

**Step 5: Run tests to verify they pass**

Run: `dotnet test tests/MtgDecker.Application.Tests --filter "GetDeckStatsTests" --verbosity quiet`
Expected: All tests PASS

**Step 6: Run full test suite**

Run: `dotnet test tests/ --verbosity quiet`
Expected: All tests PASS (may need to update any other tests that construct DeckStats)

**Step 7: Commit**

```bash
git add src/MtgDecker.Application/Stats/GetDeckStatsQuery.cs tests/MtgDecker.Application.Tests/Stats/GetDeckStatsTests.cs
git commit -m "feat: add average CMC, land/spell ratio, rarity breakdown to deck stats"
```

---

### Task 2: Update DeckBuilder UI to Display New Stats

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/DeckBuilder.razor`

**Step 1: Add new stats display**

In the stats bar section (around line 29-52), add a fourth column or extend the types column:

Replace the types `MudItem` with an expanded stats section:

```razor
<MudItem xs="12" md="4">
    <MudText Typo="Typo.subtitle2" Class="mb-1">Deck Info</MudText>
    <MudStack Spacing="1">
        @foreach (var type in (_stats.TypeBreakdown ?? new()))
        {
            <MudText Typo="Typo.body2">@type.Key: @type.Value</MudText>
        }
        <MudDivider Class="my-1" />
        <MudText Typo="Typo.body2">Avg CMC: @_stats.AverageCmc.ToString("F2")</MudText>
        <MudText Typo="Typo.body2">Lands: @_stats.LandCount | Spells: @_stats.SpellCount</MudText>
        @if (_stats.RarityBreakdown.Count > 0)
        {
            <MudText Typo="Typo.body2">
                @string.Join(" | ", _stats.RarityBreakdown.OrderBy(r => r.Key).Select(r => $"{r.Key}: {r.Value}"))
            </MudText>
        }
    </MudStack>
</MudItem>
```

**Step 2: Run all tests**

Run: `dotnet test tests/ --verbosity quiet`
Expected: All tests PASS

**Step 3: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/DeckBuilder.razor
git commit -m "feat: display extended stats in deck builder"
git push
```
