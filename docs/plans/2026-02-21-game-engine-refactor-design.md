# GameEngine Refactor — Strategy Pattern for Action Handlers

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement the corresponding plan task-by-task.

**Goal:** Break the 3,325-line GameEngine god class into focused action handler classes using the strategy pattern, and extract duplicated targeting logic into a shared helper.

**Architecture:** Each of the 11 `ActionType` cases in `ExecuteAction` becomes its own `IActionHandler` class in an `Actions/` folder. GameEngine becomes an orchestrator that dispatches actions to handlers via a dictionary lookup. A shared `FindAndChooseTargetsAsync` method on GameEngine eliminates ~180 lines of duplicated targeting logic across CastSpell, Flashback, and CastAdventure.

---

## Problem

`GameEngine.cs` is 3,325 lines with these issues:

- `ExecuteAction` is a 1,212-line switch statement handling 11 action types
- Targeting logic (build eligible targets → prompt player → validate → convert to TargetInfo) is copy-pasted across CastSpell (lines 561-622), Flashback (lines 1038-1092), and CastAdventure (lines 1341-1371) — ~180 lines of duplication
- The file handles too many concerns: action execution, combat, stack resolution, continuous effects, triggers, mana payment, state-based actions

## Approach: Strategy Pattern for Actions (Option B)

### What changes

**New files:**

```
src/MtgDecker.Engine/Actions/
  IActionHandler.cs
  PlayCardHandler.cs
  TapCardHandler.cs
  UntapCardHandler.cs
  ActivateFetchHandler.cs
  CastSpellHandler.cs
  ActivateAbilityHandler.cs
  CycleHandler.cs
  FlashbackHandler.cs
  ActivateLoyaltyAbilityHandler.cs
  NinjutsuHandler.cs
  CastAdventureHandler.cs
```

**IActionHandler interface:**

```csharp
namespace MtgDecker.Engine.Actions;

internal interface IActionHandler
{
    Task ExecuteAsync(GameAction action, GameEngine engine, GameState state);
}
```

Handlers are `internal` — implementation details of the Engine project, not a public API. They receive GameEngine directly (not an interface) because they need access to many internal methods and are never used outside this project.

**GameEngine.ExecuteAction becomes a dispatch table:**

```csharp
private readonly Dictionary<ActionType, IActionHandler> _handlers;

internal async Task ExecuteAction(GameAction action)
{
    if (_handlers.TryGetValue(action.Type, out var handler))
        await handler.ExecuteAsync(action, this, _state);
    else
        _state.Log($"Unknown action type: {action.Type}");
}
```

The handler dictionary is built in the GameEngine constructor.

### Shared targeting helper

A new `internal` method on GameEngine consolidates the duplicated targeting logic:

```csharp
internal async Task<List<TargetInfo>?> FindAndChooseTargetsAsync(
    TargetFilter filter, Player caster, IPlayerDecisionHandler handler,
    CancellationToken ct = default)
```

This method:
1. Builds eligible targets from both battlefields (respecting shroud/hexproof/protection)
2. Adds player sentinels if the filter allows player targets
3. Adds stack objects if the filter allows spell targets
4. Calls `handler.ChooseTarget(eligible)` to prompt the player
5. Validates the choice and converts to `TargetInfo`
6. Returns `null` if cancelled (target cancellation)

All three handlers (CastSpell, Flashback, CastAdventure) call it identically.

### What stays in GameEngine

Everything except the 11 action type cases:

- Game flow: `StartGameAsync`, `RunTurnAsync`, `RunMulliganAsync`, `RunPriorityAsync`, `DiscardToHandSizeAsync`
- Stack resolution: `ResolveTopOfStackAsync`
- Combat: `RunCombatAsync`, `ResolveCombatDamage`, `ProcessCombatDeaths`
- Continuous effects: `RecalculateState` + per-layer helpers
- State-based actions: `CheckStateBasedActionsAsync`
- Triggers: All `Queue*TriggersOnStackAsync` methods
- Mana: `PayManaCostAsync`, `CanPayAlternateCost`, `PayAlternateCostAsync`
- Helpers: `DrawCards`, `UndoLastAction`, `CanCastSorcery`, `MoveToGraveyardWithReplacement`, targeting/protection helpers
- Action dispatch: New ~10-line `ExecuteAction` with handler dictionary

### Line count impact

| Component | Before | After |
|-----------|--------|-------|
| GameEngine.cs | 3,325 | ~2,000 |
| Actions/ (11 handlers + interface) | 0 | ~1,200 |
| Targeting duplication | ~180 | 0 (one ~60-line method) |
| **Total** | **3,325** | **~3,200** |

Same total code, but no single file over 2,000 lines and no method over 200 lines.

## Extraction Order

Extract targeting helper first, then handlers from simplest to most complex. Each extraction is one commit with all 1,672 engine tests passing.

1. **FindAndChooseTargetsAsync** — shared targeting method (prerequisite for 3 handlers)
2. **UntapCardHandler** — 9 lines, validates the pattern
3. **CycleHandler** — 40 lines
4. **ActivateFetchHandler** — 53 lines
5. **ActivateLoyaltyAbilityHandler** — 75 lines
6. **PlayCardHandler** — 84 lines
7. **CastAdventureHandler** — 91 lines, uses targeting helper
8. **NinjutsuHandler** — 92 lines
9. **FlashbackHandler** — 154 lines, uses targeting helper
10. **TapCardHandler** — 157 lines, includes mana ability logic
11. **CastSpellHandler** — 162 lines, uses targeting helper
12. **ActivateAbilityHandler** — 274 lines, largest handler

## Testing Strategy

**No behavior changes.** This is a purely structural refactoring. All 1,672 existing engine tests are the safety net — they must pass after each extraction step.

**No new handler unit tests.** The handlers are already covered by existing integration tests (DelverIntegrationTests, SpellEffectTests, CombatTests, etc.). Writing isolated handler tests would duplicate coverage.

**One new test class:** `FindAndChooseTargetsTests` for the extracted targeting helper, verifying:
- Battlefield targets found (both players)
- Shroud/hexproof exclusion
- Player sentinels added when filter allows
- Stack targets added when filter allows
- Cancellation returns null

## Future: Path to Full Decomposition (Option C)

This refactoring sets up easy extraction of `CombatManager`, `ContinuousEffectsEngine`, and `StackResolver` later if needed. The action handlers define the interface surface that those classes would use — the seams are already in place after B.
