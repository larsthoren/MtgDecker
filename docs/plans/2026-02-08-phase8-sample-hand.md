# Phase 8: Sample Hand / Playtesting Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a sample hand simulator to the deck builder that draws a random 7-card opening hand with London mulligan support and draw-next-card functionality.

**Architecture:** Pure domain logic — a `SampleHandSimulator` in the Domain layer that takes a list of card IDs + quantities, shuffles them into a virtual library, and draws hands. No API calls, no database changes. The UI is a dialog launched from the DeckBuilder page showing card images.

**Tech Stack:** .NET 10, MudBlazor, xUnit + FluentAssertions

---

### Task 1: Create SampleHandSimulator Domain Service

**Files:**
- Create: `src/MtgDecker.Domain/Services/SampleHandSimulator.cs`
- Create: `tests/MtgDecker.Domain.Tests/Services/SampleHandSimulatorTests.cs`

**Step 1: Write the failing tests**

Create `tests/MtgDecker.Domain.Tests/Services/SampleHandSimulatorTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Domain.Services;

namespace MtgDecker.Domain.Tests.Services;

public class SampleHandSimulatorTests
{
    [Fact]
    public void NewGame_DrawsSevenCards()
    {
        var library = CreateLibrary(60);
        var sim = new SampleHandSimulator(library);

        sim.NewGame();

        sim.Hand.Should().HaveCount(7);
        sim.LibraryCount.Should().Be(53);
    }

    [Fact]
    public void DrawCard_AddsOneCardToHand()
    {
        var library = CreateLibrary(60);
        var sim = new SampleHandSimulator(library);
        sim.NewGame();

        sim.DrawCard();

        sim.Hand.Should().HaveCount(8);
        sim.LibraryCount.Should().Be(52);
    }

    [Fact]
    public void DrawCard_EmptyLibrary_ReturnsFalse()
    {
        var library = CreateLibrary(7);
        var sim = new SampleHandSimulator(library);
        sim.NewGame(); // draws all 7

        var result = sim.DrawCard();

        result.Should().BeFalse();
        sim.Hand.Should().HaveCount(7);
    }

    [Fact]
    public void Mulligan_FirstMulligan_DrawsSixCards()
    {
        var library = CreateLibrary(60);
        var sim = new SampleHandSimulator(library);
        sim.NewGame();

        sim.Mulligan();

        sim.Hand.Should().HaveCount(6);
        sim.MulliganCount.Should().Be(1);
        sim.LibraryCount.Should().Be(54);
    }

    [Fact]
    public void Mulligan_SecondMulligan_DrawsFiveCards()
    {
        var library = CreateLibrary(60);
        var sim = new SampleHandSimulator(library);
        sim.NewGame();
        sim.Mulligan();

        sim.Mulligan();

        sim.Hand.Should().HaveCount(5);
        sim.MulliganCount.Should().Be(2);
    }

    [Fact]
    public void Mulligan_SixthMulligan_DrawsOneCard()
    {
        var library = CreateLibrary(60);
        var sim = new SampleHandSimulator(library);
        sim.NewGame();
        for (int i = 0; i < 5; i++) sim.Mulligan();

        sim.Mulligan();

        sim.Hand.Should().HaveCount(1);
        sim.MulliganCount.Should().Be(6);
    }

    [Fact]
    public void Mulligan_AtMinimumHand_CannotMulliganFurther()
    {
        var library = CreateLibrary(60);
        var sim = new SampleHandSimulator(library);
        sim.NewGame();
        for (int i = 0; i < 6; i++) sim.Mulligan();

        var result = sim.Mulligan();

        result.Should().BeFalse();
        sim.Hand.Should().HaveCount(1);
    }

    [Fact]
    public void NewGame_Reshuffles_ResetsState()
    {
        var library = CreateLibrary(60);
        var sim = new SampleHandSimulator(library);
        sim.NewGame();
        sim.Mulligan();
        sim.DrawCard();

        sim.NewGame();

        sim.Hand.Should().HaveCount(7);
        sim.MulliganCount.Should().Be(0);
        sim.LibraryCount.Should().Be(53);
    }

    [Fact]
    public void Hand_ContainsOnlyCardsFromLibrary()
    {
        var cardIds = new List<Guid>();
        for (int i = 0; i < 60; i++)
            cardIds.Add(Guid.NewGuid());

        var sim = new SampleHandSimulator(cardIds);
        sim.NewGame();

        sim.Hand.Should().OnlyContain(id => cardIds.Contains(id));
    }

    [Fact]
    public void Constructor_ExpandsQuantities()
    {
        // Simulate a deck with 4 copies of 15 different cards = 60 cards
        var entries = new List<(Guid CardId, int Quantity)>();
        for (int i = 0; i < 15; i++)
            entries.Add((Guid.NewGuid(), 4));

        var sim = SampleHandSimulator.FromDeckEntries(entries);
        sim.NewGame();

        sim.Hand.Should().HaveCount(7);
        sim.LibraryCount.Should().Be(53);
    }

    private static List<Guid> CreateLibrary(int count)
    {
        return Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/MtgDecker.Domain.Tests --filter "SampleHandSimulatorTests" --verbosity quiet`
