# Engine Full-Play Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement 7 engine systems so both starter decks (Goblins + Enchantress) are near-fully playable.

**Architecture:** Refactors triggered abilities to use the stack with APNAP ordering, then layers on aura mechanics, cycling, dynamic mana, opponent cost modification, evasion keywords, and mass recursion. All changes are in the `MtgDecker.Engine` project.

**Tech Stack:** .NET 10, C# 14, xUnit, FluentAssertions, NSubstitute

**Working directory:** `C:\Users\larst\MtgDecker\.worktrees\continuous-effects\`

**Build/test commands:**
```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet build src/MtgDecker.Engine/
dotnet test tests/MtgDecker.Engine.Tests/
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~ClassName"
```

**Design doc:** `docs/plans/2026-02-12-engine-full-play-design.md`

---

## Task 1: IStackObject Interface + TriggeredAbilityStackObject

**Files:**
- Create: `src/MtgDecker.Engine/IStackObject.cs`
- Create: `src/MtgDecker.Engine/TriggeredAbilityStackObject.cs`
- Modify: `src/MtgDecker.Engine/StackObject.cs`
- Create: `tests/MtgDecker.Engine.Tests/StackObjectTypeTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/MtgDecker.Engine.Tests/StackObjectTypeTests.cs
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Triggers;
using NSubstitute;

namespace MtgDecker.Engine.Tests;

public class StackObjectTypeTests
{
    [Fact]
    public void StackObject_Implements_IStackObject()
    {
        var card = new GameCard { Name = "Lightning Bolt" };
        var so = new StackObject(card, Guid.NewGuid(), new(), new(), 1);
        IStackObject iface = so;
        iface.ControllerId.Should().Be(so.ControllerId);
    }

    [Fact]
    public void TriggeredAbilityStackObject_Implements_IStackObject()
    {
        var source = new GameCard { Name = "Goblin Matron" };
        var effect = Substitute.For<IEffect>();
        var controllerId = Guid.NewGuid();

        var taso = new TriggeredAbilityStackObject(source, controllerId, effect);

        IStackObject iface = taso;
        iface.ControllerId.Should().Be(controllerId);
        taso.Source.Should().Be(source);
        taso.Effect.Should().Be(effect);
        taso.Target.Should().BeNull();
        taso.TargetPlayerId.Should().BeNull();
    }

    [Fact]
    public void TriggeredAbilityStackObject_With_Target()
    {
        var source = new GameCard { Name = "Sharpshooter" };
        var target = new GameCard { Name = "Elf" };
        var effect = Substitute.For<IEffect>();
        var controllerId = Guid.NewGuid();

        var taso = new TriggeredAbilityStackObject(source, controllerId, effect, target);

        taso.Target.Should().Be(target);
    }

    [Fact]
    public void TriggeredAbilityStackObject_Has_Unique_Id()
    {
        var effect = Substitute.For<IEffect>();
        var t1 = new TriggeredAbilityStackObject(new GameCard(), Guid.NewGuid(), effect);
        var t2 = new TriggeredAbilityStackObject(new GameCard(), Guid.NewGuid(), effect);

        t1.Id.Should().NotBe(t2.Id);
    }
}
```

**Step 2:** Run tests — expect FAIL (types don't exist yet).

**Step 3: Implement**

```csharp
// src/MtgDecker.Engine/IStackObject.cs
namespace MtgDecker.Engine;

public interface IStackObject
{
    Guid Id { get; }
    Guid ControllerId { get; }
}
```

```csharp
// src/MtgDecker.Engine/TriggeredAbilityStackObject.cs
using MtgDecker.Engine.Triggers;

namespace MtgDecker.Engine;

public class TriggeredAbilityStackObject : IStackObject
{
    public Guid Id { get; } = Guid.NewGuid();
    public GameCard Source { get; }
    public Guid ControllerId { get; }
    public IEffect Effect { get; }
    public GameCard? Target { get; init; }
    public Guid? TargetPlayerId { get; init; }

    public TriggeredAbilityStackObject(GameCard source, Guid controllerId, IEffect effect, GameCard? target = null)
    {
        Source = source;
        ControllerId = controllerId;
        Effect = effect;
        Target = target;
    }
}
```

Modify `StackObject.cs` — add `: IStackObject`:
```csharp
public class StackObject : IStackObject
```

**Step 4:** Run tests — expect PASS.

**Step 5:** Commit: `feat(engine): add IStackObject interface and TriggeredAbilityStackObject type`

---

## Task 2: Refactor Stack to IStackObject + Dispatch Resolution

**Files:**
- Modify: `src/MtgDecker.Engine/GameState.cs:18` — change Stack type
- Modify: `src/MtgDecker.Engine/GameEngine.cs:1297,1320-1372` — update Stack access + ResolveTopOfStackAsync
- Modify: `src/MtgDecker.Engine/GameEngine.cs:1275-1318` — RunPriorityAsync Stack reference
- Test: existing tests must still pass after type change

**Step 1:** Change `GameState.Stack` from `List<StackObject>` to `List<IStackObject>`:

```csharp
// GameState.cs line 18
public List<IStackObject> Stack { get; } = new();
```

**Step 2:** Update `RunPriorityAsync` — the `_state.Stack.Count` check is fine since it's on `List<IStackObject>`.

**Step 3:** Update `ResolveTopOfStackAsync` — dispatch by type:

Replace the entire method body (lines 1320-1372) with:

```csharp
private async Task ResolveTopOfStackAsync(CancellationToken ct = default)
{
    if (_state.Stack.Count == 0) return;

    var top = _state.Stack[^1];
    _state.Stack.RemoveAt(_state.Stack.Count - 1);
    var controller = top.ControllerId == _state.Player1.Id ? _state.Player1 : _state.Player2;

    if (top is TriggeredAbilityStackObject triggered)
    {
        _state.Log($"Resolving triggered ability: {triggered.Source.Name} — {triggered.Effect.GetType().Name.Replace("Effect", "")}");
        var context = new EffectContext(_state, controller, triggered.Source, controller.DecisionHandler)
        {
            Target = triggered.Target,
            TargetPlayerId = triggered.TargetPlayerId,
        };
        await triggered.Effect.Execute(context, ct);
        await OnBoardChangedAsync(ct);
        return;
    }

    if (top is StackObject spell)
    {
        _state.Log($"Resolving {spell.Card.Name}.");

        if (CardDefinitions.TryGet(spell.Card.Name, out var def) && def.Effect != null)
        {
            if (spell.Targets.Count > 0)
            {
                var allTargetsLegal = true;
                foreach (var target in spell.Targets)
                {
                    var targetOwner = target.PlayerId == _state.Player1.Id ? _state.Player1 : _state.Player2;
                    var targetZone = targetOwner.GetZone(target.Zone);
                    if (!targetZone.Contains(target.CardId))
                    {
                        allTargetsLegal = false;
                        break;
                    }
                }

                if (!allTargetsLegal)
                {
                    _state.Log($"{spell.Card.Name} fizzles (illegal target).");
                    controller.Graveyard.Add(spell.Card);
                    return;
                }
            }

            def.Effect.Resolve(_state, spell);
            controller.Graveyard.Add(spell.Card);
            await OnBoardChangedAsync(ct);
        }
        else
        {
            if (spell.Card.IsCreature || spell.Card.CardTypes.HasFlag(CardType.Enchantment)
                || spell.Card.CardTypes.HasFlag(CardType.Artifact))
            {
                spell.Card.TurnEnteredBattlefield = _state.TurnNumber;
                controller.Battlefield.Add(spell.Card);
                await OnBoardChangedAsync(ct);
            }
            else
            {
                controller.Graveyard.Add(spell.Card);
            }
        }
    }
}
```

**Step 4:** Update `ActionType.CastSpell` in `ExecuteAction` — the `_state.Stack.Add(stackObj)` call is fine since `StackObject : IStackObject`.

**Step 5:** Build and run ALL tests: `dotnet test tests/MtgDecker.Engine.Tests/`

All 681 existing tests should still pass — this is a pure type widening with no behavior change.

**Step 6:** Commit: `refactor(engine): widen Stack to IStackObject, dispatch resolution by type`

---

## Task 3: Refactor Trigger Methods to Queue on Stack

This is the core behavior change. All four trigger methods change from executing effects inline to adding `TriggeredAbilityStackObject` to the stack. The priority loop in `RunPriorityAsync` then resolves them.

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs:1171-1273` — all 4 trigger methods
- Modify: `src/MtgDecker.Engine/GameEngine.cs:28-84` — RunTurnAsync (move delayed triggers into end step phase)
- Modify: `src/MtgDecker.Engine/GameEngine.cs:720-849` — RunCombatAsync (add priority rounds after triggers)
- Create: `tests/MtgDecker.Engine.Tests/StackTriggerTests.cs`

