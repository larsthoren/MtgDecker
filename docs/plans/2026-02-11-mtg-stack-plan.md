# MTG Stack Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add the MTG stack mechanic — spells go on a LIFO stack, players get response windows, instants can be cast at instant speed, and two instants (Swords to Plowshares, Naturalize) resolve with targeting and effects.

**Architecture:** New types (StackObject, TargetInfo, SpellEffect, TargetFilter) live in `MtgDecker.Engine`. The GameEngine gets a CastSpell path that validates timing, prompts for targets, pays mana, and pushes to stack. RunPriorityAsync is modified so "both pass + stack non-empty" resolves the top rather than advancing phases. Existing land drops and sandbox mode remain unchanged.

**Tech Stack:** .NET 10, C# 14, xUnit + FluentAssertions (testing), MudBlazor (UI)

**Design doc:** `docs/plans/2026-02-11-mtg-stack-design.md`

---

### Task 1: StackObject and TargetInfo Value Objects

**Files:**
- Create: `src/MtgDecker.Engine/StackObject.cs`
- Create: `src/MtgDecker.Engine/TargetInfo.cs`
- Create: `tests/MtgDecker.Engine.Tests/StackObjectTests.cs`

**Step 1: Write failing tests for StackObject**

```csharp
// tests/MtgDecker.Engine.Tests/StackObjectTests.cs
using FluentAssertions;

namespace MtgDecker.Engine.Tests;

public class StackObjectTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var card = new GameCard { Name = "Lightning Bolt" };
        var controllerId = Guid.NewGuid();
        var target = new TargetInfo(Guid.NewGuid(), controllerId, Enums.ZoneType.Battlefield);

        var obj = new StackObject(card, controllerId, new Dictionary<Mana.ManaColor, int>(), new List<TargetInfo> { target }, 1);

        obj.Id.Should().NotBeEmpty();
        obj.Card.Should().Be(card);
        obj.ControllerId.Should().Be(controllerId);
        obj.Targets.Should().ContainSingle().Which.Should().Be(target);
        obj.Timestamp.Should().Be(1);
    }

    [Fact]
    public void Constructor_WithNoTargets_HasEmptyList()
    {
        var card = new GameCard { Name = "Forest" };
        var controllerId = Guid.NewGuid();

        var obj = new StackObject(card, controllerId, new Dictionary<Mana.ManaColor, int>(), new List<TargetInfo>(), 0);

        obj.Targets.Should().BeEmpty();
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "StackObjectTests" -v n`
Expected: FAIL — StackObject/TargetInfo not defined

**Step 3: Implement StackObject and TargetInfo**

```csharp
// src/MtgDecker.Engine/TargetInfo.cs
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public record TargetInfo(Guid CardId, Guid PlayerId, ZoneType Zone);
```

