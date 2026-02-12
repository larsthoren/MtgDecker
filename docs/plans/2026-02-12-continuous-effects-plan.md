# Continuous Effects, Legendary Rule & Fetch Lands Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a unified continuous effect system (P/T modifiers, keyword grants, cost reduction, extra land drops), the legendary rule, fetch lands, and haste to the MtgDecker game engine.

**Architecture:** A `ContinuousEffect` record system on `GameState` is rebuilt from battlefield permanents after every board change. A central `RecalculateState()` method applies all effects. State-based actions (life, legendary rule) run after recalculation. Fetch lands use a new `ActivateFetch` action type.

**Tech Stack:** .NET 10, C# 14, xUnit + FluentAssertions (testing)

**Design doc:** `docs/plans/2026-02-12-continuous-effects-design.md`

**Worktree:** `.worktrees/continuous-effects/` on branch `feature/continuous-effects`

**Environment:**
```bash
export PATH="/c/Program Files/dotnet:$PATH"
# Test:
dotnet test tests/MtgDecker.Engine.Tests/
# Build web:
dotnet build src/MtgDecker.Web/
```

**Baseline:** 488 engine tests passing.

---

### Task 1: ContinuousEffect types and Keyword enum

Add the core data types. No behavior yet — just the records and enums.

**Files:**
- Create: `src/MtgDecker.Engine/ContinuousEffect.cs`
- Create: `src/MtgDecker.Engine/Enums/Keyword.cs`
- Create: `src/MtgDecker.Engine/FetchAbility.cs`
- Test: `tests/MtgDecker.Engine.Tests/ContinuousEffectTypeTests.cs`

**Step 1: Write the failing test**

```csharp
// tests/MtgDecker.Engine.Tests/ContinuousEffectTypeTests.cs
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class ContinuousEffectTypeTests
{
    [Fact]
    public void ContinuousEffect_Can_Be_Created_With_PowerToughness_Modifier()
    {
        var effect = new ContinuousEffect(
            Guid.NewGuid(),
            ContinuousEffectType.ModifyPowerToughness,
            (card, player) => card.IsCreature,
            PowerMod: 1, ToughnessMod: 1);

        effect.Type.Should().Be(ContinuousEffectType.ModifyPowerToughness);
        effect.PowerMod.Should().Be(1);
        effect.ToughnessMod.Should().Be(1);
    }

    [Fact]
    public void ContinuousEffect_Can_Be_Created_With_Keyword_Grant()
    {
        var effect = new ContinuousEffect(
            Guid.NewGuid(),
            ContinuousEffectType.GrantKeyword,
            (card, player) => card.IsCreature,
            GrantedKeyword: Keyword.Haste);

        effect.GrantedKeyword.Should().Be(Keyword.Haste);
    }

    [Fact]
    public void ContinuousEffect_Can_Be_Created_With_Cost_Modification()
    {
        var effect = new ContinuousEffect(
            Guid.NewGuid(),
            ContinuousEffectType.ModifyCost,
            (card, player) => true,
            CostMod: -1,
            CostApplies: c => c.Subtypes.Contains("Goblin"));

        effect.CostMod.Should().Be(-1);
        effect.CostApplies.Should().NotBeNull();
    }

    [Fact]
    public void ContinuousEffect_Can_Be_Created_With_ExtraLandDrop()
    {
        var effect = new ContinuousEffect(
            Guid.NewGuid(),
            ContinuousEffectType.ExtraLandDrop,
            (card, player) => true,
            ExtraLandDrops: 1);

        effect.ExtraLandDrops.Should().Be(1);
    }

    [Fact]
    public void FetchAbility_Stores_SearchTypes()
    {
        var fetch = new FetchAbility(["Mountain", "Forest"]);
        fetch.SearchTypes.Should().BeEquivalentTo(["Mountain", "Forest"]);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "ContinuousEffectTypeTests"`
Expected: FAIL — types don't exist yet.

**Step 3: Write minimal implementation**

```csharp
// src/MtgDecker.Engine/Enums/Keyword.cs
namespace MtgDecker.Engine.Enums;

public enum Keyword
{
    Haste,
    Shroud,
    Mountainwalk,
}
```

```csharp
// src/MtgDecker.Engine/ContinuousEffect.cs
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public enum ContinuousEffectType
{
    ModifyPowerToughness,
    GrantKeyword,
    ModifyCost,
    ExtraLandDrop,
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
    int ExtraLandDrops = 0);
```

```csharp
// src/MtgDecker.Engine/FetchAbility.cs
namespace MtgDecker.Engine;

public record FetchAbility(IReadOnlyList<string> SearchTypes);
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "ContinuousEffectTypeTests"`
Expected: 5 PASS

