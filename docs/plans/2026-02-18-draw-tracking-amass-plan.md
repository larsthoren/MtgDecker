# Draw Tracking + Amass Mechanic — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add draw-tracking, amass mechanic, Flash keyword enforcement, and +1/+1 counter support to the game engine, enabling full implementation of Orcish Bowmasters.

**Architecture:** Extend Player with draw counter + draw-step exemption flag. Centralize draw logic into a single method that fires GameEvent.DrawCard. Add AmassEffect (create/grow Army token with +1/+1 counters). Add HasFlash to CardDefinition for instant-speed creature casting. Wire BowmastersEffect as composite (amass + targeted damage). Register Orcish Bowmasters with Flash, ETB trigger, and opponent-draw trigger.

**Tech Stack:** C# 14, .NET 10, xUnit + FluentAssertions, MtgDecker.Engine

---

## Task 1: Add CounterType.PlusOnePlusOne

**Files:**
- Modify: `src/MtgDecker.Engine/Enums/CounterType.cs`
- Test: `tests/MtgDecker.Engine.Tests/PlusOnePlusOneCounterTests.cs`

**Step 1: Write failing test**

Create `tests/MtgDecker.Engine.Tests/PlusOnePlusOneCounterTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class PlusOnePlusOneCounterTests
{
    [Fact]
    public void GameCard_AddPlusOnePlusOneCounters_TracksCorrectly()
    {
        var card = new GameCard { Name = "Test Creature", BasePower = 2, BaseToughness = 2 };

        card.AddCounters(CounterType.PlusOnePlusOne, 3);

        card.GetCounters(CounterType.PlusOnePlusOne).Should().Be(3);
    }

    [Fact]
    public void GameCard_RemovePlusOnePlusOneCounter_Decrements()
    {
        var card = new GameCard { Name = "Test Creature", BasePower = 2, BaseToughness = 2 };
        card.AddCounters(CounterType.PlusOnePlusOne, 3);

        card.RemoveCounter(CounterType.PlusOnePlusOne).Should().BeTrue();
        card.GetCounters(CounterType.PlusOnePlusOne).Should().Be(2);
    }
}
```

**Step 2: Run to verify failure**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "PlusOnePlusOneCounterTests"`
Expected: Build failure — `CounterType.PlusOnePlusOne` doesn't exist.

**Step 3: Add the enum value**

In `src/MtgDecker.Engine/Enums/CounterType.cs`, add after `Mining`:

```csharp
PlusOnePlusOne,
```

**Step 4: Run tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "PlusOnePlusOneCounterTests"`
Expected: All pass.

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/Enums/CounterType.cs tests/MtgDecker.Engine.Tests/PlusOnePlusOneCounterTests.cs
git commit -m "feat(engine): add CounterType.PlusOnePlusOne"
```

---

## Task 2: +1/+1 counters modify Power/Toughness in RecalculateState

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (RecalculateState method, after Layer 7c)
- Test: `tests/MtgDecker.Engine.Tests/PlusOnePlusOneCounterTests.cs` (add tests)

**Step 1: Write failing tests**

Add to `PlusOnePlusOneCounterTests.cs`:

```csharp
using MtgDecker.Engine.Tests.Helpers;

// ... inside the class:

private static (GameEngine engine, GameState state, Player player) SetupGame()
{
    var h1 = new TestDecisionHandler();
    var h2 = new TestDecisionHandler();
    var p1 = new Player(Guid.NewGuid(), "P1", h1);
    var p2 = new Player(Guid.NewGuid(), "P2", h2);
    var state = new GameState(p1, p2);
    var engine = new GameEngine(state);
    return (engine, state, p1);
}

[Fact]
public void RecalculateState_CreatureWithPlusOnePlusOneCounters_HasModifiedPT()
{
    var (engine, state, player) = SetupGame();
    var creature = new GameCard
    {
        Name = "Test Creature",
        BasePower = 2,
        BaseToughness = 2,
        CardTypes = CardType.Creature,
    };
    creature.AddCounters(CounterType.PlusOnePlusOne, 3);
    player.Battlefield.Add(creature);

    engine.RecalculateState();

    creature.Power.Should().Be(5);  // 2 base + 3 counters
    creature.Toughness.Should().Be(5);  // 2 base + 3 counters
}

[Fact]
public void RecalculateState_NonCreatureWithCounters_NoEffect()
{
    var (engine, state, player) = SetupGame();
    var enchantment = new GameCard
    {
        Name = "Test Enchantment",
        CardTypes = CardType.Enchantment,
    };
    enchantment.AddCounters(CounterType.PlusOnePlusOne, 2);
    player.Battlefield.Add(enchantment);

    engine.RecalculateState();

    enchantment.Power.Should().BeNull();
    enchantment.Toughness.Should().BeNull();
}
```

**Step 2: Run to verify failure**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "PlusOnePlusOneCounterTests"`
Expected: `RecalculateState_CreatureWithPlusOnePlusOneCounters_HasModifiedPT` fails — Power is 2, not 5.

**Step 3: Implement in RecalculateState**

In `src/MtgDecker.Engine/GameEngine.cs`, in the `RecalculateState()` method, after the Layer 7c ModifyPT loop (after `ApplyPowerToughnessEffect` calls, around line 1767), add:

