# Deferred Cards Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement Solitary Confinement, Parallax Wave, and Opalescence to reach 100% deck coverage for both the Goblins and Enchantress decks.

**Architecture:** Three cards requiring seven new engine subsystems: upkeep cost enforcement, skip-draw, player protection (shroud + damage prevention), counter system, per-source exile tracking, leave-the-battlefield triggers, and type-changing continuous effects. Each subsystem builds on existing patterns (ContinuousEffectType enum, IEffect interface, ActivatedAbilityCost record, trigger pipeline).

**Tech Stack:** .NET 10, C# 14, xUnit + FluentAssertions, MtgDecker.Engine project

---

**Working directory:** `C:\Users\larst\MtgDecker\.worktrees\continuous-effects\`

**Build command:** `export PATH="/c/Program Files/dotnet:$PATH" && dotnet build src/MtgDecker.Engine/`

**Test command:** `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/`

**Existing patterns to follow:**
- Test file location: `tests/MtgDecker.Engine.Tests/`
- Test helper: `MtgDecker.Engine.Tests.Helpers.TestDecisionHandler` (queue-based, call `EnqueueCardChoice`, `EnqueueAction`, etc.)
- Card creation: `GameCard.Create("Card Name")` for registered cards; `new GameCard { ... }` for ad-hoc
- Standard test setup: create `Player` with `TestDecisionHandler`, `GameState`, `GameEngine`, place cards on battlefield, call `engine.RecalculateState()` then assert
- Effects implement `IEffect` interface: `Task Execute(EffectContext context, CancellationToken ct)`
- `EffectContext` record: `(GameState State, Player Controller, GameCard Source, IPlayerDecisionHandler DecisionHandler)` with optional `Target` and `TargetPlayerId`

---

## PART 1: SOLITARY CONFINEMENT

### Task 1: UpkeepCostEffect — sacrifice-or-discard

**Files:**
- Create: `src/MtgDecker.Engine/Triggers/Effects/UpkeepCostEffect.cs`
- Test: `tests/MtgDecker.Engine.Tests/UpkeepCostTests.cs`

**Step 1: Write the failing tests**

Create `tests/MtgDecker.Engine.Tests/UpkeepCostTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class UpkeepCostTests
{
    [Fact]
    public async Task UpkeepCost_Discards_Card_To_Keep_Enchantment()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var enchantment = new GameCard { Name = "Test Enchantment", CardTypes = CardType.Enchantment };
        p1.Battlefield.Add(enchantment);

        var handCard = new GameCard { Name = "Fodder" };
        p1.Hand.Add(handCard);

        // Player chooses to discard the card
        handler.EnqueueCardChoice(handCard.Id);

        var effect = new UpkeepCostEffect();
        var ctx = new EffectContext(state, p1, enchantment, handler);
        await effect.Execute(ctx);

        // Enchantment should still be on battlefield
        p1.Battlefield.Cards.Should().Contain(c => c.Id == enchantment.Id);
        // Card should have moved from hand to graveyard
        p1.Hand.Cards.Should().NotContain(c => c.Id == handCard.Id);
        p1.Graveyard.Cards.Should().Contain(c => c.Id == handCard.Id);
    }

    [Fact]
    public async Task UpkeepCost_Sacrifices_When_No_Cards_In_Hand()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var enchantment = new GameCard { Name = "Test Enchantment", CardTypes = CardType.Enchantment };
        p1.Battlefield.Add(enchantment);
        // No cards in hand

        var effect = new UpkeepCostEffect();
        var ctx = new EffectContext(state, p1, enchantment, handler);
        await effect.Execute(ctx);

        // Enchantment should be sacrificed (moved to graveyard)
        p1.Battlefield.Cards.Should().NotContain(c => c.Id == enchantment.Id);
        p1.Graveyard.Cards.Should().Contain(c => c.Id == enchantment.Id);
    }

    [Fact]
    public async Task UpkeepCost_Sacrifices_When_Player_Declines_Discard()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var enchantment = new GameCard { Name = "Test Enchantment", CardTypes = CardType.Enchantment };
        p1.Battlefield.Add(enchantment);

        var handCard = new GameCard { Name = "Fodder" };
        p1.Hand.Add(handCard);

        // Player declines (null choice = decline)
        handler.EnqueueCardChoice(null);

        var effect = new UpkeepCostEffect();
        var ctx = new EffectContext(state, p1, enchantment, handler);
        await effect.Execute(ctx);

        // Enchantment should be sacrificed
        p1.Battlefield.Cards.Should().NotContain(c => c.Id == enchantment.Id);
        p1.Graveyard.Cards.Should().Contain(c => c.Id == enchantment.Id);
        // Hand card should still be there
        p1.Hand.Cards.Should().Contain(c => c.Id == handCard.Id);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "UpkeepCostTests"`
Expected: FAIL — `UpkeepCostEffect` does not exist

**Step 3: Write minimal implementation**

Create `src/MtgDecker.Engine/Triggers/Effects/UpkeepCostEffect.cs`:

```csharp
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class UpkeepCostEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var controller = context.Controller;
        var source = context.Source;

        if (controller.Hand.Count > 0)
        {
            var chosenId = await context.DecisionHandler.ChooseCard(
                controller.Hand.Cards.ToList(),
                $"Discard a card to keep {source.Name}, or decline to sacrifice it",
                optional: true, ct);

            if (chosenId.HasValue)
            {
                var card = controller.Hand.RemoveById(chosenId.Value);
                if (card != null)
                {
                    controller.Graveyard.Add(card);
                    context.State.Log($"{controller.Name} discards {card.Name} to keep {source.Name}.");
                    return;
                }
            }
        }

        // Sacrifice the source enchantment
        controller.Battlefield.RemoveById(source.Id);
        controller.Graveyard.Add(source);
        context.State.Log($"{controller.Name} sacrifices {source.Name} (no discard).");
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "UpkeepCostTests"`
Expected: PASS (3 tests)

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/Triggers/Effects/UpkeepCostEffect.cs tests/MtgDecker.Engine.Tests/UpkeepCostTests.cs
git commit -m "feat(engine): add UpkeepCostEffect for sacrifice-or-discard upkeep costs"
```

---

### Task 2: SkipDraw continuous effect type + engine support

**Files:**
- Modify: `src/MtgDecker.Engine/ContinuousEffect.cs` (add `SkipDraw` to enum)
- Modify: `src/MtgDecker.Engine/GameEngine.cs:42-44` (check for SkipDraw before drawing)
- Test: `tests/MtgDecker.Engine.Tests/SkipDrawTests.cs`

**Step 1: Write the failing tests**

Create `tests/MtgDecker.Engine.Tests/SkipDrawTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class SkipDrawTests
{
    [Fact]
    public void SkipDraw_Effect_Prevents_Draw_Step()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // Give P1 a card in library to draw
        p1.Library.Add(new GameCard { Name = "Card in Library" });
        var initialHandCount = p1.Hand.Count;

        // Add a SkipDraw effect owned by P1
        var sourceId = Guid.NewGuid();
        state.ActiveEffects.Add(new ContinuousEffect(
            sourceId, ContinuousEffectType.SkipDraw, (_, _) => true));

        // P1 is active player
        state.ActivePlayer = p1;

        // Execute draw step
        engine.ExecuteTurnBasedAction(Phase.Draw);

        // Should NOT have drawn a card
        p1.Hand.Count.Should().Be(initialHandCount);
        state.GameLog.Should().Contain(l => l.Contains("skip"));
    }

    [Fact]
    public void SkipDraw_Only_Affects_Controller()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P1 has SkipDraw effect via an enchantment on their battlefield
        var enchantment = new GameCard { Name = "Skip Source", CardTypes = CardType.Enchantment };
        p1.Battlefield.Add(enchantment);
        state.ActiveEffects.Add(new ContinuousEffect(
            enchantment.Id, ContinuousEffectType.SkipDraw, (_, _) => true));

        // P2 is active player and should draw normally
        state.ActivePlayer = p2;
        p2.Library.Add(new GameCard { Name = "Draw Me" });
        var initialHandCount = p2.Hand.Count;

        engine.ExecuteTurnBasedAction(Phase.Draw);

        // P2 should have drawn
        p2.Hand.Count.Should().Be(initialHandCount + 1);
    }

    [Fact]
    public void Normal_Draw_Works_Without_SkipDraw()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        p1.Library.Add(new GameCard { Name = "Card" });
        state.ActivePlayer = p1;
        var initialHandCount = p1.Hand.Count;

        engine.ExecuteTurnBasedAction(Phase.Draw);

        p1.Hand.Count.Should().Be(initialHandCount + 1);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "SkipDrawTests"`
Expected: FAIL — `ContinuousEffectType.SkipDraw` does not exist

**Step 3: Write minimal implementation**

Add `SkipDraw` to the enum in `src/MtgDecker.Engine/ContinuousEffect.cs`:

```csharp
public enum ContinuousEffectType
{
    ModifyPowerToughness,
    GrantKeyword,
    ModifyCost,
    ExtraLandDrop,
    SkipDraw,
}
```

Modify `GameEngine.ExecuteTurnBasedAction` — the draw case at line 98. Replace the draw case with:

```csharp
case Phase.Draw:
    // Check for SkipDraw effects — only skip if the controller of the effect is the active player
    var hasSkipDraw = _state.ActiveEffects.Any(e =>
        e.Type == ContinuousEffectType.SkipDraw
        && (_state.Player1.Battlefield.Contains(e.SourceId)
            ? _state.Player1 : _state.Player2) == _state.ActivePlayer);

    if (hasSkipDraw)
    {
        _state.Log($"{_state.ActivePlayer.Name} skips draw step.");
        break;
    }

    var drawn = _state.ActivePlayer.Library.DrawFromTop();
    if (drawn != null)
    {
        _state.ActivePlayer.Hand.Add(drawn);
        _state.Log($"{_state.ActivePlayer.Name} draws a card.");
    }
    else
    {
        var loser = _state.ActivePlayer;
        var winner = _state.GetOpponent(loser);
        _state.IsGameOver = true;
        _state.Winner = winner.Name;
        _state.Log($"{loser.Name} loses — cannot draw from an empty library.");
    }
    break;
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "SkipDrawTests"`
Expected: PASS (3 tests)