**Step 1: Write tests for the new behavior**

```csharp
// tests/MtgDecker.Engine.Tests/StackTriggerTests.cs
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;
using NSubstitute;

namespace MtgDecker.Engine.Tests;

public class StackTriggerTests
{
    private (GameState state, Player player1, Player player2, TestDecisionHandler handler1, TestDecisionHandler handler2, GameEngine engine) CreateSetup()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Player 1", handler1);
        var p2 = new Player(Guid.NewGuid(), "Player 2", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (state, p1, p2, handler1, handler2, engine);
    }

    [Fact]
    public async Task ETB_Self_Trigger_Goes_On_Stack()
    {
        var (state, p1, _, _, _, engine) = CreateSetup();

        // Goblin Matron has ETB trigger (Self, SearchLibraryEffect)
        var matron = GameCard.Create("Goblin Matron");
        matron.TurnEnteredBattlefield = state.TurnNumber;
        p1.Battlefield.Add(matron);

        // Add a Goblin to library for the search
        p1.Library.Add(GameCard.Create("Mogg Fanatic"));

        // After calling ProcessTriggersAsync, the trigger should be on the stack, not resolved
        await engine.QueueSelfTriggersOnStackAsync(GameEvent.EnterBattlefield, matron, p1);

        state.Stack.Should().ContainSingle();
        state.Stack[0].Should().BeOfType<TriggeredAbilityStackObject>();
        var taso = (TriggeredAbilityStackObject)state.Stack[0];
        taso.Source.Name.Should().Be("Goblin Matron");
        taso.Effect.Should().BeOfType<SearchLibraryEffect>();
    }

    [Fact]
    public async Task Board_Triggers_Collect_From_Both_Players_APNAP()
    {
        var (state, p1, p2, _, _, engine) = CreateSetup();

        // Both players have enchantress effects
        var enchantress1 = GameCard.Create("Enchantress's Presence");
        p1.Battlefield.Add(enchantress1);

        var enchantress2 = GameCard.Create("Argothian Enchantress");
        p2.Battlefield.Add(enchantress2);

        // Active player casts an enchantment
        var enchantment = new GameCard { Name = "Test Enchantment", CardTypes = CardType.Enchantment };

        await engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, enchantment);

        // Only active player's enchantress should trigger (ControllerCastsEnchantment checks ActivePlayer)
        // p1 is active player, so p1's Enchantress's Presence should trigger
        state.Stack.Should().ContainSingle();
        var taso = (TriggeredAbilityStackObject)state.Stack[0];
        taso.ControllerId.Should().Be(p1.Id);
    }

    [Fact]
    public async Task Delayed_Triggers_Go_On_Stack()
    {
        var (state, p1, _, _, _, engine) = CreateSetup();

        var effect = Substitute.For<IEffect>();
        state.DelayedTriggers.Add(new DelayedTrigger(GameEvent.EndStep, effect, p1.Id));

        await engine.QueueDelayedTriggersOnStackAsync(GameEvent.EndStep);

        state.Stack.Should().ContainSingle();
        state.Stack[0].Should().BeOfType<TriggeredAbilityStackObject>();
        state.DelayedTriggers.Should().BeEmpty();
    }
}
```