Expected: FAIL — `SampleHandSimulator` does not exist

**Step 3: Implement SampleHandSimulator**

Create `src/MtgDecker.Domain/Services/SampleHandSimulator.cs`:

```csharp
namespace MtgDecker.Domain.Services;

public class SampleHandSimulator
{
    private readonly List<Guid> _originalLibrary;
    private List<Guid> _library = new();
    private readonly List<Guid> _hand = new();
    private readonly Random _rng = new();

    public IReadOnlyList<Guid> Hand => _hand;
    public int LibraryCount => _library.Count;
    public int MulliganCount { get; private set; }

    public SampleHandSimulator(List<Guid> cardIds)
    {
        _originalLibrary = new List<Guid>(cardIds);
    }

    public static SampleHandSimulator FromDeckEntries(List<(Guid CardId, int Quantity)> entries)
    {
        var cardIds = new List<Guid>();
        foreach (var (cardId, qty) in entries)
        {
            for (int i = 0; i < qty; i++)
                cardIds.Add(cardId);
        }
        return new SampleHandSimulator(cardIds);
    }

    public void NewGame()
    {
        _hand.Clear();
        MulliganCount = 0;
        Shuffle();
        Draw(7);
    }

    public bool DrawCard()
    {
        if (_library.Count == 0) return false;
        _hand.Add(_library[0]);
        _library.RemoveAt(0);
        return true;
    }

    public bool Mulligan()
    {
        var newHandSize = 7 - (MulliganCount + 1);
        if (newHandSize < 1) return false;

        MulliganCount++;
        _hand.Clear();
        Shuffle();
        Draw(newHandSize);
        return true;
    }

    private void Shuffle()
    {
        _library = new List<Guid>(_originalLibrary);
        // Fisher-Yates shuffle
        for (int i = _library.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (_library[i], _library[j]) = (_library[j], _library[i]);
        }
    }

    private void Draw(int count)
    {
        var toDraw = Math.Min(count, _library.Count);
        for (int i = 0; i < toDraw; i++)
        {
            _hand.Add(_library[0]);
            _library.RemoveAt(0);
        }
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/MtgDecker.Domain.Tests --filter "SampleHandSimulatorTests" --verbosity quiet`
Expected: All 10 tests PASS

**Step 5: Commit**

```bash
git add src/MtgDecker.Domain/Services/SampleHandSimulator.cs tests/MtgDecker.Domain.Tests/Services/SampleHandSimulatorTests.cs
git commit -m "feat: add SampleHandSimulator domain service with tests"
```

---

### Task 2: Create Playtest Dialog UI

**Files:**
- Create: `src/MtgDecker.Web/Components/Pages/PlaytestDialog.razor`
- Modify: `src/MtgDecker.Web/Components/Pages/DeckBuilder.razor`

**Step 1: Create PlaytestDialog.razor**

Create `src/MtgDecker.Web/Components/Pages/PlaytestDialog.razor`:

```razor
@using MtgDecker.Domain.Entities
@using MtgDecker.Domain.Services
@inject IMediator Mediator

<MudDialog>
    <TitleContent>
        <MudStack Row="true" AlignItems="AlignItems.Center" Spacing="2">
            <MudText Typo="Typo.h6">Playtest: @DeckName</MudText>
            @if (_simulator != null)
            {
                <MudChip T="string" Size="Size.Small">Library: @_simulator.LibraryCount</MudChip>
                @if (_simulator.MulliganCount > 0)
                {
                    <MudChip T="string" Size="Size.Small" Color="Color.Warning">
                        Mulligan @_simulator.MulliganCount
                    </MudChip>
                }
            }
        </MudStack>
    </TitleContent>
    <DialogContent>
        @if (_loading)
        {
            <MudProgressLinear Indeterminate="true" />
        }
        else if (_simulator != null)
        {
            <MudText Typo="Typo.subtitle2" Class="mb-2">Hand (@_simulator.Hand.Count cards)</MudText>
            <MudGrid>
                @foreach (var cardId in _simulator.Hand)
                {
                    @if (_cards.TryGetValue(cardId, out var card))
                    {
                        <MudItem xs="6" sm="4" md="3">
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
                        </MudItem>
                    }
                }
            </MudGrid>
        }
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="NewGame" Variant="Variant.Outlined" StartIcon="@Icons.Material.Filled.Refresh">
            New Hand
        </MudButton>
        <MudButton OnClick="MulliganHand" Variant="Variant.Outlined" Color="Color.Warning"
                   Disabled="@(_simulator == null || _simulator.MulliganCount >= 6)"
                   StartIcon="@Icons.Material.Filled.Replay">
            Mulligan (@(7 - (_simulator?.MulliganCount ?? 0) - 1))
        </MudButton>
        <MudButton OnClick="DrawNext" Variant="Variant.Outlined" Color="Color.Primary"
                   Disabled="@(_simulator == null || _simulator.LibraryCount == 0)"
                   StartIcon="@Icons.Material.Filled.Style">
            Draw
        </MudButton>
        <MudSpacer />
        <MudButton OnClick="Close">Close</MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;
    [Parameter] public Guid DeckId { get; set; }
    [Parameter] public string DeckName { get; set; } = string.Empty;

    private SampleHandSimulator? _simulator;
    private Dictionary<Guid, Card> _cards = new();
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        var deck = await Mediator.Send(new MtgDecker.Application.Decks.GetDeckQuery(DeckId));
        if (deck == null) return;

        // Load card data for all deck entries
        var cardIds = deck.Entries.Select(e => e.CardId).Distinct().ToList();
        var cards = await Mediator.Send(new MtgDecker.Application.Cards.GetCardsByIdsQuery(cardIds));
        _cards = cards.ToDictionary(c => c.Id);

        // Build simulator from main deck entries only
        var mainDeckEntries = deck.Entries
            .Where(e => e.Category == MtgDecker.Domain.Enums.DeckCategory.MainDeck)
            .Select(e => (e.CardId, e.Quantity))
            .ToList();

        _simulator = SampleHandSimulator.FromDeckEntries(mainDeckEntries);
        _simulator.NewGame();
        _loading = false;
    }

    private void NewGame()
    {
        _simulator?.NewGame();
    }

    private void MulliganHand()
    {
        _simulator?.Mulligan();
    }

    private void DrawNext()
    {
        _simulator?.DrawCard();
    }

    private void Close() => MudDialog.Cancel();
}
```