Run: `dotnet test tests/MtgDecker.Engine.Tests/`
Expected: 493 PASS (488 existing + 5 new), 0 FAIL

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/ContinuousEffect.cs src/MtgDecker.Engine/Enums/Keyword.cs src/MtgDecker.Engine/FetchAbility.cs tests/MtgDecker.Engine.Tests/ContinuousEffectTypeTests.cs
git commit -m "feat(engine): add ContinuousEffect, Keyword, and FetchAbility types"
```

---

### Task 2: GameCard BasePower/BaseToughness refactor + IsLegendary + ActiveKeywords

Rename `Power`/`Toughness` to `BasePower`/`BaseToughness`. Add computed `Power`/`Toughness` properties that return effective values. Add `IsLegendary`, `ActiveKeywords`, and `FetchAbility`. Update `HasSummoningSickness` for haste.

**Files:**
- Modify: `src/MtgDecker.Engine/GameCard.cs`
- Modify: `src/MtgDecker.Engine/CardDefinition.cs` (add IsLegendary, FetchAbility, ContinuousEffects)
- Test: `tests/MtgDecker.Engine.Tests/GameCardRefactorTests.cs`

**Step 1: Write the failing test**

```csharp
// tests/MtgDecker.Engine.Tests/GameCardRefactorTests.cs
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class GameCardRefactorTests
{
    [Fact]
    public void Power_Returns_BasePower_When_No_Effective_Set()
    {
        var card = new GameCard { BasePower = 3, BaseToughness = 4 };
        card.Power.Should().Be(3);
        card.Toughness.Should().Be(4);
    }

    [Fact]
    public void Power_Returns_EffectivePower_When_Set()
    {
        var card = new GameCard { BasePower = 3, BaseToughness = 4 };
        card.EffectivePower = 5;
        card.EffectiveToughness = 6;
        card.Power.Should().Be(5);
        card.Toughness.Should().Be(6);
    }

    [Fact]
    public void IsLegendary_Defaults_To_False()
    {
        var card = new GameCard();
        card.IsLegendary.Should().BeFalse();
    }

    [Fact]
    public void ActiveKeywords_Starts_Empty()
    {
        var card = new GameCard();
        card.ActiveKeywords.Should().BeEmpty();
    }

    [Fact]
    public void HasSummoningSickness_False_When_Has_Haste()
    {
        var card = new GameCard
        {
            BasePower = 2, BaseToughness = 2,
            CardTypes = CardType.Creature,
            TurnEnteredBattlefield = 5
        };
        card.ActiveKeywords.Add(Keyword.Haste);
        card.HasSummoningSickness(5).Should().BeFalse();
    }

    [Fact]
    public void HasSummoningSickness_True_Without_Haste()
    {
        var card = new GameCard
        {
            BasePower = 2, BaseToughness = 2,
            CardTypes = CardType.Creature,
            TurnEnteredBattlefield = 5
        };
        card.HasSummoningSickness(5).Should().BeTrue();
    }

    [Fact]
    public void FetchAbility_Can_Be_Set()
    {
        var card = new GameCard { FetchAbility = new FetchAbility(["Mountain", "Forest"]) };
        card.FetchAbility.Should().NotBeNull();
        card.FetchAbility!.SearchTypes.Should().Contain("Mountain");
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "GameCardRefactorTests"`
Expected: FAIL — `BasePower` property doesn't exist.

**Step 3: Write minimal implementation**

Modify `src/MtgDecker.Engine/GameCard.cs`:

- Rename the backing properties `Power`/`Toughness` to `BasePower`/`BaseToughness` (with `{ get; set; }`)
- Add `EffectivePower`/`EffectiveToughness` (`int?`, `{ get; set; }`)
- Add computed `Power` → `EffectivePower ?? BasePower` and `Toughness` → `EffectiveToughness ?? BaseToughness`
- Add `IsLegendary` (`bool`, `{ get; init; }`)
- Add `ActiveKeywords` (`HashSet<Keyword>`, `{ get; } = new()`)
- Add `FetchAbility` (`FetchAbility?`, `{ get; init; }`)
- Update `HasSummoningSickness` to check `!ActiveKeywords.Contains(Keyword.Haste)`
- Update both `Create()` factory methods: where they set `Power = def.Power`, change to `BasePower = def.Power` (same for Toughness). Also forward `IsLegendary` and `FetchAbility` from definition.

Modify `src/MtgDecker.Engine/CardDefinition.cs`:

- Add `public bool IsLegendary { get; init; }`
- Add `public FetchAbility? FetchAbility { get; init; }`
- Add `public IReadOnlyList<ContinuousEffect> ContinuousEffects { get; init; } = [];`

**Important:** All existing tests that read `Power`/`Toughness` still work because the computed properties return the same values (no effective override set yet). The `Create()` methods still pass through values from `CardDefinition` — just to `BasePower`/`BaseToughness` instead.

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "GameCardRefactorTests"`
Expected: 7 PASS

Run: `dotnet test tests/MtgDecker.Engine.Tests/`
Expected: 500 PASS (493 + 7), 0 FAIL — all existing tests still pass.

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/GameCard.cs src/MtgDecker.Engine/CardDefinition.cs tests/MtgDecker.Engine.Tests/GameCardRefactorTests.cs
git commit -m "feat(engine): refactor GameCard to BasePower/Toughness, add IsLegendary, ActiveKeywords, FetchAbility"
```

---

### Task 3: Player.MaxLandDrops + GameState.ActiveEffects

Add `MaxLandDrops` to `Player` (default 1). Add `ActiveEffects` list to `GameState`. Update the land drop check in `GameEngine.ExecuteAction` to use `MaxLandDrops`.

**Files:**
- Modify: `src/MtgDecker.Engine/Player.cs` (add `MaxLandDrops`)
- Modify: `src/MtgDecker.Engine/GameState.cs` (add `ActiveEffects`)
- Modify: `src/MtgDecker.Engine/GameEngine.cs:118` (change land drop check)
- Test: `tests/MtgDecker.Engine.Tests/ExtraLandDropTests.cs`

**Step 1: Write the failing test**

```csharp
// tests/MtgDecker.Engine.Tests/ExtraLandDropTests.cs
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class ExtraLandDropTests
{
    [Fact]
    public void Player_MaxLandDrops_Defaults_To_1()
    {
        var handler = new TestDecisionHandler();
        var player = new Player(Guid.NewGuid(), "Test", handler);
        player.MaxLandDrops.Should().Be(1);
    }

    [Fact]
    public async Task Player_Can_Play_Two_Lands_When_MaxLandDrops_Is_2()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        p1.MaxLandDrops = 2;

        var land1 = new GameCard { Name = "Forest", CardTypes = CardType.Land };
        var land2 = new GameCard { Name = "Mountain", CardTypes = CardType.Land };
        p1.Hand.Add(land1);
        p1.Hand.Add(land2);

        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, land1.Id));
        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, land2.Id));

        p1.Battlefield.Cards.Should().HaveCount(2);
        p1.LandsPlayedThisTurn.Should().Be(2);
    }

    [Fact]
    public async Task Player_Cannot_Play_Third_Land_When_MaxLandDrops_Is_2()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        p1.MaxLandDrops = 2;

        var land1 = new GameCard { Name = "F1", CardTypes = CardType.Land };
        var land2 = new GameCard { Name = "F2", CardTypes = CardType.Land };
        var land3 = new GameCard { Name = "F3", CardTypes = CardType.Land };
        p1.Hand.Add(land1);
        p1.Hand.Add(land2);
        p1.Hand.Add(land3);

        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, land1.Id));
        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, land2.Id));
        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, land3.Id));

        p1.Battlefield.Cards.Should().HaveCount(2);
        p1.Hand.Cards.Should().HaveCount(1);
    }

    [Fact]
    public void GameState_Has_ActiveEffects_List()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.ActiveEffects.Should().NotBeNull();
        state.ActiveEffects.Should().BeEmpty();
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "ExtraLandDropTests"`
Expected: FAIL — `MaxLandDrops` doesn't exist.

**Step 3: Write minimal implementation**

In `src/MtgDecker.Engine/Player.cs`, add:
```csharp
public int MaxLandDrops { get; set; } = 1;
```

In `src/MtgDecker.Engine/GameState.cs`, add:
```csharp
public List<ContinuousEffect> ActiveEffects { get; } = new();
```

In `src/MtgDecker.Engine/GameEngine.cs`, change line 118 from:
```csharp
if (player.LandsPlayedThisTurn >= 1)
```
to:
```csharp
if (player.LandsPlayedThisTurn >= player.MaxLandDrops)
```

**Step 4: Run tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "ExtraLandDropTests"`
Expected: 4 PASS

Run: `dotnet test tests/MtgDecker.Engine.Tests/`
Expected: 504 PASS, 0 FAIL

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/Player.cs src/MtgDecker.Engine/GameState.cs src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/ExtraLandDropTests.cs
git commit -m "feat(engine): add MaxLandDrops to Player, ActiveEffects to GameState"
```

---

### Task 4: RecalculateState() — P/T modifiers and keywords

Implement `RecalculateState()` on `GameEngine` that rebuilds `ActiveEffects` from battlefield permanents and applies P/T mods + keyword grants.

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (add `RecalculateState()`)
- Test: `tests/MtgDecker.Engine.Tests/ContinuousEffectTests.cs`

**Step 1: Write the failing test**

```csharp
// tests/MtgDecker.Engine.Tests/ContinuousEffectTests.cs
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Triggers;

namespace MtgDecker.Engine.Tests;

