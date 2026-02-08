# Phase 11: Additional Export Formats Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add plain text, CSV, and clipboard-copy export formats for decks, alongside the existing Arena and MTGO formats.

**Architecture:** Extend the existing `ExportDeckHandler` to support "Text" and "CSV" format strings. Add a clipboard-copy button to the export dialog. No new files needed in the Application layer — just extend the existing handler.

**Tech Stack:** .NET 10, MudBlazor, xUnit + FluentAssertions + NSubstitute

---

### Task 1: Add Plain Text and CSV Export Formats

**Files:**
- Modify: `src/MtgDecker.Application/DeckExport/ExportDeckQuery.cs`
- Create: `tests/MtgDecker.Application.Tests/DeckExport/ExportDeckTests.cs`

**Step 1: Write the failing tests**

Create `tests/MtgDecker.Application.Tests/DeckExport/ExportDeckTests.cs`:

```csharp
using FluentAssertions;
using NSubstitute;
using MtgDecker.Application.DeckExport;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Tests.DeckExport;

public class ExportDeckTests
{
    private readonly IDeckRepository _deckRepo = Substitute.For<IDeckRepository>();
    private readonly ICardRepository _cardRepo = Substitute.For<ICardRepository>();

    [Fact]
    public async Task Handle_TextFormat_ReturnsSimpleList()
    {
        var (deck, cards) = CreateTestDeckWithCards();
        SetupMocks(deck, cards);
        var handler = new ExportDeckHandler(_deckRepo, _cardRepo);

        var result = await handler.Handle(new ExportDeckQuery(deck.Id, "Text"), CancellationToken.None);

        result.Should().Contain("4 Lightning Bolt");
        result.Should().Contain("2 Counterspell");
        result.Should().NotContain("(");  // No set codes in text format
    }

    [Fact]
    public async Task Handle_TextFormat_IncludesSideboard()
    {
        var (deck, cards) = CreateTestDeckWithSideboard();
        SetupMocks(deck, cards);
        var handler = new ExportDeckHandler(_deckRepo, _cardRepo);

        var result = await handler.Handle(new ExportDeckQuery(deck.Id, "Text"), CancellationToken.None);

        result.Should().Contain("Sideboard");
        result.Should().Contain("2 Pyroblast");
    }

    [Fact]
    public async Task Handle_CsvFormat_ReturnsHeaderAndRows()
    {
        var (deck, cards) = CreateTestDeckWithCards();
        SetupMocks(deck, cards);
        var handler = new ExportDeckHandler(_deckRepo, _cardRepo);

        var result = await handler.Handle(new ExportDeckQuery(deck.Id, "CSV"), CancellationToken.None);

        var lines = result.Split(Environment.NewLine);
        lines[0].Should().Be("Quantity,Name,Set,Category");
        lines.Should().Contain(l => l.Contains("4,Lightning Bolt,LEA,MainDeck"));
        lines.Should().Contain(l => l.Contains("2,Counterspell,LEA,MainDeck"));
    }

    [Fact]
    public async Task Handle_CsvFormat_EscapesCommasInNames()
    {
        var cardId = Guid.NewGuid();
        var card = new Card
        {
            Id = cardId, Name = "Jace, the Mind Sculptor", TypeLine = "Planeswalker",
            Rarity = "mythic", SetCode = "wwk", SetName = "Worldwake",
            ScryfallId = "a", OracleId = "a", CollectorNumber = "31"
        };
        var deck = new Deck
        {
            Id = Guid.NewGuid(), Name = "Test", Format = Format.Legacy, UserId = Guid.NewGuid(),
            Entries = new List<DeckEntry>
            {
                new() { Id = Guid.NewGuid(), CardId = cardId, Quantity = 3, Category = DeckCategory.MainDeck }
            }
        };
        SetupMocks(deck, new List<Card> { card });
        var handler = new ExportDeckHandler(_deckRepo, _cardRepo);

        var result = await handler.Handle(new ExportDeckQuery(deck.Id, "CSV"), CancellationToken.None);

        result.Should().Contain("3,\"Jace, the Mind Sculptor\",WWK,MainDeck");
    }

    [Fact]
    public async Task Handle_ArenaFormat_StillWorks()
    {
        var (deck, cards) = CreateTestDeckWithCards();
        SetupMocks(deck, cards);
        var handler = new ExportDeckHandler(_deckRepo, _cardRepo);

        var result = await handler.Handle(new ExportDeckQuery(deck.Id, "Arena"), CancellationToken.None);

        result.Should().StartWith("Deck");
        result.Should().Contain("4 Lightning Bolt (LEA)");
    }

    private (Deck, List<Card>) CreateTestDeckWithCards()
    {
        var bolt = new Card { Id = Guid.NewGuid(), Name = "Lightning Bolt", TypeLine = "Instant", Rarity = "common", SetCode = "lea", SetName = "Alpha", ScryfallId = "a", OracleId = "a", CollectorNumber = "161" };
        var counter = new Card { Id = Guid.NewGuid(), Name = "Counterspell", TypeLine = "Instant", Rarity = "uncommon", SetCode = "lea", SetName = "Alpha", ScryfallId = "b", OracleId = "b", CollectorNumber = "54" };
        var deck = new Deck
        {
            Id = Guid.NewGuid(), Name = "Test", Format = Format.Legacy, UserId = Guid.NewGuid(),
            Entries = new List<DeckEntry>
            {
                new() { Id = Guid.NewGuid(), CardId = bolt.Id, Quantity = 4, Category = DeckCategory.MainDeck },
                new() { Id = Guid.NewGuid(), CardId = counter.Id, Quantity = 2, Category = DeckCategory.MainDeck }
            }
        };
        return (deck, new List<Card> { bolt, counter });
    }

    private (Deck, List<Card>) CreateTestDeckWithSideboard()
    {
        var bolt = new Card { Id = Guid.NewGuid(), Name = "Lightning Bolt", TypeLine = "Instant", Rarity = "common", SetCode = "lea", SetName = "Alpha", ScryfallId = "a", OracleId = "a", CollectorNumber = "161" };
        var pyro = new Card { Id = Guid.NewGuid(), Name = "Pyroblast", TypeLine = "Instant", Rarity = "common", SetCode = "ice", SetName = "Ice Age", ScryfallId = "c", OracleId = "c", CollectorNumber = "212" };
        var deck = new Deck
        {
            Id = Guid.NewGuid(), Name = "Test", Format = Format.Legacy, UserId = Guid.NewGuid(),
            Entries = new List<DeckEntry>
            {
                new() { Id = Guid.NewGuid(), CardId = bolt.Id, Quantity = 4, Category = DeckCategory.MainDeck },
                new() { Id = Guid.NewGuid(), CardId = pyro.Id, Quantity = 2, Category = DeckCategory.Sideboard }
            }
        };
        return (deck, new List<Card> { bolt, pyro });
    }

    private void SetupMocks(Deck deck, List<Card> cards)
    {
        _deckRepo.GetByIdAsync(deck.Id, Arg.Any<CancellationToken>()).Returns(deck);
        _cardRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>()).Returns(cards);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/MtgDecker.Application.Tests --filter "ExportDeckTests" --verbosity quiet`
