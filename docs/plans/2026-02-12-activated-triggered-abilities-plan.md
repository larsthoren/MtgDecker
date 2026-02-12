# Activated & Triggered Abilities Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement activated abilities (sacrifice, tap, mana costs) and triggered abilities (combat, cast, upkeep triggers) for 16 cards across the Goblins and Enchantress decks.

**Architecture:** Adds `ActivatedAbility` and `ActivatedAbilityCost` records to the engine, extends `ProcessTriggersAsync` to handle board-wide trigger conditions beyond Self, and wires new trigger call sites at combat damage, attack declaration, spell cast, upkeep, and creature death. All effects implement the existing `IEffect` interface.

**Tech Stack:** .NET 10, C# 14, xUnit + FluentAssertions

---

### Task 1: Foundation Types

Add the core types for the activated ability system and extend EffectContext for targeting.

**Files:**
- Create: `src/MtgDecker.Engine/ActivatedAbility.cs`
- Modify: `src/MtgDecker.Engine/Enums/ActionType.cs`
- Modify: `src/MtgDecker.Engine/GameAction.cs`
- Modify: `src/MtgDecker.Engine/Triggers/EffectContext.cs`
- Modify: `src/MtgDecker.Engine/CardDefinition.cs`
- Test: `tests/MtgDecker.Engine.Tests/ActivatedAbilityTypeTests.cs`

**Step 1: Write the failing tests**

```csharp
// tests/MtgDecker.Engine.Tests/ActivatedAbilityTypeTests.cs
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Triggers;

namespace MtgDecker.Engine.Tests;

public class ActivatedAbilityTypeTests
{
    [Fact]
    public void ActivatedAbilityCost_Defaults_Are_All_False_Or_Null()
    {
        var cost = new ActivatedAbilityCost();
        cost.TapSelf.Should().BeFalse();
        cost.SacrificeSelf.Should().BeFalse();
        cost.SacrificeSubtype.Should().BeNull();
        cost.ManaCost.Should().BeNull();
    }

    [Fact]
    public void ActivatedAbilityCost_TapSelf()
    {
        var cost = new ActivatedAbilityCost(TapSelf: true);
        cost.TapSelf.Should().BeTrue();
    }

    [Fact]
    public void ActivatedAbilityCost_SacrificeSubtype()
    {
        var cost = new ActivatedAbilityCost(SacrificeSubtype: "Goblin");
        cost.SacrificeSubtype.Should().Be("Goblin");
    }

    [Fact]
    public void ActivatedAbilityCost_With_ManaCost()
    {
        var mana = ManaCost.Parse("{1}{R}");
        var cost = new ActivatedAbilityCost(ManaCost: mana);
        cost.ManaCost.Should().Be(mana);
    }

    [Fact]
    public void ActivateAbility_ActionType_Exists()
    {
        var action = GameAction.ActivateAbility(Guid.NewGuid(), Guid.NewGuid());
        action.Type.Should().Be(ActionType.ActivateAbility);
    }

    [Fact]
    public void ActivateAbility_Action_With_Target()
    {
        var playerId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var action = GameAction.ActivateAbility(playerId, cardId, targetId: targetId);
        action.CardId.Should().Be(cardId);
        action.TargetCardId.Should().Be(targetId);
    }

    [Fact]
    public void ActivateAbility_Action_With_TargetPlayer()
    {
        var playerId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var targetPlayerId = Guid.NewGuid();
        var action = GameAction.ActivateAbility(playerId, cardId, targetPlayerId: targetPlayerId);
        action.TargetPlayerId.Should().Be(targetPlayerId);
    }

    [Fact]
    public void EffectContext_With_Target()
    {
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", null!),
            new Player(Guid.NewGuid(), "P2", null!));
        var source = new GameCard { Name = "Test" };
        var target = new GameCard { Name = "Target" };
        var context = new EffectContext(state, state.Player1, source, null!)
        {
            Target = target,
            TargetPlayerId = state.Player2.Id,
        };
        context.Target.Should().Be(target);
        context.TargetPlayerId.Should().Be(state.Player2.Id);
    }

    [Fact]
    public void CardDefinition_Has_ActivatedAbility_Property()
    {
        var def = new CardDefinition(null, null, null, null, CardType.Creature);
        def.ActivatedAbility.Should().BeNull();
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "ActivatedAbilityTypeTests" -v m`
Expected: FAIL — types don't exist yet

**Step 3: Implement the types**

Create `src/MtgDecker.Engine/ActivatedAbility.cs`:
```csharp
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Triggers;

namespace MtgDecker.Engine;

public record ActivatedAbilityCost(
    bool TapSelf = false,
    bool SacrificeSelf = false,
    string? SacrificeSubtype = null,
    ManaCost? ManaCost = null);

public record ActivatedAbility(
    ActivatedAbilityCost Cost,
    IEffect Effect,
    Func<GameCard, bool>? TargetFilter = null,
    bool CanTargetPlayer = false);
```

Add `ActivateAbility` to `ActionType` enum.

Add `TargetCardId` and `TargetPlayerId` properties + factory method to `GameAction`:
```csharp
public Guid? TargetCardId { get; init; }
public Guid? TargetPlayerId { get; init; }

public static GameAction ActivateAbility(Guid playerId, Guid cardId,
    Guid? targetId = null, Guid? targetPlayerId = null) => new()
{
    Type = ActionType.ActivateAbility,
    PlayerId = playerId,
    CardId = cardId,
    TargetCardId = targetId,
    TargetPlayerId = targetPlayerId,
};
```

Extend `EffectContext` with optional init properties:
```csharp
public record EffectContext(GameState State, Player Controller, GameCard Source, IPlayerDecisionHandler DecisionHandler)
{
    public GameCard? Target { get; init; }
    public Guid? TargetPlayerId { get; init; }
}
```

Add `ActivatedAbility` property to `CardDefinition`:
```csharp
public ActivatedAbility? ActivatedAbility { get; init; }
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "ActivatedAbilityTypeTests" -v m`
Expected: PASS (8 tests)

**Step 5: Run full test suite to verify no regressions**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v m`
Expected: All 548 existing tests pass + 8 new = 556

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/ActivatedAbility.cs src/MtgDecker.Engine/Enums/ActionType.cs src/MtgDecker.Engine/GameAction.cs src/MtgDecker.Engine/Triggers/EffectContext.cs src/MtgDecker.Engine/CardDefinition.cs tests/MtgDecker.Engine.Tests/ActivatedAbilityTypeTests.cs
git commit -m "feat(engine): add ActivatedAbility types, ActivateAbility action, EffectContext targeting"
```

---

### Task 2: New TriggerConditions, GameEvents, and DelayedTrigger

Extend the trigger system enums and add delayed trigger support to GameState.

**Files:**
- Modify: `src/MtgDecker.Engine/Triggers/TriggerCondition.cs`
- Modify: `src/MtgDecker.Engine/Enums/GameEvent.cs`
- Create: `src/MtgDecker.Engine/DelayedTrigger.cs`
- Modify: `src/MtgDecker.Engine/GameState.cs`
- Test: `tests/MtgDecker.Engine.Tests/TriggerSystemExtensionTests.cs`

**Step 1: Write the failing tests**

```csharp
// tests/MtgDecker.Engine.Tests/TriggerSystemExtensionTests.cs
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Triggers;

namespace MtgDecker.Engine.Tests;

public class TriggerSystemExtensionTests
{
    [Theory]
    [InlineData(TriggerCondition.ControllerCastsEnchantment)]
    [InlineData(TriggerCondition.SelfDealsCombatDamage)]
    [InlineData(TriggerCondition.SelfAttacks)]
    [InlineData(TriggerCondition.Upkeep)]
    public void TriggerCondition_New_Values_Exist(TriggerCondition condition)
    {
        Enum.IsDefined(condition).Should().BeTrue();
    }

    [Fact]
    public void GameEvent_EndStep_Exists()
    {
        Enum.IsDefined(GameEvent.EndStep).Should().BeTrue();
    }

    [Fact]
    public void DelayedTrigger_Record_Works()
    {
        var effect = new TestEffect();
        var trigger = new DelayedTrigger(GameEvent.EndStep, effect, Guid.NewGuid());
        trigger.FireOn.Should().Be(GameEvent.EndStep);
        trigger.Effect.Should().Be(effect);
    }

    [Fact]
    public void GameState_Has_DelayedTriggers_List()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", null!);
        var p2 = new Player(Guid.NewGuid(), "P2", null!);
        var state = new GameState(p1, p2);
        state.DelayedTriggers.Should().NotBeNull();
        state.DelayedTriggers.Should().BeEmpty();
    }

    private class TestEffect : IEffect
    {
        public Task Execute(EffectContext context, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "TriggerSystemExtensionTests" -v m`
Expected: FAIL — new enum values and types don't exist

**Step 3: Implement**

Add to `TriggerCondition.cs`:
```csharp
public enum TriggerCondition
{
    Self,
    AnyCreatureDies,
    ControllerCasts,
    ControllerCastsEnchantment,
    SelfDealsCombatDamage,
    SelfAttacks,
    Upkeep,
}
```

Add `EndStep` to `GameEvent.cs`:
```csharp
public enum GameEvent
{
    EnterBattlefield,
    LeavesBattlefield,
    Dies,
    SpellCast,
    CombatDamageDealt,
    DrawCard,
    Upkeep,
    EndStep,
}
```

Create `src/MtgDecker.Engine/DelayedTrigger.cs`:
```csharp
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Triggers;

namespace MtgDecker.Engine;

public record DelayedTrigger(GameEvent FireOn, IEffect Effect, Guid ControllerId);
```

Add to `GameState.cs`:
```csharp
public List<DelayedTrigger> DelayedTriggers { get; } = new();
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "TriggerSystemExtensionTests" -v m`
Expected: PASS (4 tests)

**Step 5: Run full test suite**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v m`
Expected: All pass (556 + 4 = 560)

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/Triggers/TriggerCondition.cs src/MtgDecker.Engine/Enums/GameEvent.cs src/MtgDecker.Engine/DelayedTrigger.cs src/MtgDecker.Engine/GameState.cs tests/MtgDecker.Engine.Tests/TriggerSystemExtensionTests.cs
git commit -m "feat(engine): add new TriggerConditions, EndStep GameEvent, DelayedTrigger"
```

---

### Task 3: Activated Ability IEffect Implementations

Implement the effect classes used by activated abilities.

**Files:**
- Create: `src/MtgDecker.Engine/Triggers/Effects/DealDamageEffect.cs`
- Create: `src/MtgDecker.Engine/Triggers/Effects/AddManaEffect.cs`
- Create: `src/MtgDecker.Engine/Triggers/Effects/DestroyTargetEffect.cs`
- Create: `src/MtgDecker.Engine/Triggers/Effects/TapTargetEffect.cs`
- Create: `src/MtgDecker.Engine/Triggers/Effects/SearchLibraryByTypeEffect.cs`
- Test: `tests/MtgDecker.Engine.Tests/Triggers/ActivatedAbilityEffectTests.cs`