public class ContinuousEffectTests
{
    private static (GameEngine engine, GameState state, Player p1, Player p2) Setup()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, p1, p2);
    }

    [Fact]
    public void Lord_Buffs_Other_Creatures_Of_Same_Subtype()
    {
        var (engine, state, p1, _) = Setup();

        // Goblin King: +1/+1 to other Goblins
        var kingId = Guid.NewGuid();
        var king = new GameCard
        {
            Id = kingId, Name = "Goblin King", BasePower = 2, BaseToughness = 2,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };

        var grunt = new GameCard
        {
            Name = "Goblin Grunt", BasePower = 1, BaseToughness = 1,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };

        p1.Battlefield.Add(king);
        p1.Battlefield.Add(grunt);

        // Manually add effect as the lord would provide
        state.ActiveEffects.Add(new ContinuousEffect(
            kingId, ContinuousEffectType.ModifyPowerToughness,
            (card, _) => card.IsCreature && card.Subtypes.Contains("Goblin"),
            PowerMod: 1, ToughnessMod: 1));

        engine.RecalculateState();

        // King should NOT buff itself
        king.Power.Should().Be(2);
        king.Toughness.Should().Be(2);

        // Grunt should be buffed
        grunt.Power.Should().Be(2);
        grunt.Toughness.Should().Be(2);
    }

    [Fact]
    public void Multiple_Lords_Stack()
    {
        var (engine, state, p1, _) = Setup();

        var king1Id = Guid.NewGuid();
        var king2Id = Guid.NewGuid();
        var king1 = new GameCard
        {
            Id = king1Id, Name = "King 1", BasePower = 2, BaseToughness = 2,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };
        var king2 = new GameCard
        {
            Id = king2Id, Name = "King 2", BasePower = 2, BaseToughness = 2,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };
        var grunt = new GameCard
        {
            Name = "Grunt", BasePower = 1, BaseToughness = 1,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };

        p1.Battlefield.Add(king1);
        p1.Battlefield.Add(king2);
        p1.Battlefield.Add(grunt);

        state.ActiveEffects.Add(new ContinuousEffect(
            king1Id, ContinuousEffectType.ModifyPowerToughness,
            (card, _) => card.IsCreature && card.Subtypes.Contains("Goblin"),
            PowerMod: 1, ToughnessMod: 1));
        state.ActiveEffects.Add(new ContinuousEffect(
            king2Id, ContinuousEffectType.ModifyPowerToughness,
            (card, _) => card.IsCreature && card.Subtypes.Contains("Goblin"),
            PowerMod: 1, ToughnessMod: 1));

        engine.RecalculateState();

        // Each king buffs the other (+1/+1) but not itself
        king1.Power.Should().Be(3);
        king2.Power.Should().Be(3);
        // Grunt gets +2/+2 from two lords
        grunt.Power.Should().Be(3);
        grunt.Toughness.Should().Be(3);
    }

    [Fact]
    public void Keyword_Grant_Adds_To_ActiveKeywords()
    {
        var (engine, state, p1, _) = Setup();

        var warchiefId = Guid.NewGuid();
        var warchief = new GameCard
        {
            Id = warchiefId, Name = "Warchief", BasePower = 2, BaseToughness = 2,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };
        var goblin = new GameCard
        {
            Name = "Goblin", BasePower = 1, BaseToughness = 1,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };
        var elf = new GameCard
        {
            Name = "Elf", BasePower = 1, BaseToughness = 1,
            CardTypes = CardType.Creature, Subtypes = ["Elf"]
        };

        p1.Battlefield.Add(warchief);
        p1.Battlefield.Add(goblin);
        p1.Battlefield.Add(elf);

        state.ActiveEffects.Add(new ContinuousEffect(
            warchiefId, ContinuousEffectType.GrantKeyword,
            (card, _) => card.IsCreature && card.Subtypes.Contains("Goblin"),
            GrantedKeyword: Keyword.Haste));

        engine.RecalculateState();

        goblin.ActiveKeywords.Should().Contain(Keyword.Haste);
        warchief.ActiveKeywords.Should().Contain(Keyword.Haste);
        elf.ActiveKeywords.Should().NotContain(Keyword.Haste);
    }

    [Fact]
    public void ExtraLandDrop_Updates_MaxLandDrops()
    {
        var (engine, state, p1, _) = Setup();

        var exploration = new GameCard
        {
            Name = "Exploration", CardTypes = CardType.Enchantment
        };
        p1.Battlefield.Add(exploration);

        state.ActiveEffects.Add(new ContinuousEffect(
            exploration.Id, ContinuousEffectType.ExtraLandDrop,
            (_, _) => true, ExtraLandDrops: 1));

        engine.RecalculateState();

        p1.MaxLandDrops.Should().Be(2);
    }

    [Fact]
    public void Recalculate_Resets_Effective_When_No_Effects()
    {
        var (engine, state, p1, _) = Setup();

        var goblin = new GameCard
        {
            Name = "Goblin", BasePower = 1, BaseToughness = 1,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };
        goblin.EffectivePower = 5; // leftover from previous calculation
        p1.Battlefield.Add(goblin);

        engine.RecalculateState();

        goblin.EffectivePower.Should().BeNull();
        goblin.Power.Should().Be(1);
    }

    [Fact]
    public void UntilEndOfTurn_Effect_Applies_Before_Cleanup()
    {
        var (engine, state, p1, _) = Setup();

        var goblin = new GameCard
        {
            Name = "Goblin", BasePower = 1, BaseToughness = 1,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };
        p1.Battlefield.Add(goblin);

        state.ActiveEffects.Add(new ContinuousEffect(
            Guid.NewGuid(), ContinuousEffectType.ModifyPowerToughness,
            (card, _) => card.IsCreature && card.Subtypes.Contains("Goblin"),
            PowerMod: 3, UntilEndOfTurn: true));

        engine.RecalculateState();
        goblin.Power.Should().Be(4);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "ContinuousEffectTests"`
Expected: FAIL — `RecalculateState()` doesn't exist.

**Step 3: Write minimal implementation**

Add to `src/MtgDecker.Engine/GameEngine.cs`:

```csharp
public void RecalculateState()
{
    // Reset all effective values
    foreach (var card in _state.Player1.Battlefield.Cards)
    {
        card.EffectivePower = null;
        card.EffectiveToughness = null;
        card.ActiveKeywords.Clear();
    }
    foreach (var card in _state.Player2.Battlefield.Cards)
    {
        card.EffectivePower = null;
        card.EffectiveToughness = null;
        card.ActiveKeywords.Clear();
    }

    // Reset land drops
    _state.Player1.MaxLandDrops = 1;
    _state.Player2.MaxLandDrops = 1;

    // Apply effects
    foreach (var effect in _state.ActiveEffects)
    {
        switch (effect.Type)
        {
            case ContinuousEffectType.ModifyPowerToughness:
                ApplyPowerToughnessEffect(effect, _state.Player1);
                ApplyPowerToughnessEffect(effect, _state.Player2);
                break;

            case ContinuousEffectType.GrantKeyword:
                ApplyKeywordEffect(effect, _state.Player1);
                ApplyKeywordEffect(effect, _state.Player2);
                break;

            case ContinuousEffectType.ExtraLandDrop:
                // Extra land drops apply to the controller of the source
                var sourceOwner = _state.Player1.Battlefield.Cards.Any(c => c.Id == effect.SourceId)
                    ? _state.Player1 : _state.Player2;
                sourceOwner.MaxLandDrops += effect.ExtraLandDrops;
                break;
        }
    }
}

private void ApplyPowerToughnessEffect(ContinuousEffect effect, Player player)
{
    foreach (var card in player.Battlefield.Cards)
    {
        if (card.Id == effect.SourceId) continue; // lords don't buff themselves
        if (!effect.Applies(card, player)) continue;

        card.EffectivePower = (card.EffectivePower ?? card.BasePower ?? 0) + effect.PowerMod;
        card.EffectiveToughness = (card.EffectiveToughness ?? card.BaseToughness ?? 0) + effect.ToughnessMod;
    }
}

private void ApplyKeywordEffect(ContinuousEffect effect, Player player)
{
    foreach (var card in player.Battlefield.Cards)
    {
        if (!effect.Applies(card, player)) continue;
        if (effect.GrantedKeyword.HasValue)
            card.ActiveKeywords.Add(effect.GrantedKeyword.Value);
    }
}
```

**Step 4: Run tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "ContinuousEffectTests"`
Expected: 6 PASS

Run: `dotnet test tests/MtgDecker.Engine.Tests/`
Expected: 510 PASS, 0 FAIL

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/ContinuousEffectTests.cs
git commit -m "feat(engine): add RecalculateState for P/T mods, keywords, and extra land drops"
```

---

### Task 5: Wire RecalculateState + rebuild ActiveEffects from battlefield

Currently tests manually add effects to `state.ActiveEffects`. The engine should automatically rebuild effects from `CardDefinition.ContinuousEffects` on all battlefield permanents. Wire `RecalculateState()` into the game loop at all board-change points.

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (rebuild effects in `RecalculateState`, call after board changes)
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs` (add `ContinuousEffects` to Goblin King, Exploration, Goblin Warchief)
- Test: `tests/MtgDecker.Engine.Tests/ContinuousEffectWiringTests.cs`

**Step 1: Write the failing test**

```csharp
// tests/MtgDecker.Engine.Tests/ContinuousEffectWiringTests.cs
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class ContinuousEffectWiringTests
{
    private static (GameEngine engine, GameState state, Player p1, Player p2) Setup()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, p1, p2);
    }

    [Fact]
    public void Goblin_King_Auto_Buffs_Other_Goblins_From_CardDefinitions()
    {
        var (engine, state, p1, _) = Setup();

        var king = GameCard.Create("Goblin King", "Creature — Goblin");
        var grunt = new GameCard
        {
            Name = "Goblin Grunt", BasePower = 1, BaseToughness = 1,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };

        p1.Battlefield.Add(king);
        p1.Battlefield.Add(grunt);

        engine.RecalculateState();

        grunt.Power.Should().Be(2);
        grunt.Toughness.Should().Be(2);
        // King doesn't buff itself
        king.Power.Should().Be(2);
    }

    [Fact]
    public void Exploration_Grants_Extra_Land_Drop_From_CardDefinitions()
    {
        var (engine, _, p1, _) = Setup();

        var exploration = GameCard.Create("Exploration", "Enchantment");
        p1.Battlefield.Add(exploration);

        engine.RecalculateState();

        p1.MaxLandDrops.Should().Be(2);
    }

    [Fact]
    public void Goblin_Warchief_Grants_Haste_From_CardDefinitions()
    {
        var (engine, _, p1, _) = Setup();

        var warchief = GameCard.Create("Goblin Warchief", "Creature — Goblin");
        var lackey = GameCard.Create("Goblin Lackey", "Creature — Goblin");

        p1.Battlefield.Add(warchief);
        p1.Battlefield.Add(lackey);

        engine.RecalculateState();

        lackey.ActiveKeywords.Should().Contain(Keyword.Haste);
        warchief.ActiveKeywords.Should().Contain(Keyword.Haste);
    }

    [Fact]
    public void Effects_Removed_When_Source_Leaves_Battlefield()
    {
        var (engine, state, p1, _) = Setup();

        var king = GameCard.Create("Goblin King", "Creature — Goblin");
        var grunt = new GameCard
        {
            Name = "Goblin Grunt", BasePower = 1, BaseToughness = 1,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };

        p1.Battlefield.Add(king);
        p1.Battlefield.Add(grunt);

        engine.RecalculateState();
        grunt.Power.Should().Be(2);

        // King leaves battlefield
        p1.Battlefield.RemoveById(king.Id);
        engine.RecalculateState();

        grunt.Power.Should().Be(1);
        state.ActiveEffects.Should().BeEmpty();
    }

    [Fact]
    public void Two_Explorations_Grant_Three_Land_Drops()
    {
        var (engine, _, p1, _) = Setup();

        p1.Battlefield.Add(GameCard.Create("Exploration", "Enchantment"));
        p1.Battlefield.Add(GameCard.Create("Exploration", "Enchantment"));

        engine.RecalculateState();

        p1.MaxLandDrops.Should().Be(3);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "ContinuousEffectWiringTests"`
Expected: FAIL — CardDefinitions don't have ContinuousEffects yet.

**Step 3: Write minimal implementation**

Update `RecalculateState()` in `GameEngine.cs` to rebuild `ActiveEffects` before applying:

```csharp
public void RecalculateState()
{
    // Rebuild ActiveEffects from battlefield
    _state.ActiveEffects.Clear();
    RebuildActiveEffects(_state.Player1);
    RebuildActiveEffects(_state.Player2);

    // Reset all effective values ... (rest stays the same)
}

private void RebuildActiveEffects(Player player)
{
    foreach (var card in player.Battlefield.Cards)
    {
        if (!CardDefinitions.TryGet(card.Name, out var def)) continue;
        foreach (var templateEffect in def.ContinuousEffects)
        {
            // Stamp with actual card ID
            var effect = templateEffect with { SourceId = card.Id };
            _state.ActiveEffects.Add(effect);
        }
    }
}
```

Update `CardDefinitions.cs` to add ContinuousEffects:

```csharp
["Goblin King"] = new(ManaCost.Parse("{1}{R}{R}"), null, 2, 2, CardType.Creature)
{
    Subtypes = ["Goblin"],
    ContinuousEffects = [new ContinuousEffect(
        Guid.Empty, ContinuousEffectType.ModifyPowerToughness,
        (card, _) => card.IsCreature && card.Subtypes.Contains("Goblin"),
        PowerMod: 1, ToughnessMod: 1)]
},

["Goblin Warchief"] = new(ManaCost.Parse("{1}{R}{R}"), null, 2, 2, CardType.Creature)
{
    Subtypes = ["Goblin"],
    ContinuousEffects = [
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
            (card, _) => card.IsCreature && card.Subtypes.Contains("Goblin"),
            GrantedKeyword: Keyword.Haste),
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyCost,
            (_, _) => true, CostMod: -1,
            CostApplies: c => c.Subtypes.Contains("Goblin"))]
},

["Exploration"] = new(ManaCost.Parse("{G}"), null, null, null, CardType.Enchantment)
{
    ContinuousEffects = [new ContinuousEffect(
        Guid.Empty, ContinuousEffectType.ExtraLandDrop,
        (_, _) => true, ExtraLandDrops: 1)]
},
```

**Step 4: Run tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "ContinuousEffectWiringTests"`
Expected: 5 PASS

Run: `dotnet test tests/MtgDecker.Engine.Tests/`
Expected: 515 PASS, 0 FAIL

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/GameEngine.cs src/MtgDecker.Engine/CardDefinitions.cs tests/MtgDecker.Engine.Tests/ContinuousEffectWiringTests.cs
git commit -m "feat(engine): auto-rebuild ActiveEffects from battlefield, wire Goblin King/Warchief/Exploration"
```

---

### Task 6: Legendary rule (async SBA with player choice)

Implement the legendary rule as part of state-based actions. Make `CheckStateBasedActions` async. Add `IsLegendary` to Serra's Sanctum in `CardDefinitions`.

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (`CheckStateBasedActions` → async, add legendary check)
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs` (Serra's Sanctum `IsLegendary = true`)
- Modify: `src/MtgDecker.Engine/GameCard.cs` (auto-detect legendary from TypeLine in `Create()`)
- Test: `tests/MtgDecker.Engine.Tests/LegendaryRuleTests.cs`

**Step 1: Write the failing test**

```csharp
// tests/MtgDecker.Engine.Tests/LegendaryRuleTests.cs
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class LegendaryRuleTests
{
    [Fact]
    public async Task Two_Legendaries_Same_Name_One_Goes_To_Graveyard()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var sanctum1 = new GameCard
        {
            Name = "Serra's Sanctum", CardTypes = CardType.Land, IsLegendary = true
        };
        var sanctum2 = new GameCard
        {
            Name = "Serra's Sanctum", CardTypes = CardType.Land, IsLegendary = true
        };

        p1.Battlefield.Add(sanctum1);
        p1.Battlefield.Add(sanctum2);

        // Player chooses to keep the first one
        handler.EnqueueCardChoice(sanctum1.Id);

        await engine.CheckStateBasedActionsAsync();

        p1.Battlefield.Cards.Should().HaveCount(1);
        p1.Battlefield.Cards[0].Id.Should().Be(sanctum1.Id);
        p1.Graveyard.Cards.Should().HaveCount(1);
        p1.Graveyard.Cards[0].Id.Should().Be(sanctum2.Id);
    }

    [Fact]
    public async Task No_Legendary_Duplicates_No_Action()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var sanctum = new GameCard
        {
            Name = "Serra's Sanctum", CardTypes = CardType.Land, IsLegendary = true
        };
        var mountain = new GameCard
        {
            Name = "Mountain", CardTypes = CardType.Land
        };

        p1.Battlefield.Add(sanctum);
        p1.Battlefield.Add(mountain);

        await engine.CheckStateBasedActionsAsync();

        p1.Battlefield.Cards.Should().HaveCount(2);
        p1.Graveyard.Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task Three_Legendaries_Two_Go_To_Graveyard()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var s1 = new GameCard { Name = "Serra's Sanctum", CardTypes = CardType.Land, IsLegendary = true };
        var s2 = new GameCard { Name = "Serra's Sanctum", CardTypes = CardType.Land, IsLegendary = true };
        var s3 = new GameCard { Name = "Serra's Sanctum", CardTypes = CardType.Land, IsLegendary = true };

        p1.Battlefield.Add(s1);
        p1.Battlefield.Add(s2);
        p1.Battlefield.Add(s3);

        handler.EnqueueCardChoice(s2.Id);

        await engine.CheckStateBasedActionsAsync();

        p1.Battlefield.Cards.Should().HaveCount(1);
        p1.Battlefield.Cards[0].Id.Should().Be(s2.Id);
        p1.Graveyard.Cards.Should().HaveCount(2);
    }

    [Fact]
    public void SerrasSanctum_Is_Legendary_In_CardDefinitions()
    {
        var card = GameCard.Create("Serra's Sanctum", "Legendary Land");
        card.IsLegendary.Should().BeTrue();
    }
}
```

**Note on TestDecisionHandler:** The existing `TestDecisionHandler` has `ChooseCard` which returns queued `Guid?` values. Add a helper method `EnqueueCardChoice(Guid id)` that enqueues the id. If it doesn't already exist, the implementer should add it — it's just a `Queue<Guid?>` with an enqueue method.

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "LegendaryRuleTests"`
Expected: FAIL — `CheckStateBasedActionsAsync` doesn't exist.