```csharp
// src/MtgDecker.Engine/StackObject.cs
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine;

public class StackObject
{
    public Guid Id { get; } = Guid.NewGuid();
    public GameCard Card { get; }
    public Guid ControllerId { get; }
    public Dictionary<ManaColor, int> ManaPaid { get; }
    public IReadOnlyList<TargetInfo> Targets { get; }
    public int Timestamp { get; }

    public StackObject(GameCard card, Guid controllerId, Dictionary<ManaColor, int> manaPaid, List<TargetInfo> targets, int timestamp)
    {
        Card = card;
        ControllerId = controllerId;
        ManaPaid = manaPaid;
        Targets = targets.AsReadOnly();
        Timestamp = timestamp;
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "StackObjectTests" -v n`
Expected: PASS (2 tests)

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/StackObject.cs src/MtgDecker.Engine/TargetInfo.cs tests/MtgDecker.Engine.Tests/StackObjectTests.cs
git commit -m "feat(engine): add StackObject and TargetInfo value objects"
```

---

### Task 2: TargetFilter and SpellEffect Abstractions

**Files:**
- Create: `src/MtgDecker.Engine/TargetFilter.cs`
- Create: `src/MtgDecker.Engine/SpellEffect.cs`
- Create: `tests/MtgDecker.Engine.Tests/TargetFilterTests.cs`

**Step 1: Write failing tests for TargetFilter**

```csharp
// tests/MtgDecker.Engine.Tests/TargetFilterTests.cs
using FluentAssertions;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class TargetFilterTests
{
    [Fact]
    public void CreatureFilter_MatchesCreatureOnBattlefield()
    {
        var filter = TargetFilter.Creature();
        var creature = GameCard.Create("Mogg Fanatic", "Creature — Goblin");

        filter.IsLegal(creature, ZoneType.Battlefield).Should().BeTrue();
    }

    [Fact]
    public void CreatureFilter_RejectsLand()
    {
        var filter = TargetFilter.Creature();
        var land = GameCard.Create("Forest", "Basic Land — Forest");

        filter.IsLegal(land, ZoneType.Battlefield).Should().BeFalse();
    }

    [Fact]
    public void CreatureFilter_RejectsCreatureNotOnBattlefield()
    {
        var filter = TargetFilter.Creature();
        var creature = GameCard.Create("Mogg Fanatic", "Creature — Goblin");

        filter.IsLegal(creature, ZoneType.Hand).Should().BeFalse();
    }

    [Fact]
    public void EnchantmentOrArtifactFilter_MatchesEnchantmentOnBattlefield()
    {
        var filter = TargetFilter.EnchantmentOrArtifact();
        var enchantment = GameCard.Create("Wild Growth", "Enchantment");

        filter.IsLegal(enchantment, ZoneType.Battlefield).Should().BeTrue();
    }

    [Fact]
    public void EnchantmentOrArtifactFilter_RejectsCreature()
    {
        var filter = TargetFilter.EnchantmentOrArtifact();
        var creature = GameCard.Create("Mogg Fanatic", "Creature — Goblin");

        filter.IsLegal(creature, ZoneType.Battlefield).Should().BeFalse();
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "TargetFilterTests" -v n`
Expected: FAIL — TargetFilter not defined

**Step 3: Implement TargetFilter and SpellEffect**

```csharp
// src/MtgDecker.Engine/TargetFilter.cs
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public class TargetFilter
{
    private readonly Func<GameCard, ZoneType, bool> _predicate;

    private TargetFilter(Func<GameCard, ZoneType, bool> predicate)
    {
        _predicate = predicate;
    }

    public bool IsLegal(GameCard card, ZoneType zone) => _predicate(card, zone);

    public static TargetFilter Creature() => new((card, zone) =>
        zone == ZoneType.Battlefield && card.IsCreature);

    public static TargetFilter EnchantmentOrArtifact() => new((card, zone) =>
        zone == ZoneType.Battlefield &&
        (card.CardTypes.HasFlag(CardType.Enchantment) || card.CardTypes.HasFlag(CardType.Artifact)));
}
```

```csharp
// src/MtgDecker.Engine/SpellEffect.cs
namespace MtgDecker.Engine;

public abstract class SpellEffect
{
    public abstract void Resolve(GameState state, StackObject spell);
}
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "TargetFilterTests" -v n`
Expected: PASS (5 tests)

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/TargetFilter.cs src/MtgDecker.Engine/SpellEffect.cs tests/MtgDecker.Engine.Tests/TargetFilterTests.cs
git commit -m "feat(engine): add TargetFilter and SpellEffect abstractions"
```

---

### Task 3: Engine Plumbing — Enums, GameAction, GameState, CardDefinition

**Files:**
- Modify: `src/MtgDecker.Engine/Enums/ActionType.cs`
- Modify: `src/MtgDecker.Engine/GameAction.cs`
- Modify: `src/MtgDecker.Engine/GameState.cs`
- Modify: `src/MtgDecker.Engine/CardDefinition.cs`
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Create: `tests/MtgDecker.Engine.Tests/StackPlumbingTests.cs`

**Step 1: Write failing tests for new plumbing**

```csharp
// tests/MtgDecker.Engine.Tests/StackPlumbingTests.cs
using FluentAssertions;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class StackPlumbingTests
{
    [Fact]
    public void ActionType_HasCastSpell()
    {
        Enum.IsDefined(typeof(ActionType), "CastSpell").Should().BeTrue();
    }

    [Fact]
    public void GameAction_CastSpell_Factory()
    {
        var playerId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var action = GameAction.CastSpell(playerId, cardId);

        action.Type.Should().Be(ActionType.CastSpell);
        action.PlayerId.Should().Be(playerId);
        action.CardId.Should().Be(cardId);
    }

    [Fact]
    public void GameState_HasEmptyStack()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new Tests.Helpers.TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new Tests.Helpers.TestDecisionHandler());
        var state = new GameState(p1, p2);

        state.Stack.Should().BeEmpty();
    }

    [Fact]
    public void CardDefinition_SwordsHasTargetFilter()
    {
        CardDefinitions.TryGet("Swords to Plowshares", out var def).Should().BeTrue();
        def!.TargetFilter.Should().NotBeNull();
    }

    [Fact]
    public void CardDefinition_SwordsHasSpellEffect()
    {
        CardDefinitions.TryGet("Swords to Plowshares", out var def).Should().BeTrue();
        def!.Effect.Should().NotBeNull();
    }

    [Fact]
    public void CardDefinition_NaturalizeHasTargetFilter()
    {
        CardDefinitions.TryGet("Naturalize", out var def).Should().BeTrue();
        def!.TargetFilter.Should().NotBeNull();
    }

    [Fact]
    public void CardDefinition_MoggFanatic_NoTargetFilter()
    {
        CardDefinitions.TryGet("Mogg Fanatic", out var def).Should().BeTrue();
        def!.TargetFilter.Should().BeNull();
        def.Effect.Should().BeNull();
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "StackPlumbingTests" -v n`
Expected: FAIL — CastSpell not in enum, Stack not on GameState, etc.

**Step 3: Add CastSpell to ActionType**

```csharp
// src/MtgDecker.Engine/Enums/ActionType.cs
namespace MtgDecker.Engine.Enums;

public enum ActionType
{
    PassPriority,
    PlayCard,
    TapCard,
    UntapCard,
    MoveCard,
    CastSpell
}
```

**Step 4: Add CastSpell factory to GameAction**

Add to `src/MtgDecker.Engine/GameAction.cs` after the MoveCard factory:

```csharp
public static GameAction CastSpell(Guid playerId, Guid cardId) => new()
{
    Type = ActionType.CastSpell,
    PlayerId = playerId,
    CardId = cardId,
    SourceZone = ZoneType.Hand
};
```

**Step 5: Add Stack to GameState**

Add to `src/MtgDecker.Engine/GameState.cs` after the `CombatState? Combat` property:

```csharp
public List<StackObject> Stack { get; } = new();
```

**Step 6: Extend CardDefinition with TargetFilter and SpellEffect**

Replace the record in `src/MtgDecker.Engine/CardDefinition.cs`:

```csharp
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine;

public record CardDefinition(
    ManaCost? ManaCost,
    ManaAbility? ManaAbility,
    int? Power,
    int? Toughness,
    CardType CardTypes,
    TargetFilter? TargetFilter = null,
    SpellEffect? Effect = null
);
```

**Step 7: Create SwordsToPlowsharesEffect and NaturalizeEffect**

Create `src/MtgDecker.Engine/Effects/SwordsToPlowsharesEffect.cs`:

```csharp
namespace MtgDecker.Engine.Effects;

public class SwordsToPlowsharesEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        if (spell.Targets.Count == 0) return;
        var target = spell.Targets[0];
        var owner = target.PlayerId == state.Player1.Id ? state.Player1 : state.Player2;
        var creature = owner.Battlefield.RemoveById(target.CardId);
        if (creature == null) return; // fizzle handled elsewhere
        var power = creature.Power ?? 0;
        owner.AdjustLife(power);
        owner.Exile.Add(creature);
        state.Log($"{creature.Name} is exiled. {owner.Name} gains {power} life ({owner.Life}).");
    }
}
```

Create `src/MtgDecker.Engine/Effects/NaturalizeEffect.cs`:

```csharp
namespace MtgDecker.Engine.Effects;

public class NaturalizeEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        if (spell.Targets.Count == 0) return;
        var target = spell.Targets[0];
        var owner = target.PlayerId == state.Player1.Id ? state.Player1 : state.Player2;
        var permanent = owner.Battlefield.RemoveById(target.CardId);
        if (permanent == null) return; // fizzle handled elsewhere
        owner.Graveyard.Add(permanent);
        state.Log($"{permanent.Name} is destroyed.");
    }
}
```

**Step 8: Update CardDefinitions registry for Swords and Naturalize**

In `src/MtgDecker.Engine/CardDefinitions.cs`, update the two entries:

```csharp
["Naturalize"] = new(ManaCost.Parse("{1}{G}"), null, null, null, CardType.Instant,
    TargetFilter.EnchantmentOrArtifact(), new Effects.NaturalizeEffect()),

["Swords to Plowshares"] = new(ManaCost.Parse("{W}"), null, null, null, CardType.Instant,
    TargetFilter.Creature(), new Effects.SwordsToPlowsharesEffect()),
```

**Step 9: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "StackPlumbingTests" -v n`
Expected: PASS (7 tests)

**Step 10: Run all existing tests to verify no regressions**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v n`
Expected: All 261 existing tests still pass (CardDefinition is backward-compatible with optional params)

**Step 11: Commit**

```bash
git add -A
git commit -m "feat(engine): add stack plumbing — CastSpell action, GameState.Stack, spell effects"
```

---

### Task 4: Decision Handler — ChooseTarget

**Files:**
- Modify: `src/MtgDecker.Engine/IPlayerDecisionHandler.cs`
- Modify: `tests/MtgDecker.Engine.Tests/Helpers/TestDecisionHandler.cs`
- Create: `tests/MtgDecker.Engine.Tests/TestDecisionHandlerTargetTests.cs`

**Step 1: Write failing tests for target decision**

```csharp
// tests/MtgDecker.Engine.Tests/TestDecisionHandlerTargetTests.cs
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class TestDecisionHandlerTargetTests
{
    [Fact]
    public async Task ChooseTarget_DequeuesEnqueued()
    {
        var handler = new TestDecisionHandler();
        var target = new TargetInfo(Guid.NewGuid(), Guid.NewGuid(), ZoneType.Battlefield);
        handler.EnqueueTarget(target);

        var eligible = new List<GameCard> { GameCard.Create("Mogg Fanatic", "Creature — Goblin") };
        var result = await handler.ChooseTarget("Swords to Plowshares", eligible);

        result.Should().Be(target);
    }

    [Fact]
    public async Task ChooseTarget_DefaultsToFirstEligible()
    {
        var handler = new TestDecisionHandler();
        var card = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        var playerId = Guid.NewGuid();

        var eligible = new List<GameCard> { card };
        var result = await handler.ChooseTarget("Swords to Plowshares", eligible, playerId);

        result.CardId.Should().Be(card.Id);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "TestDecisionHandlerTargetTests" -v n`
Expected: FAIL — ChooseTarget/EnqueueTarget not defined

**Step 3: Add ChooseTarget to IPlayerDecisionHandler**

Add to `src/MtgDecker.Engine/IPlayerDecisionHandler.cs`:

```csharp
Task<TargetInfo> ChooseTarget(string spellName, IReadOnlyList<GameCard> eligibleTargets, Guid defaultOwnerId = default, CancellationToken ct = default);
```

**Step 4: Implement in TestDecisionHandler**

Add to `tests/MtgDecker.Engine.Tests/Helpers/TestDecisionHandler.cs`:

Queue field:
```csharp
private readonly Queue<TargetInfo> _targetQueue = new();
```

Enqueue method:
```csharp
public void EnqueueTarget(TargetInfo target) => _targetQueue.Enqueue(target);
```

ChooseTarget implementation:
```csharp
public Task<TargetInfo> ChooseTarget(string spellName, IReadOnlyList<GameCard> eligibleTargets, Guid defaultOwnerId = default, CancellationToken ct = default)
{
    if (_targetQueue.Count > 0)
        return Task.FromResult(_targetQueue.Dequeue());
    // Default: target the first eligible card
    var card = eligibleTargets[0];
    return Task.FromResult(new TargetInfo(card.Id, defaultOwnerId, Enums.ZoneType.Battlefield));
}
```

**Step 5: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "TestDecisionHandlerTargetTests" -v n`
Expected: PASS (2 tests)

**Step 6: Run all tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v n`
Expected: All pass (interface change requires InteractiveDecisionHandler update — add throw NotImplementedException stub for now if needed)

**Step 7: Commit**

```bash
git add -A
git commit -m "feat(engine): add ChooseTarget to decision handler interface"
```

---

### Task 5: Timing Validation

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs`
- Create: `tests/MtgDecker.Engine.Tests/TimingValidationTests.cs`

**Step 1: Write failing tests for timing**

```csharp
// tests/MtgDecker.Engine.Tests/TimingValidationTests.cs
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class TimingValidationTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler h1, TestDecisionHandler h2) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        for (int i = 0; i < 40; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}" });
            p2.Library.Add(new GameCard { Name = $"Card{i}" });
        }
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, h1, h2);
    }

    [Fact]
    public void CanCastSorcery_InMainPhaseActivePlayerEmptyStack_True()
    {
        var (engine, state, _, _) = CreateSetup();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        engine.CanCastSorcery(state.Player1.Id).Should().BeTrue();
    }

    [Fact]
    public void CanCastSorcery_InCombatPhase_False()
    {
        var (engine, state, _, _) = CreateSetup();
        state.CurrentPhase = Phase.Combat;
        state.ActivePlayer = state.Player1;

        engine.CanCastSorcery(state.Player1.Id).Should().BeFalse();
    }

    [Fact]
    public void CanCastSorcery_NonActivePlayer_False()
    {
        var (engine, state, _, _) = CreateSetup();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        engine.CanCastSorcery(state.Player2.Id).Should().BeFalse();
    }

    [Fact]
    public void CanCastSorcery_StackNotEmpty_False()
    {
        var (engine, state, _, _) = CreateSetup();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;
        state.Stack.Add(new StackObject(
            new GameCard { Name = "Dummy" }, Guid.NewGuid(),
            new Dictionary<Mana.ManaColor, int>(), new List<TargetInfo>(), 0));

        engine.CanCastSorcery(state.Player1.Id).Should().BeFalse();
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "TimingValidationTests" -v n`
Expected: FAIL — CanCastSorcery not defined

**Step 3: Implement timing validation**

Add to `src/MtgDecker.Engine/GameEngine.cs`:

```csharp
public bool CanCastSorcery(Guid playerId)
{
    return _state.ActivePlayer.Id == playerId
        && (_state.CurrentPhase == Phase.MainPhase1 || _state.CurrentPhase == Phase.MainPhase2)
        && _state.Stack.Count == 0;
}
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "TimingValidationTests" -v n`
Expected: PASS (4 tests)

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/TimingValidationTests.cs
git commit -m "feat(engine): add sorcery-speed timing validation"
```

---

### Task 6: CastSpell Engine Flow (Place on Stack)

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (ExecuteAction, lines 89-274)
- Create: `tests/MtgDecker.Engine.Tests/CastSpellStackTests.cs`

**Step 1: Write failing test — instant goes on stack**

```csharp
// tests/MtgDecker.Engine.Tests/CastSpellStackTests.cs
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class CastSpellStackTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler h1, TestDecisionHandler h2) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        for (int i = 0; i < 40; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}" });
            p2.Library.Add(new GameCard { Name = $"Card{i}" });
        }
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, h1, h2);
    }

    [Fact]
    public async Task CastInstant_GoesOnStack()
    {
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var swords = GameCard.Create("Swords to Plowshares");
        state.Player1.Hand.Add(swords);
        state.Player1.ManaPool.Add(ManaColor.White, 1);

        // Create a target creature on opponent's battlefield
        var creature = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        state.Player2.Battlefield.Add(creature);
        h1.EnqueueTarget(new TargetInfo(creature.Id, state.Player2.Id, ZoneType.Battlefield));

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, swords.Id));

        state.Stack.Should().HaveCount(1);
        state.Stack[0].Card.Should().Be(swords);
        state.Stack[0].ControllerId.Should().Be(state.Player1.Id);
        state.Player1.Hand.Cards.Should().NotContain(c => c.Id == swords.Id);
        state.Player1.ManaPool.GetTotal().Should().Be(0);
    }

    [Fact]
    public async Task CastInstant_TargetRecorded()
    {
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var swords = GameCard.Create("Swords to Plowshares");
        state.Player1.Hand.Add(swords);
        state.Player1.ManaPool.Add(ManaColor.White, 1);

        var creature = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        state.Player2.Battlefield.Add(creature);
        h1.EnqueueTarget(new TargetInfo(creature.Id, state.Player2.Id, ZoneType.Battlefield));

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, swords.Id));

        state.Stack[0].Targets.Should().ContainSingle()
            .Which.CardId.Should().Be(creature.Id);
    }

    [Fact]
    public async Task CastSorcerySpeed_InCombat_Rejected()
    {
        var (engine, state, _, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.Combat;

        var replenish = GameCard.Create("Replenish");
        state.Player1.Hand.Add(replenish);
        state.Player1.ManaPool.Add(ManaColor.White, 4);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, replenish.Id));

        state.Stack.Should().BeEmpty();
        state.Player1.Hand.Cards.Should().Contain(c => c.Id == replenish.Id);
    }

    [Fact]
    public async Task CastInstant_InCombat_Allowed()
    {
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.Combat;

        var swords = GameCard.Create("Swords to Plowshares");
        state.Player1.Hand.Add(swords);
        state.Player1.ManaPool.Add(ManaColor.White, 1);

        var creature = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        state.Player2.Battlefield.Add(creature);
        h1.EnqueueTarget(new TargetInfo(creature.Id, state.Player2.Id, ZoneType.Battlefield));

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, swords.Id));

        state.Stack.Should().HaveCount(1);
    }

    [Fact]
    public async Task CastSpell_InsufficientMana_Rejected()
    {
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var swords = GameCard.Create("Swords to Plowshares");
        state.Player1.Hand.Add(swords);
        // No mana added

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, swords.Id));

        state.Stack.Should().BeEmpty();
        state.Player1.Hand.Cards.Should().Contain(c => c.Id == swords.Id);
    }

    [Fact]
    public async Task CastCreatureSpell_GoesOnStack()
    {
        var (engine, state, _, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var goblin = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        state.Player1.Hand.Add(goblin);
        state.Player1.ManaPool.Add(ManaColor.Red, 1);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, goblin.Id));

        state.Stack.Should().HaveCount(1);
        state.Stack[0].Card.Name.Should().Be("Mogg Fanatic");
        state.Player1.Hand.Cards.Should().NotContain(c => c.Id == goblin.Id);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "CastSpellStackTests" -v n`
Expected: FAIL — CastSpell case not handled in ExecuteAction

**Step 3: Implement CastSpell in ExecuteAction**

Add new case to `ExecuteAction` in `src/MtgDecker.Engine/GameEngine.cs` (inside the switch, after the MoveCard case):

```csharp
case ActionType.CastSpell:
{
    var castPlayer = action.PlayerId == _state.Player1.Id ? _state.Player1 : _state.Player2;
    var castCard = castPlayer.Hand.Cards.FirstOrDefault(c => c.Id == action.CardId);
    if (castCard == null)
    {
        _state.Log("Card not found in hand.");
        return;
    }

    // Look up definition
    if (!CardDefinitions.TryGet(castCard.Name, out var def) || def.ManaCost == null)
    {
        _state.Log($"Cannot cast {castCard.Name} — no registered mana cost.");
        return;
    }

    // Timing check: instants can be cast any time, everything else is sorcery speed
    bool isInstant = def.CardTypes.HasFlag(CardType.Instant);
    if (!isInstant && !CanCastSorcery(castPlayer.Id))
    {
        _state.Log($"Cannot cast {castCard.Name} at this time (sorcery-speed only).");
        return;
    }

    // Mana validation
    var pool = castPlayer.ManaPool;
    foreach (var (color, amount) in def.ManaCost.ColorRequirements)
    {
        if (pool.GetAmount(color) < amount)
        {
            _state.Log($"Not enough mana to cast {castCard.Name}.");
            return;
        }
    }
    var totalAvailable = pool.GetTotal();
    var totalCost = def.ManaCost.ColorRequirements.Values.Sum() + def.ManaCost.GenericCost;
    if (totalAvailable < totalCost)
    {
        _state.Log($"Not enough mana to cast {castCard.Name}.");
        return;
    }

    // Target prompt (if spell requires targeting)
    var targets = new List<TargetInfo>();
    if (def.TargetFilter != null)
    {
        var eligible = new List<GameCard>();
        var opponent = _state.GetOpponent(castPlayer);
        foreach (var c in castPlayer.Battlefield.Cards)
            if (def.TargetFilter.IsLegal(c, ZoneType.Battlefield))
                eligible.Add(c);
        foreach (var c in opponent.Battlefield.Cards)
            if (def.TargetFilter.IsLegal(c, ZoneType.Battlefield))
                eligible.Add(c);

        if (eligible.Count == 0)
        {
            _state.Log($"No legal targets for {castCard.Name}.");
            return;
        }

        var target = await castPlayer.DecisionHandler.ChooseTarget(
            castCard.Name, eligible, opponent.Id, ct);
        targets.Add(target);
    }

    // Pay mana — colored first
    var manaPaid = new Dictionary<ManaColor, int>();
    foreach (var (color, amount) in def.ManaCost.ColorRequirements)
    {
        pool.Deduct(color, amount);
        manaPaid[color] = amount;
    }

    // Pay generic
    if (def.ManaCost.GenericCost > 0)
    {
        var available = pool.GetAvailable();
        var genericPayment = await castPlayer.DecisionHandler
            .ChooseGenericPayment(def.ManaCost.GenericCost, available, ct);
        foreach (var (color, amount) in genericPayment)
        {
            pool.Deduct(color, amount);
            manaPaid[color] = manaPaid.GetValueOrDefault(color) + amount;
        }
    }

    // Move card from hand, create StackObject
    castPlayer.Hand.RemoveById(castCard.Id);
    var stackObj = new StackObject(castCard, castPlayer.Id, manaPaid, targets, _state.Stack.Count);
    _state.Stack.Add(stackObj);

    // Record for undo
    action.ManaCostPaid = def.ManaCost;
    castPlayer.ActionHistory.Push(action);

    _state.Log($"{castPlayer.Name} casts {castCard.Name}.");
    break;
}
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "CastSpellStackTests" -v n`
Expected: PASS (6 tests)

**Step 5: Run all tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v n`
Expected: All pass (existing PlayCard tests unaffected — they use PlayCard action, not CastSpell)

**Step 6: Commit**

```bash
git add -A
git commit -m "feat(engine): CastSpell action places spells on stack with timing/mana/target validation"
```

---

### Task 7: Stack Resolution in RunPriorityAsync

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (RunPriorityAsync, lines 523-556)
- Create: `tests/MtgDecker.Engine.Tests/StackResolutionTests.cs`

**Step 1: Write failing tests for stack resolution**

```csharp
// tests/MtgDecker.Engine.Tests/StackResolutionTests.cs
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class StackResolutionTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler h1, TestDecisionHandler h2) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        for (int i = 0; i < 40; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}" });
            p2.Library.Add(new GameCard { Name = $"Card{i}" });
        }
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, h1, h2);
    }

    [Fact]
    public async Task BothPass_StackNonEmpty_ResolvesTop()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        // Put a creature spell on the stack manually
        var goblin = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        var stackObj = new StackObject(goblin, state.Player1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);
        state.Stack.Add(stackObj);

        // Both players pass → resolve → creature enters battlefield
        // Then both pass again → stack empty → advance
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        // After resolve, priority again
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));

        await engine.RunPriorityAsync();

        state.Stack.Should().BeEmpty();
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == goblin.Id);
    }

    [Fact]
    public async Task BothPass_StackEmpty_Advances()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();

        // Both pass with empty stack → just returns (advances phase)
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));

        await engine.RunPriorityAsync();

        state.Stack.Should().BeEmpty();
    }

    [Fact]
    public async Task InstantSorcery_ResolvesToGraveyard()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var creature = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        creature.Power = 1;
        creature.Toughness = 1;
        state.Player2.Battlefield.Add(creature);

        var swords = GameCard.Create("Swords to Plowshares");
        var stackObj = new StackObject(swords, state.Player1.Id,
            new Dictionary<ManaColor, int> { [ManaColor.White] = 1 },
            new List<TargetInfo> { new(creature.Id, state.Player2.Id, ZoneType.Battlefield) }, 0);
        state.Stack.Add(stackObj);

        // Both pass → resolve Swords
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));

        await engine.RunPriorityAsync();

        // Swords should be in caster's graveyard
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Id == swords.Id);
        // Creature should be exiled
        state.Player2.Exile.Cards.Should().Contain(c => c.Id == creature.Id);
        // Opponent gains life equal to power
        state.Player2.Life.Should().Be(21);
    }

    [Fact]
    public async Task LIFO_ResolvesTopFirst()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var card1 = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        var card2 = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        state.Stack.Add(new StackObject(card1, state.Player1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0));
        state.Stack.Add(new StackObject(card2, state.Player1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 1));

        // card2 is on top (added last), resolves first
        // Both pass → resolve card2 → priority → both pass → resolve card1 → priority → both pass → return
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));

        await engine.RunPriorityAsync();

        state.Stack.Should().BeEmpty();
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == card2.Id);
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == card1.Id);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "StackResolutionTests" -v n`
Expected: FAIL — RunPriorityAsync doesn't resolve stack yet

**Step 3: Implement stack resolution**

Replace `RunPriorityAsync` in `src/MtgDecker.Engine/GameEngine.cs`:

```csharp
internal async Task RunPriorityAsync(CancellationToken ct = default)
{
    _state.PriorityPlayer = _state.ActivePlayer;
    bool activePlayerPassed = false;
    bool nonActivePlayerPassed = false;

    while (true)
    {
        ct.ThrowIfCancellationRequested();

        var action = await _state.PriorityPlayer.DecisionHandler
            .GetAction(_state, _state.PriorityPlayer.Id, ct);

        if (action.Type == ActionType.PassPriority)
        {
            if (_state.PriorityPlayer == _state.ActivePlayer)
                activePlayerPassed = true;
            else
                nonActivePlayerPassed = true;

            if (activePlayerPassed && nonActivePlayerPassed)
            {
                if (_state.Stack.Count > 0)
                {
                    ResolveTopOfStack();
                    // After resolution, active player gets priority again
                    _state.PriorityPlayer = _state.ActivePlayer;
                    activePlayerPassed = false;
                    nonActivePlayerPassed = false;
                    continue;
                }
                // Stack empty — advance phase
                return;
            }

            _state.PriorityPlayer = _state.GetOpponent(_state.PriorityPlayer);
        }
        else
        {
            await ExecuteAction(action, ct);
            activePlayerPassed = false;
            nonActivePlayerPassed = false;
            _state.PriorityPlayer = _state.ActivePlayer;
        }
    }
}
```

**Step 4: Implement ResolveTopOfStack**

Add to `src/MtgDecker.Engine/GameEngine.cs`:

```csharp
private void ResolveTopOfStack()
{
    if (_state.Stack.Count == 0) return;

    var top = _state.Stack[^1];
    _state.Stack.RemoveAt(_state.Stack.Count - 1);
    var controller = top.ControllerId == _state.Player1.Id ? _state.Player1 : _state.Player2;

    _state.Log($"Resolving {top.Card.Name}.");

    // Check if spell has a registered effect
    if (CardDefinitions.TryGet(top.Card.Name, out var def) && def.Effect != null)
    {
        // Check target legality (fizzle check)
        if (top.Targets.Count > 0)
        {
            var allTargetsLegal = true;
            foreach (var target in top.Targets)
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
                _state.Log($"{top.Card.Name} fizzles (illegal target).");
                controller.Graveyard.Add(top.Card);
                return;
            }
        }

        def.Effect.Resolve(_state, top);
        controller.Graveyard.Add(top.Card);
    }
    else
    {
        // Default: permanent enters battlefield
        if (top.Card.IsCreature || top.Card.CardTypes.HasFlag(CardType.Enchantment)
            || top.Card.CardTypes.HasFlag(CardType.Artifact))
        {
            top.Card.TurnEnteredBattlefield = _state.TurnNumber;
            controller.Battlefield.Add(top.Card);
        }
        else
        {
            // Instant/sorcery without effect → graveyard
            controller.Graveyard.Add(top.Card);
        }
    }
}
```

**Step 5: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "StackResolutionTests" -v n`
Expected: PASS (4 tests)