Expected: FAIL — Text and CSV formats not handled

**Step 3: Add Text and CSV formats to ExportDeckHandler**

Update `src/MtgDecker.Application/DeckExport/ExportDeckQuery.cs`. Add these format blocks in the `Handle` method:

Add `Text` format handling before the MTGO `else`:

```csharp
else if (request.Format.Equals("Text", StringComparison.OrdinalIgnoreCase))
{
    foreach (var entry in mainDeck)
    {
        if (cards.TryGetValue(entry.CardId, out var card))
            lines.Add($"{entry.Quantity} {card.Name}");
    }

    if (sideboard.Any())
    {
        lines.Add("");
        lines.Add("Sideboard");
        foreach (var entry in sideboard)
        {
            if (cards.TryGetValue(entry.CardId, out var card))
                lines.Add($"{entry.Quantity} {card.Name}");
        }
    }
}
else if (request.Format.Equals("CSV", StringComparison.OrdinalIgnoreCase))
{
    lines.Add("Quantity,Name,Set,Category");
    foreach (var entry in deck.Entries)
    {
        if (cards.TryGetValue(entry.CardId, out var card))
        {
            var name = card.Name.Contains(',') ? $"\"{card.Name}\"" : card.Name;
            var setCode = card.SetCode.ToUpperInvariant();
            lines.Add($"{entry.Quantity},{name},{setCode},{entry.Category}");
        }
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/MtgDecker.Application.Tests --filter "ExportDeckTests" --verbosity quiet`
Expected: All tests PASS

**Step 5: Commit**

```bash
git add src/MtgDecker.Application/DeckExport/ExportDeckQuery.cs tests/MtgDecker.Application.Tests/DeckExport/ExportDeckTests.cs
git commit -m "feat: add Text and CSV export formats"
```

---

### Task 2: Update Export Dialog with New Formats and Clipboard Copy

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/ExportDeckDialog.razor`

**Step 1: Read the current ExportDeckDialog**

First read the file to understand the current structure.

**Step 2: Add new format options**

Add "Text" and "CSV" to the format selector. The dialog likely has a `MudSelect` for format choice — add:

```razor
<MudSelectItem Value="@("Text")">Plain Text</MudSelectItem>
<MudSelectItem Value="@("CSV")">CSV (Spreadsheet)</MudSelectItem>
```

**Step 3: Add clipboard copy button**

Add a "Copy to Clipboard" button next to or above the export text output:

```razor
<MudButton Variant="Variant.Filled" Color="Color.Primary"
           StartIcon="@Icons.Material.Filled.ContentCopy"
           OnClick="CopyToClipboard">
    Copy to Clipboard
</MudButton>
```

In the `@code` block, inject `IJSRuntime` and add:

```csharp
@inject IJSRuntime JS

private async Task CopyToClipboard()
{
    if (!string.IsNullOrEmpty(_exportText))
    {
        await JS.InvokeVoidAsync("navigator.clipboard.writeText", _exportText);
        Snackbar.Add("Copied to clipboard!", Severity.Success);
    }
}
```

**Step 4: Run all tests**

Run: `dotnet test tests/ --verbosity quiet`
Expected: All tests PASS

**Step 5: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/ExportDeckDialog.razor
git commit -m "feat: add Text/CSV formats and clipboard copy to export dialog"
git push
```
