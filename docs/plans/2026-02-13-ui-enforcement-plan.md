# UI Enforcement Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Strip all sandbox/free actions from the game UI so every player action goes through engine-enforced rules.

**Architecture:** Remove MoveCard, Draw Card button, Life buttons, manual Untap, sandbox play fallback, and blanket Undo. Add summoning sickness enforcement on tap, summoning sickness visual indicator, scoped mana-tap undo, and multi-step cancel. ActionMenu becomes minimal: Play (from hand) and Tap (from battlefield, with summoning sickness check).

**Tech Stack:** .NET 10, Blazor, MudBlazor, xUnit + FluentAssertions

**Environment:**
```bash
export PATH="/c/Program Files/dotnet:$PATH"
# Build: dotnet build src/MtgDecker.Web/
# Test:  dotnet test tests/MtgDecker.Engine.Tests/
# All tests: dotnet test tests/MtgDecker.Engine.Tests/ && dotnet test tests/MtgDecker.Domain.Tests/ && dotnet test tests/MtgDecker.Application.Tests/ && dotnet test tests/MtgDecker.Infrastructure.Tests/
```

**Working directory:** `C:\Users\larst\MtgDecker\.worktrees\ui-enforcement`

---

### Task 1: Remove MoveCard from Engine

Remove the MoveCard action type, factory method, execution case, and undo case from the engine. Delete tests that exercise MoveCard.

**Files:**
- Modify: `src/MtgDecker.Engine/Enums/ActionType.cs` — Remove `MoveCard` enum value
- Modify: `src/MtgDecker.Engine/GameAction.cs:53-60` — Remove `MoveCard` factory method
- Modify: `src/MtgDecker.Engine/GameEngine.cs:304-314` — Remove `case ActionType.MoveCard` in `ExecuteAction`
- Modify: `src/MtgDecker.Engine/GameEngine.cs:846-854` — Remove `case ActionType.MoveCard` in `UndoLastAction`
- Delete tests: `tests/MtgDecker.Engine.Tests/GameEngineActionExecutionTests.cs` — Remove `ExecuteAction_MoveCard_MovesBetweenZones` test
- Delete tests: `tests/MtgDecker.Engine.Tests/GameActionTests.cs` — Remove `MoveCard_CreatesMoveAction` test
- Delete tests: `tests/MtgDecker.Engine.Tests/GameEngineUndoTests.cs` — Remove `UndoMoveCard_ReversesSourceAndDestination`, `Undo_LogsReversal_MoveCard`, `Undo_PopOnlyOnSuccess_MoveCard` tests
- Modify: `tests/MtgDecker.Engine.Tests/GameEngineIntegrationTests.cs:228` — Replace `MoveCard` with direct zone manipulation
- Modify: `tests/MtgDecker.Engine.Tests/GameEngineUndoTests.cs:284` — Replace `MoveCard` in `Undo_MultipleSequentialActions` with a different action

**Step 1: Remove MoveCard enum value**

In `src/MtgDecker.Engine/Enums/ActionType.cs`, delete the `MoveCard` line from the enum.

**Step 2: Remove MoveCard factory method**

In `src/MtgDecker.Engine/GameAction.cs`, delete the `MoveCard` static factory method (lines 53-60).

**Step 3: Remove MoveCard execution case**

In `src/MtgDecker.Engine/GameEngine.cs`, delete `case ActionType.MoveCard:` and its body (lines 304-314).

**Step 4: Remove MoveCard undo case**

In `src/MtgDecker.Engine/GameEngine.cs`, delete `case ActionType.MoveCard:` and its body in `UndoLastAction` (lines 846-854).

**Step 5: Delete MoveCard tests**

In `tests/MtgDecker.Engine.Tests/GameEngineActionExecutionTests.cs`, delete the `ExecuteAction_MoveCard_MovesBetweenZones` test method.

In `tests/MtgDecker.Engine.Tests/GameActionTests.cs`, delete the `MoveCard_CreatesMoveAction` test method.