**Step 6: Run all tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v n`
Expected: All pass

**Step 7: Commit**

```bash
git add -A
git commit -m "feat(engine): stack resolution — LIFO resolve on double-pass, spell effects, fizzle"
```

---

### Task 8: Spell Effect Tests

**Files:**
- Create: `tests/MtgDecker.Engine.Tests/SpellEffectTests.cs`

**Step 1: Write tests for Swords to Plowshares effect**

```csharp
// tests/MtgDecker.Engine.Tests/SpellEffectTests.cs
using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class SpellEffectTests
{
    private GameState CreateState()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        return new GameState(p1, p2);
    }

    [Fact]
    public void SwordsToPlowshares_ExilesCreature_GainsLife()
    {
        var state = CreateState();
        var creature = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        creature.Power = 1;
        state.Player2.Battlefield.Add(creature);

        var swords = GameCard.Create("Swords to Plowshares");
        var spell = new StackObject(swords, state.Player1.Id,
            new Dictionary<ManaColor, int> { [ManaColor.White] = 1 },
            new List<TargetInfo> { new(creature.Id, state.Player2.Id, ZoneType.Battlefield) }, 0);

        new SwordsToPlowsharesEffect().Resolve(state, spell);

        state.Player2.Battlefield.Cards.Should().NotContain(c => c.Id == creature.Id);
        state.Player2.Exile.Cards.Should().Contain(c => c.Id == creature.Id);
        state.Player2.Life.Should().Be(21); // 20 + 1 power
    }

    [Fact]
    public void SwordsToPlowshares_HighPowerCreature_GainsMoreLife()
    {
        var state = CreateState();
        var creature = new GameCard { Name = "Big Creature", Power = 5, Toughness = 5, CardTypes = CardType.Creature };
        state.Player2.Battlefield.Add(creature);

        var swords = GameCard.Create("Swords to Plowshares");
        var spell = new StackObject(swords, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(creature.Id, state.Player2.Id, ZoneType.Battlefield) }, 0);

        new SwordsToPlowsharesEffect().Resolve(state, spell);

        state.Player2.Life.Should().Be(25); // 20 + 5 power
    }

    [Fact]
    public void Naturalize_DestroysEnchantment()
    {
        var state = CreateState();
        var enchantment = GameCard.Create("Wild Growth", "Enchantment");
        state.Player2.Battlefield.Add(enchantment);

        var naturalize = GameCard.Create("Naturalize");
        var spell = new StackObject(naturalize, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(enchantment.Id, state.Player2.Id, ZoneType.Battlefield) }, 0);

        new NaturalizeEffect().Resolve(state, spell);

        state.Player2.Battlefield.Cards.Should().NotContain(c => c.Id == enchantment.Id);
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Id == enchantment.Id);
    }

    [Fact]
    public void SwordsToPlowshares_TargetGone_NoEffect()
    {
        var state = CreateState();
        var creature = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        // creature NOT on battlefield — simulate it was removed

        var swords = GameCard.Create("Swords to Plowshares");
        var spell = new StackObject(swords, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(creature.Id, state.Player2.Id, ZoneType.Battlefield) }, 0);

        new SwordsToPlowsharesEffect().Resolve(state, spell);

        state.Player2.Life.Should().Be(20); // unchanged
        state.Player2.Exile.Cards.Should().BeEmpty();
    }
}
```

**Step 2: Run tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "SpellEffectTests" -v n`
Expected: PASS (4 tests — effects were implemented in Task 3)