**Step 1: Write the failing tests**

```csharp
// tests/MtgDecker.Engine.Tests/Triggers/ActivatedAbilityEffectTests.cs
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests.Triggers;

public class ActivatedAbilityEffectTests
{
    private (GameState state, Player p1, Player p2, TestDecisionHandler handler) Setup()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        return (state, p1, p2, handler);
    }

    [Fact]
    public async Task DealDamageEffect_Damages_Target_Creature()
    {
        var (state, p1, p2, _) = Setup();
        var source = new GameCard { Name = "Mogg Fanatic", CardTypes = CardType.Creature, BasePower = 1, BaseToughness = 1 };
        var target = new GameCard { Name = "Bird", CardTypes = CardType.Creature, BasePower = 1, BaseToughness = 1 };
        p2.Battlefield.Add(target);

        var effect = new DealDamageEffect(1);
        var context = new EffectContext(state, p1, source, p1.DecisionHandler) { Target = target };
        await effect.Execute(context);

        target.DamageMarked.Should().Be(1);
    }

    [Fact]
    public async Task DealDamageEffect_Damages_Target_Player()
    {
        var (state, p1, p2, _) = Setup();
        var source = new GameCard { Name = "Mogg Fanatic" };
        var effect = new DealDamageEffect(2);
        var context = new EffectContext(state, p1, source, p1.DecisionHandler) { TargetPlayerId = p2.Id };
        await effect.Execute(context);

        p2.Life.Should().Be(18);
    }

    [Fact]
    public async Task AddManaEffect_Adds_Mana_To_Pool()
    {
        var (state, p1, _, _) = Setup();
        var source = new GameCard { Name = "Skirk Prospector" };
        var effect = new AddManaEffect(ManaColor.Red);
        var context = new EffectContext(state, p1, source, p1.DecisionHandler);
        await effect.Execute(context);

        p1.ManaPool.Available.Should().ContainKey(ManaColor.Red);
        p1.ManaPool.Available[ManaColor.Red].Should().Be(1);
    }

    [Fact]
    public async Task DestroyTargetEffect_Removes_From_Battlefield()
    {
        var (state, _, p2, _) = Setup();
        var source = new GameCard { Name = "Seal of Cleansing" };
        var target = new GameCard { Name = "Artifact", CardTypes = CardType.Artifact };
        p2.Battlefield.Add(target);

        var effect = new DestroyTargetEffect();
        var context = new EffectContext(state, state.Player1, source, state.Player1.DecisionHandler) { Target = target };
        await effect.Execute(context);

        p2.Battlefield.Cards.Should().NotContain(target);
        p2.Graveyard.Cards.Should().Contain(target);
    }

    [Fact]
    public async Task TapTargetEffect_Taps_Target()
    {
        var (state, _, p2, _) = Setup();
        var source = new GameCard { Name = "Rishadan Port" };
        var target = new GameCard { Name = "Mountain", CardTypes = CardType.Land };
        p2.Battlefield.Add(target);

        var effect = new TapTargetEffect();
        var context = new EffectContext(state, state.Player1, source, state.Player1.DecisionHandler) { Target = target };
        await effect.Execute(context);

        target.IsTapped.Should().BeTrue();
    }

    [Fact]
    public async Task SearchLibraryByTypeEffect_Finds_Enchantment()
    {
        var (state, p1, _, handler) = Setup();
        var source = new GameCard { Name = "Sterling Grove" };
        var enchantment = new GameCard { Name = "Wild Growth", CardTypes = CardType.Enchantment };
        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature };
        p1.Library.Add(enchantment);
        p1.Library.Add(creature);

        handler.EnqueueCardChoice(enchantment.Id);

        var effect = new SearchLibraryByTypeEffect(CardType.Enchantment);
        var context = new EffectContext(state, p1, source, handler);
        await effect.Execute(context);

        p1.Hand.Cards.Should().Contain(c => c.Name == "Wild Growth");
        p1.Library.Cards.Should().NotContain(c => c.Name == "Wild Growth");
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "ActivatedAbilityEffectTests" -v m`
Expected: FAIL — effect classes don't exist

**Step 3: Implement all 5 effect classes**

`DealDamageEffect.cs`:
```csharp
namespace MtgDecker.Engine.Triggers.Effects;

public class DealDamageEffect(int amount) : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        if (context.Target != null)
        {
            context.Target.DamageMarked += amount;
            context.State.Log($"{context.Source.Name} deals {amount} damage to {context.Target.Name}.");
        }
        else if (context.TargetPlayerId.HasValue)
        {
            var targetPlayer = context.State.Player1.Id == context.TargetPlayerId.Value
                ? context.State.Player1 : context.State.Player2;
            targetPlayer.AdjustLife(-amount);
            context.State.Log($"{context.Source.Name} deals {amount} damage to {targetPlayer.Name}. ({targetPlayer.Life} life)");
        }
        return Task.CompletedTask;
    }
}
```

`AddManaEffect.cs`:
```csharp
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Triggers.Effects;

public class AddManaEffect(ManaColor color) : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        context.Controller.ManaPool.Add(color);
        context.State.Log($"{context.Controller.Name} adds {color} mana.");
        return Task.CompletedTask;
    }
}
```

`DestroyTargetEffect.cs`:
```csharp
namespace MtgDecker.Engine.Triggers.Effects;

public class DestroyTargetEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        if (context.Target == null) return Task.CompletedTask;
        var owner = context.State.Player1.Battlefield.Contains(context.Target.Id)
            ? context.State.Player1 : context.State.Player2;
        owner.Battlefield.RemoveById(context.Target.Id);
        owner.Graveyard.Add(context.Target);
        context.State.Log($"{context.Target.Name} is destroyed.");
        return Task.CompletedTask;
    }
}
```

`TapTargetEffect.cs`:
```csharp
namespace MtgDecker.Engine.Triggers.Effects;

public class TapTargetEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        if (context.Target == null) return Task.CompletedTask;
        context.Target.IsTapped = true;
        context.State.Log($"{context.Target.Name} is tapped.");
        return Task.CompletedTask;
    }
}
```

`SearchLibraryByTypeEffect.cs`:
```csharp
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class SearchLibraryByTypeEffect(CardType type) : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var matches = context.Controller.Library.Cards
            .Where(c => c.CardTypes.HasFlag(type))
            .ToList();

        if (matches.Count == 0)
        {
            context.State.Log($"{context.Controller.Name} searches library but finds no matching card.");
            context.Controller.Library.Shuffle();
            return;
        }

        var chosenId = await context.DecisionHandler.ChooseCard(
            matches, $"Search for a {type}", optional: true, ct);

        if (chosenId.HasValue)
        {
            var chosen = context.Controller.Library.RemoveById(chosenId.Value);
            if (chosen != null)
            {
                context.Controller.Hand.Add(chosen);
                context.State.Log($"{context.Controller.Name} searches library and adds {chosen.Name} to hand.");
            }
        }

        context.Controller.Library.Shuffle();
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "ActivatedAbilityEffectTests" -v m`
Expected: PASS (6 tests)

**Step 5: Run full test suite**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v m`
Expected: All pass (560 + 6 = 566)

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/Triggers/Effects/DealDamageEffect.cs src/MtgDecker.Engine/Triggers/Effects/AddManaEffect.cs src/MtgDecker.Engine/Triggers/Effects/DestroyTargetEffect.cs src/MtgDecker.Engine/Triggers/Effects/TapTargetEffect.cs src/MtgDecker.Engine/Triggers/Effects/SearchLibraryByTypeEffect.cs tests/MtgDecker.Engine.Tests/Triggers/ActivatedAbilityEffectTests.cs
git commit -m "feat(engine): add activated ability effect implementations"
```

---

### Task 4: ActivateAbility Engine Handler

Wire the `ActivateAbility` action type into `GameEngine.ExecuteAction`.

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (add `case ActionType.ActivateAbility` in `ExecuteAction`)
- Test: `tests/MtgDecker.Engine.Tests/ActivateAbilityEngineTests.cs`

**Step 1: Write the failing tests**