In `tests/MtgDecker.Engine.Tests/GameEngineUndoTests.cs`, delete these test methods:
- `UndoMoveCard_ReversesSourceAndDestination`
- `Undo_LogsReversal_MoveCard`
- `Undo_PopOnlyOnSuccess_MoveCard`

**Step 6: Fix integration tests that use MoveCard**

In `tests/MtgDecker.Engine.Tests/GameEngineIntegrationTests.cs`, find the line that uses `GameAction.MoveCard(state.Player1.Id, card.Id, ZoneType.Battlefield, ZoneType.Graveyard)` and replace it with direct zone manipulation:
```csharp
state.Player1.Battlefield.RemoveById(card.Id);
state.Player1.Graveyard.Add(card);
```

In `tests/MtgDecker.Engine.Tests/GameEngineUndoTests.cs`, the `Undo_MultipleSequentialActions` test uses MoveCard as one of the actions. Replace it with a TapCard action on a land (which is still undoable).

**Step 7: Build and run tests**

Run: `dotnet build src/MtgDecker.Web/ && dotnet test tests/MtgDecker.Engine.Tests/`
Expected: Build succeeds, all remaining tests pass.

**Step 8: Commit**

```bash
git add -A
git commit -m "feat(engine): remove MoveCard action type entirely"
```

---

### Task 2: Remove Sandbox Play Fallback