**Step 2:** Run tests — expect FAIL (methods don't exist yet).

**Step 3: Implement the new trigger queueing methods**

Add to `GameEngine.cs` — three new public methods that REPLACE the old private ones:

```csharp
/// <summary>Queues Self triggers for a specific card onto the stack.</summary>
internal async Task QueueSelfTriggersOnStackAsync(GameEvent evt, GameCard source, Player controller, CancellationToken ct = default)
{
    if (source.Triggers.Count == 0) return;

    foreach (var trigger in source.Triggers)
    {
        if (trigger.Event != evt) continue;
        if (trigger.Condition != TriggerCondition.Self) continue;

        _state.Log($"{source.Name} triggers: {trigger.Effect.GetType().Name.Replace("Effect", "")}");
        _state.Stack.Add(new TriggeredAbilityStackObject(source, controller.Id, trigger.Effect));
    }
}

/// <summary>Queues board-wide triggers onto the stack with APNAP ordering.</summary>
internal async Task QueueBoardTriggersOnStackAsync(GameEvent evt, GameCard? relevantCard, CancellationToken ct = default)
{
    // Collect triggers per player — APNAP: active player first (bottom), non-active on top
    var activePlayer = _state.ActivePlayer;
    var nonActivePlayer = _state.GetOpponent(activePlayer);

    var activeTriggers = CollectBoardTriggers(evt, relevantCard, activePlayer);
    var nonActiveTriggers = CollectBoardTriggers(evt, relevantCard, nonActivePlayer);

    // Active player's triggers go on stack first (will resolve last — correct per APNAP)
    foreach (var t in activeTriggers)
        _state.Stack.Add(t);

    // Non-active player's triggers go on top (will resolve first)
    foreach (var t in nonActiveTriggers)
        _state.Stack.Add(t);
}

private List<TriggeredAbilityStackObject> CollectBoardTriggers(GameEvent evt, GameCard? relevantCard, Player player)
{
    var result = new List<TriggeredAbilityStackObject>();
    var permanents = player.Battlefield.Cards.ToList();

    foreach (var permanent in permanents)
    {
        var triggers = permanent.Triggers.Count > 0
            ? permanent.Triggers
            : (CardDefinitions.TryGet(permanent.Name, out var def) ? def.Triggers : []);
        if (triggers.Count == 0) continue;

        foreach (var trigger in triggers)
        {
            if (trigger.Event != evt) continue;
            if (trigger.Condition == TriggerCondition.Self) continue;

            bool matches = trigger.Condition switch
            {
                TriggerCondition.AnyCreatureDies =>
                    evt == GameEvent.Dies && relevantCard != null && relevantCard.IsCreature,
                TriggerCondition.ControllerCastsEnchantment =>
                    evt == GameEvent.SpellCast
                    && relevantCard != null
                    && relevantCard.CardTypes.HasFlag(CardType.Enchantment)
                    && _state.ActivePlayer == player,
                TriggerCondition.SelfDealsCombatDamage =>
                    evt == GameEvent.CombatDamageDealt
                    && relevantCard != null
                    && relevantCard.Id == permanent.Id,
                TriggerCondition.Upkeep =>
                    evt == GameEvent.Upkeep
                    && _state.ActivePlayer == player,
                TriggerCondition.SelfAttacks => false,
                _ => false,
            };

            if (matches)
            {
                _state.Log($"{permanent.Name} triggers: {trigger.Effect.GetType().Name.Replace("Effect", "")}");
                result.Add(new TriggeredAbilityStackObject(permanent, player.Id, trigger.Effect));
            }
        }
    }

    return result;
}

/// <summary>Queues attack triggers onto the stack.</summary>
internal async Task QueueAttackTriggersOnStackAsync(GameCard attacker, CancellationToken ct = default)
{
    var player = _state.ActivePlayer;
    var triggers = attacker.Triggers.Count > 0
        ? attacker.Triggers
        : (CardDefinitions.TryGet(attacker.Name, out var def) ? def.Triggers : []);

    foreach (var trigger in triggers)
    {
        if (trigger.Condition != TriggerCondition.SelfAttacks) continue;

        _state.Log($"{attacker.Name} triggers: {trigger.Effect.GetType().Name.Replace("Effect", "")}");
        _state.Stack.Add(new TriggeredAbilityStackObject(attacker, player.Id, trigger.Effect));
    }
}

/// <summary>Queues delayed triggers onto the stack and removes them from the list.</summary>
internal async Task QueueDelayedTriggersOnStackAsync(GameEvent evt, CancellationToken ct = default)
{
    var toFire = _state.DelayedTriggers.Where(d => d.FireOn == evt).ToList();
    foreach (var delayed in toFire)
    {
        var controller = delayed.ControllerId == _state.Player1.Id ? _state.Player1 : _state.Player2;
        var source = new GameCard { Name = "Delayed Trigger" };
        _state.Stack.Add(new TriggeredAbilityStackObject(source, controller.Id, delayed.Effect));
        _state.DelayedTriggers.Remove(delayed);
    }
}
```

**Step 4:** Now replace ALL call sites of the old trigger methods throughout `GameEngine.cs`:

In `ExecuteAction`:
- Line 144: `await ProcessTriggersAsync(...)` → `await QueueSelfTriggersOnStackAsync(...)`
- Line 243: same replacement
- Line 247: `await ProcessBoardTriggersAsync(...)` → `await QueueBoardTriggersOnStackAsync(...)`
- Line 259: same as line 144
- Line 354: same as line 144

In `RunTurnAsync`:
- Line 52: `await ProcessBoardTriggersAsync(GameEvent.Upkeep, null, ct)` → `await QueueBoardTriggersOnStackAsync(GameEvent.Upkeep, null, ct)`
- Lines 72: Move delayed trigger processing INTO the phase loop. Add before the `if (phase.Phase == Phase.Combat)`:
```csharp
if (phase.Phase == Phase.EndStep)
{
    await QueueDelayedTriggersOnStackAsync(GameEvent.EndStep, ct);
}
```
- Remove the old line 72 (`await ProcessDelayedTriggersAsync(GameEvent.EndStep, ct)`)

In `RunCombatAsync`:
- Line 774: `await ProcessAttackTriggersAsync(card, ct)` → `await QueueAttackTriggersOnStackAsync(card, ct)`
- After all attack triggers are queued (after the foreach loop on line 770-775), add a priority round:
```csharp
// Priority round after attack triggers
if (_state.Stack.Count > 0)
    await RunPriorityAsync(ct);
```
- Line 827: `await ProcessBoardTriggersAsync(...)` → `await QueueBoardTriggersOnStackAsync(...)`
- After the unblocked attacker trigger loop (line 824-828), add:
```csharp
if (_state.Stack.Count > 0)
    await RunPriorityAsync(ct);
```
- Line 837: `await ProcessBoardTriggersAsync(GameEvent.Dies, deadCard, ct)` → `await QueueBoardTriggersOnStackAsync(GameEvent.Dies, deadCard, ct)`
- After the dies trigger loop (line 834-838), add:
```csharp
if (_state.Stack.Count > 0)
    await RunPriorityAsync(ct);
```

In `CheckStateBasedActionsAsync` (where `CheckLethalDamage` fires Dies board triggers):
- Find where Dies triggers are fired inside the SBA loop. If `CheckLethalDamage` calls `ProcessBoardTriggersAsync`, replace it too. Looking at the current code, `CheckLethalDamage` just adds to a `deaths` list. The SBA loop in `CheckStateBasedActionsAsync` (lines 1041-1105) may fire triggers via board triggers — check and replace any calls.

Delete the old methods: `ProcessTriggersAsync`, `ProcessBoardTriggersAsync`, `ProcessAttackTriggersAsync`, `ProcessDelayedTriggersAsync`.

**Step 5:** Build: `dotnet build src/MtgDecker.Engine/`

Fix any compilation errors from the replacement.

**Step 6:** Run ALL tests: `dotnet test tests/MtgDecker.Engine.Tests/`

Many tests will now fail because triggers that were previously resolved inline are now on the stack waiting for resolution. This is expected. The test fixes are in **Task 4**.

**Step 7:** Commit: `refactor(engine): queue all triggers on stack with APNAP ordering`

---

## Task 4: Fix Tests for Stack-Based Trigger Resolution

The trigger refactor in Task 3 will break tests that expect triggers to resolve inline. The fix pattern is consistent: after any action that queues triggers, add a priority round (or manually resolve the stack in unit tests).

**Files:**
- Modify: Many test files in `tests/MtgDecker.Engine.Tests/`

**Step 1: Identify failing tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/` and collect all failures.

**Step 2: Fix pattern**

For integration tests that use `GameEngine` and expect trigger effects to have happened:

- Tests that call `engine.ExecuteAction(playCard)` directly: After the action, the trigger is on the stack. Call `await engine.ResolveStackAsync(ct)` or run a mini priority loop. Since `ResolveTopOfStackAsync` is private, either:
  - Make it `internal` for testing, OR
  - Add a helper: `internal async Task ResolveAllTriggersAsync(CancellationToken ct = default)` that resolves the stack until empty:

```csharp
// Add to GameEngine.cs
internal async Task ResolveAllTriggersAsync(CancellationToken ct = default)
{
    while (_state.Stack.Count > 0)
        await ResolveTopOfStackAsync(ct);
}
```

Then in tests, after `engine.ExecuteAction(action)`, add `await engine.ResolveAllTriggersAsync()`.

- Tests using `TestDecisionHandler` that go through `RunTurnAsync` or `RunPriorityAsync`: These should work correctly because the priority loop resolves the stack. However, the `TestDecisionHandler.GetAction` must now pass priority enough times for triggers to resolve. Ensure the default behavior (pass) handles this.

- **Unit tests for effects** (e.g., `TriggeredAbilityEffectTests`): These test `IEffect.Execute()` directly and are NOT affected.

- **CardRegistration tests**: These only verify `CardDefinitions.TryGet()` and inspect trigger/effect types. NOT affected.

- **BoardWideTriggerTests**: If these call `ProcessBoardTriggersAsync` directly, they now call the renamed method and need stack resolution. Update to call `QueueBoardTriggersOnStackAsync` + `ResolveAllTriggersAsync`.

- **DelayedTriggerTests**: Same pattern — call `QueueDelayedTriggersOnStackAsync` + `ResolveAllTriggersAsync`.

- **ActivatedTriggeredIntegrationTests**: These go through `ExecuteAction`. Add `ResolveAllTriggersAsync` after each action that triggers.

**Step 3:** Fix each failing test file by adding `await engine.ResolveAllTriggersAsync()` after trigger-causing actions.

**Step 4:** Run ALL tests: `dotnet test tests/MtgDecker.Engine.Tests/`

All 681+ tests should pass.

**Step 5:** Commit: `fix(engine): update tests for stack-based trigger resolution`

---

## Task 5: Dynamic Mana Abilities + Serra's Sanctum

**Files:**
- Modify: `src/MtgDecker.Engine/Mana/ManaAbility.cs` — add Dynamic variant
- Modify: `src/MtgDecker.Engine/GameEngine.cs:264-294` — handle Dynamic in TapCard
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs:160` — update Serra's Sanctum
- Create: `tests/MtgDecker.Engine.Tests/DynamicManaTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/MtgDecker.Engine.Tests/DynamicManaTests.cs
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests;

public class DynamicManaTests
{
    [Fact]
    public void ManaAbility_Dynamic_Creates_Correct_Type()
    {
        var ability = ManaAbility.Dynamic(ManaColor.White, p => p.Battlefield.Cards.Count);
        ability.Type.Should().Be(ManaAbilityType.Dynamic);
        ability.DynamicColor.Should().Be(ManaColor.White);
        ability.CountFunc.Should().NotBeNull();
    }

    [Fact]
    public async Task SerrasSanctum_Taps_For_White_Per_Enchantment()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // Add Serra's Sanctum to battlefield
        var sanctum = GameCard.Create("Serra's Sanctum");
        p1.Battlefield.Add(sanctum);

        // Add 3 enchantments
        p1.Battlefield.Add(new GameCard { Name = "Enchantment1", CardTypes = CardType.Enchantment });
        p1.Battlefield.Add(new GameCard { Name = "Enchantment2", CardTypes = CardType.Enchantment });
        p1.Battlefield.Add(new GameCard { Name = "Enchantment3", CardTypes = CardType.Enchantment });

        var action = GameAction.TapCard(p1.Id, sanctum.Id);
        await engine.ExecuteAction(action);

        p1.ManaPool.Available[ManaColor.White].Should().Be(3);
    }

    [Fact]
    public async Task SerrasSanctum_Zero_Enchantments_Produces_No_Mana()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var sanctum = GameCard.Create("Serra's Sanctum");
        p1.Battlefield.Add(sanctum);

        var action = GameAction.TapCard(p1.Id, sanctum.Id);
        await engine.ExecuteAction(action);

        p1.ManaPool.Available.GetValueOrDefault(ManaColor.White).Should().Be(0);
    }
}
```

**Step 2:** Run tests — expect FAIL.

**Step 3: Implement**

Add to `ManaAbility.cs`:
```csharp
public ManaColor? DynamicColor { get; }
public Func<Player, int>? CountFunc { get; }

// Update constructor:
private ManaAbility(ManaAbilityType type, ManaColor? fixedColor, IReadOnlyList<ManaColor>? choiceColors,
    ManaColor? dynamicColor = null, Func<Player, int>? countFunc = null)
{
    Type = type;
    FixedColor = fixedColor;
    ChoiceColors = choiceColors;
    DynamicColor = dynamicColor;
    CountFunc = countFunc;
}

public static ManaAbility Dynamic(ManaColor color, Func<Player, int> countFunc) =>
    new(ManaAbilityType.Dynamic, null, null, color, countFunc);
```

Add `Dynamic` to `ManaAbilityType`:
```csharp
public enum ManaAbilityType { Fixed, Choice, Dynamic }
```

Handle Dynamic in `GameEngine.ExecuteAction` `ActionType.TapCard` (after the Choice branch):
```csharp
else if (ability.Type == ManaAbilityType.Dynamic)
{
    var amount = ability.CountFunc!(player);
    if (amount > 0)
    {
        player.ManaPool.Add(ability.DynamicColor!.Value, amount);
        _state.Log($"{player.Name} taps {tapTarget.Name} for {amount} {ability.DynamicColor}.");
    }
    else
    {
        _state.Log($"{player.Name} taps {tapTarget.Name} (produces no mana).");
    }
}
```

Update `CardDefinitions.cs` — change Serra's Sanctum registration (line ~160):
```csharp
["Serra's Sanctum"] = new(null, ManaAbility.Dynamic(ManaColor.White,
    p => p.Battlefield.Cards.Count(c => c.CardTypes.HasFlag(CardType.Enchantment))),
    null, null, CardType.Land) { IsLegendary = true },
```

Update existing `ManaAbility.Fixed` and `ManaAbility.Choice` factory calls to pass the new optional params (or update the old constructor to keep backward compat).

**Step 4:** Run tests — expect PASS.

**Step 5:** Commit: `feat(engine): add dynamic mana abilities, implement Serra's Sanctum`

---

## Task 6: Opponent Cost Modification + Aura of Silence

**Files:**
- Modify: `src/MtgDecker.Engine/ContinuousEffect.cs` — add CostAppliesToOpponent
- Modify: `src/MtgDecker.Engine/GameEngine.cs:151-158,393-400` — update cost reduction logic
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs:145` — register Aura of Silence abilities
- Create: `tests/MtgDecker.Engine.Tests/OpponentCostModTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/MtgDecker.Engine.Tests/OpponentCostModTests.cs
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests;

public class OpponentCostModTests
{
    [Fact]
    public void ContinuousEffect_CostAppliesToOpponent_Defaults_False()
    {
        var effect = new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyCost,
            (_, _) => true, CostMod: 2);
        effect.CostAppliesToOpponent.Should().BeFalse();
    }

    [Fact]
    public async Task AuraOfSilence_Taxes_Opponent_Enchantments()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P1 has Aura of Silence on battlefield
        var aura = GameCard.Create("Aura of Silence");
        p1.Battlefield.Add(aura);
        engine.RecalculateState();

        // P2 tries to cast a {1}{G} enchantment — should cost {3}{G} instead
        var enchantment = new GameCard
        {
            Name = "Test Enchantment",
            CardTypes = CardType.Enchantment,
            ManaCost = ManaCost.Parse("{1}{G}")
        };
        p2.Hand.Add(enchantment);

        // Give P2 exactly {1}{G} — should NOT be enough (needs {3}{G})
        p2.ManaPool.Add(ManaColor.Green, 1);
        p2.ManaPool.Add(ManaColor.Colorless, 1);

        state.ActivePlayer = p2;
        var action = GameAction.PlayCard(p2.Id, enchantment.Id);
        await engine.ExecuteAction(action);

        // Card should still be in hand (not enough mana)
        p2.Hand.Cards.Should().Contain(c => c.Name == "Test Enchantment");
    }

    [Fact]
    public async Task AuraOfSilence_Does_Not_Tax_Own_Enchantments()
    {
        var handler1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P1 has Aura of Silence
        var aura = GameCard.Create("Aura of Silence");
        p1.Battlefield.Add(aura);
        engine.RecalculateState();

        // P1 casts own enchantment — should NOT be taxed
        var enchantment = new GameCard
        {
            Name = "Test Enchantment",
            CardTypes = CardType.Enchantment,
            ManaCost = ManaCost.Parse("{1}{G}")
        };
        p1.Hand.Add(enchantment);
        p1.ManaPool.Add(ManaColor.Green, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 1);

        var action = GameAction.PlayCard(p1.Id, enchantment.Id);
        await engine.ExecuteAction(action);

        // Card should be on battlefield (enough mana without tax)
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Test Enchantment");
    }
}
```

**Step 2:** Run tests — expect FAIL.

**Step 3: Implement**

Add `CostAppliesToOpponent` to `ContinuousEffect` record:
```csharp
public record ContinuousEffect(
    Guid SourceId,
    ContinuousEffectType Type,
    Func<GameCard, Player, bool> Applies,
    int PowerMod = 0,
    int ToughnessMod = 0,
    bool UntilEndOfTurn = false,
    Keyword? GrantedKeyword = null,
    int CostMod = 0,
    Func<GameCard, bool>? CostApplies = null,
    int ExtraLandDrops = 0,
    bool CostAppliesToOpponent = false,
    bool ExcludeSelf = false);