```csharp
// === LAYER 7d: +1/+1 counter adjustments (MTG Layer 7d) ===
foreach (var player in new[] { _state.Player1, _state.Player2 })
{
    foreach (var card in player.Battlefield.Cards)
    {
        var plusCounters = card.GetCounters(CounterType.PlusOnePlusOne);
        if (plusCounters > 0 && card.IsCreature)
        {
            card.EffectivePower = (card.EffectivePower ?? card.BasePower ?? 0) + plusCounters;
            card.EffectiveToughness = (card.EffectiveToughness ?? card.BaseToughness ?? 0) + plusCounters;
        }
    }
}
```

**Step 4: Run tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "PlusOnePlusOneCounterTests"`
Expected: All pass.

**Step 5: Run full engine suite**

Run: `dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All pass. No regressions.

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/PlusOnePlusOneCounterTests.cs
git commit -m "feat(engine): +1/+1 counters modify P/T in RecalculateState (Layer 7d)"
```

---

## Task 3: Add draw tracking to Player

**Files:**
- Modify: `src/MtgDecker.Engine/Player.cs`
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (turn-start reset)
- Test: `tests/MtgDecker.Engine.Tests/DrawTrackingTests.cs`

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/DrawTrackingTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class DrawTrackingTests
{
    [Fact]
    public void Player_DrawsThisTurn_StartsAtZero()
    {
        var handler = new TestDecisionHandler();
        var player = new Player(Guid.NewGuid(), "Test", handler);

        player.DrawsThisTurn.Should().Be(0);
    }

    [Fact]
    public void Player_DrawStepDrawExempted_StartsAsFalse()
    {
        var handler = new TestDecisionHandler();
        var player = new Player(Guid.NewGuid(), "Test", handler);

        player.DrawStepDrawExempted.Should().BeFalse();
    }
}
```

**Step 2: Run to verify failure**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "DrawTrackingTests"`
Expected: Build failure — `DrawsThisTurn` and `DrawStepDrawExempted` don't exist on Player.

**Step 3: Add properties to Player**

In `src/MtgDecker.Engine/Player.cs`, add after `CreaturesDiedThisTurn`:

```csharp
public int DrawsThisTurn { get; set; }
public bool DrawStepDrawExempted { get; set; }
```

**Step 4: Add turn-start reset**

In `src/MtgDecker.Engine/GameEngine.cs`, in `RunTurnAsync()` (around line 34), after the existing resets, add:

```csharp
_state.Player1.DrawsThisTurn = 0;
_state.Player2.DrawsThisTurn = 0;
_state.Player1.DrawStepDrawExempted = false;
_state.Player2.DrawStepDrawExempted = false;
```

**Step 5: Run tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "DrawTrackingTests"`
Expected: All pass.

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/Player.cs src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/DrawTrackingTests.cs
git commit -m "feat(engine): add DrawsThisTurn and DrawStepDrawExempted to Player"
```

---

## Task 4: Centralize draw logic and fire GameEvent.DrawCard

This is the most complex task. The engine currently draws cards in two places:
1. `DrawCards(player, count)` method (line ~2518) — for opening hand, cycling, effects
2. Draw step TBA (line ~143) — direct inline draw

We need to centralize into a single async method that tracks draws and fires events.

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs`
- Test: `tests/MtgDecker.Engine.Tests/DrawTrackingTests.cs` (add tests)

**Step 1: Write failing tests**

Add to `DrawTrackingTests.cs`:

```csharp
private static (GameEngine engine, GameState state, Player p1, Player p2,
    TestDecisionHandler h1, TestDecisionHandler h2) SetupGame()
{
    var h1 = new TestDecisionHandler();
    var h2 = new TestDecisionHandler();
    var p1 = new Player(Guid.NewGuid(), "P1", h1);
    var p2 = new Player(Guid.NewGuid(), "P2", h2);
    var state = new GameState(p1, p2);
    var engine = new GameEngine(state);
    return (engine, state, p1, p2, h1, h2);
}

[Fact]
public void DrawCards_IncrementsDrawsThisTurn()
{
    var (engine, state, player, _, _, _) = SetupGame();

    // Add cards to library
    player.Library.Add(GameCard.Create("Card1", "Instant"));
    player.Library.Add(GameCard.Create("Card2", "Instant"));
    player.Library.Add(GameCard.Create("Card3", "Instant"));

    engine.DrawCards(player, 3);

    player.DrawsThisTurn.Should().Be(3);
    player.Hand.Count.Should().Be(3);
}

[Fact]
public void DrawCards_DrawStepDraw_ExemptsFirstDraw()
{
    var (engine, state, player, _, _, _) = SetupGame();
    player.Library.Add(GameCard.Create("Card1", "Instant"));

    engine.DrawCards(player, 1, isDrawStepDraw: true);

    player.DrawsThisTurn.Should().Be(1);
    player.DrawStepDrawExempted.Should().BeTrue();
}
```

**Step 2: Run to verify failure**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "DrawTrackingTests"`
Expected: Build failure — `DrawCards` doesn't accept `isDrawStepDraw` parameter.

**Step 3: Refactor DrawCards**

In `src/MtgDecker.Engine/GameEngine.cs`:

Replace the existing `DrawCards` method (line ~2518) with:

```csharp
internal void DrawCards(Player player, int count, bool isDrawStepDraw = false)
{
    for (int i = 0; i < count; i++)
    {
        var card = player.Library.DrawFromTop();
        if (card != null)
        {
            player.Hand.Add(card);
            player.DrawsThisTurn++;

            // Track draw step exemption for Bowmasters-style triggers
            if (isDrawStepDraw && !player.DrawStepDrawExempted)
            {
                player.DrawStepDrawExempted = true;
                // First draw of draw step is exempt from draw triggers
            }
        }
        else
        {
            var winner = _state.GetOpponent(player);
            _state.IsGameOver = true;
            _state.Winner = winner.Name;
            _state.Log($"{player.Name} loses — cannot draw from an empty library.");
            return;
        }
    }
}
```

Also replace the inline draw in `ExecuteTurnBasedAction` (the `Phase.Draw` case, line ~143) to use the centralized method:

```csharp
case Phase.Draw:
    var hasSkipDraw = _state.ActiveEffects.Any(e =>
        e.Type == ContinuousEffectType.SkipDraw
        && (_state.Player1.Battlefield.Contains(e.SourceId)
            ? _state.Player1 : _state.Player2).Id == _state.ActivePlayer.Id);

    if (hasSkipDraw)
    {
        _state.Log($"{_state.ActivePlayer.Name}'s draw is skipped.");
        break;
    }

    _state.ActivePlayer.DrawStepDrawExempted = false; // Reset for this draw step
    DrawCards(_state.ActivePlayer, 1, isDrawStepDraw: true);
    break;