**Step 3: Commit**

```bash
git add tests/MtgDecker.Engine.Tests/SpellEffectTests.cs
git commit -m "test(engine): add spell effect tests for Swords to Plowshares and Naturalize"
```

---

### Task 9: Fizzle and Undo for Stack Spells

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (UndoLastAction)
- Create: `tests/MtgDecker.Engine.Tests/StackFizzleTests.cs`
- Create: `tests/MtgDecker.Engine.Tests/StackUndoTests.cs`

**Step 1: Write failing fizzle test (integration)**

```csharp
// tests/MtgDecker.Engine.Tests/StackFizzleTests.cs
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class StackFizzleTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler h1, TestDecisionHandler h2) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        for (int i = 0; i < 40; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}" });
            p2.Library.Add(new GameCard { Name = $"Card{i}" });
        }
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, h1, h2);
    }

    [Fact]
    public async Task Fizzle_TargetRemoved_SpellGoesToGraveyard()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var creature = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        state.Player2.Battlefield.Add(creature);

        var swords = GameCard.Create("Swords to Plowshares");
        state.Stack.Add(new StackObject(swords, state.Player1.Id,
            new Dictionary<ManaColor, int> { [ManaColor.White] = 1 },
            new List<TargetInfo> { new(creature.Id, state.Player2.Id, ZoneType.Battlefield) }, 0));

        // Remove target before resolution
        state.Player2.Battlefield.RemoveById(creature.Id);

        // Both pass → resolve → fizzle
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));

        await engine.RunPriorityAsync();

        // Spell goes to graveyard (fizzled)
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Id == swords.Id);
        // Creature not exiled (it was already removed)
        state.Player2.Life.Should().Be(20); // no life gain
    }
}
```