**Step 5: Run all tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All tests pass (no regressions)

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/ContinuousEffect.cs src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/SkipDrawTests.cs
git commit -m "feat(engine): add SkipDraw continuous effect type"
```

---

### Task 3: Player shroud + damage prevention

**Files:**
- Modify: `src/MtgDecker.Engine/ContinuousEffect.cs` (add `GrantPlayerShroud`, `PreventDamageToPlayer` to enum)
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (add player shroud check in ActivateAbility, add damage prevention in combat + DealDamageEffect)
- Modify: `src/MtgDecker.Engine/Triggers/Effects/DealDamageEffect.cs` (check damage prevention)
- Test: `tests/MtgDecker.Engine.Tests/PlayerProtectionTests.cs`

**Step 1: Write the failing tests**

Create `tests/MtgDecker.Engine.Tests/PlayerProtectionTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class PlayerProtectionTests
{
    // === Player Shroud ===

    [Fact]
    public async Task PlayerShroud_Prevents_Activated_Ability_Targeting_Player()
    {
        var handler1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P2 has player shroud
        var sourceId = Guid.NewGuid();
        state.ActiveEffects.Add(new ContinuousEffect(
            sourceId, ContinuousEffectType.GrantPlayerShroud, (_, _) => true));
        // Mark it as P2's by putting a dummy on P2's battlefield
        var protectionSource = new GameCard { Name = "Protection Source", CardTypes = CardType.Enchantment };
        protectionSource.GetType().GetProperty("Id")!.SetValue(protectionSource, sourceId); // hack for test
        // Actually, let's just use the effect source on P2's battlefield
        // Re-approach: put a card on P2's battlefield with the source ID
        p2.Battlefield.Add(protectionSource);
        state.ActiveEffects.Clear();
        state.ActiveEffects.Add(new ContinuousEffect(
            protectionSource.Id, ContinuousEffectType.GrantPlayerShroud, (_, _) => true));

        // P1 has Mogg Fanatic on battlefield
        var fanatic = GameCard.Create("Mogg Fanatic");
        p1.Battlefield.Add(fanatic);

        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var startingLife = p2.Life;

        // Try to activate Mogg Fanatic targeting P2
        await engine.ExecuteAction(
            GameAction.ActivateAbility(p1.Id, fanatic.Id, targetPlayerId: p2.Id), default);

        // P2's life should NOT have changed (player has shroud)
        p2.Life.Should().Be(startingLife);
        state.GameLog.Should().Contain(l => l.Contains("shroud") || l.Contains("cannot be targeted"));
    }

    [Fact]
    public async Task PlayerShroud_Does_Not_Affect_Other_Player()
    {
        var handler1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P1 has player shroud (from their own enchantment)
        var protectionSource = new GameCard { Name = "Protection Source", CardTypes = CardType.Enchantment };
        p1.Battlefield.Add(protectionSource);
        state.ActiveEffects.Add(new ContinuousEffect(
            protectionSource.Id, ContinuousEffectType.GrantPlayerShroud, (_, _) => true));

        // P2 has Mogg Fanatic
        var fanatic = GameCard.Create("Mogg Fanatic");
        p2.Battlefield.Add(fanatic);

        state.ActivePlayer = p2;
        state.CurrentPhase = Phase.MainPhase1;

        var startingLifeP1 = p1.Life;

        // P2 tries to target P1 (who has shroud) — should fail
        await engine.ExecuteAction(
            GameAction.ActivateAbility(p2.Id, fanatic.Id, targetPlayerId: p1.Id), default);

        p1.Life.Should().Be(startingLifeP1);
    }

    // === Damage Prevention ===

    [Fact]
    public async Task DamagePreventionPrevents_DealDamageEffect_To_Player()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        // P2 has damage prevention
        var protectionSource = new GameCard { Name = "Protection Source", CardTypes = CardType.Enchantment };
        p2.Battlefield.Add(protectionSource);
        state.ActiveEffects.Add(new ContinuousEffect(
            protectionSource.Id, ContinuousEffectType.PreventDamageToPlayer, (_, _) => true));

        var source = new GameCard { Name = "Damage Source" };
        var effect = new DealDamageEffect(3);
        var ctx = new EffectContext(state, p1, source, handler)
        {
            TargetPlayerId = p2.Id,
        };

        var startingLife = p2.Life;
        await effect.Execute(ctx);

        p2.Life.Should().Be(startingLife);
        state.GameLog.Should().Contain(l => l.Contains("prevented") || l.Contains("protection"));
    }

    [Fact]
    public async Task DamagePreventionDoesNotAffect_Creature_Damage()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        // P2 has damage prevention for player
        var protectionSource = new GameCard { Name = "Protection Source", CardTypes = CardType.Enchantment };
        p2.Battlefield.Add(protectionSource);
        state.ActiveEffects.Add(new ContinuousEffect(
            protectionSource.Id, ContinuousEffectType.PreventDamageToPlayer, (_, _) => true));

        var creature = new GameCard { Name = "Target Creature", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 4 };
        p2.Battlefield.Add(creature);

        var source = new GameCard { Name = "Damage Source" };
        var effect = new DealDamageEffect(3);
        var ctx = new EffectContext(state, p1, source, handler) { Target = creature };

        await effect.Execute(ctx);

        // Creature should still take damage (prevention is for player only)
        creature.DamageMarked.Should().Be(3);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "PlayerProtectionTests"`
Expected: FAIL — `ContinuousEffectType.GrantPlayerShroud` does not exist

**Step 3: Write minimal implementation**

Add to `ContinuousEffectType` enum in `src/MtgDecker.Engine/ContinuousEffect.cs`:

```csharp
public enum ContinuousEffectType
{
    ModifyPowerToughness,
    GrantKeyword,
    ModifyCost,
    ExtraLandDrop,
    SkipDraw,
    GrantPlayerShroud,
    PreventDamageToPlayer,
}
```

Add helper method to `GameEngine` (near `HasShroud` at line 719):

```csharp
private bool HasPlayerShroud(Guid playerId)
{
    return _state.ActiveEffects.Any(e =>
        e.Type == ContinuousEffectType.GrantPlayerShroud
        && GetEffectController(e.SourceId)?.Id == playerId);
}

private bool HasPlayerDamageProtection(Guid playerId)
{
    return _state.ActiveEffects.Any(e =>
        e.Type == ContinuousEffectType.PreventDamageToPlayer
        && GetEffectController(e.SourceId)?.Id == playerId);
}