```

**Step 4: Run tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "DrawTrackingTests"`
Expected: All pass.

**Step 5: Run full engine suite**

Run: `dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All pass. The DrawCards refactor should be transparent to existing tests.

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/DrawTrackingTests.cs
git commit -m "feat(engine): centralize draw logic with draw-step tracking"
```

---

## Task 5: Add TriggerCondition.OpponentDrawsExceptFirst and trigger firing

**Files:**
- Modify: `src/MtgDecker.Engine/Triggers/TriggerCondition.cs`
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (DrawCards + CollectBoardTriggers)
- Test: `tests/MtgDecker.Engine.Tests/DrawTrackingTests.cs` (add trigger tests)

**Step 1: Write failing tests**

Add to `DrawTrackingTests.cs`:

```csharp
[Fact]
public async Task OpponentDrawsExceptFirst_DoesNotFireOnFirstDrawStepDraw()
{
    var (engine, state, p1, p2, h1, h2) = SetupGame();
    state.SetActivePlayer(p1);
    state.CurrentPhase = Phase.MainPhase1;

    // Create a permanent with OpponentDrawsExceptFirst trigger for p1
    var triggerCard = new GameCard
    {
        Name = "Draw Watcher",
        CardTypes = CardType.Creature,
        BasePower = 1,
        BaseToughness = 1,
        Triggers = [new Trigger(GameEvent.DrawCard, TriggerCondition.OpponentDrawsExceptFirst,
            new DealDamageEffect(1))],
    };
    p1.Battlefield.Add(triggerCard);
    triggerCard.TurnEnteredBattlefield = state.TurnNumber - 1;

    // P2 draws their first card (draw step draw) — should NOT trigger
    p2.Library.Add(GameCard.Create("Card1", "Instant"));
    p2.DrawStepDrawExempted = false;
    engine.DrawCards(p2, 1, isDrawStepDraw: true);

    // No triggered abilities should be on the stack
    state.Stack.Should().BeEmpty();
}

[Fact]
public async Task OpponentDrawsExceptFirst_FiresOnSecondDraw()
{
    var (engine, state, p1, p2, h1, h2) = SetupGame();
    state.SetActivePlayer(p1);
    state.CurrentPhase = Phase.MainPhase1;

    // Create a permanent with OpponentDrawsExceptFirst trigger for p1
    var triggerCard = new GameCard
    {
        Name = "Draw Watcher",
        CardTypes = CardType.Creature,
        BasePower = 1,
        BaseToughness = 1,
        Triggers = [new Trigger(GameEvent.DrawCard, TriggerCondition.OpponentDrawsExceptFirst,
            new DealDamageEffect(1))],
    };
    p1.Battlefield.Add(triggerCard);
    triggerCard.TurnEnteredBattlefield = state.TurnNumber - 1;

    // P2's first draw step draw (exempt)
    p2.Library.Add(GameCard.Create("Card1", "Instant"));
    p2.DrawStepDrawExempted = false;
    engine.DrawCards(p2, 1, isDrawStepDraw: true);

    // P2 draws again (not draw step) — should trigger
    p2.Library.Add(GameCard.Create("Card2", "Instant"));
    engine.DrawCards(p2, 1);

    // Should have a triggered ability on the stack
    state.Stack.Count.Should().Be(1);
}

[Fact]
public async Task OpponentDrawsExceptFirst_DoesNotFireOnOwnDraw()
{
    var (engine, state, p1, p2, h1, h2) = SetupGame();
    state.SetActivePlayer(p1);
    state.CurrentPhase = Phase.MainPhase1;

    // P1 has a permanent with OpponentDrawsExceptFirst trigger
    var triggerCard = new GameCard
    {
        Name = "Draw Watcher",
        CardTypes = CardType.Creature,
        BasePower = 1,
        BaseToughness = 1,
        Triggers = [new Trigger(GameEvent.DrawCard, TriggerCondition.OpponentDrawsExceptFirst,
            new DealDamageEffect(1))],
    };
    p1.Battlefield.Add(triggerCard);
    triggerCard.TurnEnteredBattlefield = state.TurnNumber - 1;

    // P1 (controller) draws — should NOT trigger (it's not an opponent)
    p1.Library.Add(GameCard.Create("Card1", "Instant"));
    p1.DrawStepDrawExempted = true; // Already drew in draw step
    engine.DrawCards(p1, 1);

    state.Stack.Should().BeEmpty();
}
```

**Note:** These tests reference `DealDamageEffect` from `MtgDecker.Engine.Triggers.Effects` — add the appropriate `using` statement. The tests may need adjustment depending on whether `DrawCards` fires triggers synchronously or if a separate trigger-firing step is needed. If `DrawCards` remains non-async, the trigger queuing may need to happen via a different mechanism (see Step 3 notes).