```

Update cost reduction in `GameEngine.ExecuteAction` at both `ActionType.PlayCard` (line ~152) and `ActionType.CastSpell` (line ~393). Replace the cost reduction calculation:

```csharp
var costReduction = 0;
foreach (var e in _state.ActiveEffects.Where(e => e.Type == ContinuousEffectType.ModifyCost))
{
    if (e.CostApplies != null && !e.CostApplies(playCard)) continue;

    if (e.CostAppliesToOpponent)
    {
        // Tax effect: only applies to opponent of the effect's controller
        var effectController = _state.Player1.Battlefield.Contains(e.SourceId) ? _state.Player1
            : _state.Player2.Battlefield.Contains(e.SourceId) ? _state.Player2 : null;
        if (effectController == null || effectController.Id == player.Id) continue;
    }

    costReduction += e.CostMod;
}
if (costReduction != 0)
    effectiveCost = effectiveCost.WithGenericReduction(-costReduction);
```

Register Aura of Silence in `CardDefinitions.cs` (replace line ~145):
```csharp
["Aura of Silence"] = new(ManaCost.Parse("{1}{W}{W}"), null, null, null, CardType.Enchantment)
{
    ContinuousEffects =
    [
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyCost,
            (_, _) => true, CostMod: 2,
            CostApplies: c => c.CardTypes.HasFlag(CardType.Artifact) || c.CardTypes.HasFlag(CardType.Enchantment),
            CostAppliesToOpponent: true),
    ],
    ActivatedAbility = new(new ActivatedAbilityCost(SacrificeSelf: true),
        new DestroyTargetEffect(),
        c => c.CardTypes.HasFlag(CardType.Artifact) || c.CardTypes.HasFlag(CardType.Enchantment)),
},
```

**Step 4:** Run tests — expect PASS.

**Step 5:** Commit: `feat(engine): add opponent cost modification, implement Aura of Silence`

---

## Task 7: Mountainwalk Combat Logic + Pyromancer Update

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs:784-803` — blocker eligibility check
- Modify: `src/MtgDecker.Engine/Triggers/Effects/PyromancerEffect.cs` — add mountainwalk grant
- Create: `tests/MtgDecker.Engine.Tests/MountainwalkTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/MtgDecker.Engine.Tests/MountainwalkTests.cs
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class MountainwalkTests
{
    [Fact]
    public async Task Creature_With_Mountainwalk_Cannot_Be_Blocked_When_Defender_Has_Mountain()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Attacker", handler1);
        var p2 = new Player(Guid.NewGuid(), "Defender", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // Attacker has a creature with Mountainwalk
        var goblin = new GameCard { Name = "Walker", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        goblin.ActiveKeywords.Add(Keyword.Mountainwalk);
        goblin.TurnEnteredBattlefield = 0; // No summoning sickness
        p1.Battlefield.Add(goblin);

        // Defender has a Mountain
        var mountain = GameCard.Create("Mountain");
        p2.Battlefield.Add(mountain);

        // Defender has a blocker
        var blocker = new GameCard { Name = "Wall", CardTypes = CardType.Creature, BasePower = 0, BaseToughness = 4 };
        p2.Battlefield.Add(blocker);

        state.TurnNumber = 2;

        // Set up combat: attacker declares goblin, defender tries to block
        handler1.EnqueueAttackers([goblin.Id]);
        handler2.EnqueueBlockers(new Dictionary<Guid, Guid> { { blocker.Id, goblin.Id } });

        await engine.RunCombatAsync(default);

        // The blocker assignment should be rejected (mountainwalk is unblockable)
        // Damage should go through to defending player
        p2.Life.Should().BeLessThan(20);
    }

    [Fact]
    public async Task Creature_With_Mountainwalk_Can_Be_Blocked_When_Defender_Has_No_Mountain()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Attacker", handler1);
        var p2 = new Player(Guid.NewGuid(), "Defender", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var goblin = new GameCard { Name = "Walker", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        goblin.ActiveKeywords.Add(Keyword.Mountainwalk);
        goblin.TurnEnteredBattlefield = 0;
        p1.Battlefield.Add(goblin);

        // Defender has NO Mountain — just a Plains
        p2.Battlefield.Add(GameCard.Create("Plains"));

        var blocker = new GameCard { Name = "Wall", CardTypes = CardType.Creature, BasePower = 0, BaseToughness = 4 };
        p2.Battlefield.Add(blocker);

        state.TurnNumber = 2;
        handler1.EnqueueAttackers([goblin.Id]);
        handler2.EnqueueBlockers(new Dictionary<Guid, Guid> { { blocker.Id, goblin.Id } });

        await engine.RunCombatAsync(default);

        // Blocker should succeed — no mountainwalk evasion
        p2.Life.Should().Be(20);
    }
}
```

