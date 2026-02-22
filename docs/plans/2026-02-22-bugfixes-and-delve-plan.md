# Bugfixes and Delve Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fix land-play-on-opponent-turn bug, fix Daze alternate cost forced selection, implement Delve mechanic with graveyard viewer.

**Architecture:** Three independent fixes. Bug fixes are engine-level guards + UI wiring. Delve adds a new cost-reduction mechanic to CastSpellHandler with a multi-select graveyard exile prompt.

**Tech Stack:** .NET 10, Blazor, xUnit + FluentAssertions, MtgDecker.Engine

---

### Task 1: PlayLand Validation — Tests

**Files:**
- Create: `tests/MtgDecker.Engine.Tests/PlayLandValidationTests.cs`

**Step 1: Write three failing tests**

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class PlayLandValidationTests
{
    private GameEngine CreateGame(
        out GameState state,
        out TestDecisionHandler p1Handler,
        out TestDecisionHandler p2Handler)
    {
        p1Handler = new TestDecisionHandler();
        p2Handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Alice", p1Handler);
        var p2 = new Player(Guid.NewGuid(), "Bob", p2Handler);

        var deck1 = new DeckBuilder().AddLand("Forest", 36).AddCard("Grizzly Bears", 24, "Creature — Bear").Build();
        var deck2 = new DeckBuilder().AddLand("Mountain", 36).AddCard("Goblin Guide", 24, "Creature — Goblin").Build();

        foreach (var card in deck1) p1.Library.Add(card);
        foreach (var card in deck2) p2.Library.Add(card);

        state = new GameState(p1, p2);
        return new GameEngine(state);
    }

    [Fact]
    public async Task PlayLand_OnOpponentsTurn_IsRejected()
    {
        var engine = CreateGame(out var state, out _, out var p2Handler);
        await engine.StartGameAsync();

        // It's P1's turn — P2 tries to play a land
        state.ActivePlayer.Should().BeSameAs(state.Player1);
        var p2Land = state.Player2.Hand.Cards.First(c => c.IsLand);
        var beforeCount = state.Player2.Battlefield.Cards.Count;

        p2Handler.EnqueueAction(GameAction.PlayLand(state.Player2.Id, p2Land.Id));
        state.CurrentPhase = Phase.MainPhase1;
        await engine.ExecuteActionAsync(GameAction.PlayLand(state.Player2.Id, p2Land.Id));

        state.Player2.Battlefield.Cards.Count.Should().Be(beforeCount, "non-active player cannot play lands");
    }

    [Fact]
    public async Task PlayLand_DuringCombat_IsRejected()
    {
        var engine = CreateGame(out var state, out var p1Handler, out _);
        await engine.StartGameAsync();

        state.CurrentPhase = Phase.Combat;
        var p1Land = state.Player1.Hand.Cards.First(c => c.IsLand);
        var beforeCount = state.Player1.Battlefield.Cards.Count;

        await engine.ExecuteActionAsync(GameAction.PlayLand(state.Player1.Id, p1Land.Id));

        state.Player1.Battlefield.Cards.Count.Should().Be(beforeCount, "cannot play lands during combat");
    }

    [Fact]
    public async Task PlayLand_WhenStackNotEmpty_IsRejected()
    {
        var engine = CreateGame(out var state, out _, out _);
        await engine.StartGameAsync();

        state.CurrentPhase = Phase.MainPhase1;
        // Push a dummy spell on the stack
        var dummyCard = GameCard.Create("Dummy", "Instant");
        state.StackPush(new StackObject(dummyCard, state.Player1.Id, new(), new(), 0));

        var p1Land = state.Player1.Hand.Cards.First(c => c.IsLand);
        var beforeCount = state.Player1.Battlefield.Cards.Count;

        await engine.ExecuteActionAsync(GameAction.PlayLand(state.Player1.Id, p1Land.Id));

        state.Player1.Battlefield.Cards.Count.Should().Be(beforeCount, "cannot play lands while stack is non-empty");
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "PlayLandValidationTests" -v minimal`
Expected: 3 FAIL (lands are played because no validation exists)

**Step 3: Commit failing tests**

```bash
git add tests/MtgDecker.Engine.Tests/PlayLandValidationTests.cs
git commit -m "test: add failing tests for PlayLand validation"
```

---

### Task 2: PlayLand Validation — Implementation

**Files:**
- Modify: `src/MtgDecker.Engine/Actions/PlayLandHandler.cs`

**Step 1: Add validation guards**

Insert after line 17 (after the `!playCard.IsLand` check), before line 19 (the `LandsPlayedThisTurn` check):

```csharp
        if (state.ActivePlayer.Id != action.PlayerId)
        {
            state.Log($"Cannot play land — only the active player can play lands.");
            return;
        }

        if (state.CurrentPhase != Phase.MainPhase1 && state.CurrentPhase != Phase.MainPhase2)
        {
            state.Log($"Cannot play land — lands can only be played during main phases.");
            return;
        }

        if (state.StackCount > 0)
        {
            state.Log($"Cannot play land — the stack must be empty.");
            return;
        }
```

**Step 2: Run tests to verify they pass**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "PlayLandValidationTests" -v minimal`
Expected: 3 PASS

**Step 3: Run full engine test suite**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ -v minimal`
Expected: All pass (existing tests play lands during active player's main phase)

**Step 4: Commit**

```bash
git add src/MtgDecker.Engine/Actions/PlayLandHandler.cs
git commit -m "fix(engine): validate active player, phase, and stack for land drops"
```

---

### Task 3: Alternate Cost — GameAction + Engine

**Files:**
- Modify: `src/MtgDecker.Engine/GameAction.cs`
- Modify: `src/MtgDecker.Engine/Actions/CastSpellHandler.cs`
- Create: `tests/MtgDecker.Engine.Tests/AlternateCostChoiceTests.cs`

**Step 1: Write failing test**

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class AlternateCostChoiceTests
{
    private GameEngine CreateGame(
        out GameState state,
        out TestDecisionHandler p1Handler,
        out TestDecisionHandler p2Handler)
    {
        p1Handler = new TestDecisionHandler();
        p2Handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Alice", p1Handler);
        var p2 = new Player(Guid.NewGuid(), "Bob", p2Handler);

        var deck1 = new DeckBuilder().AddLand("Island", 36).AddCard("Daze", 24, "Instant").Build();
        var deck2 = new DeckBuilder().AddLand("Mountain", 36).AddCard("Goblin Guide", 24, "Creature — Goblin").Build();

        foreach (var card in deck1) p1.Library.Add(card);
        foreach (var card in deck2) p2.Library.Add(card);

        state = new GameState(p1, p2);
        return new GameEngine(state);
    }

    [Fact]
    public async Task CastSpell_WithUseAlternateCostFlag_UsesAlternateCost()
    {
        var engine = CreateGame(out var state, out var p1Handler, out _);
        await engine.StartGameAsync();

        state.CurrentPhase = Phase.MainPhase1;

        // Give player an Island on battlefield and Daze in hand
        var island = GameCard.Create("Island", "Basic Land — Island");
        state.Player1.Battlefield.Add(island);
        var daze = state.Player1.Hand.Cards.First(c => c.Name == "Daze");

        // Need a spell on the stack to counter (Daze targets a spell)
        var targetSpell = GameCard.Create("Lightning Bolt", "Instant");
        state.StackPush(new StackObject(targetSpell, state.Player2.Id, new(), new(), 0));

        // Enqueue target choice for the counterspell
        p1Handler.EnqueueTarget(new TargetInfo(targetSpell.Id, state.Player2.Id, ZoneType.Stack));

        // Cast with UseAlternateCost flag — should return Island, not pay mana
        var action = GameAction.CastSpell(state.Player1.Id, daze.Id);
        action.UseAlternateCost = true;
        await engine.ExecuteActionAsync(action);

        // Island should be returned to hand (alternate cost)
        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Id == island.Id);
        state.Player1.Hand.Cards.Should().Contain(c => c.Id == island.Id);
        // Daze should be on the stack
        state.Stack.Should().Contain(s => s is StackObject so && so.Card.Name == "Daze");
    }

    [Fact]
    public async Task CastSpell_WithoutFlag_PaysMana()
    {
        var engine = CreateGame(out var state, out var p1Handler, out _);
        await engine.StartGameAsync();

        state.CurrentPhase = Phase.MainPhase1;

        // Give player mana to pay {1}{U}
        state.Player1.ManaPool.Add(ManaColor.Blue, 1);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 1);

        // Put an Island on battlefield (so alternate cost is also available)
        var island = GameCard.Create("Island", "Basic Land — Island");
        state.Player1.Battlefield.Add(island);

        var daze = state.Player1.Hand.Cards.First(c => c.Name == "Daze");

        // Need a spell to target
        var targetSpell = GameCard.Create("Lightning Bolt", "Instant");
        state.StackPush(new StackObject(targetSpell, state.Player2.Id, new(), new(), 0));
        p1Handler.EnqueueTarget(new TargetInfo(targetSpell.Id, state.Player2.Id, ZoneType.Stack));

        // Cast without flag — should pay mana, Island stays on battlefield
        await engine.ExecuteActionAsync(GameAction.CastSpell(state.Player1.Id, daze.Id));

        // Island should still be on battlefield (mana was paid, not alternate cost)
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == island.Id);
        // Mana pool should be drained
        state.Player1.ManaPool.Total.Should().Be(0);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "AlternateCostChoiceTests" -v minimal`
Expected: FAIL (UseAlternateCost property doesn't exist yet)

**Step 3: Add `UseAlternateCost` to GameAction**

In `src/MtgDecker.Engine/GameAction.cs`, add after `IsLandDrop` property (line 28):

```csharp
    public bool UseAlternateCost { get; set; }
```

**Step 4: Update CastSpellHandler to respect the flag**

In `src/MtgDecker.Engine/Actions/CastSpellHandler.cs`, replace lines 53-70 (the `useAlternateCost` decision block):

```csharp
        bool useAlternateCost = action.UseAlternateCost;

        if (!useAlternateCost)
        {
            if (!canPayMana && !canPayAlternate)
            {
                state.Log($"Not enough mana to cast {castCard.Name}.");
                return;
            }

            if (canPayAlternate && !canPayMana)
            {
                useAlternateCost = true;
            }
            else if (canPayAlternate && canPayMana)
            {
                var choice = await castPlayer.DecisionHandler.ChooseCard(
                    [castCard], $"Pay mana for {castCard.Name}? (skip to use alternate cost)", optional: true, ct);
                useAlternateCost = !choice.HasValue;
            }
        }
        else if (!canPayAlternate)
        {
            state.Log($"Cannot pay alternate cost for {castCard.Name}.");
            return;
        }
```

**Step 5: Run tests to verify they pass**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "AlternateCostChoiceTests" -v minimal`
Expected: 2 PASS

**Step 6: Run full engine test suite**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ -v minimal`
Expected: All pass

**Step 7: Commit**

```bash
git add src/MtgDecker.Engine/GameAction.cs src/MtgDecker.Engine/Actions/CastSpellHandler.cs tests/MtgDecker.Engine.Tests/AlternateCostChoiceTests.cs
git commit -m "fix(engine): add UseAlternateCost flag to GameAction, respect in CastSpellHandler"
```

---

### Task 4: Alternate Cost — UI (ActionMenu + GameBoard)

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/Game/ActionMenu.razor`
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor`

**Step 1: Add "Cast (alt cost)" to ActionMenu**

In `ActionMenu.razor`, add after the "Play" button block (after line 21, before the Tap button):

```razor
            @if (IsOwnCard && CurrentZone == ZoneType.Hand && HasAlternateCost)
            {
                <MudButton Size="Size.Small" Variant="Variant.Text" FullWidth="true"
                           StartIcon="@Icons.Material.Filled.SwapHoriz" Color="Color.Warning"
                           Style="justify-content: flex-start;"
                           OnClick="() => OnPlayAlternate.InvokeAsync()">Cast (alt cost)</MudButton>
            }
```

Add parameters to `@code` block (after `OnPlay` on line 56):

```csharp
    [Parameter] public bool HasAlternateCost { get; set; }
    [Parameter] public EventCallback OnPlayAlternate { get; set; }
```

**Step 2: Wire ActionMenu in GameBoard**

In `GameBoard.razor`, find the `<ActionMenu>` usage (around line 320). Add the new parameters:

```razor
                    HasAlternateCost="@HasAlternateCostForCard(_selectedCard)"
                    OnPlayAlternate="HandlePlayAlternate"
```

**Step 3: Add helper methods to GameBoard `@code` block**

Add after `HandleActivate` method:

```csharp
    private bool HasAlternateCostForCard(GameCard? card)
    {
        if (card == null) return false;
        return CardDefinitions.TryGet(card.Name, out var def) && def.AlternateCost != null;
    }

    private async Task HandlePlayAlternate()
    {
        if (_selectedCard == null) return;
        var action = GameAction.CastSpell(LocalPlayer.Id, _selectedCard.Id);
        action.UseAlternateCost = true;
        await OnAction.InvokeAsync(action);
        ClearSelection();
    }
```

**Step 4: Remove the early alternate-cost dispatch in HandlePlay**

In `GameBoard.razor`, find `HandlePlay` method. Remove the alternate cost early-dispatch block (the `if (CardDefinitions.TryGet(...) && altDef.AlternateCost != null)` block). The code that checks for alternate cost and dispatches immediately should be deleted. Keep the Phyrexian check and the pending cast mode fallthrough.

The `HandlePlay` method's `!CanPay` block should become:

```csharp
        if (_selectedCard.ManaCost != null && !LocalPlayer.ManaPool.CanPay(_selectedCard.ManaCost))
        {
            // Check for Phyrexian mana cost — let engine handle payment prompts
            if (_selectedCard.ManaCost.HasPhyrexianCost)
            {
                await OnAction.InvokeAsync(GameAction.CastSpell(LocalPlayer.Id, _selectedCard.Id));
                ClearSelection();
                return;
            }

            // Enter pending cast mode — player taps lands to pay
            _pendingCastCard = _selectedCard;
            ClearSelection();
            return;
        }
```

**Step 5: Build to verify**

Run: `dotnet build src/MtgDecker.Web/`
Expected: Build succeeded

**Step 6: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/Game/ActionMenu.razor src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor
git commit -m "fix(web): add 'Cast (alt cost)' to action menu, remove auto-dispatch"
```

---

### Task 5: Delve — CardDefinition + Decision Handler Interface

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinition.cs`
- Modify: `src/MtgDecker.Engine/IPlayerDecisionHandler.cs`
- Modify: `src/MtgDecker.Engine/InteractiveDecisionHandler.cs`
- Modify: `src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs`
- Modify: `tests/MtgDecker.Engine.Tests/Helpers/TestDecisionHandler.cs`

**Step 1: Add HasDelve to CardDefinition**

In `src/MtgDecker.Engine/CardDefinition.cs`, add after `HasFlash` (line 37):

```csharp
    public bool HasDelve { get; init; }
```

**Step 2: Add ChooseCardsToExile to IPlayerDecisionHandler**

In `src/MtgDecker.Engine/IPlayerDecisionHandler.cs`, add after `ChoosePhyrexianPayment`:

```csharp
    Task<IReadOnlyList<GameCard>> ChooseCardsToExile(
        IReadOnlyList<GameCard> options, int maxCount, string prompt, CancellationToken ct = default);
```

**Step 3: Implement in TestDecisionHandler**

In `tests/MtgDecker.Engine.Tests/Helpers/TestDecisionHandler.cs`:

Add queue field:
```csharp
    private readonly Queue<Func<IReadOnlyList<GameCard>, int, IReadOnlyList<GameCard>>> _exileChoices = new();
```

Add enqueue method:
```csharp
    public void EnqueueExileChoice(Func<IReadOnlyList<GameCard>, int, IReadOnlyList<GameCard>> chooser) =>
        _exileChoices.Enqueue(chooser);
```

Add implementation:
```csharp
    public Task<IReadOnlyList<GameCard>> ChooseCardsToExile(
        IReadOnlyList<GameCard> options, int maxCount, string prompt, CancellationToken ct = default)
    {
        if (_exileChoices.Count > 0)
            return Task.FromResult(_exileChoices.Dequeue()(options, maxCount));
        // Default: exile as many as possible (greedy)
        return Task.FromResult<IReadOnlyList<GameCard>>(options.Take(maxCount).ToList());
    }
```

**Step 4: Implement in InteractiveDecisionHandler**

In `src/MtgDecker.Engine/InteractiveDecisionHandler.cs`, add using the existing `ChooseCard` pattern but with multi-select. Use the card choice mechanism with a loop:

```csharp
    public async Task<IReadOnlyList<GameCard>> ChooseCardsToExile(
        IReadOnlyList<GameCard> options, int maxCount, string prompt, CancellationToken ct = default)
    {
        // Reuse discard flow for multi-select card choice
        _isWaitingForDiscard = true;
        _discardOptions = options.ToList();
        _discardCount = maxCount;
        _discardPrompt = prompt;
        _discardTcs = new TaskCompletionSource<IReadOnlyList<GameCard>>();
        _onStateChanged?.Invoke();

        var result = await _discardTcs.Task;
        _isWaitingForDiscard = false;
        _discardOptions = null;
        _onStateChanged?.Invoke();
        return result;
    }
```

Note: Check InteractiveDecisionHandler for the exact discard field names and pattern — reuse the same UI mechanism (the discard prompt already supports multi-select with a count). The `IsWaitingForDiscard`/`DiscardOptions`/`DiscardCount` properties and `SubmitDiscard` method can be reused for exile selection since the UI is identical (select N cards, confirm).

**Step 5: Implement in AiBotDecisionHandler**

In `src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs`:

```csharp
    public Task<IReadOnlyList<GameCard>> ChooseCardsToExile(
        IReadOnlyList<GameCard> options, int maxCount, string prompt, CancellationToken ct = default)
    {
        // AI: exile as many as possible to maximize cost reduction
        return Task.FromResult<IReadOnlyList<GameCard>>(options.Take(maxCount).ToList());
    }
```

**Step 6: Build to verify**

Run: `dotnet build src/MtgDecker.Web/`
Expected: Build succeeded

**Step 7: Commit**

```bash
git add src/MtgDecker.Engine/CardDefinition.cs src/MtgDecker.Engine/IPlayerDecisionHandler.cs src/MtgDecker.Engine/InteractiveDecisionHandler.cs src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs tests/MtgDecker.Engine.Tests/Helpers/TestDecisionHandler.cs
git commit -m "feat(engine): add HasDelve flag and ChooseCardsToExile to decision handlers"
```

---

### Task 6: Delve — CastSpellHandler Integration + Tests

**Files:**
- Modify: `src/MtgDecker.Engine/Actions/CastSpellHandler.cs`
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs` (Murktide Regent registration)
- Create: `tests/MtgDecker.Engine.Tests/DelveTests.cs`

**Step 1: Write failing tests**

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class DelveTests
{
    private GameEngine CreateGame(
        out GameState state,
        out TestDecisionHandler p1Handler,
        out TestDecisionHandler p2Handler)
    {
        p1Handler = new TestDecisionHandler();
        p2Handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Alice", p1Handler);
        var p2 = new Player(Guid.NewGuid(), "Bob", p2Handler);

        var deck1 = new DeckBuilder().AddLand("Island", 36).AddCard("Murktide Regent", 24, "Creature — Dragon").Build();
        var deck2 = new DeckBuilder().AddLand("Mountain", 36).AddCard("Goblin Guide", 24, "Creature — Goblin").Build();

        foreach (var card in deck1) p1.Library.Add(card);
        foreach (var card in deck2) p2.Library.Add(card);

        state = new GameState(p1, p2);
        return new GameEngine(state);
    }

    [Fact]
    public async Task Delve_ExileGraveyardCards_ReducesGenericCost()
    {
        var engine = CreateGame(out var state, out var p1Handler, out _);
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        // Murktide costs {5}{U}{U} — exile 5 cards to reduce to {U}{U}
        var murktide = state.Player1.Hand.Cards.First(c => c.Name == "Murktide Regent");

        // Put 5 cards in graveyard
        for (int i = 0; i < 5; i++)
            state.Player1.Graveyard.Add(GameCard.Create($"Filler{i}", "Instant"));

        // Give just {U}{U} mana (relies on Delve to cover the {5})
        state.Player1.ManaPool.Add(ManaColor.Blue, 2);

        // Enqueue exile choice: exile all 5
        p1Handler.EnqueueExileChoice((cards, max) => cards.Take(max).ToList());

        await engine.ExecuteActionAsync(GameAction.CastSpell(state.Player1.Id, murktide.Id));

        state.Stack.Should().Contain(s => s is StackObject so && so.Card.Name == "Murktide Regent");
        state.Player1.Exile.Cards.Count.Should().Be(5, "5 graveyard cards exiled for Delve");
        state.Player1.Graveyard.Count.Should().Be(0, "all graveyard cards were exiled");
    }

    [Fact]
    public async Task Delve_PartialExile_PaysRemainingWithMana()
    {
        var engine = CreateGame(out var state, out var p1Handler, out _);
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var murktide = state.Player1.Hand.Cards.First(c => c.Name == "Murktide Regent");

        // Put 3 cards in graveyard (can only exile 3 of the needed 5)
        for (int i = 0; i < 3; i++)
            state.Player1.Graveyard.Add(GameCard.Create($"Filler{i}", "Instant"));

        // Give {2}{U}{U} — covers remaining 2 generic after exiling 3
        state.Player1.ManaPool.Add(ManaColor.Blue, 2);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 2);

        p1Handler.EnqueueExileChoice((cards, max) => cards.Take(max).ToList());

        await engine.ExecuteActionAsync(GameAction.CastSpell(state.Player1.Id, murktide.Id));

        state.Stack.Should().Contain(s => s is StackObject so && so.Card.Name == "Murktide Regent");
        state.Player1.Exile.Cards.Count.Should().Be(3);
        state.Player1.ManaPool.Total.Should().Be(0, "remaining 2 generic paid from mana pool");
    }

    [Fact]
    public async Task Delve_ZeroExile_PaysFullMana()
    {
        var engine = CreateGame(out var state, out var p1Handler, out _);
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var murktide = state.Player1.Hand.Cards.First(c => c.Name == "Murktide Regent");

        // No graveyard cards — must pay full {5}{U}{U}
        state.Player1.ManaPool.Add(ManaColor.Blue, 2);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 5);

        await engine.ExecuteActionAsync(GameAction.CastSpell(state.Player1.Id, murktide.Id));

        state.Stack.Should().Contain(s => s is StackObject so && so.Card.Name == "Murktide Regent");
        state.Player1.ManaPool.Total.Should().Be(0);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "DelveTests" -v minimal`
Expected: FAIL

**Step 3: Register Murktide Regent with HasDelve**

In `src/MtgDecker.Engine/CardDefinitions.cs`, find the Murktide Regent entry and update:

```csharp
["Murktide Regent"] = new(ManaCost.Parse("{5}{U}{U}"), null, 3, 3, CardType.Creature)
    { Subtypes = ["Dragon"], HasDelve = true },
```

**Step 4: Add Delve logic to CastSpellHandler**

In `src/MtgDecker.Engine/Actions/CastSpellHandler.cs`, insert AFTER computing `castEffectiveCost` (after the cost modification block, before the `canPayMana` check). The Delve logic reduces the effective cost before checking if mana pool can pay:

```csharp
        // Delve: exile graveyard cards to reduce generic cost
        int delveExileCount = 0;
        List<GameCard>? delveExiledCards = null;
        if (def?.HasDelve == true && castEffectiveCost.GenericCost > 0)
        {
            var graveyardCards = castPlayer.Graveyard.Cards.ToList();
            if (graveyardCards.Count > 0)
            {
                var maxExile = Math.Min(graveyardCards.Count, castEffectiveCost.GenericCost);
                delveExiledCards = (await castPlayer.DecisionHandler.ChooseCardsToExile(
                    graveyardCards, maxExile, $"Exile cards from graveyard for Delve (up to {maxExile})", ct)).ToList();
                delveExileCount = delveExiledCards.Count;
                if (delveExileCount > 0)
                    castEffectiveCost = castEffectiveCost.WithGenericReduction(delveExileCount);
            }
        }
```

Then, AFTER the payment block (after `castPlayer.PendingManaTaps.Clear()` on line ~103), add the actual exile:

```csharp
        // Exile Delve cards
        if (delveExiledCards != null)
        {
            foreach (var exiled in delveExiledCards)
            {
                castPlayer.Graveyard.RemoveById(exiled.Id);
                castPlayer.Exile.Add(exiled);
            }
        }
```

**Step 5: Run tests to verify they pass**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "DelveTests" -v minimal`
Expected: 3 PASS

**Step 6: Run full engine test suite**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ -v minimal`
Expected: All pass

**Step 7: Commit**

```bash
git add src/MtgDecker.Engine/Actions/CastSpellHandler.cs src/MtgDecker.Engine/CardDefinitions.cs tests/MtgDecker.Engine.Tests/DelveTests.cs
git commit -m "feat(engine): implement Delve mechanic — exile graveyard cards to reduce generic cost"
```

---

### Task 7: Graveyard Viewer — UI

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor`
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor.css`

**Step 1: Add graveyard toggle state**

In `GameBoard.razor` `@code` block, add fields:

```csharp
    private bool _showPlayerGraveyard;
    private bool _showOpponentGraveyard;
```

**Step 2: Make graveyard chips clickable**

In the player info bar, change the Grave chip to be clickable:

```razor
        <MudChip T="string" Size="Size.Small" Color="Color.Default"
                 OnClick="() => _showPlayerGraveyard = !_showPlayerGraveyard"
                 Style="cursor: pointer;">
            Grave: @LocalPlayer.Graveyard.Cards.Count
        </MudChip>
```

Same for opponent info bar:

```razor
        <MudChip T="string" Size="Size.Small" Color="Color.Default"
                 OnClick="() => _showOpponentGraveyard = !_showOpponentGraveyard"
                 Style="cursor: pointer;">
            Grave: @OpponentPlayer.Graveyard.Cards.Count
        </MudChip>
```

**Step 3: Add graveyard panel markup**

Add graveyard viewer panels after the player/opponent info bars. Player graveyard after the player info bar:

```razor
    @if (_showPlayerGraveyard && LocalPlayer.Graveyard.Cards.Count > 0)
    {
        <div class="graveyard-panel">
            <div class="graveyard-header">
                <MudText Typo="Typo.caption" Style="font-weight: 600;">Your Graveyard (@LocalPlayer.Graveyard.Cards.Count)</MudText>
                <MudIconButton Size="Size.Small" Icon="@Icons.Material.Filled.Close"
                               OnClick="() => _showPlayerGraveyard = false" />
            </div>
            <div class="graveyard-cards">
                @foreach (var card in LocalPlayer.Graveyard.Cards)
                {
                    <CardDisplay Name="@card.Name" ImageUrl="@card.ImageUrl" CardSize="100"
                                 Clickable="false"
                                 OnHoverStart="SetHoveredCard"
                                 OnHoverEnd="ClearHoveredCard" />
                }
            </div>
        </div>
    }
```

Opponent graveyard after the opponent info bar:

```razor
    @if (_showOpponentGraveyard && OpponentPlayer.Graveyard.Cards.Count > 0)
    {
        <div class="graveyard-panel">
            <div class="graveyard-header">
                <MudText Typo="Typo.caption" Style="font-weight: 600;">Opponent Graveyard (@OpponentPlayer.Graveyard.Cards.Count)</MudText>
                <MudIconButton Size="Size.Small" Icon="@Icons.Material.Filled.Close"
                               OnClick="() => _showOpponentGraveyard = false" />
            </div>
            <div class="graveyard-cards">
                @foreach (var card in OpponentPlayer.Graveyard.Cards)
                {
                    <CardDisplay Name="@card.Name" ImageUrl="@card.ImageUrl" CardSize="100"
                                 Clickable="false"
                                 OnHoverStart="SetHoveredCard"
                                 OnHoverEnd="ClearHoveredCard" />
                }
            </div>
        </div>
    }
```

**Step 4: Add CSS for graveyard panel**

In `GameBoard.razor.css`:

```css
/* Graveyard viewer panel */
.graveyard-panel {
    background: rgba(20, 20, 20, 0.95);
    border: 1px solid rgba(255, 255, 255, 0.15);
    border-radius: 8px;
    padding: 8px;
    max-height: 200px;
    overflow-y: auto;
}

.graveyard-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 6px;
}

.graveyard-cards {
    display: flex;
    flex-wrap: wrap;
    gap: 4px;
}
```

**Step 5: Build to verify**

Run: `dotnet build src/MtgDecker.Web/`
Expected: Build succeeded

**Step 6: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor.css
git commit -m "feat(web): add graveyard viewer panel, clickable grave chips"
```