**Step 2: Run fizzle test**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "StackFizzleTests" -v n`
Expected: PASS (fizzle logic was implemented in ResolveTopOfStack in Task 7)

**Step 3: Write failing undo test**

```csharp
// tests/MtgDecker.Engine.Tests/StackUndoTests.cs
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class StackUndoTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler h1) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        for (int i = 0; i < 40; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}" });
            p2.Library.Add(new GameCard { Name = $"Card{i}" });
        }
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, h1);
    }

    [Fact]
    public async Task UndoCastSpell_RemovesFromStack_RefundsMana_ReturnsToHand()
    {
        var (engine, state, h1) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var swords = GameCard.Create("Swords to Plowshares");
        state.Player1.Hand.Add(swords);
        state.Player1.ManaPool.Add(ManaColor.White, 1);

        var creature = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        state.Player2.Battlefield.Add(creature);
        h1.EnqueueTarget(new TargetInfo(creature.Id, state.Player2.Id, ZoneType.Battlefield));

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, swords.Id));
        state.Stack.Should().HaveCount(1);

        var result = engine.UndoLastAction(state.Player1.Id);

        result.Should().BeTrue();
        state.Stack.Should().BeEmpty();
        state.Player1.Hand.Cards.Should().Contain(c => c.Id == swords.Id);
        state.Player1.ManaPool.GetAmount(ManaColor.White).Should().Be(1);
    }
}
```

**Step 4: Run undo test to verify it fails**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "StackUndoTests" -v n`
Expected: FAIL — UndoLastAction doesn't handle CastSpell yet