**Step 2:** Run tests — expect FAIL.

**Step 3: Implement**

In `GameEngine.RunCombatAsync`, in the blocker validation loop (around line 793-802), add mountainwalk check:

```csharp
foreach (var (blockerId, attackerCardId) in blockerAssignments)
{
    if (!eligibleBlockers.Any(c => c.Id == blockerId) || !validAttackerIds.Contains(attackerCardId))
        continue;

    var attackerCard = attacker.Battlefield.Cards.First(c => c.Id == attackerCardId);

    // Mountainwalk: can't be blocked if defender controls a Mountain
    if (attackerCard.ActiveKeywords.Contains(Keyword.Mountainwalk)
        && defender.Battlefield.Cards.Any(c => c.Subtypes.Contains("Mountain")))
    {
        _state.Log($"{attackerCard.Name} has mountainwalk — cannot be blocked.");
        continue;
    }

    _state.Combat.DeclareBlocker(blockerId, attackerCardId);
    var blockerCard = defender.Battlefield.Cards.First(c => c.Id == blockerId);
    _state.Log($"{defender.Name} blocks {attackerCard.Name} with {blockerCard.Name}.");
}
```

Update `PyromancerEffect.cs` — add mountainwalk grant alongside the existing pump:

```csharp
var mountainwalk = new ContinuousEffect(
    context.Source.Id,
    ContinuousEffectType.GrantKeyword,
    (card, _) => card.IsCreature && card.Subtypes.Contains("Goblin"),
    GrantedKeyword: Keyword.Mountainwalk,
    UntilEndOfTurn: true);
context.State.ActiveEffects.Add(mountainwalk);
```

**Step 4:** Run tests — expect PASS.

**Step 5:** Commit: `feat(engine): add mountainwalk evasion, update Goblin Pyromancer`

---

## Task 8: Shroud Targeting + Sterling Grove

**Files:**
- Modify: `src/MtgDecker.Engine/ContinuousEffect.cs` — ExcludeSelf already added in Task 6
- Modify: `src/MtgDecker.Engine/GameEngine.cs:1012-1020` — apply ExcludeSelf in ApplyKeywordEffect
- Modify: `src/MtgDecker.Engine/GameEngine.cs` — shroud check in targeting (ActivateAbility, CastSpell)
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs:141-144` — update Sterling Grove
- Create: `tests/MtgDecker.Engine.Tests/ShroudTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/MtgDecker.Engine.Tests/ShroudTests.cs
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class ShroudTests
{
    [Fact]
    public void SterlingGrove_Grants_Shroud_To_Other_Enchantments()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var grove = GameCard.Create("Sterling Grove");
        p1.Battlefield.Add(grove);

        var enchantment = new GameCard { Name = "Some Enchantment", CardTypes = CardType.Enchantment };
        p1.Battlefield.Add(enchantment);

        engine.RecalculateState();

        // Other enchantment should have shroud
        enchantment.ActiveKeywords.Should().Contain(Keyword.Shroud);
        // Sterling Grove itself should NOT have shroud
        grove.ActiveKeywords.Should().NotContain(Keyword.Shroud);
    }

    [Fact]
    public async Task Shroud_Prevents_Targeting_By_Spells()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P2 has Sterling Grove + another enchantment
        var grove = GameCard.Create("Sterling Grove");
        p2.Battlefield.Add(grove);
        var enchantment = new GameCard { Name = "Protected", CardTypes = CardType.Enchantment };
        p2.Battlefield.Add(enchantment);
        engine.RecalculateState();

        // P1 tries to target the enchantment with Naturalize (destroy artifact/enchantment)
        // The enchantment has shroud — should not be targetable
        // Sterling Grove itself should be targetable (no self-shroud)
        enchantment.ActiveKeywords.Should().Contain(Keyword.Shroud);
        grove.ActiveKeywords.Should().NotContain(Keyword.Shroud);
    }
}
```

**Step 2:** Run tests — expect FAIL.

**Step 3: Implement**

In `ApplyKeywordEffect` (line ~1012), add ExcludeSelf check:
```csharp
private void ApplyKeywordEffect(ContinuousEffect effect, Player player)
{
    foreach (var card in player.Battlefield.Cards)
    {
        if (effect.ExcludeSelf && card.Id == effect.SourceId) continue;
        if (!effect.Applies(card, player)) continue;
        if (effect.GrantedKeyword.HasValue)
            card.ActiveKeywords.Add(effect.GrantedKeyword.Value);
    }
}
```

Update Sterling Grove in `CardDefinitions.cs`:
```csharp
["Sterling Grove"] = new(ManaCost.Parse("{G}{W}"), null, null, null, CardType.Enchantment)
{
    ContinuousEffects =
    [
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
            (card, _) => card.CardTypes.HasFlag(CardType.Enchantment),
            GrantedKeyword: Keyword.Shroud,
            ExcludeSelf: true),
    ],
    ActivatedAbility = new(new ActivatedAbilityCost(SacrificeSelf: true, ManaCost: ManaCost.Parse("{1}")),
        new SearchLibraryByTypeEffect(CardType.Enchantment)),
},
```

Add shroud check in `GameEngine` targeting code — in `ActivateAbility` handler when finding effect target, and in `CastSpell` when `ChooseTarget` filters eligible targets. Add a helper:

```csharp
private bool HasShroud(GameCard card) => card.ActiveKeywords.Contains(Keyword.Shroud);
```

In `ActionType.ActivateAbility` effect target resolution (around line 618-622), filter out shrouded permanents:
```csharp
if (effectTarget != null && HasShroud(effectTarget))
{
    _state.Log($"{effectTarget.Name} has shroud — cannot be targeted.");
    break;
}
```

**Step 4:** Run tests — expect PASS.

**Step 5:** Commit: `feat(engine): add shroud keyword and Sterling Grove shroud grant`

---

## Task 9: Aura Types + GameCard.AttachedTo

**Files:**
- Create: `src/MtgDecker.Engine/Enums/AuraTarget.cs`
- Modify: `src/MtgDecker.Engine/CardDefinition.cs` — add AuraTarget
- Modify: `src/MtgDecker.Engine/GameCard.cs` — add AttachedTo
- Modify: `src/MtgDecker.Engine/Enums/GameEvent.cs` — add TapForMana
- Modify: `src/MtgDecker.Engine/Triggers/TriggerCondition.cs` — add AttachedPermanentTapped
- Create: `tests/MtgDecker.Engine.Tests/AuraTypeTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/MtgDecker.Engine.Tests/AuraTypeTests.cs
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class AuraTypeTests
{
    [Fact]
    public void GameCard_AttachedTo_Defaults_Null()
    {
        var card = new GameCard();
        card.AttachedTo.Should().BeNull();
    }

    [Fact]
    public void GameCard_AttachedTo_Can_Be_Set()
    {
        var targetId = Guid.NewGuid();
        var card = new GameCard { AttachedTo = targetId };
        card.AttachedTo.Should().Be(targetId);
    }

    [Fact]
    public void WildGrowth_Has_AuraTarget_Land()
    {
        CardDefinitions.TryGet("Wild Growth", out var def).Should().BeTrue();
        def!.AuraTarget.Should().Be(AuraTarget.Land);
    }

    [Fact]
    public void GameEvent_TapForMana_Exists()
    {
        Enum.IsDefined(GameEvent.TapForMana).Should().BeTrue();
    }

    [Fact]
    public void TriggerCondition_AttachedPermanentTapped_Exists()
    {
        Enum.IsDefined(TriggerCondition.AttachedPermanentTapped).Should().BeTrue();
    }
}
```

**Step 2:** Run tests — expect FAIL.

**Step 3: Implement**

```csharp
// src/MtgDecker.Engine/Enums/AuraTarget.cs
namespace MtgDecker.Engine.Enums;