**Step 2: Run to verify failure**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "DrawTrackingTests"`
Expected: Build failure — `TriggerCondition.OpponentDrawsExceptFirst` doesn't exist.

**Step 3: Implement**

**3a: Add TriggerCondition enum value**

In `src/MtgDecker.Engine/Triggers/TriggerCondition.cs`, add:

```csharp
OpponentDrawsExceptFirst,   // When opponent draws (except first draw step draw)
```

**3b: Add trigger firing to DrawCards**

The key challenge: `DrawCards` is not async, but `QueueBoardTriggersOnStackAsync` is async. Two approaches:

**Option A (recommended):** Keep DrawCards synchronous. Add a new helper method that collects and pushes draw triggers directly (like QueueSelfTriggers but for draw events). The method can be synchronous since it just pushes to the stack — the actual async resolution happens later.

Add to `GameEngine.cs`:

```csharp
private void QueueDrawTriggers(Player drawingPlayer, GameCard drawnCard)
{
    foreach (var player in new[] { _state.Player1, _state.Player2 })
    {
        foreach (var card in player.Battlefield.Cards)
        {
            if (card.AbilitiesRemoved) continue;
            foreach (var trigger in card.Triggers)
            {
                if (trigger.Event != GameEvent.DrawCard) continue;
                if (trigger.Condition != TriggerCondition.OpponentDrawsExceptFirst) continue;

                // Only fires for opponent draws
                if (drawingPlayer.Id == player.Id) continue;

                _state.Log($"{card.Name} triggers on {drawingPlayer.Name}'s draw.");
                _state.StackPush(new TriggeredAbilityStackObject(card, player.Id, trigger.Effect));
            }
        }
    }
}
```

**3c: Call from DrawCards**

In the `DrawCards` method, after tracking the draw and checking exemption, add the trigger call:

```csharp
internal void DrawCards(Player player, int count, bool isDrawStepDraw = false)
{
    for (int i = 0; i < count; i++)
    {
        var card = player.Library.DrawFromTop();
        if (card != null)
        {
            player.Hand.Add(card);
            player.DrawsThisTurn++;

            if (isDrawStepDraw && !player.DrawStepDrawExempted)
            {
                player.DrawStepDrawExempted = true;
                // First draw of draw step — exempt from draw triggers
            }
            else
            {
                // Fire draw triggers (e.g., Orcish Bowmasters)
                QueueDrawTriggers(player, card);
            }
        }
        else
        {
            var winner = _state.GetOpponent(player);
            _state.IsGameOver = true;
            _state.Winner = winner.Name;
            _state.Log($"{player.Name} loses — cannot draw from an empty library.");
            return;
        }
    }
}
```

**3d: Add to CollectBoardTriggers**

In the `CollectBoardTriggers` method (around line 2119), add the pattern match case. Note: this may not be needed if QueueDrawTriggers handles it directly. If the existing QueueBoardTriggersOnStackAsync is used elsewhere for DrawCard events, add:

```csharp
(GameEvent.DrawCard, TriggerCondition.OpponentDrawsExceptFirst) => true,
```

However, since we're using the direct `QueueDrawTriggers` approach (synchronous), we may not need to touch CollectBoardTriggers at all. The direct approach is simpler and avoids async issues.

**Step 4: Run tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "DrawTrackingTests"`
Expected: All pass.

**Step 5: Run full engine suite**

Run: `dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All pass.

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/Triggers/TriggerCondition.cs src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/DrawTrackingTests.cs
git commit -m "feat(engine): add OpponentDrawsExceptFirst trigger condition with draw tracking"
```

---

## Task 6: Implement AmassEffect

**Files:**
- Create: `src/MtgDecker.Engine/Triggers/Effects/AmassEffect.cs`
- Test: `tests/MtgDecker.Engine.Tests/AmassEffectTests.cs`

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/AmassEffectTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class AmassEffectTests
{
    private static (EffectContext context, Player player, GameState state) CreateContext()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var source = new GameCard { Name = "Source" };
        var context = new EffectContext(state, p1, source, h1);
        return (context, p1, state);
    }

    [Fact]
    public async Task AmassOrcs_NoArmyExists_CreatesOrcArmyTokenWithCounter()
    {
        var (context, player, state) = CreateContext();
        var effect = new AmassEffect("Orc", 1);

        await effect.Execute(context);

        player.Battlefield.Cards.Should().HaveCount(1);
        var token = player.Battlefield.Cards[0];
        token.IsToken.Should().BeTrue();
        token.Name.Should().Be("Orc Army");
        token.IsCreature.Should().BeTrue();
        token.Subtypes.Should().Contain("Orc");
        token.Subtypes.Should().Contain("Army");
        token.BasePower.Should().Be(0);
        token.BaseToughness.Should().Be(0);
        token.GetCounters(CounterType.PlusOnePlusOne).Should().Be(1);
    }

    [Fact]
    public async Task AmassOrcs_ArmyExists_AddsCounterToExisting()
    {
        var (context, player, state) = CreateContext();

        // Pre-existing Army token
        var army = new GameCard
        {
            Name = "Orc Army",
            BasePower = 0,
            BaseToughness = 0,
            CardTypes = CardType.Creature,
            Subtypes = ["Orc", "Army"],
            IsToken = true,
        };
        army.AddCounters(CounterType.PlusOnePlusOne, 2);
        player.Battlefield.Add(army);

        var effect = new AmassEffect("Orc", 1);
        await effect.Execute(context);

        // Should still have exactly one Army (no new token created)
        player.Battlefield.Cards.Where(c => c.Subtypes.Contains("Army")).Should().HaveCount(1);
        army.GetCounters(CounterType.PlusOnePlusOne).Should().Be(3);
    }

    [Fact]
    public async Task AmassOrcs_HigherAmassValue_AddsMultipleCounters()
    {
        var (context, player, state) = CreateContext();
        var effect = new AmassEffect("Orc", 3);

        await effect.Execute(context);

        var token = player.Battlefield.Cards[0];
        token.GetCounters(CounterType.PlusOnePlusOne).Should().Be(3);
    }
}
```

**Step 2: Run to verify failure**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "AmassEffectTests"`
Expected: Build failure — `AmassEffect` class doesn't exist.