**Step 2: Add GetCardsByIdsQuery if it doesn't exist**

Check if `GetCardsByIdsQuery` exists in `src/MtgDecker.Application/Cards/`. If not, create it.

Create `src/MtgDecker.Application/Cards/GetCardsByIdsQuery.cs`:

```csharp
using MediatR;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;

namespace MtgDecker.Application.Cards;

public record GetCardsByIdsQuery(List<Guid> CardIds) : IRequest<List<Card>>;

public class GetCardsByIdsHandler : IRequestHandler<GetCardsByIdsQuery, List<Card>>
{
    private readonly ICardRepository _cardRepository;

    public GetCardsByIdsHandler(ICardRepository cardRepository)
    {
        _cardRepository = cardRepository;
    }

    public async Task<List<Card>> Handle(GetCardsByIdsQuery request, CancellationToken cancellationToken)
    {
        return await _cardRepository.GetByIdsAsync(request.CardIds, cancellationToken);
    }
}
```

**Step 3: Add Playtest button to DeckBuilder**

In `DeckBuilder.razor`, add a Playtest button in the header area (after the existing MudSpacer, around line 18):

```razor
<MudButton Variant="Variant.Outlined" Color="Color.Tertiary"
           StartIcon="@Icons.Material.Filled.Casino"
           OnClick="OpenPlaytest">
    Playtest
</MudButton>
```

Add the method in the `@code` block:

```csharp
[Inject] private IDialogService DialogService { get; set; } = default!;

private async Task OpenPlaytest()
{
    var parameters = new DialogParameters<PlaytestDialog>
    {
        { x => x.DeckId, DeckId },
        { x => x.DeckName, _deck?.Name ?? "" }
    };
    var options = new DialogOptions
    {
        MaxWidth = MaxWidth.Large,
        FullWidth = true,
        CloseOnEscapeKey = true
    };
    await DialogService.ShowAsync<PlaytestDialog>("Playtest", parameters, options);
}
```

**Step 4: Run all tests**

Run: `dotnet test tests/ --verbosity quiet`
Expected: All tests PASS

**Step 5: Commit**

```bash
git add src/MtgDecker.Domain/Services/SampleHandSimulator.cs src/MtgDecker.Web/Components/Pages/PlaytestDialog.razor src/MtgDecker.Web/Components/Pages/DeckBuilder.razor src/MtgDecker.Application/Cards/GetCardsByIdsQuery.cs
git commit -m "feat: add playtest dialog with sample hand simulator"
```

---

### Task 3: Visual Polish and Edge Cases

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/PlaytestDialog.razor`

**Step 1: Add turn counter display**

Add a turn counter that increments with each draw. In the `@code` block:

```csharp
private int _turnCount = 0;
```

Update `NewGame`:
```csharp
private void NewGame()
{
    _simulator?.NewGame();
    _turnCount = 0;
}
```

Update `DrawNext`:
```csharp
private void DrawNext()
{
    if (_simulator?.DrawCard() == true)
        _turnCount++;
}
```

Add turn display in the TitleContent:
```razor
<MudChip T="string" Size="Size.Small" Color="Color.Info">Turn: @_turnCount</MudChip>
```

**Step 2: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/PlaytestDialog.razor
git commit -m "feat: add turn counter to playtest dialog"
git push
```