public enum AuraTarget
{
    Land,
    Creature,
    Permanent,
}
```

Add to `CardDefinition.cs`:
```csharp
public AuraTarget? AuraTarget { get; init; }
```

Add to `GameCard.cs`:
```csharp
public Guid? AttachedTo { get; set; }
```

Add `TapForMana` to `GameEvent.cs`:
```csharp
TapForMana,
```

Add `AttachedPermanentTapped` to `TriggerCondition.cs`:
```csharp
AttachedPermanentTapped,
```

Update Wild Growth in `CardDefinitions.cs`:
```csharp
["Wild Growth"] = new(ManaCost.Parse("{G}"), null, null, null, CardType.Enchantment)
{
    Subtypes = ["Aura"],
    AuraTarget = AuraTarget.Land,
},
```

**Step 4:** Run tests — expect PASS.

**Step 5:** Commit: `feat(engine): add aura types, AttachedTo, TapForMana event`

---

## Task 10: Aura Casting + SBA + Wild Growth Mana Trigger

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs` — aura casting flow, aura SBA, TapForMana event
- Create: `src/MtgDecker.Engine/Triggers/Effects/AddBonusManaEffect.cs`
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs` — Wild Growth triggers
- Create: `tests/MtgDecker.Engine.Tests/AuraCastingTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/MtgDecker.Engine.Tests/AuraCastingTests.cs
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class AuraCastingTests
{
    [Fact]
    public async Task Casting_Aura_Prompts_For_Target_And_Attaches()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var forest = GameCard.Create("Forest");
        p1.Battlefield.Add(forest);

        var wildGrowth = GameCard.Create("Wild Growth");
        wildGrowth.ManaCost = ManaCost.Parse("{G}");
        p1.Hand.Add(wildGrowth);
        p1.ManaPool.Add(ManaColor.Green, 1);

        // Queue the choice: attach to forest
        handler.EnqueueCardChoice(forest.Id);

        var action = GameAction.PlayCard(p1.Id, wildGrowth.Id);
        await engine.ExecuteAction(action);

        wildGrowth.AttachedTo.Should().Be(forest.Id);
        p1.Battlefield.Cards.Should().Contain(wildGrowth);
    }

    [Fact]
    public async Task Aura_Falls_Off_When_Target_Leaves_Battlefield()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var forest = GameCard.Create("Forest");
        p1.Battlefield.Add(forest);

        var wildGrowth = new GameCard { Name = "Wild Growth", CardTypes = CardType.Enchantment, AttachedTo = forest.Id };
        p1.Battlefield.Add(wildGrowth);

        // Remove the land
        p1.Battlefield.RemoveById(forest.Id);

        // SBA check should move the aura to graveyard
        await engine.CheckStateBasedActionsAsync(default);

        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Wild Growth");
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Wild Growth");
    }

    [Fact]
    public async Task WildGrowth_Adds_Green_When_Enchanted_Land_Tapped()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var forest = GameCard.Create("Forest");
        p1.Battlefield.Add(forest);

        var wildGrowth = GameCard.Create("Wild Growth");
        wildGrowth.AttachedTo = forest.Id;
        p1.Battlefield.Add(wildGrowth);

        // Tap the forest
        var action = GameAction.TapCard(p1.Id, forest.Id);
        await engine.ExecuteAction(action);

        // Should get 1G from Forest + 1G from Wild Growth = 2G total
        p1.ManaPool.Available[ManaColor.Green].Should().Be(2);
    }
}
```

**Step 2:** Run tests — expect FAIL.

**Step 3: Implement**

**AddBonusManaEffect:**
```csharp
// src/MtgDecker.Engine/Triggers/Effects/AddBonusManaEffect.cs
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Triggers.Effects;

public class AddBonusManaEffect(ManaColor color) : IEffect
{
    public ManaColor Color { get; } = color;

    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        context.Controller.ManaPool.Add(Color);
        context.State.Log($"Wild Growth adds {Color} mana.");
        return Task.CompletedTask;
    }
}
```

**Aura casting flow** — In `GameEngine.ExecuteAction` `ActionType.PlayCard`, after moving the card to battlefield (around line 239-244), add aura attachment logic:

```csharp
// After placing enchantment on battlefield:
if (CardDefinitions.TryGet(playCard.Name, out var castDef) && castDef.AuraTarget.HasValue)
{
    // Aura: prompt for attachment target
    var eligible = player.Battlefield.Cards.Concat(
        _state.GetOpponent(player).Battlefield.Cards)
        .Where(c => castDef.AuraTarget switch
        {
            AuraTarget.Land => c.IsLand,
            AuraTarget.Creature => c.IsCreature,
            AuraTarget.Permanent => true,
            _ => false,
        })
        .Where(c => !HasShroud(c))
        .ToList();

    if (eligible.Count > 0)
    {
        var chosenId = await player.DecisionHandler.ChooseCard(
            eligible, $"Choose a target for {playCard.Name}", optional: false, ct);
        if (chosenId.HasValue)
            playCard.AttachedTo = chosenId.Value;
    }

    if (!playCard.AttachedTo.HasValue)
    {
        // No valid target — aura goes to graveyard
        player.Battlefield.RemoveById(playCard.Id);
        player.Graveyard.Add(playCard);
        _state.Log($"{playCard.Name} has no valid target — goes to graveyard.");
    }
}
```

**Aura SBA** — In `CheckStateBasedActionsAsync`, add aura detachment check:

```csharp
// Check auras
foreach (var player in new[] { _state.Player1, _state.Player2 })
{
    var auras = player.Battlefield.Cards
        .Where(c => c.AttachedTo.HasValue)
        .ToList();
    foreach (var aura in auras)
    {
        var targetExists = _state.Player1.Battlefield.Contains(aura.AttachedTo!.Value)
            || _state.Player2.Battlefield.Contains(aura.AttachedTo!.Value);
        if (!targetExists)
        {
            player.Battlefield.RemoveById(aura.Id);
            player.Graveyard.Add(aura);
            _state.Log($"{aura.Name} falls off (enchanted permanent left battlefield).");
            actionTaken = true;
        }
    }
}
```

**TapForMana trigger** — In `ActionType.TapCard`, after producing mana from any ability type, check for auras attached to the tapped land and fire their mana triggers immediately (mana abilities don't use the stack):

```csharp
// After mana production (Fixed/Choice/Dynamic), check for aura mana triggers
if (tapTarget.ManaAbility != null)
{
    // ... existing mana production code ...

    // Fire mana triggers from auras (immediate — mana abilities don't use stack)
    foreach (var aura in player.Battlefield.Cards.Where(c => c.AttachedTo == tapTarget.Id))
    {
        var auraTriggers = aura.Triggers.Count > 0
            ? aura.Triggers
            : (CardDefinitions.TryGet(aura.Name, out var auraDef) ? auraDef.Triggers : []);

        foreach (var trigger in auraTriggers)
        {
            if (trigger.Condition == TriggerCondition.AttachedPermanentTapped)
            {
                var ctx = new EffectContext(_state, player, aura, player.DecisionHandler);
                await trigger.Effect.Execute(ctx);
            }
        }
    }
}
```

**Update Wild Growth in CardDefinitions:**
```csharp
["Wild Growth"] = new(ManaCost.Parse("{G}"), null, null, null, CardType.Enchantment)
{
    Subtypes = ["Aura"],
    AuraTarget = AuraTarget.Land,
    Triggers = [new Trigger(GameEvent.TapForMana, TriggerCondition.AttachedPermanentTapped, new AddBonusManaEffect(ManaColor.Green))],
},
```

**Step 4:** Run tests — expect PASS.

**Step 5:** Commit: `feat(engine): add aura casting, SBA detachment, Wild Growth mana trigger`

---

## Task 11: Cycling Infrastructure + Gempalm Incinerator

**Files:**
- Modify: `src/MtgDecker.Engine/Enums/ActionType.cs` — add Cycle
- Modify: `src/MtgDecker.Engine/Enums/GameEvent.cs` — add Cycle
- Modify: `src/MtgDecker.Engine/CardDefinition.cs` — add CyclingCost, CyclingTriggers
- Modify: `src/MtgDecker.Engine/GameAction.cs` — add Cycle factory
- Modify: `src/MtgDecker.Engine/GameEngine.cs` — Cycle action handler
- Create: `src/MtgDecker.Engine/Triggers/Effects/GempalmIncineratorEffect.cs`
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs:58` — update Gempalm Incinerator
- Create: `tests/MtgDecker.Engine.Tests/CyclingTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/MtgDecker.Engine.Tests/CyclingTests.cs
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class CyclingTests
{
    [Fact]
    public void GempalmIncinerator_Has_CyclingCost()
    {
        CardDefinitions.TryGet("Gempalm Incinerator", out var def).Should().BeTrue();
        def!.CyclingCost.Should().NotBeNull();
        def.CyclingCost!.ToString().Should().Be("{1}{R}");
    }

    [Fact]
    public async Task Cycle_Discards_Card_And_Draws()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var gempalm = GameCard.Create("Gempalm Incinerator");
        p1.Hand.Add(gempalm);
        p1.Library.Add(new GameCard { Name = "Drawn Card" });

        // Pay cycling cost: {1}{R}
        p1.ManaPool.Add(ManaColor.Red, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 1);

        var action = GameAction.Cycle(p1.Id, gempalm.Id);
        await engine.ExecuteAction(action);

        // Gempalm should be in graveyard
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Gempalm Incinerator");
        // Should have drawn a card
        p1.Hand.Cards.Should().Contain(c => c.Name == "Drawn Card");
        // Cycling trigger should be on the stack
        state.Stack.Should().ContainSingle();
        state.Stack[0].Should().BeOfType<TriggeredAbilityStackObject>();
    }

    [Fact]
    public async Task GempalmIncinerator_Trigger_Deals_Damage_Equal_To_Goblin_Count()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // 3 Goblins on battlefield
        p1.Battlefield.Add(new GameCard { Name = "Goblin 1", CardTypes = CardType.Creature, Subtypes = ["Goblin"], BasePower = 1, BaseToughness = 1 });
        p1.Battlefield.Add(new GameCard { Name = "Goblin 2", CardTypes = CardType.Creature, Subtypes = ["Goblin"], BasePower = 1, BaseToughness = 1 });
        p1.Battlefield.Add(new GameCard { Name = "Goblin 3", CardTypes = CardType.Creature, Subtypes = ["Goblin"], BasePower = 1, BaseToughness = 1 });

        var target = new GameCard { Name = "Elf", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 3 };
        p2.Battlefield.Add(target);

        // Set up: target the Elf
        handler.EnqueueCardChoice(target.Id);

        var effect = new GempalmIncineratorEffect();
        var context = new EffectContext(state, p1, new GameCard { Name = "Gempalm Incinerator" }, handler);
        await effect.Execute(context);

        // 3 Goblins → 3 damage
        target.DamageMarked.Should().Be(3);
    }
}
```