**Step 3: Implement AmassEffect**

Create `src/MtgDecker.Engine/Triggers/Effects/AmassEffect.cs`:

```csharp
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Amass [subtype] N: If you control an Army creature, put N +1/+1 counters on it.
/// Otherwise, create a 0/0 [subtype] Army creature token, then put N +1/+1 counters on it.
/// MTG rule 701.44.
/// </summary>
public class AmassEffect(string subtype, int amount) : IEffect
{
    public string Subtype { get; } = subtype;
    public int Amount { get; } = amount;

    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        // Find an existing Army creature controlled by this player
        var army = context.Controller.Battlefield.Cards
            .FirstOrDefault(c => c.IsCreature && c.Subtypes.Contains("Army", StringComparer.OrdinalIgnoreCase));

        if (army == null)
        {
            // Create a 0/0 [Subtype] Army creature token
            army = new GameCard
            {
                Name = $"{Subtype} Army",
                BasePower = 0,
                BaseToughness = 0,
                CardTypes = CardType.Creature,
                Subtypes = [Subtype, "Army"],
                IsToken = true,
                TurnEnteredBattlefield = context.State.TurnNumber,
            };
            context.Controller.Battlefield.Add(army);
            context.State.Log($"{context.Controller.Name} creates a {Subtype} Army token (0/0).");
        }

        army.AddCounters(CounterType.PlusOnePlusOne, Amount);
        context.State.Log($"Amass {Subtype} {Amount}: {army.Name} now has {army.GetCounters(CounterType.PlusOnePlusOne)} +1/+1 counter(s).");

        return Task.CompletedTask;
    }
}
```

**Step 4: Run tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "AmassEffectTests"`
Expected: All pass.

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/Triggers/Effects/AmassEffect.cs tests/MtgDecker.Engine.Tests/AmassEffectTests.cs
git commit -m "feat(engine): implement AmassEffect for Orc Army token creation/growth"
```

---

## Task 7: Add HasFlash to CardDefinition + enforce in CastSpell

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinition.cs`
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (CastSpell handler)
- Test: `tests/MtgDecker.Engine.Tests/FlashKeywordTests.cs`

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/FlashKeywordTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class FlashKeywordTests
{
    private static (GameEngine engine, GameState state, Player p1, Player p2,
        TestDecisionHandler h1, TestDecisionHandler h2) SetupGame()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, p1, p2, h1, h2);
    }

    [Fact]
    public async Task FlashCreature_CanBeCastDuringOpponentsTurn()
    {
        var (engine, state, p1, p2, h1, h2) = SetupGame();

        // It's P2's turn, P1 has priority (e.g., during P2's main phase with stack)
        state.SetActivePlayer(p2);
        state.CurrentPhase = Phase.MainPhase1;

        // Give P1 mana and a Flash creature in hand
        p1.ManaPool.Add(ManaColor.Black, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 1);

        // Register a test Flash creature in CardDefinitions for this test
        // We'll use Orcish Bowmasters once it's registered.
        // For now, test the HasFlash mechanism directly.
        var card = GameCard.Create("Flash Test Creature");
        card.ManaCost = ManaCost.Parse("{1}{B}");
        card.CardTypes = CardType.Creature;
        card.BasePower = 1;
        card.BaseToughness = 1;
        p1.Hand.Add(card);

        // P1 tries to cast during P2's turn — should succeed if engine checks HasFlash
        h1.EnqueueAction(GameAction.CastSpell(p1.Id, card.Id));
        h1.EnqueueAction(GameAction.Pass(p1.Id));
        h2.EnqueueAction(GameAction.Pass(p2.Id));

        await engine.RunPriorityLoopAsync(CancellationToken.None);

        // The creature should be on the stack or battlefield
        // (exact assertion depends on whether both players passed to resolve)
        var onStack = state.Stack.Any();
        var onBattlefield = p1.Battlefield.Cards.Any(c => c.Name == "Flash Test Creature");
        (onStack || onBattlefield).Should().BeTrue(
            "Flash creature should be castable during opponent's turn");
    }
}
```

**Note:** This test needs a CardDefinitions entry with `HasFlash = true` for "Flash Test Creature". You may need to temporarily register it, or alternatively test by directly verifying the CastSpell logic. The exact test pattern may need adjustment — check how existing CastSpell tests work (e.g., in the engine test files). The key assertion: a creature with `HasFlash` bypasses the sorcery-speed check.