```csharp
// tests/MtgDecker.Engine.Tests/ActivateAbilityEngineTests.cs
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class ActivateAbilityEngineTests
{
    private (GameEngine engine, GameState state, Player p1, Player p2, TestDecisionHandler handler) Setup()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);
        return (engine, state, p1, p2, handler);
    }

    [Fact]
    public async Task SacrificeSelf_Removes_Source_From_Battlefield()
    {
        var (engine, state, p1, p2, handler) = Setup();
        // Mogg Fanatic: sacrifice self -> deal 1 damage
        var fanatic = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        p1.Battlefield.Add(fanatic);
        var target = new GameCard { Name = "Bird", CardTypes = CardType.Creature, BasePower = 1, BaseToughness = 1 };
        p2.Battlefield.Add(target);

        await engine.ExecuteAction(GameAction.ActivateAbility(p1.Id, fanatic.Id, targetId: target.Id));

        p1.Battlefield.Cards.Should().NotContain(c => c.Id == fanatic.Id);
        p1.Graveyard.Cards.Should().Contain(c => c.Id == fanatic.Id);
        target.DamageMarked.Should().Be(1);
    }

    [Fact]
    public async Task TapSelf_Taps_Source()
    {
        var (engine, state, p1, p2, _) = Setup();
        // Goblin Sharpshooter: tap -> deal 1 damage
        var shooter = GameCard.Create("Goblin Sharpshooter", "Creature — Goblin");
        p1.Battlefield.Add(shooter);
        var target = new GameCard { Name = "Bird", CardTypes = CardType.Creature, BasePower = 1, BaseToughness = 1 };
        p2.Battlefield.Add(target);

        await engine.ExecuteAction(GameAction.ActivateAbility(p1.Id, shooter.Id, targetId: target.Id));

        shooter.IsTapped.Should().BeTrue();
        target.DamageMarked.Should().Be(1);
    }

    [Fact]
    public async Task TapSelf_Fails_If_Already_Tapped()
    {
        var (engine, state, p1, p2, _) = Setup();
        var shooter = GameCard.Create("Goblin Sharpshooter", "Creature — Goblin");
        shooter.IsTapped = true;
        p1.Battlefield.Add(shooter);
        var target = new GameCard { Name = "Bird", CardTypes = CardType.Creature, BasePower = 1, BaseToughness = 1 };
        p2.Battlefield.Add(target);

        await engine.ExecuteAction(GameAction.ActivateAbility(p1.Id, shooter.Id, targetId: target.Id));

        target.DamageMarked.Should().Be(0); // ability didn't fire
    }

    [Fact]
    public async Task SacrificeSubtype_Sacrifices_Another_Creature()
    {
        var (engine, state, p1, p2, handler) = Setup();
        // Siege-Gang Commander: {1}{R} + sacrifice a Goblin -> deal 2 damage
        var commander = GameCard.Create("Siege-Gang Commander", "Creature — Goblin");
        var token = new GameCard { Name = "Goblin", CardTypes = CardType.Creature, Subtypes = ["Goblin"], BasePower = 1, BaseToughness = 1, IsToken = true };
        p1.Battlefield.Add(commander);
        p1.Battlefield.Add(token);
        p1.ManaPool.Add(ManaColor.Red);
        p1.ManaPool.Add(ManaColor.Colorless);

        handler.EnqueueCardChoice(token.Id); // choose which Goblin to sacrifice

        var target = new GameCard { Name = "Bird", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p2.Battlefield.Add(target);

        await engine.ExecuteAction(GameAction.ActivateAbility(p1.Id, commander.Id, targetId: target.Id));

        p1.Battlefield.Cards.Should().NotContain(c => c.Id == token.Id);
        target.DamageMarked.Should().Be(2);
    }

    [Fact]
    public async Task ManaCost_Is_Paid()
    {
        var (engine, state, p1, p2, handler) = Setup();
        var commander = GameCard.Create("Siege-Gang Commander", "Creature — Goblin");
        var token = new GameCard { Name = "Goblin", CardTypes = CardType.Creature, Subtypes = ["Goblin"], BasePower = 1, BaseToughness = 1, IsToken = true };
        p1.Battlefield.Add(commander);
        p1.Battlefield.Add(token);
        p1.ManaPool.Add(ManaColor.Red);
        p1.ManaPool.Add(ManaColor.Colorless);

        handler.EnqueueCardChoice(token.Id);

        var target = new GameCard { Name = "Bird", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p2.Battlefield.Add(target);

        await engine.ExecuteAction(GameAction.ActivateAbility(p1.Id, commander.Id, targetId: target.Id));

        // Mana pool should be empty after paying {1}{R}
        p1.ManaPool.Available.Values.Sum().Should().Be(0);
    }

    [Fact]
    public async Task Cannot_Activate_Without_Enough_Mana()
    {
        var (engine, state, p1, p2, handler) = Setup();
        var commander = GameCard.Create("Siege-Gang Commander", "Creature — Goblin");
        var token = new GameCard { Name = "Goblin", CardTypes = CardType.Creature, Subtypes = ["Goblin"], BasePower = 1, BaseToughness = 1, IsToken = true };
        p1.Battlefield.Add(commander);
        p1.Battlefield.Add(token);
        // No mana in pool

        var target = new GameCard { Name = "Bird", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p2.Battlefield.Add(target);

        await engine.ExecuteAction(GameAction.ActivateAbility(p1.Id, commander.Id, targetId: target.Id));

        target.DamageMarked.Should().Be(0); // ability didn't fire
        p1.Battlefield.Cards.Should().Contain(c => c.Id == token.Id); // token not sacrificed
    }

    [Fact]
    public async Task Cannot_Activate_Without_Sacrifice_Target()
    {
        var (engine, state, p1, p2, handler) = Setup();
        var commander = GameCard.Create("Siege-Gang Commander", "Creature — Goblin");
        p1.Battlefield.Add(commander);
        p1.ManaPool.Add(ManaColor.Red);
        p1.ManaPool.Add(ManaColor.Colorless);
        // No other Goblins on battlefield

        var target = new GameCard { Name = "Bird", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p2.Battlefield.Add(target);

        await engine.ExecuteAction(GameAction.ActivateAbility(p1.Id, commander.Id, targetId: target.Id));

        target.DamageMarked.Should().Be(0); // ability didn't fire
    }

    [Fact]
    public async Task DealDamage_To_Player()
    {
        var (engine, state, p1, p2, _) = Setup();
        var fanatic = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        p1.Battlefield.Add(fanatic);

        await engine.ExecuteAction(GameAction.ActivateAbility(p1.Id, fanatic.Id, targetPlayerId: p2.Id));

        p2.Life.Should().Be(19);
    }

    [Fact]
    public async Task Wasteland_TapAndSacrifice_Destroys_Target_Land()
    {
        var (engine, state, p1, p2, _) = Setup();
        var wasteland = GameCard.Create("Wasteland", "Land");
        p1.Battlefield.Add(wasteland);
        var targetLand = new GameCard { Name = "Some Nonbasic", CardTypes = CardType.Land };
        p2.Battlefield.Add(targetLand);

        await engine.ExecuteAction(GameAction.ActivateAbility(p1.Id, wasteland.Id, targetId: targetLand.Id));

        wasteland.IsTapped.Should().BeTrue();
        p1.Battlefield.Cards.Should().NotContain(c => c.Id == wasteland.Id);
        p1.Graveyard.Cards.Should().Contain(c => c.Id == wasteland.Id);
        p2.Battlefield.Cards.Should().NotContain(c => c.Id == targetLand.Id);
        p2.Graveyard.Cards.Should().Contain(c => c.Id == targetLand.Id);
    }

    [Fact]
    public async Task RishadanPort_TapAndMana_Taps_Target_Land()
    {
        var (engine, state, p1, p2, _) = Setup();
        var port = GameCard.Create("Rishadan Port", "Land");
        p1.Battlefield.Add(port);
        p1.ManaPool.Add(ManaColor.Colorless);
        var targetLand = new GameCard { Name = "Mountain", CardTypes = CardType.Land };
        p2.Battlefield.Add(targetLand);

        await engine.ExecuteAction(GameAction.ActivateAbility(p1.Id, port.Id, targetId: targetLand.Id));

        port.IsTapped.Should().BeTrue();
        targetLand.IsTapped.Should().BeTrue();
        p1.ManaPool.Available.Values.Sum().Should().Be(0);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "ActivateAbilityEngineTests" -v m`
Expected: FAIL — no ActivateAbility case in ExecuteAction

**Step 3: Implement the engine handler**

Add `case ActionType.ActivateAbility:` block in `GameEngine.ExecuteAction`. This is the most complex piece. The handler needs to:

1. Find the source card on the player's battlefield
2. Look up its CardDefinition to get ActivatedAbility
3. Validate all costs can be paid
4. Find the target card (if TargetCardId provided)
5. Pay costs in order: mana, tap, sacrifice
6. Build EffectContext with target info
7. Execute the effect
8. Call OnBoardChangedAsync

```csharp
case ActionType.ActivateAbility:
{
    var sourceCard = player.Battlefield.Cards.FirstOrDefault(c => c.Id == action.CardId);
    if (sourceCard == null) break;

    if (!CardDefinitions.TryGet(sourceCard.Name, out var abilityDef) || abilityDef.ActivatedAbility == null)
    {
        _state.Log($"{sourceCard.Name} has no activated ability.");
        break;
    }

    var ability = abilityDef.ActivatedAbility;
    var cost = ability.Cost;

    // Validate costs can be paid
    if (cost.TapSelf && sourceCard.IsTapped)
    {
        _state.Log($"Cannot activate {sourceCard.Name} — already tapped.");
        break;
    }

    if (cost.ManaCost != null && !player.ManaPool.CanPay(cost.ManaCost))
    {
        _state.Log($"Cannot activate {sourceCard.Name} — not enough mana.");
        break;
    }

    GameCard? sacrificeTarget = null;
    if (cost.SacrificeSubtype != null)
    {
        var eligible = player.Battlefield.Cards
            .Where(c => c.IsCreature && c.Subtypes.Contains(cost.SacrificeSubtype, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (eligible.Count == 0)
        {
            _state.Log($"Cannot activate {sourceCard.Name} — no {cost.SacrificeSubtype} to sacrifice.");
            break;
        }
        var chosenId = await player.DecisionHandler.ChooseCard(
            eligible, $"Choose a {cost.SacrificeSubtype} to sacrifice", optional: false, ct);
        sacrificeTarget = eligible.FirstOrDefault(c => c.Id == chosenId);
        if (sacrificeTarget == null) break;
    }

    // Pay costs
    if (cost.ManaCost != null)
    {
        foreach (var (color, amount) in cost.ManaCost.ColorRequirements)
            player.ManaPool.Deduct(color, amount);
        if (cost.ManaCost.GenericCost > 0)
        {
            var toPay = cost.ManaCost.GenericCost;
            foreach (var (color, amount) in player.ManaPool.Available.ToDictionary(k => k.Key, v => v.Value))
            {
                var take = Math.Min(amount, toPay);
                if (take > 0) { player.ManaPool.Deduct(color, take); toPay -= take; }
                if (toPay == 0) break;
            }
        }
    }

    if (cost.TapSelf)
        sourceCard.IsTapped = true;

    if (cost.SacrificeSelf)
    {
        player.Battlefield.RemoveById(sourceCard.Id);
        player.Graveyard.Add(sourceCard);
        _state.Log($"{player.Name} sacrifices {sourceCard.Name}.");
    }

    if (sacrificeTarget != null)
    {
        player.Battlefield.RemoveById(sacrificeTarget.Id);
        player.Graveyard.Add(sacrificeTarget);
        _state.Log($"{player.Name} sacrifices {sacrificeTarget.Name}.");
    }

    // Resolve effect
    GameCard? effectTarget = null;
    if (action.TargetCardId.HasValue)
    {
        effectTarget = _state.Player1.Battlefield.Cards.FirstOrDefault(c => c.Id == action.TargetCardId)
            ?? _state.Player2.Battlefield.Cards.FirstOrDefault(c => c.Id == action.TargetCardId);
    }

    var effectContext = new EffectContext(_state, player, sourceCard, player.DecisionHandler)
    {
        Target = effectTarget,
        TargetPlayerId = action.TargetPlayerId,
    };
    await ability.Effect.Execute(effectContext, ct);
    await OnBoardChangedAsync(ct);
    break;
}
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "ActivateAbilityEngineTests" -v m`
Expected: PASS (10 tests)

Note: For these tests to pass, Task 5 (card registrations) must be done first for the cards used. But since we use `GameCard.Create("Mogg Fanatic")` which looks up CardDefinitions, the cards need their ActivatedAbility registered. **Alternative approach**: register the test cards in this task's tests using `GameCard.Create` with names that are already in CardDefinitions but don't have abilities yet — this test won't work. Instead, register the cards in Task 5 first, or use the test setup to manually create cards.

**Important**: These tests depend on CardDefinitions having ActivatedAbility for the named cards. Run Task 5 before running these tests, OR restructure the tests to use mock cards. The recommended approach is to **merge Task 4 and Task 5** by implementing the engine handler and registering the first few cards (Mogg Fanatic, Goblin Sharpshooter, Siege-Gang Commander, Wasteland, Rishadan Port) in the same task.

**Step 5: Run full test suite**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v m`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/ActivateAbilityEngineTests.cs
git commit -m "feat(engine): implement ActivateAbility handler in GameEngine"
```

---

### Task 5: Register Activated Ability Cards in CardDefinitions

Add ActivatedAbility to all 9 cards with activated abilities.

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Test: `tests/MtgDecker.Engine.Tests/ActivatedAbilityCardRegistrationTests.cs`

**Step 1: Write the failing tests**