**Step 2:** Run tests — expect FAIL.

**Step 3: Implement**

Add `Cycle` to `ActionType`:
```csharp
Cycle,
```

Add `Cycle` to `GameEvent`:
```csharp
Cycle,
```

Add to `CardDefinition.cs`:
```csharp
public ManaCost? CyclingCost { get; init; }
public IReadOnlyList<Trigger> CyclingTriggers { get; init; } = [];
```

Add to `GameAction.cs`:
```csharp
public static GameAction Cycle(Guid playerId, Guid cardId) => new()
{
    Type = ActionType.Cycle,
    PlayerId = playerId,
    CardId = cardId,
    SourceZone = ZoneType.Hand,
};
```

**GempalmIncineratorEffect:**
```csharp
// src/MtgDecker.Engine/Triggers/Effects/GempalmIncineratorEffect.cs
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class GempalmIncineratorEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var goblinCount = context.Controller.Battlefield.Cards
            .Count(c => c.IsCreature && c.Subtypes.Contains("Goblin", StringComparer.OrdinalIgnoreCase));

        if (goblinCount == 0)
        {
            context.State.Log("Gempalm Incinerator deals 0 damage (no Goblins).");
            return;
        }

        // Find target creature
        var eligible = context.State.Player1.Battlefield.Cards
            .Concat(context.State.Player2.Battlefield.Cards)
            .Where(c => c.IsCreature)
            .ToList();

        if (eligible.Count == 0) return;

        var chosenId = await context.DecisionHandler.ChooseCard(
            eligible, $"Choose target for Gempalm Incinerator ({goblinCount} damage)", optional: true, ct);

        if (chosenId.HasValue)
        {
            var target = eligible.FirstOrDefault(c => c.Id == chosenId.Value);
            if (target != null)
            {
                target.DamageMarked += goblinCount;
                context.State.Log($"Gempalm Incinerator deals {goblinCount} damage to {target.Name}.");
            }
        }
    }
}
```

**Cycle handler in GameEngine.ExecuteAction:**
```csharp
case ActionType.Cycle:
{
    var cycleCard = player.Hand.Cards.FirstOrDefault(c => c.Id == action.CardId);
    if (cycleCard == null) break;

    if (!CardDefinitions.TryGet(cycleCard.Name, out var cycleDef) || cycleDef.CyclingCost == null)
    {
        _state.Log($"{cycleCard.Name} cannot be cycled.");
        break;
    }

    var cyclingCost = cycleDef.CyclingCost;
    if (!player.ManaPool.CanPay(cyclingCost))
    {
        _state.Log($"Cannot cycle {cycleCard.Name} — not enough mana.");
        break;
    }

    // Pay mana
    foreach (var (color, required) in cyclingCost.ColorRequirements)
        player.ManaPool.Deduct(color, required);
    if (cyclingCost.GenericCost > 0)
    {
        var toPay = cyclingCost.GenericCost;
        foreach (var (color, amount) in player.ManaPool.Available.Where(kv => kv.Value > 0))
        {
            var take = Math.Min(amount, toPay);
            if (take > 0) { player.ManaPool.Deduct(color, take); toPay -= take; }
            if (toPay == 0) break;
        }
    }

    // Discard to graveyard
    player.Hand.RemoveById(cycleCard.Id);
    player.Graveyard.Add(cycleCard);

    // Draw a card
    DrawCards(player, 1);
    _state.Log($"{player.Name} cycles {cycleCard.Name}.");

    // Queue cycling triggers on stack
    foreach (var trigger in cycleDef.CyclingTriggers)
    {
        _state.Log($"{cycleCard.Name} triggers: {trigger.Effect.GetType().Name.Replace("Effect", "")}");
        _state.Stack.Add(new TriggeredAbilityStackObject(cycleCard, player.Id, trigger.Effect));
    }

    player.ActionHistory.Push(action);
    break;
}
```

**Update Gempalm Incinerator in CardDefinitions:**
```csharp
["Gempalm Incinerator"] = new(ManaCost.Parse("{1}{R}"), null, 2, 1, CardType.Creature)
{
    Subtypes = ["Goblin"],
    CyclingCost = ManaCost.Parse("{1}{R}"),
    CyclingTriggers = [new Trigger(GameEvent.Cycle, TriggerCondition.Self, new GempalmIncineratorEffect())],
},
```

**Step 4:** Run tests — expect PASS.

**Step 5:** Commit: `feat(engine): add cycling action type, implement Gempalm Incinerator`

---

## Task 12: ReplenishEffect + Card Registration