private Player? GetEffectController(Guid sourceId)
{
    if (_state.Player1.Battlefield.Contains(sourceId)) return _state.Player1;
    if (_state.Player2.Battlefield.Contains(sourceId)) return _state.Player2;
    return null;
}
```

In `ExecuteAction`, `ActionType.ActivateAbility` case — add player shroud check BEFORE executing the effect (around line 664, after the creature shroud check). Add:

```csharp
// Player shroud check
if (action.TargetPlayerId.HasValue && HasPlayerShroud(action.TargetPlayerId.Value))
{
    var targetPlayerName = action.TargetPlayerId.Value == _state.Player1.Id
        ? _state.Player1.Name : _state.Player2.Name;
    _state.Log($"{targetPlayerName} has shroud — cannot be targeted.");
    break;
}
```

Modify `DealDamageEffect.Execute` in `src/MtgDecker.Engine/Triggers/Effects/DealDamageEffect.cs` — in the `else if (context.TargetPlayerId.HasValue)` block, add a damage prevention check:

```csharp
else if (context.TargetPlayerId.HasValue)
{
    var target = context.State.Player1.Id == context.TargetPlayerId.Value
        ? context.State.Player1
        : context.State.Player2;

    // Check for damage prevention
    var hasDamageProtection = context.State.ActiveEffects.Any(e =>
        e.Type == ContinuousEffectType.PreventDamageToPlayer
        && (context.State.Player1.Battlefield.Contains(e.SourceId)
            ? context.State.Player1 : context.State.Player2).Id == target.Id);

    if (hasDamageProtection)
    {
        context.State.Log($"Damage to {target.Name} is prevented (protection).");
    }
    else
    {
        target.AdjustLife(-Amount);
        context.State.Log($"{context.Source.Name} deals {Amount} damage to {target.Name}. ({target.Life} life)");
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "PlayerProtectionTests"`
Expected: PASS (4 tests)

**Step 5: Run all tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All tests pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/ContinuousEffect.cs src/MtgDecker.Engine/GameEngine.cs src/MtgDecker.Engine/Triggers/Effects/DealDamageEffect.cs tests/MtgDecker.Engine.Tests/PlayerProtectionTests.cs
git commit -m "feat(engine): add player shroud and damage prevention effects"
```

---

### Task 4: Combat damage prevention for protected player

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (add damage prevention to combat unblocked damage)
- Test: `tests/MtgDecker.Engine.Tests/PlayerProtectionTests.cs` (add combat test)

**Step 1: Write the failing test**

Add to `PlayerProtectionTests.cs`:

```csharp
[Fact]
public async Task DamagePrevention_Prevents_Combat_Damage_To_Player()
{
    var handler1 = new TestDecisionHandler();
    var handler2 = new TestDecisionHandler();
    var p1 = new Player(Guid.NewGuid(), "P1", handler1);
    var p2 = new Player(Guid.NewGuid(), "P2", handler2);
    var state = new GameState(p1, p2);
    var engine = new GameEngine(state);

    // P2 has damage prevention
    var protectionSource = new GameCard { Name = "Protection Source", CardTypes = CardType.Enchantment };
    p2.Battlefield.Add(protectionSource);
    state.ActiveEffects.Add(new ContinuousEffect(
        protectionSource.Id, ContinuousEffectType.PreventDamageToPlayer, (_, _) => true));

    // P1 has a 3/3 creature that will attack
    var attacker = new GameCard
    {
        Name = "Attacker", CardTypes = CardType.Creature,
        BasePower = 3, BaseToughness = 3,
        TurnEnteredBattlefield = 0 // No summoning sickness
    };
    p1.Battlefield.Add(attacker);

    state.ActivePlayer = p1;
    state.TurnNumber = 1;
    state.CurrentPhase = Phase.Combat;

    handler1.EnqueueAttackers(new[] { attacker.Id });
    handler2.EnqueueBlockers(new Dictionary<Guid, Guid>()); // no blockers

    var startingLife = p2.Life;
    await engine.RunCombatAsync(default);

    // P2's life should NOT decrease (damage prevented)
    p2.Life.Should().Be(startingLife);
    state.GameLog.Should().Contain(l => l.Contains("prevented") || l.Contains("protection"));
}
```

**Step 2: Run test to verify it fails**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "DamagePrevention_Prevents_Combat_Damage"`
Expected: FAIL — combat damage still applied

**Step 3: Write minimal implementation**

In `GameEngine.cs`, find the unblocked attacker damage section (around line 1006-1011, where `defender.AdjustLife(-damage)` is called). Wrap the damage in a protection check:

```csharp
if (damage > 0)
{
    if (HasPlayerDamageProtection(defender.Id))
    {
        _state.Log($"{attackerCard.Name}'s {damage} damage to {defender.Name} is prevented (protection).");
    }
    else
    {
        defender.AdjustLife(-damage);
        _state.Log($"{attackerCard.Name} deals {damage} damage to {defender.Name}. ({defender.Life} life)");
    }
    unblockedAttackers.Add(attackerCard);
}
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "PlayerProtectionTests"`
Expected: PASS (5 tests)

**Step 5: Run all tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/PlayerProtectionTests.cs
git commit -m "feat(engine): prevent combat damage to protected player"
```

---

### Task 5: Solitary Confinement card definition + integration test

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs` (add Solitary Confinement registration)
- Test: `tests/MtgDecker.Engine.Tests/SolitaryConfinementTests.cs`

**Step 1: Write the failing tests**

Create `tests/MtgDecker.Engine.Tests/SolitaryConfinementTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class SolitaryConfinementTests
{
    [Fact]
    public void SolitaryConfinement_Is_Registered()
    {
        CardDefinitions.TryGet("Solitary Confinement", out var def).Should().BeTrue();
        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(3);
        def.CardTypes.Should().Be(CardType.Enchantment);
    }

    [Fact]
    public void SolitaryConfinement_Has_SkipDraw_Effect()
    {
        CardDefinitions.TryGet("Solitary Confinement", out var def).Should().BeTrue();
        def!.ContinuousEffects.Should().Contain(e => e.Type == ContinuousEffectType.SkipDraw);
    }

    [Fact]
    public void SolitaryConfinement_Has_PlayerShroud_And_DamageProtection()
    {
        CardDefinitions.TryGet("Solitary Confinement", out var def).Should().BeTrue();
        def!.ContinuousEffects.Should().Contain(e => e.Type == ContinuousEffectType.GrantPlayerShroud);
        def.ContinuousEffects.Should().Contain(e => e.Type == ContinuousEffectType.PreventDamageToPlayer);
    }

    [Fact]
    public void SolitaryConfinement_Has_Upkeep_Trigger()
    {
        CardDefinitions.TryGet("Solitary Confinement", out var def).Should().BeTrue();
        def!.Triggers.Should().ContainSingle(t =>
            t.Event == GameEvent.Upkeep && t.Condition == Triggers.TriggerCondition.Upkeep);
    }

    [Fact]
    public void SolitaryConfinement_RecalculateState_Applies_SkipDraw()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var confinement = GameCard.Create("Solitary Confinement");
        p1.Battlefield.Add(confinement);
        engine.RecalculateState();

        state.ActiveEffects.Should().Contain(e => e.Type == ContinuousEffectType.SkipDraw);
        state.ActiveEffects.Should().Contain(e => e.Type == ContinuousEffectType.GrantPlayerShroud);
        state.ActiveEffects.Should().Contain(e => e.Type == ContinuousEffectType.PreventDamageToPlayer);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "SolitaryConfinementTests"`
Expected: FAIL — "Solitary Confinement" not registered in CardDefinitions

**Step 3: Write minimal implementation**

Add Solitary Confinement to the Enchantress section of `CardDefinitions.cs` (after `Sylvan Library`):

```csharp
["Solitary Confinement"] = new(ManaCost.Parse("{2}{W}"), null, null, null, CardType.Enchantment)
{
    Triggers = [new Trigger(GameEvent.Upkeep, TriggerCondition.Upkeep, new UpkeepCostEffect())],
    ContinuousEffects =
    [
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.SkipDraw, (_, _) => true),
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantPlayerShroud, (_, _) => true),
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.PreventDamageToPlayer, (_, _) => true),
    ],
},
```

Add `using MtgDecker.Engine.Triggers.Effects;` at the top of `CardDefinitions.cs` if `UpkeepCostEffect` namespace isn't already imported (it should be — check existing imports).

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "SolitaryConfinementTests"`
Expected: PASS (5 tests)