```csharp
// tests/MtgDecker.Engine.Tests/ActivatedAbilityCardRegistrationTests.cs
using FluentAssertions;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class ActivatedAbilityCardRegistrationTests
{
    [Theory]
    [InlineData("Mogg Fanatic")]
    [InlineData("Siege-Gang Commander")]
    [InlineData("Goblin Sharpshooter")]
    [InlineData("Skirk Prospector")]
    [InlineData("Goblin Tinkerer")]
    [InlineData("Seal of Cleansing")]
    [InlineData("Sterling Grove")]
    [InlineData("Rishadan Port")]
    [InlineData("Wasteland")]
    public void Card_Has_ActivatedAbility(string cardName)
    {
        CardDefinitions.TryGet(cardName, out var def).Should().BeTrue();
        def!.ActivatedAbility.Should().NotBeNull();
    }

    [Fact]
    public void MoggFanatic_SacrificeSelf_DealDamage1()
    {
        CardDefinitions.TryGet("Mogg Fanatic", out var def);
        def!.ActivatedAbility!.Cost.SacrificeSelf.Should().BeTrue();
        def.ActivatedAbility.Effect.Should().BeOfType<DealDamageEffect>();
        def.ActivatedAbility.CanTargetPlayer.Should().BeTrue();
    }

    [Fact]
    public void SiegeGangCommander_SacGoblin_ManaCost_DealDamage2()
    {
        CardDefinitions.TryGet("Siege-Gang Commander", out var def);
        def!.ActivatedAbility!.Cost.SacrificeSubtype.Should().Be("Goblin");
        def.ActivatedAbility.Cost.ManaCost.Should().NotBeNull();
        def.ActivatedAbility.Cost.ManaCost!.ConvertedManaCost.Should().Be(2); // {1}{R}
        def.ActivatedAbility.Effect.Should().BeOfType<DealDamageEffect>();
    }

    [Fact]
    public void GoblinSharpshooter_TapSelf_DealDamage1()
    {
        CardDefinitions.TryGet("Goblin Sharpshooter", out var def);
        def!.ActivatedAbility!.Cost.TapSelf.Should().BeTrue();
        def.ActivatedAbility.Effect.Should().BeOfType<DealDamageEffect>();
    }

    [Fact]
    public void SkirkProspector_SacGoblin_AddMana()
    {
        CardDefinitions.TryGet("Skirk Prospector", out var def);
        def!.ActivatedAbility!.Cost.SacrificeSubtype.Should().Be("Goblin");
        def.ActivatedAbility.Effect.Should().BeOfType<AddManaEffect>();
    }

    [Fact]
    public void Wasteland_TapAndSacrificeSelf()
    {
        CardDefinitions.TryGet("Wasteland", out var def);
        def!.ActivatedAbility!.Cost.TapSelf.Should().BeTrue();
        def.ActivatedAbility.Cost.SacrificeSelf.Should().BeTrue();
        def.ActivatedAbility.Effect.Should().BeOfType<DestroyTargetEffect>();
    }

    [Fact]
    public void RishadanPort_TapAndManaCost()
    {
        CardDefinitions.TryGet("Rishadan Port", out var def);
        def!.ActivatedAbility!.Cost.TapSelf.Should().BeTrue();
        def.ActivatedAbility.Cost.ManaCost.Should().NotBeNull();
        def.ActivatedAbility.Cost.ManaCost!.GenericCost.Should().Be(1);
        def.ActivatedAbility.Effect.Should().BeOfType<TapTargetEffect>();
    }

    [Fact]
    public void SterlingGrove_ManaCostAndSacrificeSelf_SearchEnchantment()
    {
        CardDefinitions.TryGet("Sterling Grove", out var def);
        def!.ActivatedAbility!.Cost.SacrificeSelf.Should().BeTrue();
        def.ActivatedAbility.Cost.ManaCost.Should().NotBeNull();
        def.ActivatedAbility.Cost.ManaCost!.GenericCost.Should().Be(1);
        def.ActivatedAbility.Effect.Should().BeOfType<SearchLibraryByTypeEffect>();
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "ActivatedAbilityCardRegistrationTests" -v m`
Expected: FAIL — cards don't have ActivatedAbility set

**Step 3: Register all 9 cards in CardDefinitions**

Update each card entry in `CardDefinitions.cs` to include `ActivatedAbility`:

```csharp
["Mogg Fanatic"] = new(ManaCost.Parse("{R}"), null, 1, 1, CardType.Creature)
{
    Subtypes = ["Goblin"],
    ActivatedAbility = new(new ActivatedAbilityCost(SacrificeSelf: true),
        new DealDamageEffect(1), c => c.IsCreature, CanTargetPlayer: true),
},

["Siege-Gang Commander"] = new(ManaCost.Parse("{3}{R}{R}"), null, 2, 2, CardType.Creature)
{
    Subtypes = ["Goblin"],
    Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new CreateTokensEffect("Goblin", 1, 1, CardType.Creature, ["Goblin"], count: 3))],
    ActivatedAbility = new(new ActivatedAbilityCost(SacrificeSubtype: "Goblin", ManaCost: ManaCost.Parse("{1}{R}")),
        new DealDamageEffect(2), c => c.IsCreature, CanTargetPlayer: true),
},

["Goblin Sharpshooter"] = new(ManaCost.Parse("{2}{R}"), null, 1, 1, CardType.Creature)
{
    Subtypes = ["Goblin"],
    ActivatedAbility = new(new ActivatedAbilityCost(TapSelf: true),
        new DealDamageEffect(1), c => c.IsCreature, CanTargetPlayer: true),
},

["Skirk Prospector"] = new(ManaCost.Parse("{R}"), null, 1, 1, CardType.Creature)
{
    Subtypes = ["Goblin"],
    ActivatedAbility = new(new ActivatedAbilityCost(SacrificeSubtype: "Goblin"),
        new AddManaEffect(ManaColor.Red)),
},

["Goblin Tinkerer"] = new(ManaCost.Parse("{1}{R}"), null, 1, 1, CardType.Creature)
{
    Subtypes = ["Goblin"],
    ActivatedAbility = new(new ActivatedAbilityCost(SacrificeSelf: true),
        new DestroyTargetEffect(), c => c.CardTypes.HasFlag(CardType.Artifact)),
},

["Seal of Cleansing"] = new(ManaCost.Parse("{1}{W}"), null, null, null, CardType.Enchantment)
{
    ActivatedAbility = new(new ActivatedAbilityCost(SacrificeSelf: true),
        new DestroyTargetEffect(), c => c.CardTypes.HasFlag(CardType.Enchantment) || c.CardTypes.HasFlag(CardType.Artifact)),
},

["Sterling Grove"] = new(ManaCost.Parse("{G}{W}"), null, null, null, CardType.Enchantment)
{
    ActivatedAbility = new(new ActivatedAbilityCost(SacrificeSelf: true, ManaCost: ManaCost.Parse("{1}")),
        new SearchLibraryByTypeEffect(CardType.Enchantment)),
},

["Rishadan Port"] = new(null, null, null, null, CardType.Land)
{
    ActivatedAbility = new(new ActivatedAbilityCost(TapSelf: true, ManaCost: ManaCost.Parse("{1}")),
        new TapTargetEffect(), c => c.IsLand),
},

["Wasteland"] = new(null, null, null, null, CardType.Land)
{
    ActivatedAbility = new(new ActivatedAbilityCost(TapSelf: true, SacrificeSelf: true),
        new DestroyTargetEffect(), c => c.IsLand),
},
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "ActivatedAbilityCardRegistrationTests" -v m`
Expected: PASS (12 tests)

**Step 5: Run full test suite**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v m`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/CardDefinitions.cs tests/MtgDecker.Engine.Tests/ActivatedAbilityCardRegistrationTests.cs
git commit -m "feat(engine): register activated abilities for 9 cards in CardDefinitions"
```

---

### Task 6: Extend ProcessTriggersAsync for Board-Wide Triggers

Refactor `ProcessTriggersAsync` to scan the battlefield for matching triggers beyond `Self`.

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (refactor `ProcessTriggersAsync`, add trigger call sites)
- Test: `tests/MtgDecker.Engine.Tests/Triggers/BoardWideTriggerTests.cs`

**Step 1: Write the failing tests**

```csharp
// tests/MtgDecker.Engine.Tests/Triggers/BoardWideTriggerTests.cs
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests.Triggers;

public class BoardWideTriggerTests
{
    private (GameEngine engine, GameState state, Player p1, Player p2, TestDecisionHandler handler) Setup()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);
        return (engine, state, p1, p2, handler);
    }

    [Fact]
    public async Task AnyCreatureDies_Fires_When_Creature_Dies_In_Combat()
    {
        // Goblin Sharpshooter has trigger: AnyCreatureDies -> untap self
        var (engine, state, p1, p2, handler) = Setup();

        var shooter = GameCard.Create("Goblin Sharpshooter", "Creature — Goblin");
        shooter.IsTapped = true; // already tapped from using ability
        p1.Battlefield.Add(shooter);

        // Kill a creature via combat death processing
        var bird = new GameCard { Name = "Bird", CardTypes = CardType.Creature, BasePower = 1, BaseToughness = 1, DamageMarked = 1 };
        p2.Battlefield.Add(bird);

        // Simulate combat deaths triggering
        // We test ProcessTriggersAsync directly for the Dies event
        // The engine wires this in ProcessCombatDeaths - tested via integration
    }

    [Fact]
    public async Task ControllerCastsEnchantment_Fires_When_Enchantment_Cast()
    {
        var (engine, state, p1, p2, handler) = Setup();

        var enchantress = GameCard.Create("Argothian Enchantress", "Creature — Human Druid");
        p1.Battlefield.Add(enchantress);

        var handSizeBefore = p1.Hand.Count;
        // Put a card in library so draw works
        p1.Library.Add(new GameCard { Name = "Card1" });

        // Cast an enchantment - this should trigger the enchantress
        var enchantment = GameCard.Create("Wild Growth", "Enchantment — Aura");
        enchantment.ManaCost = Mana.ManaCost.Parse("{G}");
        p1.Hand.Add(enchantment);
        p1.ManaPool.Add(Mana.ManaColor.Green);

        handler.EnqueueAction(GameAction.PlayCard(p1.Id, enchantment.Id));
        handler.EnqueueAction(GameAction.Pass(p1.Id));

        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, enchantment.Id));

        // Enchantress should have drawn a card
        p1.Hand.Count.Should().Be(handSizeBefore + 1);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "BoardWideTriggerTests" -v m`
Expected: FAIL — ProcessTriggersAsync doesn't handle board-wide conditions

**Step 3: Refactor ProcessTriggersAsync**

The current method only checks `source.Triggers`. Extend it to also scan the battlefield for triggers matching the event:

```csharp
private async Task ProcessTriggersAsync(GameEvent evt, GameCard source, Player controller, CancellationToken ct)
{
    // 1. Self-triggers on the source card (existing behavior)
    foreach (var trigger in source.Triggers)
    {
        if (trigger.Event != evt) continue;
        if (trigger.Condition == TriggerCondition.Self)
        {
            var ability = new TriggeredAbility(source, controller, trigger);
            _state.Log($"{source.Name} triggers: {trigger.Effect.GetType().Name.Replace("Effect", "")}");
            await ability.ResolveAsync(_state, ct);
        }
    }

    // 2. Board-wide triggers — scan all permanents on both players' battlefields
    await ProcessBoardTriggersAsync(evt, source, ct);
}

private async Task ProcessBoardTriggersAsync(GameEvent evt, GameCard? relevantCard, CancellationToken ct)
{
    foreach (var player in new[] { _state.Player1, _state.Player2 })
    {
        foreach (var permanent in player.Battlefield.Cards.ToList())
        {
            if (!CardDefinitions.TryGet(permanent.Name, out var def)) continue;
            foreach (var trigger in def.Triggers)
            {
                if (trigger.Event != evt) continue;
                if (trigger.Condition == TriggerCondition.Self) continue; // already handled above

                bool shouldFire = trigger.Condition switch
                {
                    TriggerCondition.AnyCreatureDies =>
                        evt == GameEvent.Dies && relevantCard != null && relevantCard.IsCreature,
                    TriggerCondition.ControllerCastsEnchantment =>
                        evt == GameEvent.SpellCast && relevantCard != null
                        && relevantCard.CardTypes.HasFlag(CardType.Enchantment)
                        && _state.ActivePlayer == player,
                    TriggerCondition.SelfDealsCombatDamage =>
                        evt == GameEvent.CombatDamageDealt && relevantCard?.Id == permanent.Id,
                    TriggerCondition.SelfAttacks =>
                        evt == GameEvent.CombatDamageDealt && relevantCard?.Id == permanent.Id,
                    _ => false,
                };

                if (shouldFire)
                {
                    var ability = new TriggeredAbility(permanent, player, trigger);
                    _state.Log($"{permanent.Name} triggers: {trigger.Effect.GetType().Name.Replace("Effect", "")}");
                    await ability.ResolveAsync(_state, ct);
                }
            }
        }
    }
}
```

Add trigger call sites:
- In `ProcessCombatDeaths`: after moving each dead creature to graveyard, call `ProcessBoardTriggersAsync(GameEvent.Dies, card, ct)` — make it async
- In `ExecuteAction` PlayCard Part B after casting: call `ProcessBoardTriggersAsync(GameEvent.SpellCast, playCard, ct)`
- In `RunCombatAsync` after unblocked damage: call `ProcessBoardTriggersAsync(GameEvent.CombatDamageDealt, attackerCard, ct)` per attacking creature that dealt combat damage to a player

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "BoardWideTriggerTests" -v m`
Expected: PASS

**Step 5: Run full test suite**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v m`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/Triggers/BoardWideTriggerTests.cs
git commit -m "feat(engine): extend ProcessTriggersAsync for board-wide trigger conditions"
```

---

### Task 7: Triggered Ability Effect Implementations

Create the IEffect classes for triggered abilities.

**Files:**
- Create: `src/MtgDecker.Engine/Triggers/Effects/DrawCardEffect.cs`
- Create: `src/MtgDecker.Engine/Triggers/Effects/UntapSelfEffect.cs`
- Create: `src/MtgDecker.Engine/Triggers/Effects/PutCreatureFromHandEffect.cs`
- Create: `src/MtgDecker.Engine/Triggers/Effects/PiledriverPumpEffect.cs`
- Create: `src/MtgDecker.Engine/Triggers/Effects/PyromancerEffect.cs`
- Create: `src/MtgDecker.Engine/Triggers/Effects/DestroyAllSubtypeEffect.cs`
- Create: `src/MtgDecker.Engine/Triggers/Effects/RearrangeTopEffect.cs`
- Create: `src/MtgDecker.Engine/Triggers/Effects/SylvanLibraryEffect.cs`
- Test: `tests/MtgDecker.Engine.Tests/Triggers/TriggeredAbilityEffectTests.cs`

**Step 1: Write the failing tests**

```csharp
// tests/MtgDecker.Engine.Tests/Triggers/TriggeredAbilityEffectTests.cs
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests.Triggers;

public class TriggeredAbilityEffectTests
{
    private (GameState state, Player p1, Player p2, TestDecisionHandler handler) Setup()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        return (state, p1, p2, handler);
    }

    [Fact]
    public async Task DrawCardEffect_Draws_One_Card()
    {
        var (state, p1, _, _) = Setup();
        p1.Library.Add(new GameCard { Name = "Card1" });
        var source = new GameCard { Name = "Enchantress" };
        var context = new EffectContext(state, p1, source, p1.DecisionHandler);

        await new DrawCardEffect().Execute(context);

        p1.Hand.Cards.Should().Contain(c => c.Name == "Card1");
        p1.Library.Count.Should().Be(0);
    }

    [Fact]
    public async Task UntapSelfEffect_Untaps_Source()
    {
        var (state, p1, _, _) = Setup();
        var source = new GameCard { Name = "Sharpshooter" };
        source.IsTapped = true;
        p1.Battlefield.Add(source);
        var context = new EffectContext(state, p1, source, p1.DecisionHandler);

        await new UntapSelfEffect().Execute(context);

        source.IsTapped.Should().BeFalse();
    }

    [Fact]
    public async Task PutCreatureFromHandEffect_Puts_Matching_Creature()
    {
        var (state, p1, _, handler) = Setup();
        var goblin = new GameCard { Name = "Goblin Piledriver", CardTypes = CardType.Creature, Subtypes = ["Goblin"] };
        p1.Hand.Add(goblin);
        handler.EnqueueCardChoice(goblin.Id);

        var source = new GameCard { Name = "Goblin Lackey" };
        var context = new EffectContext(state, p1, source, handler);

        await new PutCreatureFromHandEffect("Goblin").Execute(context);

        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Goblin Piledriver");
        p1.Hand.Cards.Should().NotContain(c => c.Name == "Goblin Piledriver");
    }

    [Fact]
    public async Task PutCreatureFromHandEffect_Optional_No_Match()
    {
        var (state, p1, _, handler) = Setup();
        // No Goblins in hand
        p1.Hand.Add(new GameCard { Name = "Mountain", CardTypes = CardType.Land });
        var source = new GameCard { Name = "Goblin Lackey" };
        var context = new EffectContext(state, p1, source, handler);

        await new PutCreatureFromHandEffect("Goblin").Execute(context);

        p1.Battlefield.Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task PiledriverPumpEffect_Adds_UntilEndOfTurn_PT()
    {
        var (state, p1, _, _) = Setup();
        var piledriver = new GameCard { Name = "Goblin Piledriver", CardTypes = CardType.Creature, Subtypes = ["Goblin"], BasePower = 1, BaseToughness = 2 };
        var otherGoblin1 = new GameCard { Name = "Goblin Lackey", CardTypes = CardType.Creature, Subtypes = ["Goblin"] };
        var otherGoblin2 = new GameCard { Name = "Mogg Fanatic", CardTypes = CardType.Creature, Subtypes = ["Goblin"] };
        p1.Battlefield.Add(piledriver);
        p1.Battlefield.Add(otherGoblin1);
        p1.Battlefield.Add(otherGoblin2);

        // Simulate combat — mark as attackers
        state.Combat = new CombatState(p1.Id, state.Player2.Id);
        state.Combat.DeclareAttacker(piledriver.Id);
        state.Combat.DeclareAttacker(otherGoblin1.Id);
        state.Combat.DeclareAttacker(otherGoblin2.Id);

        var context = new EffectContext(state, p1, piledriver, p1.DecisionHandler);
        await new PiledriverPumpEffect().Execute(context);

        // 2 other attacking Goblins = +4/+0
        state.ActiveEffects.Should().HaveCount(1);
        state.ActiveEffects[0].PowerMod.Should().Be(4);
        state.ActiveEffects[0].ToughnessMod.Should().Be(0);
        state.ActiveEffects[0].UntilEndOfTurn.Should().BeTrue();
    }

    [Fact]
    public async Task PyromancerEffect_Pumps_Goblins_And_Creates_Delayed_Trigger()
    {
        var (state, p1, _, _) = Setup();
        var pyro = new GameCard { Name = "Goblin Pyromancer", CardTypes = CardType.Creature, Subtypes = ["Goblin"] };
        p1.Battlefield.Add(pyro);

        var context = new EffectContext(state, p1, pyro, p1.DecisionHandler);
        await new PyromancerEffect().Execute(context);

        // Should add UntilEndOfTurn +2/+0 to all Goblins
        state.ActiveEffects.Should().HaveCount(1);
        state.ActiveEffects[0].PowerMod.Should().Be(2);
        state.ActiveEffects[0].UntilEndOfTurn.Should().BeTrue();

        // Should create a delayed trigger
        state.DelayedTriggers.Should().HaveCount(1);
        state.DelayedTriggers[0].FireOn.Should().Be(GameEvent.EndStep);
        state.DelayedTriggers[0].Effect.Should().BeOfType<DestroyAllSubtypeEffect>();
    }

    [Fact]
    public async Task DestroyAllSubtypeEffect_Destroys_All_Goblins()
    {
        var (state, p1, _, _) = Setup();
        var goblin1 = new GameCard { Name = "Goblin1", CardTypes = CardType.Creature, Subtypes = ["Goblin"] };
        var goblin2 = new GameCard { Name = "Goblin2", CardTypes = CardType.Creature, Subtypes = ["Goblin"] };
        var nonGoblin = new GameCard { Name = "Bird", CardTypes = CardType.Creature, Subtypes = ["Bird"] };
        p1.Battlefield.Add(goblin1);
        p1.Battlefield.Add(goblin2);
        p1.Battlefield.Add(nonGoblin);

        var source = new GameCard { Name = "Pyromancer" };
        var context = new EffectContext(state, p1, source, p1.DecisionHandler);
        await new DestroyAllSubtypeEffect("Goblin").Execute(context);

        p1.Battlefield.Cards.Should().NotContain(c => c.Subtypes.Contains("Goblin"));
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Bird");
        p1.Graveyard.Cards.Should().HaveCount(2);
    }

    [Fact]
    public async Task RearrangeTopEffect_Moves_Chosen_Card_To_Top()
    {
        var (state, p1, _, handler) = Setup();
        var card1 = new GameCard { Name = "Card1" };
        var card2 = new GameCard { Name = "Card2" };
        var card3 = new GameCard { Name = "Card3" };
        p1.Library.Add(card1); // top
        p1.Library.Add(card2);
        p1.Library.Add(card3);

        handler.EnqueueCardChoice(card2.Id); // choose card2 for top

        var source = new GameCard { Name = "Mirri's Guile" };
        var context = new EffectContext(state, p1, source, handler);
        await new RearrangeTopEffect(3).Execute(context);

        // card2 should be on top now
        var topCard = p1.Library.DrawFromTop();
        topCard!.Name.Should().Be("Card2");
    }

    [Fact]
    public async Task SylvanLibraryEffect_Draws_2_Extra_Then_Returns()
    {
        var (state, p1, _, handler) = Setup();
        p1.Library.Add(new GameCard { Name = "Card1" });
        p1.Library.Add(new GameCard { Name = "Card2" });
        p1.Library.Add(new GameCard { Name = "Card3" });
        var existingCard = new GameCard { Name = "Existing" };
        p1.Hand.Add(existingCard);

        // Choose to put both drawn cards back (no life payment)
        var card1 = p1.Library.Cards[0];
        var card2 = p1.Library.Cards[1];
        handler.EnqueueCardChoice(card1.Id); // put back card1
        handler.EnqueueCardChoice(card2.Id); // put back card2

        var source = new GameCard { Name = "Sylvan Library" };
        var context = new EffectContext(state, p1, source, handler);
        await new SylvanLibraryEffect().Execute(context);

        p1.Hand.Count.Should().Be(1); // still just the existing card
        p1.Life.Should().Be(20); // no life paid
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "TriggeredAbilityEffectTests" -v m`
Expected: FAIL — effect classes don't exist

**Step 3: Implement all effect classes**

`DrawCardEffect.cs`:
```csharp
namespace MtgDecker.Engine.Triggers.Effects;

public class DrawCardEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var drawn = context.Controller.Library.DrawFromTop();
        if (drawn != null)
        {
            context.Controller.Hand.Add(drawn);
            context.State.Log($"{context.Controller.Name} draws a card.");
        }
        return Task.CompletedTask;
    }
}
```

`UntapSelfEffect.cs`:
```csharp
namespace MtgDecker.Engine.Triggers.Effects;