**Step 2: Run to verify failure**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "FlashKeywordTests"`
Expected: Build failure or test failure — `HasFlash` doesn't exist on CardDefinition.

**Step 3: Add HasFlash to CardDefinition**

In `src/MtgDecker.Engine/CardDefinition.cs`, add after `EntersWithCounters`:

```csharp
public bool HasFlash { get; init; }
```

**Step 4: Enforce Flash in CastSpell**

In `src/MtgDecker.Engine/GameEngine.cs`, in the CastSpell handler (around line 448), change:

```csharp
bool isInstant = def.CardTypes.HasFlag(CardType.Instant);
if (!isInstant && !CanCastSorcery(castPlayer.Id))
```

To:

```csharp
bool isInstant = def.CardTypes.HasFlag(CardType.Instant);
bool hasFlash = def.HasFlash;
if (!isInstant && !hasFlash && !CanCastSorcery(castPlayer.Id))
```

Also update the Flashback handler (around line 884) the same way:

```csharp
bool fbIsInstant = fbDef.CardTypes.HasFlag(CardType.Instant);
bool fbHasFlash = fbDef.HasFlash;
if (!fbIsInstant && !fbHasFlash && !CanCastSorcery(fbPlayer.Id))
```

**Step 5: Register test fixture** (if needed for the test)

For the test to work, you'll need to either:
- Register "Flash Test Creature" in CardDefinitions with `HasFlash = true`
- OR set up the test differently using the engine internals

The recommended approach: register it temporarily in CardDefinitions, or adjust the test to verify the behavior with the actual Orcish Bowmasters entry (registered in Task 9). Alternatively, check how other tests handle CastSpell by reading `tests/MtgDecker.Engine.Tests/` for patterns.

**Step 6: Run tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "FlashKeywordTests"`
Expected: All pass.

**Step 7: Run full engine suite**

Run: `dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All pass.

**Step 8: Commit**

```bash
git add src/MtgDecker.Engine/CardDefinition.cs src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/FlashKeywordTests.cs
git commit -m "feat(engine): add HasFlash to CardDefinition and enforce Flash timing in CastSpell"
```

---

## Task 8: Implement BowmastersEffect (composite: amass + targeted damage)

**Files:**
- Create: `src/MtgDecker.Engine/Triggers/Effects/BowmastersEffect.cs`
- Test: `tests/MtgDecker.Engine.Tests/BowmastersEffectTests.cs`

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/BowmastersEffectTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class BowmastersEffectTests
{
    private static (EffectContext context, Player controller, Player opponent, GameState state,
        TestDecisionHandler handler) CreateContext()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var source = new GameCard { Name = "Orcish Bowmasters", BasePower = 1, BaseToughness = 1 };
        p1.Battlefield.Add(source);
        var context = new EffectContext(state, p1, source, h1);
        return (context, p1, p2, state, h1);
    }

    [Fact]
    public async Task BowmastersEffect_CreatesArmyAndDealsDamageToOpponent()
    {
        var (context, controller, opponent, state, handler) = CreateContext();

        // No target creature chosen — damage goes to opponent
        handler.EnqueueCardChoice(null); // decline creature target

        var effect = new BowmastersEffect();
        await effect.Execute(context);

        // Should have created an Orc Army token
        controller.Battlefield.Cards.Should().Contain(c =>
            c.IsToken && c.Subtypes.Contains("Army"));
        var army = controller.Battlefield.Cards.First(c => c.Subtypes.Contains("Army"));
        army.GetCounters(CounterType.PlusOnePlusOne).Should().Be(1);

        // Opponent should have taken 1 damage
        opponent.Life.Should().Be(19);
    }

    [Fact]
    public async Task BowmastersEffect_DealsDamageToTargetCreature()
    {
        var (context, controller, opponent, state, handler) = CreateContext();

        // Put a creature on opponent's battlefield
        var targetCreature = new GameCard
        {
            Name = "Bear",
            BasePower = 2,
            BaseToughness = 2,
            CardTypes = CardType.Creature,
        };
        opponent.Battlefield.Add(targetCreature);

        // Choose the creature as target
        handler.EnqueueCardChoice(targetCreature.Id);

        var effect = new BowmastersEffect();
        await effect.Execute(context);

        // Army created
        controller.Battlefield.Cards.Should().Contain(c => c.Subtypes.Contains("Army"));

        // Creature took 1 damage
        targetCreature.DamageMarked.Should().Be(1);
    }

    [Fact]
    public async Task BowmastersEffect_GrowsExistingArmy()
    {
        var (context, controller, opponent, state, handler) = CreateContext();

        // Pre-existing Army
        var army = new GameCard
        {
            Name = "Orc Army",
            BasePower = 0,
            BaseToughness = 0,
            CardTypes = CardType.Creature,
            Subtypes = ["Orc", "Army"],
            IsToken = true,
        };
        army.AddCounters(CounterType.PlusOnePlusOne, 2);
        controller.Battlefield.Add(army);

        handler.EnqueueCardChoice(null); // target opponent

        var effect = new BowmastersEffect();
        await effect.Execute(context);

        // Existing army should have grown
        army.GetCounters(CounterType.PlusOnePlusOne).Should().Be(3);
        // No new Army token created
        controller.Battlefield.Cards.Count(c => c.Subtypes.Contains("Army")).Should().Be(1);

        opponent.Life.Should().Be(19);
    }
}
```