**Step 5: Implement CastSpell undo**

Add new case to `UndoLastAction` in `src/MtgDecker.Engine/GameEngine.cs` (inside the switch, after MoveCard case):

```csharp
case ActionType.CastSpell:
    // Find the StackObject for this card and remove it
    var stackIdx = _state.Stack.FindLastIndex(s => s.Card.Id == action.CardId);
    if (stackIdx < 0) return false;
    var removedStack = _state.Stack[stackIdx];
    _state.Stack.RemoveAt(stackIdx);
    player.ActionHistory.Pop();
    player.Hand.Add(removedStack.Card);
    // Refund mana
    foreach (var (color, amount) in removedStack.ManaPaid)
        player.ManaPool.Add(color, amount);
    _state.Log($"{player.Name} undoes casting {removedStack.Card.Name}.");
    break;
```

**Step 6: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "StackUndoTests|StackFizzleTests" -v n`
Expected: PASS (2 tests)

**Step 7: Run all tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v n`
Expected: All pass

**Step 8: Commit**

```bash
git add -A
git commit -m "feat(engine): add fizzle logic and CastSpell undo for stack spells"
```

---

### Task 10: Update Existing CastSpell Tests

Existing `CastSpellTests` use `PlayCard` action for registered spells. These still work (PlayCard bypasses stack for backward compatibility). But we should also verify that CastSpell + resolution produces the same end results.

**Files:**
- Create: `tests/MtgDecker.Engine.Tests/CastSpellStackIntegrationTests.cs`

**Step 1: Write integration tests — full cast-resolve cycle**