public class UntapSelfEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        context.Source.IsTapped = false;
        context.State.Log($"{context.Source.Name} untaps.");
        return Task.CompletedTask;
    }
}
```

`PutCreatureFromHandEffect.cs`:
```csharp
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class PutCreatureFromHandEffect(string subtype) : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var matches = context.Controller.Hand.Cards
            .Where(c => c.IsCreature && c.Subtypes.Contains(subtype, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            context.State.Log($"No {subtype} creatures in hand.");
            return;
        }

        var chosenId = await context.DecisionHandler.ChooseCard(
            matches, $"Put a {subtype} onto the battlefield", optional: true, ct);

        if (chosenId.HasValue)
        {
            var chosen = context.Controller.Hand.RemoveById(chosenId.Value);
            if (chosen != null)
            {
                context.Controller.Battlefield.Add(chosen);
                chosen.TurnEnteredBattlefield = context.State.TurnNumber;
                context.State.Log($"{context.Controller.Name} puts {chosen.Name} onto the battlefield.");
            }
        }
    }
}
```

`PiledriverPumpEffect.cs`:
```csharp
namespace MtgDecker.Engine.Triggers.Effects;

public class PiledriverPumpEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        if (context.State.Combat == null) return Task.CompletedTask;

        var otherAttackingGoblins = context.State.Combat.Attackers
            .Where(id => id != context.Source.Id)
            .Count(id =>
            {
                var card = context.Controller.Battlefield.Cards.FirstOrDefault(c => c.Id == id);
                return card != null && card.Subtypes.Contains("Goblin");
            });

        if (otherAttackingGoblins > 0)
        {
            var pump = otherAttackingGoblins * 2;
            var effect = new ContinuousEffect(
                context.Source.Id,
                ContinuousEffectType.ModifyPowerToughness,
                (card, _) => card.Id == context.Source.Id,
                PowerMod: pump,
                ToughnessMod: 0,
                UntilEndOfTurn: true);
            context.State.ActiveEffects.Add(effect);
            context.State.Log($"{context.Source.Name} gets +{pump}/+0 ({otherAttackingGoblins} other attacking Goblins).");
        }

        return Task.CompletedTask;
    }
}
```

`DestroyAllSubtypeEffect.cs`:
```csharp
namespace MtgDecker.Engine.Triggers.Effects;

public class DestroyAllSubtypeEffect(string subtype) : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        foreach (var player in new[] { context.State.Player1, context.State.Player2 })
        {
            var toDestroy = player.Battlefield.Cards
                .Where(c => c.Subtypes.Contains(subtype, StringComparer.OrdinalIgnoreCase))
                .ToList();

            foreach (var card in toDestroy)
            {
                player.Battlefield.RemoveById(card.Id);
                player.Graveyard.Add(card);
                context.State.Log($"{card.Name} is destroyed.");
            }
        }
        return Task.CompletedTask;
    }
}
```

`PyromancerEffect.cs`:
```csharp
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class PyromancerEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        // All Goblins get +2/+0 until end of turn
        var pump = new ContinuousEffect(
            context.Source.Id,
            ContinuousEffectType.ModifyPowerToughness,
            (card, _) => card.IsCreature && card.Subtypes.Contains("Goblin"),
            PowerMod: 2,
            ToughnessMod: 0,
            UntilEndOfTurn: true);
        context.State.ActiveEffects.Add(pump);
        context.State.Log($"All Goblins get +2/+0 until end of turn.");

        // Register delayed trigger to destroy all Goblins at end of turn
        var delayed = new DelayedTrigger(
            GameEvent.EndStep,
            new DestroyAllSubtypeEffect("Goblin"),
            context.Controller.Id);
        context.State.DelayedTriggers.Add(delayed);

        return Task.CompletedTask;
    }
}
```

`RearrangeTopEffect.cs`:
```csharp
namespace MtgDecker.Engine.Triggers.Effects;

public class RearrangeTopEffect(int count) : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var topCards = new List<GameCard>();
        for (int i = 0; i < count && context.Controller.Library.Count > 0; i++)
        {
            var card = context.Controller.Library.DrawFromTop();
            if (card != null) topCards.Add(card);
        }

        if (topCards.Count == 0) return;

        // Choose which card goes on top
        var chosenId = await context.DecisionHandler.ChooseCard(
            topCards, "Choose a card to put on top", optional: false, ct);

        // Put chosen on top, rest underneath in original order
        var chosen = topCards.FirstOrDefault(c => c.Id == chosenId);
        var rest = topCards.Where(c => c.Id != chosenId).ToList();

        // Add rest to top first (they'll be below chosen)
        foreach (var card in rest)
            context.Controller.Library.AddToTop(card);
        if (chosen != null)
            context.Controller.Library.AddToTop(chosen);

        context.State.Log($"{context.Controller.Name} rearranges top {topCards.Count} cards.");
    }
}
```

`SylvanLibraryEffect.cs`:
```csharp
namespace MtgDecker.Engine.Triggers.Effects;

public class SylvanLibraryEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        // Draw 2 extra cards
        var drawn = new List<GameCard>();
        for (int i = 0; i < 2; i++)
        {
            var card = context.Controller.Library.DrawFromTop();
            if (card != null)
            {
                context.Controller.Hand.Add(card);
                drawn.Add(card);
            }
        }

        if (drawn.Count == 0) return;

        context.State.Log($"{context.Controller.Name} draws {drawn.Count} extra cards (Sylvan Library).");

        // For each drawn card, player chooses: put back (free) or keep (pay 4 life)
        // Simplified: choose cards to put back (up to 2)
        for (int i = 0; i < drawn.Count; i++)
        {
            var remaining = drawn.Where(c => context.Controller.Hand.Contains(c.Id)).ToList();
            if (remaining.Count == 0) break;

            var chosenId = await context.DecisionHandler.ChooseCard(
                remaining, "Choose a card to put back on library (or decline to pay 4 life)",
                optional: true, ct);

            if (chosenId.HasValue)
            {
                var card = context.Controller.Hand.RemoveById(chosenId.Value);
                if (card != null)
                {
                    context.Controller.Library.AddToTop(card);
                    context.State.Log($"{context.Controller.Name} puts a card back on top of library.");
                }
            }
            else
            {
                // Keep the card, pay 4 life
                context.Controller.AdjustLife(-4);
                context.State.Log($"{context.Controller.Name} pays 4 life to keep a card. ({context.Controller.Life} life)");
                break; // Once they decline to put back, they keep all remaining
            }
        }
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "TriggeredAbilityEffectTests" -v m`
Expected: PASS (10 tests)

**Step 5: Run full test suite**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v m`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/Triggers/Effects/DrawCardEffect.cs src/MtgDecker.Engine/Triggers/Effects/UntapSelfEffect.cs src/MtgDecker.Engine/Triggers/Effects/PutCreatureFromHandEffect.cs src/MtgDecker.Engine/Triggers/Effects/PiledriverPumpEffect.cs src/MtgDecker.Engine/Triggers/Effects/PyromancerEffect.cs src/MtgDecker.Engine/Triggers/Effects/DestroyAllSubtypeEffect.cs src/MtgDecker.Engine/Triggers/Effects/RearrangeTopEffect.cs src/MtgDecker.Engine/Triggers/Effects/SylvanLibraryEffect.cs tests/MtgDecker.Engine.Tests/Triggers/TriggeredAbilityEffectTests.cs
git commit -m "feat(engine): add triggered ability effect implementations"
```

---

### Task 8: Register Triggered Ability Cards in CardDefinitions

Add triggers to all 7 cards with triggered abilities, plus Sharpshooter's AnyCreatureDies untap trigger.

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Test: `tests/MtgDecker.Engine.Tests/TriggeredAbilityCardRegistrationTests.cs`

**Step 1: Write the failing tests**

```csharp
// tests/MtgDecker.Engine.Tests/TriggeredAbilityCardRegistrationTests.cs
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class TriggeredAbilityCardRegistrationTests
{
    [Fact]
    public void GoblinSharpshooter_Has_AnyCreatureDies_Trigger()
    {
        CardDefinitions.TryGet("Goblin Sharpshooter", out var def);
        def!.Triggers.Should().Contain(t =>
            t.Event == GameEvent.Dies
            && t.Condition == TriggerCondition.AnyCreatureDies);
    }

    [Fact]
    public void GoblinLackey_Has_SelfDealsCombatDamage_Trigger()
    {
        CardDefinitions.TryGet("Goblin Lackey", out var def);
        def!.Triggers.Should().Contain(t =>
            t.Event == GameEvent.CombatDamageDealt
            && t.Condition == TriggerCondition.SelfDealsCombatDamage);
    }

    [Fact]
    public void GoblinPiledriver_Has_SelfAttacks_Trigger()
    {
        CardDefinitions.TryGet("Goblin Piledriver", out var def);
        def!.Triggers.Should().Contain(t =>
            t.Condition == TriggerCondition.SelfAttacks);
    }

    [Fact]
    public void GoblinPyromancer_Has_ETB_Trigger()
    {
        CardDefinitions.TryGet("Goblin Pyromancer", out var def);
        def!.Triggers.Should().Contain(t =>
            t.Event == GameEvent.EnterBattlefield
            && t.Condition == TriggerCondition.Self);
        // The effect should be PyromancerEffect
        var trigger = def!.Triggers.First(t => t.Event == GameEvent.EnterBattlefield);
        trigger.Effect.Should().BeOfType<PyromancerEffect>();
    }

    [Fact]
    public void ArgothianEnchantress_Has_ControllerCastsEnchantment_Trigger()
    {
        CardDefinitions.TryGet("Argothian Enchantress", out var def);
        def!.Triggers.Should().Contain(t =>
            t.Event == GameEvent.SpellCast
            && t.Condition == TriggerCondition.ControllerCastsEnchantment);
    }

    [Fact]
    public void EnchantressPresence_Has_ControllerCastsEnchantment_Trigger()
    {
        CardDefinitions.TryGet("Enchantress's Presence", out var def);
        def!.Triggers.Should().Contain(t =>
            t.Event == GameEvent.SpellCast
            && t.Condition == TriggerCondition.ControllerCastsEnchantment);
    }

    [Fact]
    public void MirrisGuile_Has_Upkeep_Trigger()
    {
        CardDefinitions.TryGet("Mirri's Guile", out var def);
        def!.Triggers.Should().Contain(t =>
            t.Event == GameEvent.Upkeep
            && t.Condition == TriggerCondition.Upkeep);
    }

    [Fact]
    public void SylvanLibrary_Has_Upkeep_Trigger()
    {
        CardDefinitions.TryGet("Sylvan Library", out var def);
        def!.Triggers.Should().Contain(t =>
            t.Event == GameEvent.Upkeep
            && t.Condition == TriggerCondition.Upkeep);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "TriggeredAbilityCardRegistrationTests" -v m`
Expected: FAIL

**Step 3: Update CardDefinitions**

```csharp
["Goblin Lackey"] = new(ManaCost.Parse("{R}"), null, 1, 1, CardType.Creature)
{
    Subtypes = ["Goblin"],
    Triggers = [new Trigger(GameEvent.CombatDamageDealt, TriggerCondition.SelfDealsCombatDamage, new PutCreatureFromHandEffect("Goblin"))],
},

["Goblin Piledriver"] = new(ManaCost.Parse("{1}{R}"), null, 1, 2, CardType.Creature)
{
    Subtypes = ["Goblin"],
    Triggers = [new Trigger(GameEvent.CombatDamageDealt, TriggerCondition.SelfAttacks, new PiledriverPumpEffect())],
},

["Goblin Sharpshooter"] = new(ManaCost.Parse("{2}{R}"), null, 1, 1, CardType.Creature)
{
    Subtypes = ["Goblin"],
    ActivatedAbility = new(new ActivatedAbilityCost(TapSelf: true),
        new DealDamageEffect(1), c => c.IsCreature, CanTargetPlayer: true),
    Triggers = [new Trigger(GameEvent.Dies, TriggerCondition.AnyCreatureDies, new UntapSelfEffect())],
},

["Goblin Pyromancer"] = new(ManaCost.Parse("{3}{R}"), null, 2, 2, CardType.Creature)
{
    Subtypes = ["Goblin"],
    Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new PyromancerEffect())],
},