**Step 2: Run to verify failure**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "BowmastersEffectTests"`
Expected: Build failure — `BowmastersEffect` doesn't exist.

**Step 3: Implement BowmastersEffect**

Create `src/MtgDecker.Engine/Triggers/Effects/BowmastersEffect.cs`:

```csharp
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Orcish Bowmasters' combined effect: amass Orcs 1, then deal 1 damage to any target.
/// Target selection happens during resolution via the decision handler.
/// </summary>
public class BowmastersEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        // Step 1: Amass Orcs 1
        var amass = new AmassEffect("Orc", 1);
        await amass.Execute(context, ct);

        // Step 2: Deal 1 damage to any target (creature or player)
        // Get all eligible creature targets (all creatures on the battlefield)
        var eligibleCreatures = context.State.Player1.Battlefield.Cards
            .Concat(context.State.Player2.Battlefield.Cards)
            .Where(c => c.IsCreature && !c.ActiveKeywords.Contains(Keyword.Shroud))
            .ToList();

        GameCard? targetCreature = null;
        if (eligibleCreatures.Count > 0)
        {
            var chosenId = await context.DecisionHandler.ChooseCard(
                eligibleCreatures,
                "Choose target for Orcish Bowmasters (1 damage), or decline to target opponent",
                optional: true, ct);

            if (chosenId.HasValue)
                targetCreature = eligibleCreatures.FirstOrDefault(c => c.Id == chosenId.Value);
        }

        if (targetCreature != null)
        {
            targetCreature.DamageMarked += 1;
            context.State.Log($"{context.Source.Name} deals 1 damage to {targetCreature.Name}.");
        }
        else
        {
            // Default: deal 1 damage to each opponent
            var opponent = context.State.GetOpponent(context.Controller);
            opponent.AdjustLife(-1);
            context.State.Log($"{context.Source.Name} deals 1 damage to {opponent.Name}. ({opponent.Life} life)");
        }
    }
}
```

**Step 4: Run tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "BowmastersEffectTests"`
Expected: All pass.

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/Triggers/Effects/BowmastersEffect.cs tests/MtgDecker.Engine.Tests/BowmastersEffectTests.cs
git commit -m "feat(engine): implement BowmastersEffect (amass Orcs 1 + deal 1 damage to any target)"
```

---

## Task 9: Register Orcish Bowmasters in CardDefinitions

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Test: `tests/MtgDecker.Engine.Tests/OrcishBowmastersTests.cs`

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/OrcishBowmastersTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class OrcishBowmastersTests
{
    [Fact]
    public void CardDefinition_OrcishBowmasters_IsRegistered()
    {
        CardDefinitions.TryGet("Orcish Bowmasters", out var def).Should().BeTrue();
        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(2); // {1}{B}
        def.Power.Should().Be(1);
        def.Toughness.Should().Be(1);
        def.CardTypes.Should().Be(CardType.Creature);
        def.HasFlash.Should().BeTrue();
        def.Subtypes.Should().Contain("Orc").And.Contain("Archer");
    }

    [Fact]
    public void CardDefinition_OrcishBowmasters_HasETBTrigger()
    {
        CardDefinitions.TryGet("Orcish Bowmasters", out var def).Should().BeTrue();
        def!.Triggers.Should().Contain(t =>
            t.Event == GameEvent.EnterBattlefield
            && t.Condition == TriggerCondition.Self
            && t.Effect is BowmastersEffect);
    }

    [Fact]
    public void CardDefinition_OrcishBowmasters_HasDrawTrigger()
    {
        CardDefinitions.TryGet("Orcish Bowmasters", out var def).Should().BeTrue();
        def!.Triggers.Should().Contain(t =>
            t.Event == GameEvent.DrawCard
            && t.Condition == TriggerCondition.OpponentDrawsExceptFirst
            && t.Effect is BowmastersEffect);
    }

    [Fact]
    public void GameCard_Create_OrcishBowmasters_LoadsFromRegistry()
    {
        var card = GameCard.Create("Orcish Bowmasters");

        card.ManaCost.Should().NotBeNull();
        card.BasePower.Should().Be(1);
        card.BaseToughness.Should().Be(1);
        card.CardTypes.Should().Be(CardType.Creature);
        card.Subtypes.Should().Contain("Orc");
    }
}
```

**Step 2: Run to verify failure**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "OrcishBowmastersTests"`
Expected: First test fails — "Orcish Bowmasters" not found in CardDefinitions.

**Step 3: Register in CardDefinitions**

In `src/MtgDecker.Engine/CardDefinitions.cs`, add a new entry. Place it in a "// Legacy Dimir Tempo" section after the existing UR Delver cards (around line 338):

```csharp
// ─── Legacy Dimir Tempo ────────────────────────────────────────────

["Orcish Bowmasters"] = new(ManaCost.Parse("{1}{B}"), null, 1, 1, CardType.Creature)
{
    HasFlash = true,
    Subtypes = ["Orc", "Archer"],
    Triggers =
    [
        new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new BowmastersEffect()),
        new Trigger(GameEvent.DrawCard, TriggerCondition.OpponentDrawsExceptFirst, new BowmastersEffect()),
    ],
    ContinuousEffects =
    [
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
            (card, _) => card.Name == "Orcish Bowmasters",
            GrantedKeyword: Keyword.Flash,
            Layer: EffectLayer.Layer6_AbilityAddRemove),
    ],
},
```

**Note:** The `ContinuousEffects` entry grants the Flash keyword on the battlefield (for display/rules purposes). The `HasFlash = true` property handles the casting-time check. Add the necessary `using` for `BowmastersEffect` at the top of the file if needed.

**Step 4: Run tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "OrcishBowmastersTests"`
Expected: All pass.

**Step 5: Run full engine suite**

Run: `dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All pass.

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/CardDefinitions.cs tests/MtgDecker.Engine.Tests/OrcishBowmastersTests.cs
git commit -m "feat(engine): register Orcish Bowmasters with Flash, ETB, and draw triggers"
```

---

## Task 10: Integration test — full Bowmasters gameplay