**Step 3: Write minimal implementation**

1. Rename `CheckStateBasedActions()` to `CheckStateBasedActionsAsync()` returning `async Task`. Update all call sites (in `RunCombatAsync`, `RunTurnAsync`) to `await`.

2. Add legendary check after life checks:

```csharp
internal async Task CheckStateBasedActionsAsync(CancellationToken ct = default)
{
    if (_state.IsGameOver) return;

    // Existing life checks (keep as-is)...

    // Legendary rule
    await CheckLegendaryRuleAsync(_state.Player1, ct);
    await CheckLegendaryRuleAsync(_state.Player2, ct);
}

private async Task CheckLegendaryRuleAsync(Player player, CancellationToken ct)
{
    var legendaries = player.Battlefield.Cards
        .Where(c => c.IsLegendary)
        .GroupBy(c => c.Name)
        .Where(g => g.Count() > 1)
        .ToList();

    foreach (var group in legendaries)
    {
        var duplicates = group.ToList();
        var chosenId = await player.DecisionHandler.ChooseCard(
            duplicates,
            $"Choose which {duplicates[0].Name} to keep (legendary rule)",
            optional: false, ct);

        foreach (var card in duplicates.Where(c => c.Id != chosenId))
        {
            player.Battlefield.RemoveById(card.Id);
            player.Graveyard.Add(card);
            _state.Log($"{card.Name} is put into graveyard (legendary rule).");
        }
    }
}
```