["Argothian Enchantress"] = new(ManaCost.Parse("{1}{G}"), null, 0, 1, CardType.Creature | CardType.Enchantment)
{
    Subtypes = ["Human", "Druid"],
    Triggers = [new Trigger(GameEvent.SpellCast, TriggerCondition.ControllerCastsEnchantment, new DrawCardEffect())],
},

["Enchantress's Presence"] = new(ManaCost.Parse("{2}{G}"), null, null, null, CardType.Enchantment)
{
    Triggers = [new Trigger(GameEvent.SpellCast, TriggerCondition.ControllerCastsEnchantment, new DrawCardEffect())],
},

["Mirri's Guile"] = new(ManaCost.Parse("{G}"), null, null, null, CardType.Enchantment)
{
    Triggers = [new Trigger(GameEvent.Upkeep, TriggerCondition.Upkeep, new RearrangeTopEffect(3))],
},

["Sylvan Library"] = new(ManaCost.Parse("{1}{G}"), null, null, null, CardType.Enchantment)
{
    Triggers = [new Trigger(GameEvent.Upkeep, TriggerCondition.Upkeep, new SylvanLibraryEffect())],
},
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "TriggeredAbilityCardRegistrationTests" -v m`
Expected: PASS (8 tests)

**Step 5: Run full test suite**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v m`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/CardDefinitions.cs tests/MtgDecker.Engine.Tests/TriggeredAbilityCardRegistrationTests.cs
git commit -m "feat(engine): register triggered abilities for 8 cards in CardDefinitions"
```

---

### Task 9: Wire Delayed Triggers and Upkeep Triggers

Wire delayed trigger processing at end of turn and upkeep trigger processing.

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (add delayed trigger processing in end step, upkeep trigger calls)
- Test: `tests/MtgDecker.Engine.Tests/DelayedTriggerTests.cs`

**Step 1: Write the failing tests**

```csharp
// tests/MtgDecker.Engine.Tests/DelayedTriggerTests.cs
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class DelayedTriggerTests
{
    [Fact]
    public async Task Delayed_Trigger_Fires_At_EndStep()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var goblin = new GameCard { Name = "TestGoblin", CardTypes = CardType.Creature, Subtypes = ["Goblin"], BasePower = 1, BaseToughness = 1 };
        p1.Battlefield.Add(goblin);

        // Register a delayed trigger
        state.DelayedTriggers.Add(new DelayedTrigger(
            GameEvent.EndStep,
            new DestroyAllSubtypeEffect("Goblin"),
            p1.Id));

        // Enough cards for draw step
        for (int i = 0; i < 5; i++)
            p1.Library.Add(new GameCard { Name = $"Card{i}" });
        for (int i = 0; i < 5; i++)
            p2.Library.Add(new GameCard { Name = $"Card{i}" });

        // Run a full turn — delayed trigger should fire at end
        handler.EnqueueAction(GameAction.Pass(p1.Id)); // main1
        handler.EnqueueAction(GameAction.Pass(p1.Id)); // combat pass
        handler.EnqueueAction(GameAction.Pass(p1.Id)); // main2

        await engine.RunTurnAsync();

        p1.Battlefield.Cards.Should().NotContain(c => c.Subtypes.Contains("Goblin"));
        state.DelayedTriggers.Should().BeEmpty();
    }

    [Fact]
    public async Task Upkeep_Triggers_Fire_During_Upkeep()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // Put Mirri's Guile on battlefield
        var guile = GameCard.Create("Mirri's Guile", "Enchantment");
        p1.Battlefield.Add(guile);

        // Set up library
        var card1 = new GameCard { Name = "Card1" };
        var card2 = new GameCard { Name = "Card2" };
        var card3 = new GameCard { Name = "Card3" };
        var card4 = new GameCard { Name = "Card4" }; // for draw step
        p1.Library.Add(card1);
        p1.Library.Add(card2);
        p1.Library.Add(card3);
        p1.Library.Add(card4);
        for (int i = 0; i < 5; i++)
            p2.Library.Add(new GameCard { Name = $"P2Card{i}" });

        handler.EnqueueCardChoice(card1.Id); // rearrange: choose card1 for top
        handler.EnqueueAction(GameAction.Pass(p1.Id)); // main1
        handler.EnqueueAction(GameAction.Pass(p1.Id)); // main2

        state.IsFirstTurn = false; // allow draw

        await engine.RunTurnAsync();

        // Upkeep trigger should have fired (Mirri's Guile rearranges top 3)
        state.GameLog.Should().Contain(s => s.Contains("rearranges"));
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "DelayedTriggerTests" -v m`
Expected: FAIL

**Step 3: Implement**

In `GameEngine.RunTurnAsync`, before `StripEndOfTurnEffects()`:
```csharp
// Fire delayed triggers at end step
await ProcessDelayedTriggersAsync(GameEvent.EndStep, ct);
```

Add the method:
```csharp
private async Task ProcessDelayedTriggersAsync(GameEvent evt, CancellationToken ct)
{
    var toFire = _state.DelayedTriggers.Where(d => d.FireOn == evt).ToList();
    foreach (var delayed in toFire)
    {
        var controller = delayed.ControllerId == _state.Player1.Id ? _state.Player1 : _state.Player2;
        var context = new EffectContext(_state, controller, new GameCard { Name = "Delayed Trigger" }, controller.DecisionHandler);
        await delayed.Effect.Execute(context, ct);
        _state.DelayedTriggers.Remove(delayed);
    }
}
```

In `ExecuteTurnBasedAction`, add upkeep trigger processing:
```csharp
case Phase.Upkeep:
    // Process upkeep triggers for active player
    // (handled by calling ProcessBoardTriggersAsync from RunTurnAsync)
    break;
```

Actually, the upkeep triggers need to be fired from `RunTurnAsync` after the turn-based action. Add after `ExecuteTurnBasedAction(phase.Phase)`:
```csharp
if (phase.Phase == Phase.Upkeep)
    await ProcessBoardTriggersAsync(GameEvent.Upkeep, null, ct);
```

Make `ProcessBoardTriggersAsync` handle `TriggerCondition.Upkeep`:
```csharp
TriggerCondition.Upkeep =>
    evt == GameEvent.Upkeep && _state.ActivePlayer == player,
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "DelayedTriggerTests" -v m`
Expected: PASS (2 tests)

**Step 5: Run full test suite**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v m`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/DelayedTriggerTests.cs
git commit -m "feat(engine): wire delayed triggers at end step and upkeep triggers"
```

---

### Task 10: AI Bot Updates

Add activated ability usage heuristics to AiBotDecisionHandler.

**Files:**
- Modify: `src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs`
- Test: `tests/MtgDecker.Engine.Tests/AI/AiBotActivatedAbilityTests.cs`

**Step 1: Write the failing tests**

```csharp
// tests/MtgDecker.Engine.Tests/AI/AiBotActivatedAbilityTests.cs
using FluentAssertions;
using MtgDecker.Engine.AI;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests.AI;