```csharp
// tests/MtgDecker.Engine.Tests/CastSpellStackIntegrationTests.cs
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class CastSpellStackIntegrationTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler h1, TestDecisionHandler h2) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        for (int i = 0; i < 40; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}" });
            p2.Library.Add(new GameCard { Name = $"Card{i}" });
        }
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, h1, h2);
    }

    [Fact]
    public async Task CastCreature_Resolve_EntersBattlefield()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var goblin = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        state.Player1.Hand.Add(goblin);
        state.Player1.ManaPool.Add(ManaColor.Red, 1);

        // Cast → goes on stack
        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, goblin.Id));
        state.Stack.Should().HaveCount(1);

        // Manually simulate resolution (both pass twice)
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        await engine.RunPriorityAsync();

        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == goblin.Id);
        state.Stack.Should().BeEmpty();
    }

    [Fact]
    public async Task CastSwords_Resolve_ExilesAndGainsLife()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var creature = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        creature.Power = 1;
        state.Player2.Battlefield.Add(creature);

        var swords = GameCard.Create("Swords to Plowshares");
        state.Player1.Hand.Add(swords);
        state.Player1.ManaPool.Add(ManaColor.White, 1);
        h1.EnqueueTarget(new TargetInfo(creature.Id, state.Player2.Id, ZoneType.Battlefield));

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, swords.Id));

        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        await engine.RunPriorityAsync();

        state.Player2.Exile.Cards.Should().Contain(c => c.Id == creature.Id);
        state.Player2.Life.Should().Be(21);
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Id == swords.Id);
    }

    [Fact]
    public async Task CastNaturalize_Resolve_DestroysEnchantment()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var enchantment = GameCard.Create("Wild Growth", "Enchantment");
        state.Player2.Battlefield.Add(enchantment);

        var naturalize = GameCard.Create("Naturalize");
        state.Player1.Hand.Add(naturalize);
        state.Player1.ManaPool.Add(ManaColor.Green, 2);
        h1.EnqueueTarget(new TargetInfo(enchantment.Id, state.Player2.Id, ZoneType.Battlefield));

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, naturalize.Id));

        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        await engine.RunPriorityAsync();

        state.Player2.Graveyard.Cards.Should().Contain(c => c.Id == enchantment.Id);
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Id == naturalize.Id);
    }

    [Fact]
    public async Task RespondToSpell_LIFO()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        // P1 casts creature, P2 responds with Swords
        var goblin = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        state.Player1.Hand.Add(goblin);
        state.Player1.ManaPool.Add(ManaColor.Red, 1);

        var swords = GameCard.Create("Swords to Plowshares");
        state.Player2.Hand.Add(swords);
        state.Player2.ManaPool.Add(ManaColor.White, 1);

        // P1 casts Mogg Fanatic → stack
        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, goblin.Id));

        // During priority, P1 passes, P2 casts Swords targeting Goblin on stack...
        // Wait — in MTG you can't target a creature spell on the stack with Swords (it targets creatures on battlefield).
        // Swords targets creatures on the battlefield. So P2 can't respond to the creature spell with Swords.
        // Instead let's test: P1 has creature on battlefield, P2 casts Swords, P1 responds by casting another spell.

        // Better test: P1 has Mogg Fanatic on battlefield. P2 casts Swords targeting it.
        // P1 responds by casting... hmm, we only have Swords and Naturalize as instants.
        // Let's just test LIFO with two creatures.

        var goblin2 = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        state.Player1.Hand.Add(goblin2);
        state.Player1.ManaPool.Add(ManaColor.Red, 1);

        // Cast second creature
        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, goblin2.Id));

        // Stack: [Mogg Fanatic (bottom), Goblin Lackey (top)]
        state.Stack.Should().HaveCount(2);
        state.Stack[^1].Card.Name.Should().Be("Goblin Lackey"); // top

        // Both pass → resolve Goblin Lackey first
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        // After Goblin Lackey resolves, both pass again → resolve Mogg Fanatic
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        // After Mogg Fanatic resolves, both pass → stack empty → return
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));

        await engine.RunPriorityAsync();

        state.Player1.Battlefield.Cards.Should().Contain(c => c.Name == "Goblin Lackey");
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Name == "Mogg Fanatic");
    }

    [Fact]
    public async Task LandDrop_StillImmediate_NoStack()
    {
        var (engine, state, _, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var forest = GameCard.Create("Forest", "Basic Land — Forest");
        state.Player1.Hand.Add(forest);

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, forest.Id));

        state.Stack.Should().BeEmpty();
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == forest.Id);
    }

    [Fact]
    public async Task SandboxMode_StillImmediate_NoStack()
    {
        var (engine, state, _, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var unknownCard = new GameCard { Name = "Unknown Spell", TypeLine = "Creature" };
        state.Player1.Hand.Add(unknownCard);

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, unknownCard.Id));

        state.Stack.Should().BeEmpty();
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == unknownCard.Id);
    }
}
```

**Step 2: Run tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "CastSpellStackIntegrationTests" -v n`
Expected: PASS (7 tests — all should work with existing implementation)

**Step 3: Run full test suite**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v n`
Expected: All pass

**Step 4: Commit**

```bash
git add tests/MtgDecker.Engine.Tests/CastSpellStackIntegrationTests.cs
git commit -m "test(engine): add stack integration tests for full cast-resolve lifecycle"
```

---

### Task 11: InteractiveDecisionHandler — Target Support

**Files:**
- Modify: `src/MtgDecker.Engine/InteractiveDecisionHandler.cs`

**Step 1: Write failing test**

No separate test file needed — we verify via the UI integration. Just implement the TCS pattern.

**Step 2: Add target TCS to InteractiveDecisionHandler**

Add these fields and properties to `src/MtgDecker.Engine/InteractiveDecisionHandler.cs`:

```csharp
private TaskCompletionSource<TargetInfo>? _targetTcs;

public bool IsWaitingForTarget => _targetTcs is { Task.IsCompleted: false };
public string? TargetingSpellName { get; private set; }
public IReadOnlyList<GameCard>? EligibleTargets { get; private set; }
```

Add the `ChooseTarget` implementation:

```csharp
public Task<TargetInfo> ChooseTarget(string spellName, IReadOnlyList<GameCard> eligibleTargets, Guid defaultOwnerId = default, CancellationToken ct = default)
{
    TargetingSpellName = spellName;
    EligibleTargets = eligibleTargets;
    _targetTcs = new TaskCompletionSource<TargetInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
    var registration = ct.Register(() => { TargetingSpellName = null; EligibleTargets = null; _targetTcs.TrySetCanceled(); });
    _targetTcs.Task.ContinueWith(_ => registration.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
    OnWaitingForInput?.Invoke();
    return _targetTcs.Task;
}
```

Add the `SubmitTarget` method:

```csharp
public void SubmitTarget(TargetInfo target)
{
    TargetingSpellName = null;
    EligibleTargets = null;
    _targetTcs?.TrySetResult(target);
}
```

**Step 3: Build to verify compilation**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet build src/MtgDecker.Engine/ -v q`
Expected: 0 errors

**Step 4: Run all tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v n`
Expected: All pass

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/InteractiveDecisionHandler.cs
git commit -m "feat(engine): add target selection to InteractiveDecisionHandler"
```

---

### Task 12: UI — Stack Display Component

**Files:**
- Create: `src/MtgDecker.Web/Components/Pages/Game/StackDisplay.razor`
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GamePlay.razor` (add StackDisplay between player zones)

**Step 1: Read the existing GamePlay.razor layout**

Understand where to place the stack display. Read the existing page structure.

**Step 2: Create StackDisplay component**