**Files:**
- Test: `tests/MtgDecker.Engine.Tests/OrcishBowmastersTests.cs` (add integration tests)

**Step 1: Write integration tests**

Add to `OrcishBowmastersTests.cs`:

```csharp
private static (GameEngine engine, GameState state, Player p1, Player p2,
    TestDecisionHandler h1, TestDecisionHandler h2) SetupGame()
{
    var h1 = new TestDecisionHandler();
    var h2 = new TestDecisionHandler();
    var p1 = new Player(Guid.NewGuid(), "P1", h1);
    var p2 = new Player(Guid.NewGuid(), "P2", h2);
    var state = new GameState(p1, p2);
    var engine = new GameEngine(state);
    return (engine, state, p1, p2, h1, h2);
}

[Fact]
public async Task Bowmasters_CastWithFlash_ETBTriggers()
{
    var (engine, state, p1, p2, h1, h2) = SetupGame();
    state.SetActivePlayer(p1);
    state.CurrentPhase = Phase.MainPhase1;

    // Give P1 mana
    p1.ManaPool.Add(ManaColor.Black, 1);
    p1.ManaPool.Add(ManaColor.Colorless, 1);

    var bowmasters = GameCard.Create("Orcish Bowmasters");
    p1.Hand.Add(bowmasters);

    // Cast Bowmasters
    h1.EnqueueAction(GameAction.CastSpell(p1.Id, bowmasters.Id));
    h1.EnqueueAction(GameAction.Pass(p1.Id)); // pass priority after casting
    h2.EnqueueAction(GameAction.Pass(p2.Id)); // opponent passes (spell resolves)

    // ETB trigger resolves — choose no creature target (hit opponent)
    h1.EnqueueCardChoice(null); // decline creature, target opponent
    h1.EnqueueAction(GameAction.Pass(p1.Id)); // pass after trigger
    h2.EnqueueAction(GameAction.Pass(p2.Id)); // opponent passes

    await engine.RunPriorityLoopAsync(CancellationToken.None);

    // Bowmasters on battlefield
    p1.Battlefield.Cards.Should().Contain(c => c.Name == "Orcish Bowmasters");

    // Orc Army token created
    p1.Battlefield.Cards.Should().Contain(c =>
        c.IsToken && c.Subtypes.Contains("Army"));

    // Opponent took 1 damage
    p2.Life.Should().Be(19);
}

[Fact]
public async Task Bowmasters_OpponentDrawsExtraCard_TriggerFires()
{
    var (engine, state, p1, p2, h1, h2) = SetupGame();
    state.SetActivePlayer(p1);
    state.CurrentPhase = Phase.MainPhase1;

    // Bowmasters already on battlefield
    var bowmasters = GameCard.Create("Orcish Bowmasters");
    p1.Battlefield.Add(bowmasters);
    bowmasters.TurnEnteredBattlefield = state.TurnNumber - 1;

    // Simulate: opponent already drew first card in draw step
    p2.DrawStepDrawExempted = true;
    p2.DrawsThisTurn = 1;

    // Opponent draws extra card (e.g., from Brainstorm)
    p2.Library.Add(GameCard.Create("Extra Card", "Instant"));
    engine.DrawCards(p2, 1);

    // Draw trigger should be on the stack
    state.Stack.Count.Should().BeGreaterOrEqualTo(1);
}
```

**Note:** These integration tests test the full flow — casting, ETB triggers, draw triggers. The exact number of `EnqueueAction` / `EnqueueCardChoice` calls may need tuning based on how the priority loop interacts with triggered abilities. Check existing integration test patterns (e.g., `ParallaxWaveTests`, `EtbCountersTests`) for the correct queuing sequence.

**Step 2: Run tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "OrcishBowmastersTests"`
Expected: All pass.

**Step 3: Commit**

```bash
git add tests/MtgDecker.Engine.Tests/OrcishBowmastersTests.cs
git commit -m "test(engine): add Orcish Bowmasters integration tests (cast, ETB, draw trigger)"
```

---

## Task 11: Full test suite verification

**Step 1: Engine tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All pass (existing 1248+ tests + new tests).

**Step 2: All other test suites**

Run: `dotnet test tests/MtgDecker.Domain.Tests/ && dotnet test tests/MtgDecker.Application.Tests/ && dotnet test tests/MtgDecker.Infrastructure.Tests/`
Expected: All pass.

**Step 3: Web build**

Run: `dotnet build src/MtgDecker.Web/`
Expected: Success.

**Step 4: Push and create PR**

```bash
git push -u origin feat/draw-tracking-amass
gh pr create --title "feat(engine): draw tracking + amass + Flash + Orcish Bowmasters" --body "..."
```

---

## Summary

| Task | What | Key Files |
|------|------|-----------|
| 1 | CounterType.PlusOnePlusOne | Enums/CounterType.cs |
| 2 | +1/+1 counters modify P/T | GameEngine.cs (RecalculateState) |
| 3 | Player draw tracking properties | Player.cs, GameEngine.cs |
| 4 | Centralize draw logic + tracking | GameEngine.cs |
| 5 | OpponentDrawsExceptFirst trigger | TriggerCondition.cs, GameEngine.cs |
| 6 | AmassEffect | Effects/AmassEffect.cs |
| 7 | HasFlash + CastSpell enforcement | CardDefinition.cs, GameEngine.cs |
| 8 | BowmastersEffect | Effects/BowmastersEffect.cs |
| 9 | Orcish Bowmasters registration | CardDefinitions.cs |
| 10 | Integration tests | OrcishBowmastersTests.cs |
| 11 | Full suite verification + PR | — |