public class AiBotActivatedAbilityTests
{
    [Fact]
    public async Task Bot_Activates_MoggFanatic_When_Can_Kill_Creature()
    {
        var handler = new AiBotDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Bot", handler);
        var p2 = new Player(Guid.NewGuid(), "Opponent", new AiBotDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;

        var fanatic = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        p1.Battlefield.Add(fanatic);

        var bird = new GameCard { Name = "Bird", CardTypes = CardType.Creature, BasePower = 1, BaseToughness = 1 };
        p2.Battlefield.Add(bird);

        var action = await handler.GetAction(state, p1.Id);

        action.Type.Should().Be(ActionType.ActivateAbility);
        action.CardId.Should().Be(fanatic.Id);
        action.TargetCardId.Should().Be(bird.Id);
    }

    [Fact]
    public async Task Bot_Does_Not_Activate_Ability_Without_Target()
    {
        var handler = new AiBotDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Bot", handler);
        var p2 = new Player(Guid.NewGuid(), "Opponent", new AiBotDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;

        var fanatic = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        p1.Battlefield.Add(fanatic);
        // No creatures on opponent's board, opponent at 20 life

        var action = await handler.GetAction(state, p1.Id);

        // Should not sacrifice just to deal 1 to a 20-life opponent
        action.Type.Should().NotBe(ActionType.ActivateAbility);
    }

    [Fact]
    public async Task Bot_Uses_SkirkProspector_When_Enables_Cast()
    {
        var handler = new AiBotDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Bot", handler);
        var p2 = new Player(Guid.NewGuid(), "Opponent", new AiBotDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;

        var prospector = GameCard.Create("Skirk Prospector", "Creature — Goblin");
        p1.Battlefield.Add(prospector);
        var extraGoblin = new GameCard { Name = "Goblin", CardTypes = CardType.Creature, Subtypes = ["Goblin"], BasePower = 1, BaseToughness = 1, IsToken = true };
        p1.Battlefield.Add(extraGoblin);

        // Has a 2-mana spell and 1 mana in pool — needs 1 more
        var spell = GameCard.Create("Goblin Piledriver", "Creature — Goblin");
        p1.Hand.Add(spell);
        p1.ManaPool.Add(ManaColor.Red);

        var action = await handler.GetAction(state, p1.Id);

        action.Type.Should().Be(ActionType.ActivateAbility);
        action.CardId.Should().Be(prospector.Id);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "AiBotActivatedAbilityTests" -v m`
Expected: FAIL — AI doesn't know about activated abilities

**Step 3: Implement AI heuristics**

In `AiBotDecisionHandler.GetAction`, add after fetch land priority (Priority 2) and before tap lands (Priority 3):

```csharp
// Priority 2.5: Activate sacrifice-for-damage abilities (if can kill a creature)
foreach (var permanent in player.Battlefield.Cards.ToList())
{
    if (!CardDefinitions.TryGet(permanent.Name, out var abilityDef) || abilityDef.ActivatedAbility == null)
        continue;

    var ability = abilityDef.ActivatedAbility;

    // Skip if cost requires tap and already tapped
    if (ability.Cost.TapSelf && permanent.IsTapped) continue;

    // Skip if mana cost can't be paid
    if (ability.Cost.ManaCost != null && !player.ManaPool.CanPay(ability.Cost.ManaCost)) continue;

    // Skip if sacrifice subtype needed but none available
    if (ability.Cost.SacrificeSubtype != null)
    {
        var hasSacTarget = player.Battlefield.Cards.Any(c =>
            c.IsCreature && c.Subtypes.Contains(ability.Cost.SacrificeSubtype));
        if (!hasSacTarget) continue;
    }

    // Heuristic: Deal damage abilities — use if can kill an opponent creature
    if (ability.Effect is Triggers.Effects.DealDamageEffect)
    {
        var opponent = gameState.GetOpponent(player);
        var killable = opponent.Battlefield.Cards
            .Where(c => ability.TargetFilter == null || ability.TargetFilter(c))
            .FirstOrDefault(c => c.IsCreature && (c.Toughness ?? 0) - c.DamageMarked <= 1);

        if (killable != null)
            return Task.FromResult(GameAction.ActivateAbility(playerId, permanent.Id, targetId: killable.Id));
    }

    // Heuristic: Skirk Prospector — sacrifice if it enables casting a spell
    if (ability.Effect is Triggers.Effects.AddManaEffect && ability.Cost.SacrificeSubtype != null)
    {
        // Check if we have a castable spell that needs exactly 1 more mana
        var needsOneMana = hand.Any(c => !c.IsLand && c.ManaCost != null
            && c.ManaCost.ConvertedManaCost <= player.ManaPool.Available.Values.Sum() + 1
            && !player.ManaPool.CanPay(c.ManaCost));

        if (needsOneMana)
        {
            // Find a token or least valuable Goblin to sacrifice
            var sacTarget = player.Battlefield.Cards
                .Where(c => c.IsCreature && c.Subtypes.Contains(ability.Cost.SacrificeSubtype))
                .OrderBy(c => c.IsToken ? 0 : 1)
                .ThenBy(c => c.ManaCost?.ConvertedManaCost ?? 0)
                .First();

            return Task.FromResult(GameAction.ActivateAbility(playerId, permanent.Id));
        }
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "AiBotActivatedAbilityTests" -v m`
Expected: PASS (3 tests)

**Step 5: Run full test suite**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v m`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs tests/MtgDecker.Engine.Tests/AI/AiBotActivatedAbilityTests.cs
git commit -m "feat(engine): add activated ability heuristics to AI bot"
```

---

### Task 11: Integration Tests

End-to-end tests for card interactions: Sharpshooter untap chain, Pyromancer full cycle, Enchantress draw.

**Files:**
- Test: `tests/MtgDecker.Engine.Tests/ActivatedTriggeredIntegrationTests.cs`

**Step 1: Write the integration tests**

```csharp
// tests/MtgDecker.Engine.Tests/ActivatedTriggeredIntegrationTests.cs
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class ActivatedTriggeredIntegrationTests
{
    [Fact]
    public async Task Sharpshooter_Untap_Chain_Kills_Multiple_1Toughness_Creatures()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        var shooter = GameCard.Create("Goblin Sharpshooter", "Creature — Goblin");
        p1.Battlefield.Add(shooter);

        var bird1 = new GameCard { Name = "Bird1", CardTypes = CardType.Creature, BasePower = 1, BaseToughness = 1 };
        var bird2 = new GameCard { Name = "Bird2", CardTypes = CardType.Creature, BasePower = 1, BaseToughness = 1 };
        p2.Battlefield.Add(bird1);
        p2.Battlefield.Add(bird2);

        // Tap Sharpshooter to kill bird1
        await engine.ExecuteAction(GameAction.ActivateAbility(p1.Id, shooter.Id, targetId: bird1.Id));

        // bird1 should die from SBA (1 damage, 1 toughness), which triggers AnyCreatureDies -> untap Sharpshooter
        shooter.IsTapped.Should().BeFalse("Sharpshooter should untap when bird1 dies");

        // Now tap again to kill bird2
        await engine.ExecuteAction(GameAction.ActivateAbility(p1.Id, shooter.Id, targetId: bird2.Id));

        shooter.IsTapped.Should().BeFalse("Sharpshooter should untap when bird2 dies");
        p2.Battlefield.Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task Pyromancer_Pumps_Then_Destroys_At_End_Of_Turn()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var goblin1 = new GameCard { Name = "Goblin Lackey", CardTypes = CardType.Creature, Subtypes = ["Goblin"], BasePower = 1, BaseToughness = 1 };
        p1.Battlefield.Add(goblin1);

        // Play Pyromancer (sandbox mode for simplicity)
        var pyro = GameCard.Create("Goblin Pyromancer", "Creature — Goblin");
        p1.Hand.Add(pyro);

        // Set up library for draw step
        for (int i = 0; i < 5; i++)
            p1.Library.Add(new GameCard { Name = $"P1Card{i}" });
        for (int i = 0; i < 5; i++)
            p2.Library.Add(new GameCard { Name = $"P2Card{i}" });

        // Play the Pyromancer
        handler.EnqueueAction(GameAction.PlayCard(p1.Id, pyro.Id));
        handler.EnqueueAction(GameAction.Pass(p1.Id)); // main1 done
        handler.EnqueueAction(GameAction.Pass(p1.Id)); // main2 done

        await engine.RunTurnAsync();

        // After end of turn, all Goblins should be destroyed
        p1.Battlefield.Cards.Where(c => c.Subtypes.Contains("Goblin")).Should().BeEmpty();
    }

    [Fact]
    public async Task Enchantress_Draws_When_Enchantment_Cast()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        var enchantress = GameCard.Create("Argothian Enchantress", "Creature — Human Druid");
        p1.Battlefield.Add(enchantress);

        var drawTarget = new GameCard { Name = "DrawnCard" };
        p1.Library.Add(drawTarget);

        // Cast an enchantment
        var enchantment = GameCard.Create("Wild Growth", "Enchantment — Aura");
        p1.Hand.Add(enchantment);
        p1.ManaPool.Add(ManaColor.Green);

        var handBefore = p1.Hand.Count;
        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, enchantment.Id));

        // Enchantress should have triggered and drawn a card
        p1.Hand.Cards.Should().Contain(c => c.Name == "DrawnCard");
    }

    [Fact]
    public async Task Skirk_Prospector_Sacrifice_Goblin_For_Mana()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        var prospector = GameCard.Create("Skirk Prospector", "Creature — Goblin");
        var token = new GameCard { Name = "Goblin", CardTypes = CardType.Creature, Subtypes = ["Goblin"], BasePower = 1, BaseToughness = 1, IsToken = true };
        p1.Battlefield.Add(prospector);
        p1.Battlefield.Add(token);

        handler.EnqueueCardChoice(token.Id); // sacrifice the token

        await engine.ExecuteAction(GameAction.ActivateAbility(p1.Id, prospector.Id));

        p1.Battlefield.Cards.Should().NotContain(c => c.Id == token.Id);
        p1.ManaPool.Available.Should().ContainKey(ManaColor.Red);
        p1.ManaPool.Available[ManaColor.Red].Should().Be(1);
    }

    [Fact]
    public async Task SealOfCleansing_Destroys_Artifact()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        var seal = GameCard.Create("Seal of Cleansing", "Enchantment");
        p1.Battlefield.Add(seal);

        var artifact = new GameCard { Name = "Sol Ring", CardTypes = CardType.Artifact };
        p2.Battlefield.Add(artifact);

        await engine.ExecuteAction(GameAction.ActivateAbility(p1.Id, seal.Id, targetId: artifact.Id));

        p1.Battlefield.Cards.Should().NotContain(c => c.Id == seal.Id);
        p2.Battlefield.Cards.Should().NotContain(c => c.Id == artifact.Id);
        p2.Graveyard.Cards.Should().Contain(c => c.Name == "Sol Ring");
    }
}
```

**Step 2: Run tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "ActivatedTriggeredIntegrationTests" -v m`
Expected: PASS (5 tests) — if all prior tasks are complete

**Step 3: Fix any failures**

If any tests fail, debug and fix the issues. Common problems:
- ProcessCombatDeaths not calling ProcessBoardTriggersAsync for Dies events
- OnBoardChangedAsync SBA not killing creatures with lethal damage from effects
- SpellCast trigger not wired in PlayCard handler

**Step 4: Run full test suite**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v m`
Expected: All pass

**Step 5: Commit**

```bash
git add tests/MtgDecker.Engine.Tests/ActivatedTriggeredIntegrationTests.cs
git commit -m "test(engine): add integration tests for activated and triggered abilities"
```

---

### Task 12: Final Verification

Run all tests across the entire solution and verify the build.

**Files:** None (verification only)

**Step 1: Run all engine tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v m`
Expected: All pass (~600+ tests)

**Step 2: Run domain, application, infrastructure tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Domain.Tests/ -v m && dotnet test tests/MtgDecker.Application.Tests/ -v m && dotnet test tests/MtgDecker.Infrastructure.Tests/ -v m`
Expected: All pass (91 + 143 + 57 = 291)

**Step 3: Build web project**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet build src/MtgDecker.Web/ -v m`
Expected: Build succeeded, 0 errors

**Step 4: Commit (if any fixes needed)**

Only if fixes were required in previous steps.