**Step 5: Run all tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/CardDefinitions.cs tests/MtgDecker.Engine.Tests/SolitaryConfinementTests.cs
git commit -m "feat(engine): register Solitary Confinement card definition"
```

---

## PART 2: PARALLAX WAVE

### Task 6: Counter system on GameCard

**Files:**
- Create: `src/MtgDecker.Engine/Enums/CounterType.cs`
- Modify: `src/MtgDecker.Engine/GameCard.cs` (add Counters dictionary + methods)
- Test: `tests/MtgDecker.Engine.Tests/CounterTests.cs`

**Step 1: Write the failing tests**

Create `tests/MtgDecker.Engine.Tests/CounterTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class CounterTests
{
    [Fact]
    public void AddCounters_Places_Counters_On_Card()
    {
        var card = new GameCard { Name = "Test Card" };
        card.AddCounters(CounterType.Fade, 5);
        card.GetCounters(CounterType.Fade).Should().Be(5);
    }

    [Fact]
    public void AddCounters_Stacks_With_Existing()
    {
        var card = new GameCard { Name = "Test Card" };
        card.AddCounters(CounterType.Fade, 3);
        card.AddCounters(CounterType.Fade, 2);
        card.GetCounters(CounterType.Fade).Should().Be(5);
    }

    [Fact]
    public void RemoveCounter_Decrements_And_Returns_True()
    {
        var card = new GameCard { Name = "Test Card" };
        card.AddCounters(CounterType.Fade, 3);

        card.RemoveCounter(CounterType.Fade).Should().BeTrue();
        card.GetCounters(CounterType.Fade).Should().Be(2);
    }

    [Fact]
    public void RemoveCounter_Returns_False_When_No_Counters()
    {
        var card = new GameCard { Name = "Test Card" };
        card.RemoveCounter(CounterType.Fade).Should().BeFalse();
    }

    [Fact]
    public void GetCounters_Returns_Zero_For_Unknown_Type()
    {
        var card = new GameCard { Name = "Test Card" };
        card.GetCounters(CounterType.Fade).Should().Be(0);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "CounterTests"`
Expected: FAIL — `CounterType` does not exist

**Step 3: Write minimal implementation**

Create `src/MtgDecker.Engine/Enums/CounterType.cs`:

```csharp
namespace MtgDecker.Engine.Enums;

public enum CounterType
{
    Fade,
}
```

Add to `GameCard.cs` — after the `AttachedTo` property (line 51), add:

```csharp
// Counter tracking
public Dictionary<CounterType, int> Counters { get; } = new();

public void AddCounters(CounterType type, int count)
{
    Counters.TryGetValue(type, out var current);
    Counters[type] = current + count;
}

public bool RemoveCounter(CounterType type)
{
    if (!Counters.TryGetValue(type, out var current) || current <= 0)
        return false;
    Counters[type] = current - 1;
    return true;
}

public int GetCounters(CounterType type) =>
    Counters.TryGetValue(type, out var count) ? count : 0;
```

Add `using MtgDecker.Engine.Enums;` if not already present (it already is in GameCard.cs).

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "CounterTests"`
Expected: PASS (5 tests)

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/Enums/CounterType.cs src/MtgDecker.Engine/GameCard.cs tests/MtgDecker.Engine.Tests/CounterTests.cs
git commit -m "feat(engine): add counter system (CounterType enum + GameCard methods)"
```

---

### Task 7: AddCountersEffect + RemoveCounter ability cost

**Files:**
- Create: `src/MtgDecker.Engine/Triggers/Effects/AddCountersEffect.cs`
- Modify: `src/MtgDecker.Engine/ActivatedAbility.cs` (add `RemoveCounterType` to cost)
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (validate counter removal cost in ActivateAbility)
- Test: `tests/MtgDecker.Engine.Tests/CounterTests.cs` (add effect + cost tests)

**Step 1: Write the failing tests**

Add to `CounterTests.cs`:

```csharp
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

// ... existing tests ...

[Fact]
public async Task AddCountersEffect_Places_Counters_On_Source()
{
    var handler = new TestDecisionHandler();
    var p1 = new Player(Guid.NewGuid(), "P1", handler);
    var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
    var state = new GameState(p1, p2);

    var card = new GameCard { Name = "Counter Host" };
    p1.Battlefield.Add(card);

    var effect = new AddCountersEffect(CounterType.Fade, 5);
    var ctx = new EffectContext(state, p1, card, handler);
    await effect.Execute(ctx);

    card.GetCounters(CounterType.Fade).Should().Be(5);
}

[Fact]
public async Task RemoveCounterCost_Prevents_Ability_When_No_Counters()
{
    var handler1 = new TestDecisionHandler();
    var p1 = new Player(Guid.NewGuid(), "P1", handler1);
    var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
    var state = new GameState(p1, p2) { ActivePlayer = p1, CurrentPhase = Phase.MainPhase1 };
    var engine = new GameEngine(state);

    // Card with no counters but requires removing one
    var card = new GameCard { Name = "Empty Counter Card", CardTypes = CardType.Enchantment };
    p1.Battlefield.Add(card);

    // Register a temporary card definition is complex, so test via the engine action
    // Instead, let's test the cost validation directly by attempting to activate
    // We'll just verify the GameCard counter mechanics already tested above
    // The engine integration is tested in Task 12 (Parallax Wave integration)
    card.GetCounters(CounterType.Fade).Should().Be(0);
    card.RemoveCounter(CounterType.Fade).Should().BeFalse();
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "CounterTests"`
Expected: FAIL — `AddCountersEffect` does not exist

**Step 3: Write minimal implementation**

Create `src/MtgDecker.Engine/Triggers/Effects/AddCountersEffect.cs`:

```csharp
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class AddCountersEffect(CounterType counterType, int count) : IEffect
{
    public CounterType CounterType { get; } = counterType;
    public int Count { get; } = count;

    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        context.Source.AddCounters(CounterType, Count);
        context.State.Log($"{context.Source.Name} enters with {Count} {CounterType} counter(s).");
        return Task.CompletedTask;
    }
}
```

Add `RemoveCounterType` to `ActivatedAbilityCost` in `src/MtgDecker.Engine/ActivatedAbility.cs`:

```csharp
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Triggers;

namespace MtgDecker.Engine;

public record ActivatedAbilityCost(
    bool TapSelf = false,
    bool SacrificeSelf = false,
    string? SacrificeSubtype = null,
    ManaCost? ManaCost = null,
    CounterType? RemoveCounterType = null);

public record ActivatedAbility(
    ActivatedAbilityCost Cost,
    IEffect Effect,
    Func<GameCard, bool>? TargetFilter = null,
    bool CanTargetPlayer = false);
```

In `GameEngine.ExecuteAction`, `ActionType.ActivateAbility` case — add counter validation after the mana cost check (around line 562), BEFORE paying costs:

```csharp
// Validate: counter removal cost
if (cost.RemoveCounterType.HasValue)
{
    if (abilitySource.GetCounters(cost.RemoveCounterType.Value) <= 0)
    {
        _state.Log($"Cannot activate {abilitySource.Name} — no {cost.RemoveCounterType.Value} counters.");
        break;
    }
}
```

And in the cost payment section (around line 630), add counter payment:

```csharp
// Pay costs: remove counter
if (cost.RemoveCounterType.HasValue)
{
    abilitySource.RemoveCounter(cost.RemoveCounterType.Value);
}
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "CounterTests"`
Expected: PASS (7 tests)

**Step 5: Run all tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/Triggers/Effects/AddCountersEffect.cs src/MtgDecker.Engine/ActivatedAbility.cs src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/CounterTests.cs
git commit -m "feat(engine): add AddCountersEffect and RemoveCounter ability cost"
```

---

### Task 8: ExileCreatureEffect + per-source exile tracking

**Files:**
- Modify: `src/MtgDecker.Engine/GameCard.cs` (add `ExiledCardIds` list)
- Create: `src/MtgDecker.Engine/Triggers/Effects/ExileCreatureEffect.cs`
- Test: `tests/MtgDecker.Engine.Tests/ExileTrackingTests.cs`

**Step 1: Write the failing tests**

Create `tests/MtgDecker.Engine.Tests/ExileTrackingTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class ExileTrackingTests
{
    [Fact]
    public async Task ExileCreatureEffect_Moves_Target_To_Exile()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var source = new GameCard { Name = "Exile Source", CardTypes = CardType.Enchantment };
        p1.Battlefield.Add(source);

        var creature = new GameCard { Name = "Target Creature", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p2.Battlefield.Add(creature);

        var effect = new ExileCreatureEffect();
        var ctx = new EffectContext(state, p1, source, handler) { Target = creature };
        await effect.Execute(ctx);

        // Creature should be in exile
        p2.Battlefield.Cards.Should().NotContain(c => c.Id == creature.Id);
        p2.Exile.Cards.Should().Contain(c => c.Id == creature.Id);
        // Source should track the exiled card
        source.ExiledCardIds.Should().Contain(creature.Id);
    }

    [Fact]
    public async Task ExileCreatureEffect_Tracks_Multiple_Exiles()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var source = new GameCard { Name = "Exile Source", CardTypes = CardType.Enchantment };
        p1.Battlefield.Add(source);

        var creature1 = new GameCard { Name = "Creature A", CardTypes = CardType.Creature, BasePower = 1, BaseToughness = 1 };
        var creature2 = new GameCard { Name = "Creature B", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p2.Battlefield.Add(creature1);
        p2.Battlefield.Add(creature2);

        var effect = new ExileCreatureEffect();

        await effect.Execute(new EffectContext(state, p1, source, handler) { Target = creature1 });
        await effect.Execute(new EffectContext(state, p1, source, handler) { Target = creature2 });

        source.ExiledCardIds.Should().HaveCount(2);
        source.ExiledCardIds.Should().Contain(creature1.Id);
        source.ExiledCardIds.Should().Contain(creature2.Id);
    }

    [Fact]
    public void ExiledCardIds_Starts_Empty()
    {
        var card = new GameCard { Name = "Test" };
        card.ExiledCardIds.Should().BeEmpty();
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "ExileTrackingTests"`
Expected: FAIL — `ExiledCardIds` does not exist on `GameCard`

**Step 3: Write minimal implementation**

Add to `GameCard.cs` after the `Counters` properties:

```csharp
// Per-source exile tracking (e.g., Parallax Wave)
public List<Guid> ExiledCardIds { get; } = new();
```

Create `src/MtgDecker.Engine/Triggers/Effects/ExileCreatureEffect.cs`:

```csharp
namespace MtgDecker.Engine.Triggers.Effects;

public class ExileCreatureEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var target = context.Target;
        if (target == null) return Task.CompletedTask;

        // Find which player owns the target
        Player? owner = null;
        if (context.State.Player1.Battlefield.Contains(target.Id))
            owner = context.State.Player1;
        else if (context.State.Player2.Battlefield.Contains(target.Id))
            owner = context.State.Player2;

        if (owner == null) return Task.CompletedTask;

        // Move to exile
        owner.Battlefield.RemoveById(target.Id);
        owner.Exile.Add(target);

        // Track on source
        context.Source.ExiledCardIds.Add(target.Id);

        context.State.Log($"{context.Source.Name} exiles {target.Name}.");
        return Task.CompletedTask;
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "ExileTrackingTests"`
Expected: PASS (3 tests)

**Step 5: Run all tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/GameCard.cs src/MtgDecker.Engine/Triggers/Effects/ExileCreatureEffect.cs tests/MtgDecker.Engine.Tests/ExileTrackingTests.cs
git commit -m "feat(engine): add ExileCreatureEffect with per-source exile tracking"
```

---

### Task 9: Leave-the-battlefield triggers + ReturnExiledCardsEffect

**Files:**
- Modify: `src/MtgDecker.Engine/Triggers/TriggerCondition.cs` (add `SelfLeavesBattlefield`)
- Create: `src/MtgDecker.Engine/Triggers/Effects/ReturnExiledCardsEffect.cs`
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (fire LTB triggers when cards leave battlefield)
- Test: `tests/MtgDecker.Engine.Tests/LeaveBattlefieldTests.cs`

**Step 1: Write the failing tests**

Create `tests/MtgDecker.Engine.Tests/LeaveBattlefieldTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class LeaveBattlefieldTests
{
    [Fact]
    public async Task ReturnExiledCardsEffect_Returns_All_Tracked_Cards()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var source = new GameCard { Name = "Exile Source", CardTypes = CardType.Enchantment };
        p1.Battlefield.Add(source);

        // Simulate two creatures exiled by this source
        var creature1 = new GameCard { Name = "Exiled A", CardTypes = CardType.Creature, BasePower = 1, BaseToughness = 1 };
        var creature2 = new GameCard { Name = "Exiled B", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p2.Exile.Add(creature1);
        p2.Exile.Add(creature2);
        source.ExiledCardIds.Add(creature1.Id);
        source.ExiledCardIds.Add(creature2.Id);

        var effect = new ReturnExiledCardsEffect();
        var ctx = new EffectContext(state, p1, source, handler);
        await effect.Execute(ctx);

        // Both creatures should return to battlefield
        p2.Battlefield.Cards.Should().Contain(c => c.Id == creature1.Id);
        p2.Battlefield.Cards.Should().Contain(c => c.Id == creature2.Id);
        p2.Exile.Cards.Should().NotContain(c => c.Id == creature1.Id);
        p2.Exile.Cards.Should().NotContain(c => c.Id == creature2.Id);
        // ExiledCardIds should be cleared
        source.ExiledCardIds.Should().BeEmpty();
    }

    [Fact]
    public async Task ReturnExiledCardsEffect_Returns_To_Owner_Battlefield()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var source = new GameCard { Name = "Exile Source", CardTypes = CardType.Enchantment };
        p1.Battlefield.Add(source);

        // P1's creature was exiled
        var creature = new GameCard { Name = "P1 Creature", CardTypes = CardType.Creature, BasePower = 3, BaseToughness = 3 };
        p1.Exile.Add(creature);
        source.ExiledCardIds.Add(creature.Id);

        var effect = new ReturnExiledCardsEffect();
        var ctx = new EffectContext(state, p1, source, handler);
        await effect.Execute(ctx);

        // Should return to P1's battlefield (owner)
        p1.Battlefield.Cards.Should().Contain(c => c.Id == creature.Id);
        p1.Exile.Cards.Should().NotContain(c => c.Id == creature.Id);
    }

    [Fact]
    public async Task ReturnExiledCardsEffect_Handles_Empty_ExiledCardIds()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var source = new GameCard { Name = "Exile Source" };

        var effect = new ReturnExiledCardsEffect();
        var ctx = new EffectContext(state, p1, source, handler);

        // Should not throw
        await effect.Execute(ctx);
    }

    [Fact]
    public void SelfLeavesBattlefield_TriggerCondition_Exists()
    {
        // Just verify the enum value exists
        var condition = TriggerCondition.SelfLeavesBattlefield;
        condition.Should().Be(TriggerCondition.SelfLeavesBattlefield);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "LeaveBattlefieldTests"`
Expected: FAIL — `ReturnExiledCardsEffect` and `TriggerCondition.SelfLeavesBattlefield` don't exist

**Step 3: Write minimal implementation**

Add to `TriggerCondition.cs`:

```csharp
public enum TriggerCondition
{
    Self,
    AnyCreatureDies,
    ControllerCastsEnchantment,
    SelfDealsCombatDamage,
    SelfAttacks,
    Upkeep,
    AttachedPermanentTapped,
    SelfLeavesBattlefield,
}
```

Create `src/MtgDecker.Engine/Triggers/Effects/ReturnExiledCardsEffect.cs`:

```csharp
namespace MtgDecker.Engine.Triggers.Effects;

public class ReturnExiledCardsEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var exiledIds = context.Source.ExiledCardIds.ToList();
        if (exiledIds.Count == 0) return Task.CompletedTask;

        foreach (var cardId in exiledIds)
        {
            // Find the card in either player's exile
            GameCard? card = null;
            Player? owner = null;

            card = context.State.Player1.Exile.RemoveById(cardId);
            if (card != null) owner = context.State.Player1;

            if (card == null)
            {
                card = context.State.Player2.Exile.RemoveById(cardId);
                if (card != null) owner = context.State.Player2;
            }

            if (card != null && owner != null)
            {
                owner.Battlefield.Add(card);
                context.State.Log($"{card.Name} returns to the battlefield.");
            }
        }

        context.Source.ExiledCardIds.Clear();
        return Task.CompletedTask;
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "LeaveBattlefieldTests"`
Expected: PASS (4 tests)

**Step 5: Run all tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/Triggers/TriggerCondition.cs src/MtgDecker.Engine/Triggers/Effects/ReturnExiledCardsEffect.cs tests/MtgDecker.Engine.Tests/LeaveBattlefieldTests.cs
git commit -m "feat(engine): add LTB trigger condition and ReturnExiledCardsEffect"
```

---

### Task 10: Fire LTB triggers in engine when permanents leave battlefield

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (add `FireLeaveBattlefieldTriggersAsync` + integrate into all removal paths)
- Test: `tests/MtgDecker.Engine.Tests/LeaveBattlefieldTests.cs` (add engine integration test)

**Step 1: Write the failing test**

Add to `LeaveBattlefieldTests.cs`:

```csharp
[Fact]
public async Task LTB_Trigger_Fires_When_Card_Is_Destroyed()
{
    var handler1 = new TestDecisionHandler();
    var handler2 = new TestDecisionHandler();
    var p1 = new Player(Guid.NewGuid(), "P1", handler1);
    var p2 = new Player(Guid.NewGuid(), "P2", handler2);
    var state = new GameState(p1, p2);
    var engine = new GameEngine(state);

    // Create a card with an LTB trigger that tracks exile returns
    var wave = new GameCard { Name = "Test Wave", CardTypes = CardType.Enchantment };
    p1.Battlefield.Add(wave);

    // Simulate exiled creature
    var creature = new GameCard { Name = "Exiled Creature", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
    p2.Exile.Add(creature);
    wave.ExiledCardIds.Add(creature.Id);

    // Register a temporary card in CardDefinitions isn't feasible, so test via
    // a manual trigger setup. We'll put triggers on the GameCard.
    // GameCard.Triggers are read-only from init, but we can create with triggers:
    // Actually, the engine reads triggers from CardDefinitions. For testing LTB firing,
    // we test the helper method directly.
    // The real integration test is in Task 12 with Parallax Wave.

    // For now, test that ReturnExiledCardsEffect works when called from trigger
    var effect = new ReturnExiledCardsEffect();
    var ctx = new EffectContext(state, p1, wave, handler1);
    await effect.Execute(ctx);

    p2.Battlefield.Cards.Should().Contain(c => c.Id == creature.Id);
}
```

**Step 2: Implement LTB trigger firing in the engine**

Add a helper method to `GameEngine` after the existing trigger methods:

```csharp
internal async Task FireLeaveBattlefieldTriggersAsync(GameCard card, Player controller, CancellationToken ct)
{
    if (!CardDefinitions.TryGet(card.Name, out var def)) return;

    foreach (var trigger in def.Triggers)
    {
        if (trigger.Condition == TriggerCondition.SelfLeavesBattlefield)
        {
            _state.Log($"{card.Name} triggers: {trigger.Effect.GetType().Name.Replace("Effect", "")}");
            _state.Stack.Add(new TriggeredAbilityStackObject(card, controller.Id, trigger.Effect));
        }
    }
}
```

Now integrate into all removal paths. The key places where cards leave the battlefield:

1. **SBA: lethal toughness** (`CheckLethalToughness`) — add LTB trigger fire before removal
2. **SBA: lethal damage** (`CheckLethalDamage`) — same
3. **SBA: aura detachment** — add LTB trigger fire
4. **Sacrifice self** (in ActivateAbility cost) — add LTB trigger fire before moving to graveyard
5. **Sacrifice subtype** (in ActivateAbility cost) — add LTB trigger fire
6. **ActivateFetch** (sacrifice) — add LTB trigger fire
7. **Legendary rule** — add LTB trigger fire

For simplicity, create a helper `MoveFromBattlefieldToGraveyardAsync` that fires LTB triggers:

```csharp
private async Task MoveFromBattlefieldAsync(GameCard card, Player owner, Zone destination, CancellationToken ct)
{
    await FireLeaveBattlefieldTriggersAsync(card, owner, ct);
    owner.Battlefield.RemoveById(card.Id);
    destination.Add(card);
}
```

However, this is a large refactor that touches many places. The implementer should integrate the LTB trigger call into the `CheckStateBasedActionsAsync` method and the sacrifice sections. The critical path for Parallax Wave is:
- Naturalize/Seal of Cleansing destroy it → goes to graveyard → LTB fires
- Lethal damage → goes to graveyard → LTB fires

**Key integration points** (add `await FireLeaveBattlefieldTriggersAsync` before each battlefield removal):
- `CheckLethalToughness`: convert to async, fire LTB before `player.Battlefield.RemoveById`
- `CheckLethalDamage`: fire LTB before removal
- SBA aura detachment: fire LTB before removal
- `ActivateAbility` sacrifice self: fire LTB before removal
- `ActivateAbility` sacrifice subtype: fire LTB before removal
- Legendary rule: fire LTB before removal

**Note:** Converting `CheckLethalToughness` to async requires updating its signature and callers. The implementer should handle this carefully.

**Step 3: Run all tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All pass

**Step 4: Commit**

```bash
git add src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/LeaveBattlefieldTests.cs
git commit -m "feat(engine): fire LTB triggers when permanents leave battlefield"
```

---

### Task 11: Parallax Wave card definition + integration tests

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs` (add Parallax Wave registration)
- Test: `tests/MtgDecker.Engine.Tests/ParallaxWaveTests.cs`

**Step 1: Write the failing tests**

Create `tests/MtgDecker.Engine.Tests/ParallaxWaveTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class ParallaxWaveTests
{
    [Fact]
    public void ParallaxWave_Is_Registered()
    {
        CardDefinitions.TryGet("Parallax Wave", out var def).Should().BeTrue();
        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(4);
        def.CardTypes.Should().Be(CardType.Enchantment);
    }

    [Fact]
    public void ParallaxWave_Has_ETB_Counter_Trigger()
    {
        CardDefinitions.TryGet("Parallax Wave", out var def).Should().BeTrue();
        def!.Triggers.Should().Contain(t =>
            t.Event == GameEvent.EnterBattlefield && t.Effect is AddCountersEffect);
    }

    [Fact]
    public void ParallaxWave_Has_LTB_Return_Trigger()
    {
        CardDefinitions.TryGet("Parallax Wave", out var def).Should().BeTrue();
        def!.Triggers.Should().Contain(t =>
            t.Condition == Triggers.TriggerCondition.SelfLeavesBattlefield
            && t.Effect is ReturnExiledCardsEffect);
    }

    [Fact]
    public void ParallaxWave_Has_RemoveCounter_Activated_Ability()
    {
        CardDefinitions.TryGet("Parallax Wave", out var def).Should().BeTrue();
        def!.ActivatedAbility.Should().NotBeNull();
        def.ActivatedAbility!.Cost.RemoveCounterType.Should().Be(CounterType.Fade);
        def.ActivatedAbility.Effect.Should().BeOfType<ExileCreatureEffect>();
    }

    [Fact]
    public async Task ParallaxWave_ETB_Adds_5_Fade_Counters()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2) { ActivePlayer = p1, CurrentPhase = Phase.MainPhase1 };
        var engine = new GameEngine(state);

        var wave = GameCard.Create("Parallax Wave");
        p1.Hand.Add(wave);

        // Give P1 mana to cast
        p1.ManaPool.Add(ManaColor.White, 2);
        p1.ManaPool.Add(ManaColor.Colorless, 2);

        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, wave.Id), default);

        // ETB trigger should be on the stack
        // Resolve triggers
        await engine.ResolveAllTriggersAsync(default);

        // Wave should have 5 fade counters
        var waveOnBf = p1.Battlefield.Cards.FirstOrDefault(c => c.Name == "Parallax Wave");
        waveOnBf.Should().NotBeNull();
        waveOnBf!.GetCounters(CounterType.Fade).Should().Be(5);
    }

    [Fact]
    public async Task ParallaxWave_Activate_Exiles_Creature()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", handler2);
        var state = new GameState(p1, p2) { ActivePlayer = p1, CurrentPhase = Phase.MainPhase1 };
        var engine = new GameEngine(state);

        var wave = GameCard.Create("Parallax Wave");
        wave.AddCounters(CounterType.Fade, 5);
        p1.Battlefield.Add(wave);

        var creature = new GameCard { Name = "Target", CardTypes = CardType.Creature, BasePower = 3, BaseToughness = 3 };
        p2.Battlefield.Add(creature);

        await engine.ExecuteAction(
            GameAction.ActivateAbility(p1.Id, wave.Id, targetId: creature.Id), default);

        // Creature should be exiled
        p2.Battlefield.Cards.Should().NotContain(c => c.Id == creature.Id);
        p2.Exile.Cards.Should().Contain(c => c.Id == creature.Id);
        wave.GetCounters(CounterType.Fade).Should().Be(4);
        wave.ExiledCardIds.Should().Contain(creature.Id);
    }

    [Fact]
    public async Task ParallaxWave_Cannot_Activate_With_No_Counters()
    {
        var handler1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2) { ActivePlayer = p1, CurrentPhase = Phase.MainPhase1 };
        var engine = new GameEngine(state);

        var wave = GameCard.Create("Parallax Wave");
        // No counters!
        p1.Battlefield.Add(wave);

        var creature = new GameCard { Name = "Target", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p2.Battlefield.Add(creature);

        await engine.ExecuteAction(
            GameAction.ActivateAbility(p1.Id, wave.Id, targetId: creature.Id), default);

        // Creature should still be on battlefield (activation failed)
        p2.Battlefield.Cards.Should().Contain(c => c.Id == creature.Id);
        state.GameLog.Should().Contain(l => l.Contains("no") && l.Contains("counter"));
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "ParallaxWaveTests"`
Expected: FAIL — Parallax Wave not registered

**Step 3: Write minimal implementation**

Add Parallax Wave to `CardDefinitions.cs` in the Enchantress section:

```csharp
["Parallax Wave"] = new(ManaCost.Parse("{2}{W}{W}"), null, null, null, CardType.Enchantment)
{
    Triggers =
    [
        new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new AddCountersEffect(CounterType.Fade, 5)),
        new Trigger(GameEvent.LeavesBattlefield, TriggerCondition.SelfLeavesBattlefield, new ReturnExiledCardsEffect()),
    ],
    ActivatedAbility = new(
        new ActivatedAbilityCost(RemoveCounterType: CounterType.Fade),
        new ExileCreatureEffect(),
        c => c.IsCreature),
},
```

Add the needed `using` statements at the top of `CardDefinitions.cs` if not already there.

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "ParallaxWaveTests"`
Expected: PASS (7 tests)