Remove the path in PlayCard that allows cards without a CardDefinition to enter the battlefield for free. Instead, reject the action with a log message. Update all tests that relied on sandbox mode to use registered cards or direct zone manipulation.

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs:207-221` — Replace Part C (sandbox fallback) with rejection
- Test: `tests/MtgDecker.Engine.Tests/CastSpellTests.cs` — Delete sandbox tests, add rejection test
- Modify: Multiple test files that use sandbox play for test setup

**Step 1: Write the failing test for rejection**

In `tests/MtgDecker.Engine.Tests/CastSpellTests.cs`, add a new test in the "Part C" section:

```csharp
[Fact]
public async Task UnregisteredCard_WithoutManaCost_IsRejected()
{
    var (engine, state) = TestHelpers.CreateEngineWithState();
    var p1 = state.Player1;
    // Card with no ManaCost and no CardDefinition registration
    var card = GameCard.Create("Unknown Creature", "Creature — Mystery");
    p1.Hand.Add(card);

    await engine.ExecuteAction(GameAction.PlayCard(p1.Id, card.Id));

    p1.Battlefield.Cards.Should().NotContain(c => c.Id == card.Id);
    p1.Hand.Cards.Should().Contain(c => c.Id == card.Id);
    state.GameLog.Should().Contain(l => l.Contains("not supported"));
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "UnregisteredCard_WithoutManaCost_IsRejected"`
Expected: FAIL — card still enters battlefield via sandbox path.

**Step 3: Replace sandbox fallback with rejection**

In `src/MtgDecker.Engine/GameEngine.cs`, replace the Part C block (lines 207-221) with:

```csharp
else
{
    // No ManaCost, not a land — card not supported in engine
    _state.Log($"{playCard.Name} is not supported in the engine (no card definition).");
    break;
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "UnregisteredCard_WithoutManaCost_IsRejected"`
Expected: PASS

**Step 5: Delete sandbox tests**

In `tests/MtgDecker.Engine.Tests/CastSpellTests.cs`, delete:
- `SandboxCard_NoManaCost_PlaysFreely`
- `SandboxCard_GoesToBattlefield`

In `tests/MtgDecker.Engine.Tests/CastSpellStackIntegrationTests.cs`, delete:
- `SandboxMode_StillImmediate_NoStack`

**Step 6: Fix tests that used sandbox for convenience**

Many tests use sandbox play (`GameAction.PlayCard` on unregistered cards) as a shortcut to get cards onto the battlefield. These need to be changed to direct zone manipulation instead.

For each test file below, replace `await engine.ExecuteAction(GameAction.PlayCard(p1.Id, card.Id))` with direct zone adds:
```csharp
p1.Hand.RemoveById(card.Id);
p1.Battlefield.Add(card);
card.TurnEnteredBattlefield = state.TurnNumber;
```

Files to update:
- `tests/MtgDecker.Engine.Tests/GameEngineActionExecutionTests.cs` — `ExecuteAction_PlayCard_` tests at lines 24 and 39
- `tests/MtgDecker.Engine.Tests/GameEngineUndoTests.cs` — Multiple tests that use sandbox cards for undo testing
- `tests/MtgDecker.Engine.Tests/GameEngineIntegrationTests.cs:269-289` — Integration test using sandbox
- `tests/MtgDecker.Engine.Tests/OnBoardChangedIntegrationTests.cs:11-23` — Goblin King sandbox test
- `tests/MtgDecker.Engine.Tests/AuraCastingTests.cs:183` — Aura sandbox test
- `tests/MtgDecker.Engine.Tests/ParallaxWaveTests.cs:68` — Parallax Wave test

For tests that specifically test PlayCard behavior (not just setup), register the card in CardDefinitions with a mana cost, then set up mana in the player's pool before playing.

**Step 7: Build and run all engine tests**

Run: `dotnet build src/MtgDecker.Engine/ && dotnet test tests/MtgDecker.Engine.Tests/`
Expected: Build succeeds, all tests pass.

**Step 8: Commit**

```bash
git add -A
git commit -m "feat(engine): remove sandbox play fallback, reject unregistered cards"
```

---

### Task 3: Add Summoning Sickness Enforcement on TapCard

Add a check in the TapCard handler that prevents tapping creatures with summoning sickness. Lands and non-creature permanents are exempt.

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs:224-292` — Add summoning sickness check in TapCard case
- Test: `tests/MtgDecker.Engine.Tests/SummoningSicknessTests.cs` — New test file

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/SummoningSicknessTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class SummoningSicknessTests
{
    [Fact]
    public async Task TapCard_CreatureWithSummoningSickness_IsRejected()
    {
        var (engine, state) = TestHelpers.CreateEngineWithState();
        var p1 = state.Player1;
        var creature = GameCard.Create("Grizzly Bears", "Creature — Bear");
        p1.Battlefield.Add(creature);
        creature.TurnEnteredBattlefield = state.TurnNumber; // entered this turn

        await engine.ExecuteAction(GameAction.TapCard(p1.Id, creature.Id));

        creature.IsTapped.Should().BeFalse();
        state.GameLog.Should().Contain(l => l.Contains("summoning sickness"));
    }

    [Fact]
    public async Task TapCard_LandWithSummoningSickness_IsAllowed()
    {
        var (engine, state) = TestHelpers.CreateEngineWithState();
        var p1 = state.Player1;
        var land = GameCard.Create("Forest", "Basic Land — Forest");
        land.ManaAbility = new ManaAbility(ManaAbilityType.Fixed, ManaColor.Green);
        p1.Battlefield.Add(land);
        land.TurnEnteredBattlefield = state.TurnNumber; // entered this turn

        await engine.ExecuteAction(GameAction.TapCard(p1.Id, land.Id));

        land.IsTapped.Should().BeTrue();
    }

    [Fact]
    public async Task TapCard_CreatureWithoutSummoningSickness_IsAllowed()
    {
        var (engine, state) = TestHelpers.CreateEngineWithState();
        var p1 = state.Player1;
        var creature = GameCard.Create("Grizzly Bears", "Creature — Bear");
        p1.Battlefield.Add(creature);
        creature.TurnEnteredBattlefield = state.TurnNumber - 1; // entered last turn

        await engine.ExecuteAction(GameAction.TapCard(p1.Id, creature.Id));

        creature.IsTapped.Should().BeTrue();
    }

    [Fact]
    public async Task TapCard_CreatureWithHaste_IgnoresSummoningSickness()
    {
        var (engine, state) = TestHelpers.CreateEngineWithState();
        var p1 = state.Player1;
        var creature = GameCard.Create("Goblin Guide", "Creature — Goblin Scout");
        creature.HasHaste = true;
        p1.Battlefield.Add(creature);
        creature.TurnEnteredBattlefield = state.TurnNumber; // entered this turn

        await engine.ExecuteAction(GameAction.TapCard(p1.Id, creature.Id));

        creature.IsTapped.Should().BeTrue();
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "SummoningSicknessTests"`
Expected: `TapCard_CreatureWithSummoningSickness_IsRejected` FAILS (creature gets tapped anyway). Others may pass or fail depending on existing behavior.

**Step 3: Implement summoning sickness check**

In `src/MtgDecker.Engine/GameEngine.cs`, in the `case ActionType.TapCard:` handler (line 226), after the `tapTarget != null && !tapTarget.IsTapped` check, add:

```csharp
// Check summoning sickness for creatures (lands are exempt)
if (tapTarget.HasSummoningSickness(state.TurnNumber) && !tapTarget.TypeLine.Contains("Land"))
{
    _state.Log($"{tapTarget.Name} has summoning sickness.");
    break;
}
```

This uses the existing `HasSummoningSickness(int currentTurn)` method on GameCard (line 83-87) which checks `TurnEnteredBattlefield == currentTurn && !HasHaste`.

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "SummoningSicknessTests"`
Expected: All 4 tests PASS.

**Step 5: Run all engine tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All tests pass. Check that no existing tests break from the new check. If any do, fix them by setting `TurnEnteredBattlefield` to an earlier turn.

**Step 6: Commit**

```bash
git add -A
git commit -m "feat(engine): enforce summoning sickness on TapCard for creatures"
```

---

### Task 4: Redesign Undo to Scoped Mana-Tap Undo

Replace the blanket undo system with a scoped undo that only allows untapping lands whose mana hasn't been spent. Remove undo for PlayCard, UntapCard, MoveCard, and CastSpell.

**Files:**
- Modify: `src/MtgDecker.Engine/Player.cs:18` — Add `PendingManaTaps` list
- Modify: `src/MtgDecker.Engine/GameEngine.cs:224-292` — Track pending taps on mana production
- Modify: `src/MtgDecker.Engine/GameEngine.cs:801-874` — Strip `UndoLastAction` to only handle TapCard with unspent mana
- Modify: `src/MtgDecker.Engine/GameEngine.cs` — Clear pending taps when mana is spent (in CastSpell mana payment)
- Test: `tests/MtgDecker.Engine.Tests/GameEngineUndoTests.cs` — Rewrite entirely

**Step 1: Write failing tests for new undo behavior**

Rewrite `tests/MtgDecker.Engine.Tests/GameEngineUndoTests.cs` with only these test cases:

```csharp
[Fact]
public async Task UndoTap_UnspentMana_UntapsLandAndRemovesMana()
{
    // Tap a forest for G, undo before spending — should untap and remove G from pool
}

[Fact]
public async Task UndoTap_SpentMana_Rejected()
{
    // Tap a forest for G, cast a spell using G, try undo — should be rejected
}

[Fact]
public async Task UndoTap_PartiallySpentPool_RejectsAll()
{
    // Tap forest1 for G, tap forest2 for G, spend 1G on spell, try undo forest2 — rejected (mana was spent from pool)
}

[Fact]
public async Task UndoTap_NoActionHistory_ReturnsFalse()
{
    // Empty history, undo returns false
}

[Fact]
public async Task UndoPlayCard_Rejected()
{
    // Play a land, try undo — should return false
}

[Fact]
public async Task UndoCastSpell_Rejected()
{
    // Cast a spell, try undo — should return false
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "GameEngineUndoTests"`
Expected: Most tests FAIL — current undo allows PlayCard and CastSpell undo.

**Step 3: Add PendingManaTaps to Player**

In `src/MtgDecker.Engine/Player.cs`, add:
```csharp
public List<Guid> PendingManaTaps { get; } = new();
```

**Step 4: Track pending taps in TapCard handler**

In `src/MtgDecker.Engine/GameEngine.cs`, in the TapCard case, after mana is produced and action is pushed to history, add:
```csharp
player.PendingManaTaps.Add(tapTarget.Id);
```

**Step 5: Clear pending taps on mana spend**

In `src/MtgDecker.Engine/GameEngine.cs`, in the CastSpell case, after `player.ManaPool.Pay(cost)` (or wherever mana is spent), add:
```csharp
player.PendingManaTaps.Clear();
```

Do the same wherever mana is spent: CastSpell (line ~459), ActivateAbility cost payment, Cycle cost payment.

**Step 6: Rewrite UndoLastAction**

In `src/MtgDecker.Engine/GameEngine.cs`, replace the entire `UndoLastAction` method:

```csharp
public bool UndoLastAction(Guid playerId)
{
    var player = _state.GetPlayer(playerId);
    if (player.ActionHistory.Count == 0) return false;

    var action = player.ActionHistory.Peek();

    // Only TapCard can be undone, and only if mana is unspent
    if (action.Type != ActionType.TapCard)
    {
        _state.Log("Only land taps with unspent mana can be undone.");
        return false;
    }

    if (!player.PendingManaTaps.Contains(action.CardId!.Value))
    {
        _state.Log("Mana already spent — tap cannot be undone.");
        return false;
    }

    var tapTarget = player.Battlefield.Cards.FirstOrDefault(c => c.Id == action.CardId);
    if (tapTarget == null) return false;

    player.ActionHistory.Pop();
    tapTarget.IsTapped = false;
    player.PendingManaTaps.Remove(tapTarget.Id);
    if (action.ManaProduced.HasValue)
        player.ManaPool.Deduct(action.ManaProduced.Value, 1);
    _state.Log($"{player.Name} untaps {tapTarget.Name}.");
    return true;
}
```

**Step 7: Run undo tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "GameEngineUndoTests"`
Expected: All new tests PASS.

**Step 8: Run all engine tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All pass. Some old undo tests (PlayCard undo, CastSpell undo) were already deleted in Step 1. Check for any remaining tests that call `UndoLastAction` expecting PlayCard/CastSpell/MoveCard undo — these need updating.

Specifically update `tests/MtgDecker.Engine.Tests/StackUndoTests.cs` — CastSpell undo tests should now expect rejection (return false).

**Step 9: Commit**

```bash
git add -A
git commit -m "feat(engine): redesign undo to scoped mana-tap only"
```

---

### Task 5: Remove UI Sandbox Actions

Remove Draw Card button, Life +1/-1 buttons, manual Untap option, and Move to menu from the UI. Remove corresponding GameSession methods and event callbacks.

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor:153-157` — Remove life buttons
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor:182-184` — Remove draw button
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor:394-395` — Remove OnLifeAdjust, OnDrawCard parameters
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor:642-648` — Remove HandleMoveTo method
- Modify: `src/MtgDecker.Web/Components/Pages/Game/ActionMenu.razor` — Remove "Move to" menu and Untap from Tap/Untap button
- Modify: `src/MtgDecker.Web/Components/Pages/GamePage.razor:32-33` — Remove OnLifeAdjust, OnDrawCard bindings
- Modify: `src/MtgDecker.Web/Components/Pages/GamePage.razor:221-229` — Remove HandleLifeAdjust, HandleDrawCard methods
- Modify: `src/MtgDecker.Engine/GameSession.cs:171-206` — Remove AdjustLife, DrawCard methods

**Step 1: Remove GameSession.AdjustLife and GameSession.DrawCard**

In `src/MtgDecker.Engine/GameSession.cs`, delete the `AdjustLife` method (lines 171-190) and `DrawCard` method (lines 192-206).

**Step 2: Remove GamePage handlers and bindings**

In `src/MtgDecker.Web/Components/Pages/GamePage.razor`:
- Delete `HandleLifeAdjust` method (lines 221-224)
- Delete `HandleDrawCard` method (lines 226-229)
- Remove `OnLifeAdjust="HandleLifeAdjust"` binding (line 32)
- Remove `OnDrawCard="HandleDrawCard"` binding (line 33)

**Step 3: Remove GameBoard life and draw buttons**

In `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor`:
- Delete the life adjustment buttons (lines 153-157): the `-1` and `+1` MudIconButtons flanking the life chip
- Delete the draw card button (lines 182-184): the `+` PostAdd MudIconButton
- Delete the `OnLifeAdjust` parameter declaration (line 394)
- Delete the `OnDrawCard` parameter declaration (line 395)

**Step 4: Remove HandleMoveTo from GameBoard**

In `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor`, delete the `HandleMoveTo` method (lines 642-648).

**Step 5: Simplify ActionMenu**

In `src/MtgDecker.Web/Components/Pages/Game/ActionMenu.razor`:

Remove the entire "Move to" section (lines 33-42): the divider, "Move to" label, and zone list.

Change the Tap/Untap button (lines 23-31) to only show "Tap" (not "Untap"):
```razor
@if (IsOwnCard && CurrentZone == ZoneType.Battlefield && !IsTapped)
{
    <MudButton Size="Size.Small" Variant="Variant.Text" FullWidth="true"
               StartIcon="@Icons.Material.Filled.RotateRight"
               Color="Color.Info" Style="justify-content: flex-start;"
               OnClick="() => OnTapToggle.InvokeAsync()">Tap</MudButton>
}
```

Remove the `OnMoveTo` parameter and `AvailableZones` computed property.

**Step 6: Build**

Run: `dotnet build src/MtgDecker.Web/`
Expected: Build succeeds with 0 errors.

**Step 7: Commit**

```bash
git add -A
git commit -m "feat(web): remove sandbox UI actions (draw, life, move, untap)"
```

---

### Task 6: Remove Undo Button from UI

Remove the blanket undo button from the PhaseBar. Replace with per-land untap affordance on tapped lands with unspent mana.

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor` — Remove Undo button, add untap indicator on pending-tap lands
- Modify: `src/MtgDecker.Web/Components/Pages/GamePage.razor` — Remove OnUndo binding and handler
- Modify: `src/MtgDecker.Engine/GameSession.cs:160-169` — Keep Undo method (still used by new UI) but verify it routes correctly

**Step 1: Find and remove the Undo button**

In `GameBoard.razor`, find the undo button in the PhaseBar area (it has a Ctrl+Z keyboard shortcut). Remove the button and the `OnUndo` event callback parameter. Remove the keyboard shortcut handler for Ctrl+Z if it exists.

In `GamePage.razor`, remove the `OnUndo` binding and handler method.

**Step 2: Add untap affordance on tapped lands**

In `GameBoard.razor`, in the land rendering section on the battlefield, add a visual indicator on tapped lands that have unspent mana (are in `PendingManaTaps`). When clicked, invoke undo for that specific land tap.

The land card needs to show a small "untap" icon/button overlay when:
- The land is tapped (`card.IsTapped`)
- The land's Id is in the player's `PendingManaTaps` list

Clicking the untap indicator calls `OnAction` with a new approach: call `GameSession.Undo` which will try to undo the last TapCard action for that land.

Note: The current `UndoLastAction` only undoes the LAST action on the stack. If the player tapped 3 lands, they can only undo them in reverse order (LIFO). This is acceptable for now.

**Step 3: Build**

Run: `dotnet build src/MtgDecker.Web/`
Expected: Build succeeds.

**Step 4: Commit**

```bash
git add -A
git commit -m "feat(web): replace undo button with per-land untap affordance"
```

---

### Task 7: Add Summoning Sickness Visual Indicator

Add a CSS visual treatment to battlefield creatures that have summoning sickness: 60% opacity with a dashed yellow border.

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor` — Add `summoning-sick` CSS class to creature cards on battlefield
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor` — Add CSS styles

**Step 1: Add CSS class to summoning-sick creatures**

In `GameBoard.razor`, find where creature cards are rendered on the battlefield. Add a CSS class conditionally:

```razor
class="@($"battlefield-card{(card.HasSummoningSickness(State.TurnNumber) && card.TypeLine.Contains("Creature") ? " summoning-sick" : "")}")"
```

**Step 2: Add CSS styles**

In `GameBoard.razor` (or the relevant CSS file), add:

```css
.summoning-sick {
    opacity: 0.6;
    border: 2px dashed #FFD700 !important;
    border-radius: 6px;
}
```

**Step 3: Build and verify visually**

Run: `dotnet build src/MtgDecker.Web/`
Expected: Build succeeds. Visual verification: creatures that just entered the battlefield appear dimmed with a dashed yellow border.

**Step 4: Commit**

```bash
git add -A
git commit -m "feat(web): add summoning sickness visual indicator (opacity + dashed yellow border)"
```

---

### Task 8: Add Cancel Button for Multi-Step Sequences

Add a "Cancel" button to targeting and card choice overlays that reverts the current sequence. When targeting for a spell, cancel should return the spell to hand and refund mana.

**Files:**
- Modify: `src/MtgDecker.Engine/InteractiveDecisionHandler.cs` — Add `CancelTarget` method
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor` — Add cancel button to target picker overlay
- Modify: `src/MtgDecker.Engine/GameEngine.cs` — Handle target cancellation (return card to hand, refund mana)

**Step 1: Write failing test for target cancellation**

Create test in `tests/MtgDecker.Engine.Tests/TargetCancellationTests.cs`:

```csharp
[Fact]
public async Task CancelTarget_ReturnsSpellToHand_RefundsMana()
{
    // Set up a player with a Lightning Bolt and a target creature
    // Cast Lightning Bolt (goes on stack, mana paid)
    // When targeting prompt appears, cancel
    // Verify: Bolt back in hand, mana refunded, stack empty
}
```

**Step 2: Implement cancellation in InteractiveDecisionHandler**

Add a method that signals cancellation instead of target selection. The engine's targeting loop should handle this by rolling back the cast.

**Step 3: Add Cancel button to target picker UI**

In `GameBoard.razor`, in the target picker overlay (lines 248-279), add a Cancel button:

```razor
<MudButton Size="Size.Small" Variant="Variant.Text" Color="Color.Default"
           OnClick="HandleTargetCancel">Cancel</MudButton>
```

**Step 4: Build and run tests**

Run: `dotnet build src/MtgDecker.Web/ && dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All pass.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(engine+web): add cancel button for targeting sequences"
```

---

### Task 9: Update CLAUDE.md and Clean Up

Update project documentation to reflect removed sandbox features and new enforcement model.

**Files:**
- Modify: `CLAUDE.md` — Remove sandbox references, update action descriptions
- Modify: `src/MtgDecker.Engine/GameSession.cs` — Remove Undo method if no longer used, or verify it's still wired

**Step 1: Update CLAUDE.md**

In the Game Engine section, remove the line about sandbox mode: "Cards not in `CardDefinitions` registry work in sandbox mode (no mana required)."

Replace with: "Cards must be registered in `CardDefinitions` to be playable. Unregistered cards are rejected with a log message."

**Step 2: Run all tests across all projects**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ && dotnet test tests/MtgDecker.Domain.Tests/ && dotnet test tests/MtgDecker.Application.Tests/ && dotnet test tests/MtgDecker.Infrastructure.Tests/`
Expected: All tests pass across all 4 projects.

**Step 3: Commit**

```bash
git add -A
git commit -m "docs: update CLAUDE.md to reflect UI enforcement changes"
```