**Files:**
- Create: `src/MtgDecker.Engine/Effects/ReplenishEffect.cs`
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs:121` — update Replenish
- Create: `tests/MtgDecker.Engine.Tests/ReplenishTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/MtgDecker.Engine.Tests/ReplenishTests.cs
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class ReplenishTests
{
    [Fact]
    public void Replenish_Has_SpellEffect()
    {
        CardDefinitions.TryGet("Replenish", out var def).Should().BeTrue();
        def!.Effect.Should().BeOfType<ReplenishEffect>();
    }

    [Fact]
    public void ReplenishEffect_Returns_Enchantments_From_Graveyard()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.TurnNumber = 5;

        var enchantment1 = new GameCard { Name = "Enchantment A", CardTypes = CardType.Enchantment };
        var enchantment2 = new GameCard { Name = "Enchantment B", CardTypes = CardType.Enchantment };
        var creature = new GameCard { Name = "Creature", CardTypes = CardType.Creature };
        p1.Graveyard.Add(enchantment1);
        p1.Graveyard.Add(enchantment2);
        p1.Graveyard.Add(creature);

        var replenish = new GameCard { Name = "Replenish" };
        var spell = new StackObject(replenish, p1.Id, new(), new(), 1);

        var effect = new ReplenishEffect();
        effect.Resolve(state, spell);

        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Enchantment A");
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Enchantment B");
        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Creature");
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Creature");
    }

    [Fact]
    public void ReplenishEffect_Skips_Auras_Without_Targets()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var aura = new GameCard { Name = "Wild Growth", CardTypes = CardType.Enchantment, Subtypes = ["Aura"] };
        var enchantment = new GameCard { Name = "Normal Enchantment", CardTypes = CardType.Enchantment };
        p1.Graveyard.Add(aura);
        p1.Graveyard.Add(enchantment);

        var replenish = new GameCard { Name = "Replenish" };
        var spell = new StackObject(replenish, p1.Id, new(), new(), 1);

        var effect = new ReplenishEffect();
        effect.Resolve(state, spell);

        // Normal enchantment returns, aura stays in graveyard (no valid target)
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Normal Enchantment");
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Wild Growth");
    }
}
```

**Step 2:** Run tests — expect FAIL.

**Step 3: Implement**

```csharp
// src/MtgDecker.Engine/Effects/ReplenishEffect.cs
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Effects;

public class ReplenishEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        var controller = spell.ControllerId == state.Player1.Id ? state.Player1 : state.Player2;

        var enchantments = controller.Graveyard.Cards
            .Where(c => c.CardTypes.HasFlag(CardType.Enchantment))
            .ToList();

        foreach (var card in enchantments)
        {
            controller.Graveyard.RemoveById(card.Id);

            // Auras without valid targets stay in graveyard
            if (card.Subtypes.Contains("Aura"))
            {
                controller.Graveyard.Add(card);
                state.Log($"{card.Name} stays in graveyard (no valid target for aura).");
                continue;
            }

            controller.Battlefield.Add(card);
            card.TurnEnteredBattlefield = state.TurnNumber;
            state.Log($"{card.Name} returns to the battlefield.");
        }
    }
}
```

Update Replenish in `CardDefinitions.cs`:
```csharp
["Replenish"] = new(ManaCost.Parse("{3}{W}"), null, null, null, CardType.Sorcery,
    effect: new ReplenishEffect()),
```

**Step 4:** Run tests — expect PASS.

**Step 5:** Commit: `feat(engine): add ReplenishEffect for mass enchantment recursion`

---

## Task 13: AI Bot Updates for Cycling

**Files:**
- Modify: `src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs` — add cycling to GetAction

**Step 1: Write failing test**

```csharp
// Add to tests/MtgDecker.Engine.Tests/AI/AiBotCyclingTests.cs
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.AI;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests.AI;

public class AiBotCyclingTests
{
    [Fact]
    public async Task AiBot_Cycles_When_No_Better_Play()
    {
        var bot = new AiBotDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Bot", bot);
        var p2 = new Player(Guid.NewGuid(), "Opponent", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        // Only card in hand is Gempalm Incinerator with cycling cost {1}{R}
        var gempalm = GameCard.Create("Gempalm Incinerator");
        p1.Hand.Add(gempalm);

        // Has {1}{R} mana available
        p1.ManaPool.Add(ManaColor.Red, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 1);

        // No land to play, so cycling should be considered
        p1.LandsPlayedThisTurn = 1;

        var action = await bot.GetAction(state, p1.Id);

        // Bot should choose to cycle (or cast — both are valid with {1}{R})
        // The key test: Cycle is an available action type
        action.Type.Should().BeOneOf(ActionType.Cycle, ActionType.PlayCard);
    }
}
```

**Step 2:** Run test — expect FAIL (ActionType.Cycle not handled in AI).

**Step 3: Implement**

In `AiBotDecisionHandler.GetAction`, add cycling logic after the land/fetch/ability checks but before spell casting. Insert between the tap-lands block and the cast-spell block:

```csharp
// 5. Cycle: if a card can be cycled and we can't cast it (or prefer cycling)
foreach (var card in hand)
{
    if (CardDefinitions.TryGet(card.Name, out var cycleDef) && cycleDef.CyclingCost != null)
    {
        if (player.ManaPool.CanPay(cycleDef.CyclingCost))
        {
            // Prefer casting if we can afford the full cost, otherwise cycle
            if (card.ManaCost == null || !player.ManaPool.CanPay(card.ManaCost))
                return GameAction.Cycle(playerId, card.Id);
        }
    }
}
```

**Step 4:** Run tests — expect PASS.

**Step 5:** Commit: `feat(engine): add cycling support to AI bot`

---

## Task 14: Integration Tests + Final Verification

**Files:**
- Create: `tests/MtgDecker.Engine.Tests/FullPlayIntegrationTests.cs`
- Run: all test suites

**Step 1: Write integration tests verifying end-to-end flows**

```csharp
// tests/MtgDecker.Engine.Tests/FullPlayIntegrationTests.cs
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests;

public class FullPlayIntegrationTests
{
    [Fact]
    public async Task WildGrowth_On_Forest_Produces_Double_Green()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // Play Forest, attach Wild Growth, tap for double mana
        var forest = GameCard.Create("Forest");
        p1.Battlefield.Add(forest);

        var wildGrowth = GameCard.Create("Wild Growth");
        wildGrowth.AttachedTo = forest.Id;
        p1.Battlefield.Add(wildGrowth);

        var tap = GameAction.TapCard(p1.Id, forest.Id);
        await engine.ExecuteAction(tap);

        p1.ManaPool.Available[ManaColor.Green].Should().Be(2);
    }

    [Fact]
    public async Task AuraOfSilence_Sacrifice_Destroys_Enchantment()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var aura = GameCard.Create("Aura of Silence");
        p1.Battlefield.Add(aura);

        var target = new GameCard { Name = "Enemy Enchantment", CardTypes = CardType.Enchantment };
        p2.Battlefield.Add(target);

        var action = GameAction.ActivateAbility(p1.Id, aura.Id, target.Id);
        await engine.ExecuteAction(action);
        await engine.ResolveAllTriggersAsync();

        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Aura of Silence");
        p2.Battlefield.Cards.Should().NotContain(c => c.Name == "Enemy Enchantment");
    }

    [Fact]
    public void SerrasSanctum_Scales_With_Enchantment_Count()
    {
        CardDefinitions.TryGet("Serra's Sanctum", out var def).Should().BeTrue();
        def!.ManaAbility.Should().NotBeNull();
        def.ManaAbility!.Type.Should().Be(ManaAbilityType.Dynamic);
    }

    [Fact]
    public void GempalmIncinerator_Has_Cycling_And_CyclingTrigger()
    {
        CardDefinitions.TryGet("Gempalm Incinerator", out var def).Should().BeTrue();
        def!.CyclingCost.Should().NotBeNull();
        def.CyclingTriggers.Should().ContainSingle();
    }

    [Fact]
    public void Replenish_Has_Effect()
    {
        CardDefinitions.TryGet("Replenish", out var def).Should().BeTrue();
        def!.Effect.Should().NotBeNull();
    }
}
```

**Step 2: Run ALL tests across ALL projects**

```bash
dotnet test tests/MtgDecker.Engine.Tests/
dotnet test tests/MtgDecker.Domain.Tests/
dotnet test tests/MtgDecker.Application.Tests/
dotnet test tests/MtgDecker.Infrastructure.Tests/
dotnet build src/MtgDecker.Web/
```

**Step 3:** Fix any failures. Report final test counts.

**Step 4:** Commit: `test(engine): add full-play integration tests for all new features`

---

## Summary

| Task | Feature | Cards Unlocked |
|------|---------|---------------|
| 1-4 | Stack-based triggers + APNAP | All (correctness) |
| 5 | Dynamic mana abilities | Serra's Sanctum |
| 6 | Opponent cost modification | Aura of Silence |
| 7 | Mountainwalk combat logic | Goblin Pyromancer (complete) |
| 8 | Shroud targeting | Sterling Grove (complete) |
| 9-10 | Aura mechanics | Wild Growth |
| 11 | Cycling | Gempalm Incinerator |
| 12 | Mass recursion | Replenish |
| 13 | AI updates | Bot plays cycling |
| 14 | Integration tests | Verification |

**Total: 14 tasks, 7 new engine systems, 7 cards unlocked**