3. Update `CardDefinitions.cs`:
```csharp
["Serra's Sanctum"] = new(null, null, null, null, CardType.Land) { IsLegendary = true },
```

4. In `GameCard.Create()` (the enhanced overload), auto-detect legendary from type line:
```csharp
IsLegendary = def?.IsLegendary ?? typeLine.Contains("Legendary", StringComparison.OrdinalIgnoreCase),
```

**Step 4: Run tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "LegendaryRuleTests"`
Expected: 4 PASS

Run: `dotnet test tests/MtgDecker.Engine.Tests/`
Expected: 519 PASS, 0 FAIL

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/GameEngine.cs src/MtgDecker.Engine/CardDefinitions.cs src/MtgDecker.Engine/GameCard.cs tests/MtgDecker.Engine.Tests/LegendaryRuleTests.cs
git commit -m "feat(engine): add legendary rule as async SBA with player choice"
```

---

### Task 7: Fetch lands

Implement `ActivateFetch` action type. Add `FetchAbility` to Wooded Foothills and Windswept Heath. Add subtypes to basic lands.

**Files:**
- Modify: `src/MtgDecker.Engine/Enums/ActionType.cs` (add `ActivateFetch`)
- Modify: `src/MtgDecker.Engine/GameAction.cs` (add `ActivateFetch` factory)
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (handle `ActivateFetch` in `ExecuteAction`)
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs` (fetch abilities + basic land subtypes)
- Test: `tests/MtgDecker.Engine.Tests/FetchLandTests.cs`

**Step 1: Write the failing test**

```csharp
// tests/MtgDecker.Engine.Tests/FetchLandTests.cs
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class FetchLandTests
{
    [Fact]
    public async Task Fetch_Land_Sacrifices_And_Searches_Library()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        var fetch = GameCard.Create("Wooded Foothills", "Land");
        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
        var forest = GameCard.Create("Forest", "Basic Land — Forest");

        p1.Battlefield.Add(fetch);
        p1.Library.Add(mountain);
        p1.Library.Add(forest);

        // Player chooses the Mountain
        handler.EnqueueCardChoice(mountain.Id);

        await engine.ExecuteAction(GameAction.ActivateFetch(p1.Id, fetch.Id));

        // Fetch land sacrificed
        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Wooded Foothills");
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Wooded Foothills");

        // Mountain fetched to battlefield
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Mountain");
        p1.Library.Cards.Should().NotContain(c => c.Name == "Mountain");

        // 1 life paid
        p1.Life.Should().Be(19);
    }

    [Fact]
    public async Task Fetch_Land_Shuffles_Library_After_Search()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        var fetch = GameCard.Create("Wooded Foothills", "Land");
        // Add several cards to library to verify it gets shuffled
        for (int i = 0; i < 10; i++)
            p1.Library.Add(new GameCard { Name = $"Card {i}" });
        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
        p1.Library.Add(mountain);
        p1.Battlefield.Add(fetch);

        handler.EnqueueCardChoice(mountain.Id);

        await engine.ExecuteAction(GameAction.ActivateFetch(p1.Id, fetch.Id));

        // Library should still have 10 cards (mountain was removed)
        p1.Library.Count.Should().Be(10);
    }

    [Fact]
    public async Task Fetch_Land_Only_Finds_Matching_Subtypes()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        var fetch = GameCard.Create("Windswept Heath", "Land");
        var plains = GameCard.Create("Plains", "Basic Land — Plains");
        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");

        p1.Battlefield.Add(fetch);
        p1.Library.Add(plains);
        p1.Library.Add(mountain);

        // Windswept Heath searches for Plains or Forest — not Mountain
        handler.EnqueueCardChoice(plains.Id);

        await engine.ExecuteAction(GameAction.ActivateFetch(p1.Id, fetch.Id));

        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Plains");
        // Mountain should still be in library (not eligible)
        p1.Library.Cards.Should().Contain(c => c.Name == "Mountain");
    }

    [Fact]
    public async Task Fetch_Land_No_Match_In_Library_Still_Sacrifices()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        var fetch = GameCard.Create("Wooded Foothills", "Land");
        p1.Battlefield.Add(fetch);
        // Empty library — nothing to find

        await engine.ExecuteAction(GameAction.ActivateFetch(p1.Id, fetch.Id));

        p1.Battlefield.Cards.Should().BeEmpty();
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Wooded Foothills");
        p1.Life.Should().Be(19);
    }

    [Fact]
    public void Basic_Lands_Have_Subtypes_In_CardDefinitions()
    {
        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
        mountain.Subtypes.Should().Contain("Mountain");

        var forest = GameCard.Create("Forest", "Basic Land — Forest");
        forest.Subtypes.Should().Contain("Forest");

        var plains = GameCard.Create("Plains", "Basic Land — Plains");
        plains.Subtypes.Should().Contain("Plains");
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "FetchLandTests"`
Expected: FAIL — `ActivateFetch` doesn't exist.

**Step 3: Write minimal implementation**

1. Add to `ActionType.cs`:
```csharp
ActivateFetch
```

2. Add to `GameAction.cs`:
```csharp
public static GameAction ActivateFetch(Guid playerId, Guid cardId) =>
    new() { Type = ActionType.ActivateFetch, PlayerId = playerId, CardId = cardId };
```

3. Add `ActivateFetch` case to `ExecuteAction` in `GameEngine.cs`:
```csharp
case ActionType.ActivateFetch:
{
    var fetchLand = player.Battlefield.Cards.FirstOrDefault(c => c.Id == action.CardId);
    if (fetchLand == null) break;
    if (!CardDefinitions.TryGet(fetchLand.Name, out var fetchDef) || fetchDef.FetchAbility == null) break;

    // Pay costs: 1 life + sacrifice
    player.AdjustLife(-1);
    player.Battlefield.RemoveById(fetchLand.Id);
    player.Graveyard.Add(fetchLand);
    _state.Log($"{player.Name} sacrifices {fetchLand.Name}, pays 1 life ({player.Life}).");

    // Search library for matching land
    var searchTypes = fetchDef.FetchAbility.SearchTypes;
    var eligible = player.Library.Cards
        .Where(c => c.IsLand && searchTypes.Any(t =>
            c.Subtypes.Contains(t) || c.Name.Equals(t, StringComparison.OrdinalIgnoreCase)))
        .ToList();

    if (eligible.Count > 0)
    {
        var chosenId = await player.DecisionHandler.ChooseCard(
            eligible, $"Search for a land ({string.Join(" or ", searchTypes)})",
            optional: true, ct);

        if (chosenId != null)
        {
            var land = player.Library.RemoveById(chosenId.Value);
            if (land != null)
            {
                player.Battlefield.Add(land);
                land.TurnEnteredBattlefield = _state.TurnNumber;
                _state.Log($"{player.Name} fetches {land.Name}.");
                await ProcessTriggersAsync(GameEvent.EnterBattlefield, land, player, ct);
            }
        }
    }
    else
    {
        _state.Log($"{player.Name} finds no matching land.");
    }

    player.Library.Shuffle();
    player.ActionHistory.Push(action);
    break;
}
```

4. Update `CardDefinitions.cs`:
```csharp
// Basic land subtypes
["Mountain"] = new(null, ManaAbility.Fixed(ManaColor.Red), null, null, CardType.Land) { Subtypes = ["Mountain"] },
["Forest"] = new(null, ManaAbility.Fixed(ManaColor.Green), null, null, CardType.Land) { Subtypes = ["Forest"] },
["Plains"] = new(null, ManaAbility.Fixed(ManaColor.White), null, null, CardType.Land) { Subtypes = ["Plains"] },

// Fetch lands
["Wooded Foothills"] = new(null, null, null, null, CardType.Land) { FetchAbility = new FetchAbility(["Mountain", "Forest"]) },
["Windswept Heath"] = new(null, null, null, null, CardType.Land) { FetchAbility = new FetchAbility(["Plains", "Forest"]) },
```

**Step 4: Run tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "FetchLandTests"`
Expected: 5 PASS

Run: `dotnet test tests/MtgDecker.Engine.Tests/`
Expected: 524 PASS, 0 FAIL

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/Enums/ActionType.cs src/MtgDecker.Engine/GameAction.cs src/MtgDecker.Engine/GameEngine.cs src/MtgDecker.Engine/CardDefinitions.cs tests/MtgDecker.Engine.Tests/FetchLandTests.cs
git commit -m "feat(engine): add fetch land activation with library search and sacrifice"
```

---

### Task 8: Cost modification during spell casting

Apply cost reduction/increase from `ModifyCost` effects when casting spells. Affects both `PlayCard` and `CastSpell` paths.

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (apply cost mods before `CanPay`)
- Modify: `src/MtgDecker.Engine/Mana/ManaCost.cs` (add `WithGenericReduction` method)
- Test: `tests/MtgDecker.Engine.Tests/CostModificationTests.cs`

**Step 1: Write the failing test**

```csharp
// tests/MtgDecker.Engine.Tests/CostModificationTests.cs
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests;

public class CostModificationTests
{
    [Fact]
    public void ManaCost_WithGenericReduction_Reduces_Generic()
    {
        var cost = ManaCost.Parse("{3}{R}"); // 3 generic + 1 red = CMC 4
        var reduced = cost.WithGenericReduction(1);
        reduced.GenericCost.Should().Be(2);
        reduced.ColorRequirements.Should().ContainKey(ManaColor.Red);
    }

    [Fact]
    public void ManaCost_WithGenericReduction_Cannot_Go_Below_Zero()
    {
        var cost = ManaCost.Parse("{1}{R}");
        var reduced = cost.WithGenericReduction(5);
        reduced.GenericCost.Should().Be(0);
    }

    [Fact]
    public async Task Warchief_Reduces_Goblin_Spell_Cost()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        // Warchief on battlefield — Goblins cost {1} less
        var warchief = GameCard.Create("Goblin Warchief", "Creature — Goblin");
        p1.Battlefield.Add(warchief);
        engine.RecalculateState();

        // Goblin Ringleader normally costs {3}{R} = 4 total
        // With Warchief: {2}{R} = 3 total
        var ringleader = GameCard.Create("Goblin Ringleader", "Creature — Goblin");
        p1.Hand.Add(ringleader);

        // Only 3 mana available (2 colorless + 1 red)
        p1.ManaPool.Add(ManaColor.Red);
        p1.ManaPool.Add(ManaColor.Colorless);
        p1.ManaPool.Add(ManaColor.Colorless);

        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, ringleader.Id));

        // Should be cast successfully with reduced cost
        p1.Hand.Cards.Should().NotContain(c => c.Name == "Goblin Ringleader");
    }

    [Fact]
    public async Task Warchief_Does_Not_Reduce_NonGoblin_Cost()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        var warchief = GameCard.Create("Goblin Warchief", "Creature — Goblin");
        p1.Battlefield.Add(warchief);
        engine.RecalculateState();

        // Naturalize costs {1}{G} — not a Goblin, shouldn't be reduced
        var naturalize = GameCard.Create("Naturalize", "Instant");
        p1.Hand.Add(naturalize);

        // Only 1 green mana — not enough for {1}{G} without reduction
        p1.ManaPool.Add(ManaColor.Green);

        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, naturalize.Id));

        // Should NOT be cast — still in hand
        p1.Hand.Cards.Should().Contain(c => c.Name == "Naturalize");
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "CostModificationTests"`
Expected: FAIL — `WithGenericReduction` doesn't exist.

**Step 3: Write minimal implementation**

1. Add to `ManaCost.cs`:
```csharp
public ManaCost WithGenericReduction(int reduction)
{
    var newGeneric = Math.Max(0, GenericCost - reduction);
    var colorReqs = new Dictionary<ManaColor, int>(ColorRequirements);
    return new ManaCost(colorReqs, newGeneric);
}
```
Note: The `ManaCost` constructor is private. Either make it `internal` or add this as a static factory. The implementer should adjust visibility as needed.

2. In `GameEngine.ExecuteAction`, for the `PlayCard` spell-casting path (line ~133), before checking `CanPay`, compute effective cost:

```csharp
var effectiveCost = playCard.ManaCost;
var costReduction = _state.ActiveEffects
    .Where(e => e.Type == ContinuousEffectType.ModifyCost && e.CostApplies != null && e.CostApplies(playCard))
    .Sum(e => e.CostMod);