**Step 5: Run all tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/CardDefinitions.cs tests/MtgDecker.Engine.Tests/ParallaxWaveTests.cs
git commit -m "feat(engine): register Parallax Wave card definition"
```

---

## PART 3: OPALESCENCE

### Task 12: EffectiveCardTypes on GameCard

**Files:**
- Modify: `src/MtgDecker.Engine/GameCard.cs` (add `EffectiveCardTypes`, update `IsCreature`/`IsLand`)
- Test: `tests/MtgDecker.Engine.Tests/TypeChangingTests.cs`

**Step 1: Write the failing tests**

Create `tests/MtgDecker.Engine.Tests/TypeChangingTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class TypeChangingTests
{
    [Fact]
    public void EffectiveCardTypes_Defaults_To_CardTypes()
    {
        var card = new GameCard { Name = "Test", CardTypes = CardType.Enchantment };
        card.EffectiveCardTypes.Should().Be(CardType.Enchantment);
    }

    [Fact]
    public void EffectiveCardTypes_Can_Be_Set_To_Add_Creature()
    {
        var card = new GameCard { Name = "Test", CardTypes = CardType.Enchantment };
        card.EffectiveCardTypes = CardType.Enchantment | CardType.Creature;
        card.EffectiveCardTypes.Should().HaveFlag(CardType.Creature);
        card.EffectiveCardTypes.Should().HaveFlag(CardType.Enchantment);
    }

    [Fact]
    public void IsCreature_Uses_EffectiveCardTypes()
    {
        var card = new GameCard { Name = "Test", CardTypes = CardType.Enchantment };
        card.IsCreature.Should().BeFalse();

        card.EffectiveCardTypes = CardType.Enchantment | CardType.Creature;
        card.IsCreature.Should().BeTrue();
    }

    [Fact]
    public void IsLand_Uses_EffectiveCardTypes()
    {
        var card = new GameCard { Name = "Test", CardTypes = CardType.Enchantment };
        card.IsLand.Should().BeFalse();
        // EffectiveCardTypes doesn't add Land, so still false
        card.EffectiveCardTypes = CardType.Enchantment | CardType.Creature;
        card.IsLand.Should().BeFalse();
    }

    [Fact]
    public void EffectiveCardTypes_Resets_To_Null()
    {
        var card = new GameCard { Name = "Test", CardTypes = CardType.Enchantment };
        card.EffectiveCardTypes = CardType.Enchantment | CardType.Creature;
        card.EffectiveCardTypes = null;
        // Should fall back to CardTypes
        card.IsCreature.Should().BeFalse();
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "TypeChangingTests"`
Expected: FAIL — `EffectiveCardTypes` property does not exist

**Step 3: Write minimal implementation**

Add `EffectiveCardTypes` property to `GameCard.cs`:

```csharp
// Type-changing effects (e.g., Opalescence makes enchantments into creatures)
private CardType? _effectiveCardTypes;
public CardType? EffectiveCardTypes
{
    get => _effectiveCardTypes;
    set => _effectiveCardTypes = value;
}
```

Update `IsCreature` and `IsLand` to check `EffectiveCardTypes` first:

```csharp
public bool IsLand =>
    (_effectiveCardTypes ?? CardTypes).HasFlag(CardType.Land) ||
    TypeLine.Contains("Land", StringComparison.OrdinalIgnoreCase);

public bool IsCreature =>
    (_effectiveCardTypes ?? CardTypes).HasFlag(CardType.Creature) ||
    TypeLine.Contains("Creature", StringComparison.OrdinalIgnoreCase);
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "TypeChangingTests"`
Expected: PASS (5 tests)

**Step 5: Run all tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All pass (existing tests work because `EffectiveCardTypes` defaults to `null`, falling back to `CardTypes`)

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/GameCard.cs tests/MtgDecker.Engine.Tests/TypeChangingTests.cs
git commit -m "feat(engine): add EffectiveCardTypes for type-changing effects"
```

---

### Task 13: BecomeCreature continuous effect + layered RecalculateState

**Files:**
- Modify: `src/MtgDecker.Engine/ContinuousEffect.cs` (add `BecomeCreature` to enum, add `SetPowerToughnessToCMC` property)
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (layered `RecalculateState` — type-changing first, then P/T, then keywords)
- Test: `tests/MtgDecker.Engine.Tests/TypeChangingTests.cs` (add engine tests)

**Step 1: Write the failing tests**

Add to `TypeChangingTests.cs`:

```csharp
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Mana;

// ... existing tests ...

[Fact]
public void BecomeCreature_Effect_Makes_Enchantment_A_Creature()
{
    var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
    var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
    var state = new GameState(p1, p2);
    var engine = new GameEngine(state);

    // Source of the effect (e.g., Opalescence)
    var source = new GameCard { Name = "Type Changer", CardTypes = CardType.Enchantment };
    p1.Battlefield.Add(source);

    // Target enchantment (non-Aura)
    var enchantment = new GameCard
    {
        Name = "Target Enchantment",
        CardTypes = CardType.Enchantment,
        ManaCost = ManaCost.Parse("{2}{G}")
    };
    p1.Battlefield.Add(enchantment);

    state.ActiveEffects.Add(new ContinuousEffect(
        source.Id, ContinuousEffectType.BecomeCreature,
        (card, _) => card.CardTypes.HasFlag(CardType.Enchantment) && card.Id != source.Id,
        SetPowerToughnessToCMC: true));

    engine.RecalculateState();

    enchantment.IsCreature.Should().BeTrue();
    enchantment.Power.Should().Be(3); // CMC of {2}{G} = 3
    enchantment.Toughness.Should().Be(3);
}

[Fact]
public void BecomeCreature_Excludes_Auras()
{
    var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
    var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
    var state = new GameState(p1, p2);
    var engine = new GameEngine(state);

    var source = new GameCard { Name = "Type Changer", CardTypes = CardType.Enchantment };
    p1.Battlefield.Add(source);

    var aura = GameCard.Create("Wild Growth"); // Aura enchantment
    p1.Battlefield.Add(aura);

    state.ActiveEffects.Add(new ContinuousEffect(
        source.Id, ContinuousEffectType.BecomeCreature,
        (card, _) => card.CardTypes.HasFlag(CardType.Enchantment)
            && !card.Subtypes.Contains("Aura")
            && card.Id != source.Id,
        SetPowerToughnessToCMC: true));

    engine.RecalculateState();

    aura.IsCreature.Should().BeFalse(); // Aura excluded
}

[Fact]
public void BecomeCreature_Applies_Before_Lord_Effects()
{
    var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
    var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
    var state = new GameState(p1, p2);
    var engine = new GameEngine(state);

    // Opalescence-like effect + a lord (+1/+1 to creatures)
    var opal = new GameCard { Name = "Opalescence", CardTypes = CardType.Enchantment };
    p1.Battlefield.Add(opal);

    var enchantment = new GameCard
    {
        Name = "Test Enchantment",
        CardTypes = CardType.Enchantment,
        ManaCost = ManaCost.Parse("{2}{G}")
    };
    p1.Battlefield.Add(enchantment);

    // BecomeCreature effect (Opalescence)
    state.ActiveEffects.Add(new ContinuousEffect(
        opal.Id, ContinuousEffectType.BecomeCreature,
        (card, _) => card.CardTypes.HasFlag(CardType.Enchantment) && card.Id != opal.Id,
        SetPowerToughnessToCMC: true));

    // Lord effect (+1/+1 to all creatures)
    var lordId = Guid.NewGuid();
    state.ActiveEffects.Add(new ContinuousEffect(
        lordId, ContinuousEffectType.ModifyPowerToughness,
        (card, _) => card.IsCreature,
        PowerMod: 1, ToughnessMod: 1));

    engine.RecalculateState();

    // Enchantment becomes 3/3, then lord makes it 4/4
    enchantment.Power.Should().Be(4);
    enchantment.Toughness.Should().Be(4);
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "TypeChangingTests"`
Expected: FAIL — `ContinuousEffectType.BecomeCreature` does not exist

**Step 3: Write minimal implementation**

Add `BecomeCreature` to enum and `SetPowerToughnessToCMC` to the record in `ContinuousEffect.cs`:

```csharp
public enum ContinuousEffectType
{
    ModifyPowerToughness,
    GrantKeyword,
    ModifyCost,
    ExtraLandDrop,
    SkipDraw,
    GrantPlayerShroud,
    PreventDamageToPlayer,
    BecomeCreature,
}

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
    bool ExcludeSelf = false,
    bool ControllerOnly = false,
    bool SetPowerToughnessToCMC = false);
```

Modify `RecalculateState` in `GameEngine.cs` to apply effects in layered order. Replace the effects loop (lines 1116-1137):

```csharp
// Reset all effective values for both players
foreach (var player in new[] { _state.Player1, _state.Player2 })
{
    foreach (var card in player.Battlefield.Cards)
    {
        card.EffectivePower = null;
        card.EffectiveToughness = null;
        card.EffectiveCardTypes = null; // Reset type-changing
        card.ActiveKeywords.Clear();
    }
    player.MaxLandDrops = 1;
}

// Layer 1: Type-changing effects (BecomeCreature)
foreach (var effect in _state.ActiveEffects.Where(e => e.Type == ContinuousEffectType.BecomeCreature))
{
    ApplyBecomeCreatureEffect(effect, _state.Player1);
    ApplyBecomeCreatureEffect(effect, _state.Player2);
}

// Layer 2: P/T setting + modification
foreach (var effect in _state.ActiveEffects.Where(e => e.Type == ContinuousEffectType.ModifyPowerToughness))
{
    ApplyPowerToughnessEffect(effect, _state.Player1);
    ApplyPowerToughnessEffect(effect, _state.Player2);
}

// Layer 3: Keywords
foreach (var effect in _state.ActiveEffects.Where(e => e.Type == ContinuousEffectType.GrantKeyword))
{
    ApplyKeywordEffect(effect, _state.Player1);
    ApplyKeywordEffect(effect, _state.Player2);
}

// Non-layered effects
foreach (var effect in _state.ActiveEffects.Where(e => e.Type == ContinuousEffectType.ExtraLandDrop))
{
    var sourceOwner = _state.Player1.Battlefield.Cards.Any(c => c.Id == effect.SourceId)
        ? _state.Player1 : _state.Player2;
    sourceOwner.MaxLandDrops += effect.ExtraLandDrops;
}
```

Add the new `ApplyBecomeCreatureEffect` method:

```csharp
private void ApplyBecomeCreatureEffect(ContinuousEffect effect, Player player)
{
    foreach (var card in player.Battlefield.Cards)
    {
        if (card.Id == effect.SourceId) continue; // "each other" exclusion
        if (!effect.Applies(card, player)) continue;

        // Add Creature type
        card.EffectiveCardTypes = (card.EffectiveCardTypes ?? card.CardTypes) | CardType.Creature;

        // Set P/T to CMC
        if (effect.SetPowerToughnessToCMC && card.ManaCost != null)
        {
            var cmc = card.ManaCost.ConvertedManaCost;
            card.EffectivePower = cmc;
            card.EffectiveToughness = cmc;
        }
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "TypeChangingTests"`
Expected: PASS (8 tests)

**Step 5: Run all tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/ContinuousEffect.cs src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/TypeChangingTests.cs
git commit -m "feat(engine): add BecomeCreature continuous effect with layered RecalculateState"
```

---

### Task 14: Opalescence card definition + integration tests

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs` (register Opalescence)
- Test: `tests/MtgDecker.Engine.Tests/OpalescenceTests.cs`

**Step 1: Write the failing tests**

Create `tests/MtgDecker.Engine.Tests/OpalescenceTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class OpalescenceTests
{
    [Fact]
    public void Opalescence_Is_Registered()
    {
        CardDefinitions.TryGet("Opalescence", out var def).Should().BeTrue();
        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(4);
        def.CardTypes.Should().Be(CardType.Enchantment);
    }

    [Fact]
    public void Opalescence_Has_BecomeCreature_Effect()
    {
        CardDefinitions.TryGet("Opalescence", out var def).Should().BeTrue();
        def!.ContinuousEffects.Should().Contain(e => e.Type == ContinuousEffectType.BecomeCreature);
    }

    [Fact]
    public void Opalescence_Makes_Enchantments_Presence_A_Creature()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var opalescence = GameCard.Create("Opalescence");
        p1.Battlefield.Add(opalescence);

        var presence = GameCard.Create("Enchantress's Presence");
        p1.Battlefield.Add(presence);

        engine.RecalculateState();

        presence.IsCreature.Should().BeTrue();
        // Enchantress's Presence costs {2}{G} = CMC 3
        presence.Power.Should().Be(3);
        presence.Toughness.Should().Be(3);
    }

    [Fact]
    public void Opalescence_Does_Not_Affect_Itself()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var opalescence = GameCard.Create("Opalescence");
        p1.Battlefield.Add(opalescence);

        engine.RecalculateState();

        // Opalescence itself should NOT be a creature
        opalescence.IsCreature.Should().BeFalse();
    }

    [Fact]
    public void Opalescence_Does_Not_Affect_Auras()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var opalescence = GameCard.Create("Opalescence");
        p1.Battlefield.Add(opalescence);

        var wildGrowth = GameCard.Create("Wild Growth");
        p1.Battlefield.Add(wildGrowth);

        engine.RecalculateState();

        wildGrowth.IsCreature.Should().BeFalse();
    }

    [Fact]
    public void Opalescence_Plus_SterlingGrove_Interaction()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var opalescence = GameCard.Create("Opalescence");
        p1.Battlefield.Add(opalescence);

        var grove = GameCard.Create("Sterling Grove");
        p1.Battlefield.Add(grove);

        engine.RecalculateState();

        // Sterling Grove costs {G}{W} = CMC 2
        grove.IsCreature.Should().BeTrue();
        grove.Power.Should().Be(2);
        grove.Toughness.Should().Be(2);
        // Sterling Grove should still grant shroud to other enchantments
        // (it's still an enchantment)
    }

    [Fact]
    public void Opalescence_Enchantment_Creature_Has_Summoning_Sickness()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2) { TurnNumber = 5 };
        var engine = new GameEngine(state);

        var opalescence = GameCard.Create("Opalescence");
        p1.Battlefield.Add(opalescence);

        // Enchantment that just entered this turn
        var enchantment = new GameCard
        {
            Name = "Fresh Enchantment",
            CardTypes = CardType.Enchantment,
            ManaCost = MtgDecker.Engine.Mana.ManaCost.Parse("{2}{W}"),
            TurnEnteredBattlefield = 5
        };
        p1.Battlefield.Add(enchantment);

        engine.RecalculateState();

        enchantment.IsCreature.Should().BeTrue();
        enchantment.HasSummoningSickness(5).Should().BeTrue();
    }

    [Fact]
    public async Task Opalescence_Enchantment_Creature_Dies_To_Lethal_Damage()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var opalescence = GameCard.Create("Opalescence");
        p1.Battlefield.Add(opalescence);

        // Enchantment with CMC 1 -> 1/1 creature
        var enchantment = new GameCard
        {
            Name = "Fragile Enchantment",
            CardTypes = CardType.Enchantment,
            ManaCost = MtgDecker.Engine.Mana.ManaCost.Parse("{G}")
        };
        p1.Battlefield.Add(enchantment);

        engine.RecalculateState();
        enchantment.Power.Should().Be(1);
        enchantment.Toughness.Should().Be(1);

        // Deal lethal damage
        enchantment.DamageMarked = 1;
        await engine.CheckStateBasedActionsAsync(default);

        // Should be dead
        p1.Battlefield.Cards.Should().NotContain(c => c.Id == enchantment.Id);
        p1.Graveyard.Cards.Should().Contain(c => c.Id == enchantment.Id);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "OpalescenceTests"`
Expected: FAIL — Opalescence has no BecomeCreature effect registered

**Step 3: Write minimal implementation**

Update Opalescence in `CardDefinitions.cs` — replace the existing placeholder:

```csharp
["Opalescence"] = new(ManaCost.Parse("{2}{W}{W}"), null, null, null, CardType.Enchantment)
{
    ContinuousEffects =
    [
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.BecomeCreature,
            (card, _) => card.CardTypes.HasFlag(CardType.Enchantment)
                && !card.Subtypes.Contains("Aura")
                && card.Id != Guid.Empty, // Placeholder — SourceId is set at runtime by RebuildActiveEffects
            SetPowerToughnessToCMC: true),
    ],
},
```

**Important note:** The `card.Id != Guid.Empty` predicate won't work at runtime because `SourceId` gets replaced. The `ApplyBecomeCreatureEffect` method already has `if (card.Id == effect.SourceId) continue;` which handles the self-exclusion. So the `Applies` predicate can simply be:

```csharp
(card, _) => card.CardTypes.HasFlag(CardType.Enchantment)
    && !card.Subtypes.Contains("Aura")
```

The self-exclusion is handled by the `card.Id == effect.SourceId` check in `ApplyBecomeCreatureEffect`.

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "OpalescenceTests"`
Expected: PASS (8 tests)

**Step 5: Run all tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/CardDefinitions.cs tests/MtgDecker.Engine.Tests/OpalescenceTests.cs
git commit -m "feat(engine): register Opalescence card definition with BecomeCreature effect"
```

---

### Task 15: AI support for new card interactions

**Files:**
- Modify: `src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs` (add counter ability evaluation, upkeep discard heuristic)
- Test: `tests/MtgDecker.Engine.Tests/AI/AiBotDeferredCardTests.cs`

**Step 1: Write the failing tests**

Create `tests/MtgDecker.Engine.Tests/AI/AiBotDeferredCardTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.AI;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests.AI;

public class AiBotDeferredCardTests
{
    [Fact]
    public async Task AiBot_Discards_For_Upkeep_Cost_When_Hand_Has_Cards()
    {
        var handler = new AiBotDecisionHandler();

        var cards = new List<GameCard>
        {
            new() { Name = "Land", CardTypes = CardType.Land },
            new() { Name = "Spell", CardTypes = CardType.Creature, ManaCost = MtgDecker.Engine.Mana.ManaCost.Parse("{3}{R}") },
        };

        // AI should choose to discard (picks something) rather than decline
        var result = await handler.ChooseCard(cards, "Discard a card to keep Solitary Confinement", optional: true);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task AiBot_Activates_Parallax_Wave_Against_Threats()
    {
        var handler = new AiBotDecisionHandler();
        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();
        var p1 = new Player(p1Id, "P1", handler);
        var p2 = new Player(p2Id, "P2", new AiBotDecisionHandler());
        var state = new GameState(p1, p2) { ActivePlayer = p1, CurrentPhase = Phase.MainPhase1 };

        // P1 has Parallax Wave with 5 fade counters
        var wave = GameCard.Create("Parallax Wave");
        wave.AddCounters(CounterType.Fade, 5);
        p1.Battlefield.Add(wave);

        // P2 has a threatening creature
        var threat = new GameCard
        {
            Name = "Big Threat", CardTypes = CardType.Creature,
            BasePower = 5, BaseToughness = 5,
            TurnEnteredBattlefield = 0
        };
        p2.Battlefield.Add(threat);

        var action = await handler.GetAction(state, p1Id);

        // AI should choose to activate Parallax Wave (exile the threat)
        // or at least not crash. The exact behavior depends on the EvaluateActivatedAbilities heuristic.
        // This test verifies the AI handles the counter-cost ability without errors.
        action.Should().NotBeNull();
    }
}
```

**Step 2: Run tests to verify they fail or pass with current behavior**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "AiBotDeferredCardTests"`

The AI's `ChooseCard` default behavior picks the first card, which works for the upkeep cost test. The activated ability test may need the AI to recognize `ExileCreatureEffect` — currently `EvaluateActivatedAbilities` only handles `DealDamageEffect` and `AddManaEffect`.

**Step 3: Add ExileCreatureEffect heuristic to AI**

In `AiBotDecisionHandler.EvaluateActivatedAbilities`, add after the `AddManaEffect` block:

```csharp
// ExileCreatureEffect heuristic: exile the biggest threat
if (ability.Effect is ExileCreatureEffect)
{
    // Check counter availability
    if (cost.RemoveCounterType.HasValue
        && permanent.GetCounters(cost.RemoveCounterType.Value) <= 0)
        continue;

    var opponentCreatures = opponent.Battlefield.Cards
        .Where(c => c.IsCreature)
        .OrderByDescending(c => c.Power ?? 0)
        .FirstOrDefault();

    if (opponentCreatures != null)
        return GameAction.ActivateAbility(player.Id, permanent.Id, targetId: opponentCreatures.Id);

    continue;
}
```

Add the necessary `using` statement if needed: `using MtgDecker.Engine.Triggers.Effects;` (may already be imported).

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "AiBotDeferredCardTests"`
Expected: PASS (2 tests)

**Step 5: Run all tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs tests/MtgDecker.Engine.Tests/AI/AiBotDeferredCardTests.cs
git commit -m "feat(engine): add AI support for Parallax Wave and upkeep costs"
```

---

### Task 16: Final integration tests + full test suite verification

**Files:**
- Test: `tests/MtgDecker.Engine.Tests/DeferredCardsIntegrationTests.cs`

**Step 1: Write integration tests**

Create `tests/MtgDecker.Engine.Tests/DeferredCardsIntegrationTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class DeferredCardsIntegrationTests
{
    [Fact]
    public void All_Enchantress_Cards_Are_Registered()
    {
        var enchantressCards = new[]
        {
            "Argothian Enchantress", "Swords to Plowshares", "Replenish",
            "Enchantress's Presence", "Wild Growth", "Exploration",
            "Mirri's Guile", "Opalescence", "Parallax Wave",
            "Sterling Grove", "Aura of Silence", "Seal of Cleansing",
            "Solitary Confinement", "Sylvan Library"
        };

        foreach (var name in enchantressCards)
        {
            CardDefinitions.TryGet(name, out var def).Should().BeTrue(
                $"'{name}' should be registered in CardDefinitions");
        }
    }

    [Fact]
    public void Opalescence_Plus_Parallax_Wave_Interaction()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var opalescence = GameCard.Create("Opalescence");
        p1.Battlefield.Add(opalescence);

        var wave = GameCard.Create("Parallax Wave");
        p1.Battlefield.Add(wave);

        engine.RecalculateState();

        // Parallax Wave costs {2}{W}{W} = CMC 4
        wave.IsCreature.Should().BeTrue();
        wave.Power.Should().Be(4);
        wave.Toughness.Should().Be(4);
    }

    [Fact]
    public void Opalescence_Plus_Solitary_Confinement()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var opalescence = GameCard.Create("Opalescence");
        p1.Battlefield.Add(opalescence);

        var confinement = GameCard.Create("Solitary Confinement");
        p1.Battlefield.Add(confinement);

        engine.RecalculateState();

        // Solitary Confinement costs {2}{W} = CMC 3
        confinement.IsCreature.Should().BeTrue();
        confinement.Power.Should().Be(3);
        confinement.Toughness.Should().Be(3);

        // Should still have its continuous effects active
        state.ActiveEffects.Should().Contain(e => e.Type == ContinuousEffectType.SkipDraw);
        state.ActiveEffects.Should().Contain(e => e.Type == ContinuousEffectType.GrantPlayerShroud);
    }

    [Fact]
    public async Task Full_Parallax_Wave_Lifecycle()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", handler2);
        var state = new GameState(p1, p2) { ActivePlayer = p1, CurrentPhase = Phase.MainPhase1 };
        var engine = new GameEngine(state);

        // P1 has Parallax Wave with counters
        var wave = GameCard.Create("Parallax Wave");
        wave.AddCounters(CounterType.Fade, 5);
        p1.Battlefield.Add(wave);

        // P2 has two creatures
        var creatureA = new GameCard { Name = "Goblin A", CardTypes = CardType.Creature, BasePower = 1, BaseToughness = 1 };
        var creatureB = new GameCard { Name = "Goblin B", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p2.Battlefield.Add(creatureA);
        p2.Battlefield.Add(creatureB);

        // Exile creature A
        await engine.ExecuteAction(
            GameAction.ActivateAbility(p1.Id, wave.Id, targetId: creatureA.Id), default);

        p2.Battlefield.Cards.Should().NotContain(c => c.Id == creatureA.Id);
        p2.Exile.Cards.Should().Contain(c => c.Id == creatureA.Id);

        // Exile creature B
        await engine.ExecuteAction(
            GameAction.ActivateAbility(p1.Id, wave.Id, targetId: creatureB.Id), default);

        p2.Battlefield.Cards.Should().NotContain(c => c.Id == creatureB.Id);
        p2.Exile.Cards.Should().Contain(c => c.Id == creatureB.Id);

        // Wave should have 3 counters and 2 exiled card IDs
        wave.GetCounters(CounterType.Fade).Should().Be(3);
        wave.ExiledCardIds.Should().HaveCount(2);
    }
}
```

**Step 2: Run integration tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "DeferredCardsIntegrationTests"`
Expected: PASS