```razor
@* src/MtgDecker.Web/Components/Pages/Game/StackDisplay.razor *@
@using MtgDecker.Engine

@if (Stack.Count > 0)
{
    <MudPaper Class="pa-2 my-1" Outlined="true" Style="background-color: rgba(128,0,128,0.1);">
        <MudText Typo="Typo.caption" Class="mb-1">
            <MudIcon Icon="@Icons.Material.Filled.Layers" Size="Size.Small" /> Stack (@Stack.Count)
        </MudText>
        @for (int i = Stack.Count - 1; i >= 0; i--)
        {
            var item = Stack[i];
            var isTop = i == Stack.Count - 1;
            <MudChip T="string" Color="@(isTop ? Color.Secondary : Color.Default)" Size="Size.Small" Class="ma-1">
                @item.Card.Name
                @if (item.Targets.Count > 0)
                {
                    <MudText Typo="Typo.caption"> → target</MudText>
                }
            </MudChip>
        }
    </MudPaper>
}

@code {
    [Parameter] public List<StackObject> Stack { get; set; } = new();
}
```

**Step 3: Add StackDisplay to GamePlay.razor**

Find the section between the two PlayerZone components. Add:

```razor
<StackDisplay Stack="@_gameState.Stack" />
```

**Step 4: Build to verify**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet build src/MtgDecker.Web/ -v q`
Expected: 0 errors

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(web): add stack display component between player zones"
```

---

### Task 13: UI — Target Picker and CastSpell Integration

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor` (add target picker, update Play button for CastSpell)

**Step 1: Read existing PlayerZone.razor**

Understand the current card action handling, especially the Play button logic.

**Step 2: Add target picker UI**

When `InteractiveDecisionHandler.IsWaitingForTarget` is true, show a prompt with eligible targets. The user clicks an eligible card to select it.

Add to the PlayerZone component (similar pattern to mana color picker):

```razor
@if (_handler?.IsWaitingForTarget == true && _handler.EligibleTargets != null)
{
    <MudPaper Class="pa-2 my-1" Outlined="true" Style="background-color: rgba(255,165,0,0.15);">
        <MudText Typo="Typo.body2">Choose target for @_handler.TargetingSpellName:</MudText>
        @foreach (var target in _handler.EligibleTargets)
        {
            <MudButton Size="Size.Small" Variant="Variant.Outlined" Color="Color.Warning"
                       Class="ma-1" OnClick="@(() => SelectTarget(target))">
                @target.Name
            </MudButton>
        }
    </MudPaper>
}
```

Add the SelectTarget method:

```csharp
private void SelectTarget(GameCard card)
{
    var ownerId = /* determine card owner from both players' battlefields */;
    _handler?.SubmitTarget(new TargetInfo(card.Id, ownerId, Enums.ZoneType.Battlefield));
}
```

**Step 3: Update Play button to use CastSpell for non-land registered cards**

In the action menu where PlayCard is dispatched, add logic:

```csharp
private void PlaySelectedCard()
{
    if (_selectedCard == null || _player == null) return;

    if (_selectedCard.IsLand)
    {
        _handler?.SubmitAction(GameAction.PlayCard(_player.Id, _selectedCard.Id));
    }
    else
    {
        _handler?.SubmitAction(GameAction.CastSpell(_player.Id, _selectedCard.Id));
    }
    _selectedCard = null;
}
```

**Step 4: Add timing feedback toast**

When a CastSpell action fails (logged as timing/mana error), show a toast. Listen for game log entries containing "Cannot cast" and display via MudBlazor Snackbar.

**Step 5: Build to verify**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet build src/MtgDecker.Web/ -v q`
Expected: 0 errors

**Step 6: Commit**

```bash
git add -A
git commit -m "feat(web): add target picker, CastSpell action, timing feedback"
```

---

### Task 14: Full Integration Test — RunTurnAsync with Stack

**Files:**
- Create: `tests/MtgDecker.Engine.Tests/StackTurnIntegrationTests.cs`

**Step 1: Write integration test — turn with stack interactions**

```csharp
// tests/MtgDecker.Engine.Tests/StackTurnIntegrationTests.cs
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class StackTurnIntegrationTests
{
    [Fact]
    public async Task FullTurn_CastAndResolve_ViaRunTurnAsync()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        for (int i = 0; i < 40; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}" });
            p2.Library.Add(new GameCard { Name = $"Card{i}" });
        }
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        await engine.StartGameAsync();

        // Put a Mountain in hand and a Mogg Fanatic
        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
        p1.Hand.Add(mountain);
        var goblin = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        p1.Hand.Add(goblin);

        // During MainPhase1: play land, tap for mana, cast spell, both pass to resolve, both pass to advance
        // Untap phase: auto
        // Draw phase: auto
        // MainPhase1:
        h1.EnqueueAction(GameAction.PlayCard(p1.Id, mountain.Id)); // play land
        h1.EnqueueAction(GameAction.TapCard(p1.Id, mountain.Id));  // tap for mana
        h1.EnqueueAction(GameAction.CastSpell(p1.Id, goblin.Id));  // cast creature
        // After cast, both pass → resolve
        h1.EnqueueAction(GameAction.Pass(p1.Id));
        h2.EnqueueAction(GameAction.Pass(p2.Id));
        // After resolve, both pass → advance to Combat
        h1.EnqueueAction(GameAction.Pass(p1.Id));
        h2.EnqueueAction(GameAction.Pass(p2.Id));

        // Combat: no eligible attackers (summoning sickness), skipped
        // MainPhase2: both pass
        h1.EnqueueAction(GameAction.Pass(p1.Id));
        h2.EnqueueAction(GameAction.Pass(p2.Id));
        // End: both pass
        h1.EnqueueAction(GameAction.Pass(p1.Id));
        h2.EnqueueAction(GameAction.Pass(p2.Id));

        await engine.RunTurnAsync();

        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Mountain");
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Mogg Fanatic");
        state.Stack.Should().BeEmpty();
    }
}
```

**Step 2: Run test**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "StackTurnIntegrationTests" -v n`
Expected: PASS

**Step 3: Run full suite**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v n`
Expected: All pass

**Step 4: Commit**

```bash
git add tests/MtgDecker.Engine.Tests/StackTurnIntegrationTests.cs
git commit -m "test(engine): add full-turn integration test with stack cast-resolve cycle"
```

---

### Summary

| Task | What | New Tests |
|------|------|-----------|
| 1 | StackObject + TargetInfo | 2 |
| 2 | TargetFilter + SpellEffect | 5 |
| 3 | Enums, GameAction, GameState, CardDefinition, Effects | 7 |
| 4 | ChooseTarget decision handler | 2 |
| 5 | Timing validation | 4 |
| 6 | CastSpell engine flow | 6 |
| 7 | Stack resolution + ResolveTopOfStack | 4 |
| 8 | Spell effect tests | 4 |
| 9 | Fizzle + Undo | 2 |
| 10 | Integration tests (full lifecycle) | 7 |
| 11 | InteractiveDecisionHandler target | 0 (build check) |
| 12 | UI — Stack display | 0 (build check) |
| 13 | UI — Target picker + CastSpell | 0 (build check) |
| 14 | Full-turn integration test | 1 |

**Total new tests: ~44**
**Estimated commits: 14**