if (costReduction != 0)
    effectiveCost = effectiveCost.WithGenericReduction(-costReduction); // CostMod is negative for reduction

if (!player.ManaPool.CanPay(effectiveCost))
    // ... log and break
```

Then use `effectiveCost` instead of `playCard.ManaCost` for the rest of the payment logic.

3. Do the same in the `CastSpell` action path.

**Step 4: Run tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "CostModificationTests"`
Expected: 4 PASS

Run: `dotnet test tests/MtgDecker.Engine.Tests/`
Expected: 528 PASS, 0 FAIL

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/Mana/ManaCost.cs src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/CostModificationTests.cs
git commit -m "feat(engine): apply cost reduction from continuous effects during spell casting"
```

---

### Task 9: Wire OnBoardChanged into the game loop

Call `RecalculateState()` and `CheckStateBasedActionsAsync()` at every board mutation point. Add `OnBoardChangedAsync()` helper. Wire after: ETB (land drop, spell resolve, fetch), death, sacrifice, destroy, stack resolution.

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (add `OnBoardChangedAsync`, wire everywhere)
- Test: `tests/MtgDecker.Engine.Tests/OnBoardChangedIntegrationTests.cs`

**Step 1: Write the failing test**

```csharp
// tests/MtgDecker.Engine.Tests/OnBoardChangedIntegrationTests.cs
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class OnBoardChangedIntegrationTests
{
    [Fact]
    public async Task Goblin_King_Buffs_Apply_After_Land_Drop_ETB()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        var king = GameCard.Create("Goblin King", "Creature — Goblin");
        p1.Battlefield.Add(king);

        // Play a creature via sandbox (no mana cost) — should get buffed after ETB
        var grunt = new GameCard
        {
            Name = "Grunt", BasePower = 1, BaseToughness = 1,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };
        p1.Hand.Add(grunt);

        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, grunt.Id));

        // After ETB, OnBoardChanged should have recalculated
        grunt.Power.Should().Be(2);
        grunt.Toughness.Should().Be(2);
    }

    [Fact]
    public async Task Haste_From_Warchief_Allows_Immediate_Attack()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.TurnNumber = 3;
        var engine = new GameEngine(state);

        var warchief = GameCard.Create("Goblin Warchief", "Creature — Goblin");
        warchief.TurnEnteredBattlefield = 1; // already in play
        p1.Battlefield.Add(warchief);

        // New goblin entering this turn
        var lackey = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        lackey.TurnEnteredBattlefield = 3; // just entered

        p1.Battlefield.Add(lackey);
        engine.RecalculateState();

        // Lackey has haste from Warchief — no summoning sickness
        lackey.HasSummoningSickness(3).Should().BeFalse();
    }

    [Fact]
    public async Task Exploration_Allows_Two_Land_Drops_In_Full_Turn()
    {
        var handler = new TestDecisionHandler();
        handler.EnqueueAction(GameAction.Pass(Guid.Empty)); // will be fixed with actual player id

        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        var exploration = GameCard.Create("Exploration", "Enchantment");
        p1.Battlefield.Add(exploration);
        engine.RecalculateState();

        var land1 = new GameCard { Name = "Forest", CardTypes = CardType.Land };
        var land2 = new GameCard { Name = "Mountain", CardTypes = CardType.Land };
        p1.Hand.Add(land1);
        p1.Hand.Add(land2);

        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, land1.Id));
        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, land2.Id));

        p1.Battlefield.Cards.Should().HaveCount(3); // exploration + 2 lands
        p1.LandsPlayedThisTurn.Should().Be(2);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "OnBoardChangedIntegrationTests"`
Expected: FAIL — `OnBoardChangedAsync` doesn't exist / effects not applied after ETB.

**Step 3: Write minimal implementation**

Add to `GameEngine.cs`:

```csharp
internal async Task OnBoardChangedAsync(CancellationToken ct = default)
{
    RecalculateState();
    await CheckStateBasedActionsAsync(ct);
}
```

Wire `await OnBoardChangedAsync(ct)` after every board mutation in `ExecuteAction`:
- After land drop ETB (after `ProcessTriggersAsync` for land drops)
- After spell cast ETB (after `ProcessTriggersAsync` for permanents)
- After sandbox play ETB
- After fetch land (after the fetched land ETB)

Wire in `ResolveTopOfStack`:
- After permanent enters battlefield
- After spell goes to graveyard (if it destroyed something)
- Make `ResolveTopOfStack` async → `ResolveTopOfStackAsync`

Wire in `ProcessCombatDeaths`:
- After deaths are processed, call `OnBoardChangedAsync`
- Make method async if not already

Wire in `RunTurnAsync`:
- Reset `MaxLandDrops` at turn start (before `RecalculateState`)

Also call `RecalculateState()` at the end of `StartGameAsync` (so effects from starting battlefield are active).

**Step 4: Run tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "OnBoardChangedIntegrationTests"`
Expected: 3 PASS