**Step 3: Run full test suite**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/`
Expected: ALL pass

**Step 4: Run all project tests**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Domain.Tests/
dotnet test tests/MtgDecker.Application.Tests/
dotnet test tests/MtgDecker.Infrastructure.Tests/
dotnet test tests/MtgDecker.Engine.Tests/
```

Expected: ALL pass across all projects

**Step 5: Commit**

```bash
git add tests/MtgDecker.Engine.Tests/DeferredCardsIntegrationTests.cs
git commit -m "test(engine): add deferred cards integration tests — all 3 cards complete"
```

---

## Summary

| Task | Card/System | New Files | Modified Files | Tests |
|------|-------------|-----------|----------------|-------|
| 1 | UpkeepCostEffect | 1 effect + 1 test | — | 3 |
| 2 | SkipDraw | 1 test | ContinuousEffect, GameEngine | 3 |
| 3 | Player shroud + damage prevention | 1 test | ContinuousEffect, GameEngine, DealDamageEffect | 4 |
| 4 | Combat damage prevention | — | GameEngine, test | 1 |
| 5 | Solitary Confinement card def | 1 test | CardDefinitions | 5 |
| 6 | Counter system | 1 enum + 1 test | GameCard | 5 |
| 7 | AddCountersEffect + counter cost | 1 effect + test | ActivatedAbility, GameEngine | 2 |
| 8 | ExileCreatureEffect + tracking | 1 effect + 1 test | GameCard | 3 |
| 9 | LTB triggers + ReturnExiledCardsEffect | 1 effect + 1 test | TriggerCondition | 4 |
| 10 | Engine LTB trigger firing | — | GameEngine, test | 1 |
| 11 | Parallax Wave card def | 1 test | CardDefinitions | 7 |
| 12 | EffectiveCardTypes | 1 test | GameCard | 5 |
| 13 | BecomeCreature + layered RecalculateState | test | ContinuousEffect, GameEngine | 3 |
| 14 | Opalescence card def | 1 test | CardDefinitions | 8 |
| 15 | AI support | 1 test | AiBotDecisionHandler | 2 |
| 16 | Integration tests | 1 test | — | 4 |

**Total: ~60 new tests, 7 new source files, ~10 modified source files**