Run: `dotnet test tests/MtgDecker.Engine.Tests/`
Expected: 531 PASS, 0 FAIL

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/OnBoardChangedIntegrationTests.cs
git commit -m "feat(engine): wire OnBoardChangedAsync into game loop for automatic effect recalculation"
```

---

### Task 10: AI bot updates for fetch lands

Update `AiBotDecisionHandler.GetAction` to activate fetch lands. Update `BoardEvaluator` to use `Power`/`Toughness` (already returns effective values via computed properties — just verify).

**Files:**
- Modify: `src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs` (fetch land in GetAction)
- Test: `tests/MtgDecker.Engine.Tests/AI/AiBotFetchTests.cs`

**Step 1: Write the failing test**

```csharp
// tests/MtgDecker.Engine.Tests/AI/AiBotFetchTests.cs
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.AI;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests.AI;

public class AiBotFetchTests
{
    [Fact]
    public async Task Bot_Activates_Fetch_Land_During_Main_Phase()
    {
        var bot = new AiBotDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Bot", bot);
        var p2 = new Player(Guid.NewGuid(), "Opp", new AiBotDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;

        var fetch = GameCard.Create("Wooded Foothills", "Land");
        p1.Battlefield.Add(fetch);

        // Bot has a spell in hand to motivate fetching
        var spell = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        p1.Hand.Add(spell);

        // Library has a fetchable land
        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
        p1.Library.Add(mountain);

        var action = await bot.GetAction(state, p1.Id);
        action.Type.Should().Be(ActionType.ActivateFetch);
        action.CardId.Should().Be(fetch.Id);
    }

    [Fact]
    public async Task Bot_Plays_Land_Before_Fetching()
    {
        var bot = new AiBotDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Bot", bot);
        var p2 = new Player(Guid.NewGuid(), "Opp", new AiBotDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;

        var fetch = GameCard.Create("Wooded Foothills", "Land");
        p1.Battlefield.Add(fetch);

        // Bot also has a land in hand — should play it first (land drop)
        var handLand = new GameCard { Name = "Mountain", CardTypes = CardType.Land };
        p1.Hand.Add(handLand);

        var action = await bot.GetAction(state, p1.Id);
        // Should play the land from hand first (land drop), not activate fetch
        action.Type.Should().Be(ActionType.PlayCard);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "AiBotFetchTests"`
Expected: FAIL — bot doesn't know about fetch lands.

**Step 3: Write minimal implementation**

In `AiBotDecisionHandler.GetAction`, add a fetch land check after land drop but before tapping lands:

```csharp
// Priority 1: Play a land (existing)
// Priority 2 (NEW): Activate a fetch land if we have spells to cast
if (hasSpellInHand) // reuse or compute this check
{
    var fetchLand = player.Battlefield.Cards
        .FirstOrDefault(c => c.FetchAbility != null && !c.IsTapped);
    if (fetchLand != null)
        return Task.FromResult(GameAction.ActivateFetch(playerId, fetchLand.Id));
}
// Priority 3: Tap lands (existing)
// Priority 4: Cast spell (existing)
```

**Step 4: Run tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "AiBotFetchTests"`
Expected: 2 PASS

Run: `dotnet test tests/MtgDecker.Engine.Tests/`
Expected: 533 PASS, 0 FAIL

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs tests/MtgDecker.Engine.Tests/AI/AiBotFetchTests.cs
git commit -m "feat(engine): AI bot activates fetch lands during main phase"
```

---

### Task 11: End-of-turn cleanup for UntilEndOfTurn effects

Strip `UntilEndOfTurn` effects at end of turn and recalculate. This ensures temporary buffs (like Goblin Pyromancer's +3/+0) expire properly.

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (strip in `RunTurnAsync` end-of-turn)
- Test: `tests/MtgDecker.Engine.Tests/UntilEndOfTurnTests.cs`

**Step 1: Write the failing test**

```csharp
// tests/MtgDecker.Engine.Tests/UntilEndOfTurnTests.cs
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class UntilEndOfTurnTests
{
    [Fact]
    public void StripEndOfTurnEffects_Removes_Temporary_Effects()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var goblin = new GameCard
        {
            Name = "Goblin", BasePower = 1, BaseToughness = 1,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };
        p1.Battlefield.Add(goblin);

        // Add a temporary buff
        state.ActiveEffects.Add(new ContinuousEffect(
            Guid.NewGuid(), ContinuousEffectType.ModifyPowerToughness,
            (c, _) => c.IsCreature && c.Subtypes.Contains("Goblin"),
            PowerMod: 3, UntilEndOfTurn: true));

        engine.RecalculateState();
        goblin.Power.Should().Be(4); // 1 + 3

        // Simulate end of turn
        engine.StripEndOfTurnEffects();
        engine.RecalculateState();

        goblin.Power.Should().Be(1); // back to base
    }

    [Fact]
    public void StripEndOfTurnEffects_Keeps_Permanent_Effects()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var king = GameCard.Create("Goblin King", "Creature — Goblin");
        var goblin = new GameCard
        {
            Name = "Goblin", BasePower = 1, BaseToughness = 1,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };
        p1.Battlefield.Add(king);
        p1.Battlefield.Add(goblin);

        engine.RecalculateState();
        goblin.Power.Should().Be(2);

        engine.StripEndOfTurnEffects();
        engine.RecalculateState();

        // King's permanent buff still applies
        goblin.Power.Should().Be(2);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "UntilEndOfTurnTests"`
Expected: FAIL — `StripEndOfTurnEffects` doesn't exist.

**Step 3: Write minimal implementation**

Add to `GameEngine.cs`:

```csharp
public void StripEndOfTurnEffects()
{
    _state.ActiveEffects.RemoveAll(e => e.UntilEndOfTurn);
}
```

In `RunTurnAsync`, right before `ClearDamage()` (before end of turn), add:

```csharp
StripEndOfTurnEffects();
RecalculateState();
```

**Step 4: Run tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "UntilEndOfTurnTests"`
Expected: 2 PASS

Run: `dotnet test tests/MtgDecker.Engine.Tests/`
Expected: 535 PASS, 0 FAIL

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/UntilEndOfTurnTests.cs
git commit -m "feat(engine): strip UntilEndOfTurn effects at end of turn"
```

---

### Task 12: Final verification

Run all tests across all projects. Verify web build. Verify no regressions.

**Step 1: Run all test suites**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/
dotnet test tests/MtgDecker.Domain.Tests/
dotnet test tests/MtgDecker.Application.Tests/
dotnet test tests/MtgDecker.Infrastructure.Tests/
dotnet build src/MtgDecker.Web/
```

Expected: All pass, 0 errors on web build.

**Step 2: Verify commit log**

```bash
git log --oneline feature/continuous-effects --not main
```

Should show ~11 commits matching each task.
