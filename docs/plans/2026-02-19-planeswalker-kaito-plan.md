# Planeswalker System + Kaito, Bane of Nightmares — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a complete planeswalker engine subsystem and register Kaito, Bane of Nightmares with all mechanics: loyalty abilities, planeswalker combat, ninjutsu, conditional creature mode, emblems, surveil, stun counters, and life-loss tracking.

**Architecture:** Planeswalkers are a new CardType flag (64). Loyalty is tracked via CounterType.Loyalty in the existing counter system. Loyalty abilities go on the stack as ActivatedLoyaltyAbilityStackObject. Planeswalker combat extends ChooseAttackers to assign attacker targets. Kaito's conditional creature mode uses ContinuousEffects to make him a 3/4 Ninja creature with hexproof during your turn.

**Tech Stack:** C# 14, .NET 10, xUnit + FluentAssertions, MtgDecker.Engine

**Kaito Oracle Text (Duskmourn: House of Horror):**
- Cost: {2}{U}{B}, Legendary Planeswalker — Kaito, Loyalty: 4
- Ninjutsu {1}{U}{B}
- During your turn, as long as Kaito has 1+ loyalty counters, he's a 3/4 Ninja creature with hexproof.
- +1: You get an emblem with "Ninjas you control get +1/+1."
- 0: Surveil 2. Then draw a card for each opponent who lost life this turn.
- −2: Tap target creature. Put two stun counters on it.

---

## Task 1: CardType.Planeswalker + GameCard.IsPlaneswalker

**Files:**
- Modify: `src/MtgDecker.Engine/Enums/CardType.cs`
- Modify: `src/MtgDecker.Engine/GameCard.cs`
- Test: `tests/MtgDecker.Engine.Tests/PlaneswalkerCoreTests.cs`

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/PlaneswalkerCoreTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class PlaneswalkerCoreTests
{
    [Fact]
    public void CardType_Planeswalker_HasCorrectFlagValue()
    {
        ((int)CardType.Planeswalker).Should().Be(64);
    }

    [Fact]
    public void GameCard_IsPlaneswalker_TrueWhenPlaneswalkerType()
    {
        var card = new GameCard { CardTypes = CardType.Planeswalker };
        card.IsPlaneswalker.Should().BeTrue();
    }

    [Fact]
    public void GameCard_IsPlaneswalker_FalseForCreature()
    {
        var card = new GameCard { CardTypes = CardType.Creature };
        card.IsPlaneswalker.Should().BeFalse();
    }

    [Fact]
    public void GameCard_IsPlaneswalker_TrueWhenCombinedWithCreature()
    {
        var card = new GameCard { CardTypes = CardType.Creature | CardType.Planeswalker };
        card.IsPlaneswalker.Should().BeTrue();
        card.IsCreature.Should().BeTrue();
    }
}
```

**Step 2: Run to verify failure**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "PlaneswalkerCoreTests"`
Expected: Build failure — `CardType.Planeswalker` doesn't exist.

**Step 3: Implement**

In `src/MtgDecker.Engine/Enums/CardType.cs`, add:
```csharp
Planeswalker = 64,
```

In `src/MtgDecker.Engine/GameCard.cs`, add alongside `IsCreature`:
```csharp
public bool IsPlaneswalker => (EffectiveCardTypes ?? CardTypes).HasFlag(CardType.Planeswalker);
```

**Step 4: Run tests, verify pass**

**Step 5: Commit**
```bash
git add src/MtgDecker.Engine/Enums/CardType.cs src/MtgDecker.Engine/GameCard.cs tests/MtgDecker.Engine.Tests/PlaneswalkerCoreTests.cs
git commit -m "feat(engine): add CardType.Planeswalker and GameCard.IsPlaneswalker"
```

---

## Task 2: Loyalty counter system + CardDefinition.StartingLoyalty + ETB loyalty

**Files:**
- Modify: `src/MtgDecker.Engine/Enums/CounterType.cs`
- Modify: `src/MtgDecker.Engine/GameCard.cs`
- Modify: `src/MtgDecker.Engine/CardDefinition.cs`
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (ETB loyalty setup in spell resolution)
- Test: `tests/MtgDecker.Engine.Tests/PlaneswalkerCoreTests.cs` (add tests)

**Step 1: Write failing tests**

Add to `PlaneswalkerCoreTests.cs`:

```csharp
[Fact]
public void GameCard_Loyalty_ReadsFromLoyaltyCounters()
{
    var card = new GameCard { CardTypes = CardType.Planeswalker };
    card.AddCounters(CounterType.Loyalty, 4);
    card.Loyalty.Should().Be(4);
}

[Fact]
public void GameCard_Loyalty_ZeroWhenNoCounters()
{
    var card = new GameCard { CardTypes = CardType.Planeswalker };
    card.Loyalty.Should().Be(0);
}

[Fact]
public void CardDefinition_StartingLoyalty_CanBeSet()
{
    var def = new CardDefinition(null, null, null, null, CardType.Planeswalker)
    {
        StartingLoyalty = 4,
    };
    def.StartingLoyalty.Should().Be(4);
}
```

**Step 2: Run to verify failure**

**Step 3: Implement**

In `src/MtgDecker.Engine/Enums/CounterType.cs`, add:
```csharp
Loyalty,
```

In `src/MtgDecker.Engine/GameCard.cs`, add:
```csharp
public int Loyalty => GetCounters(CounterType.Loyalty);
```

In `src/MtgDecker.Engine/CardDefinition.cs`, add after `HasFlash`:
```csharp
public int? StartingLoyalty { get; init; }
```

**Step 4: ETB loyalty setup**

In `src/MtgDecker.Engine/GameEngine.cs`, find the `ApplyEntersWithCounters` method. After the existing counter application, add planeswalker loyalty setup:

```csharp
private void ApplyEntersWithCounters(GameCard card)
{
    if (CardDefinitions.TryGet(card.Name, out var def))
    {
        if (def.EntersWithCounters != null)
        {
            foreach (var (type, count) in def.EntersWithCounters)
            {
                card.AddCounters(type, count);
                _state.Log($"{card.Name} enters with {count} {type} counter(s).");
            }
        }

        // Planeswalker loyalty setup
        if (def.StartingLoyalty.HasValue && card.IsPlaneswalker)
        {
            card.AddCounters(CounterType.Loyalty, def.StartingLoyalty.Value);
            _state.Log($"{card.Name} enters with {def.StartingLoyalty.Value} loyalty.");
        }
    }
}
```

**Step 5: Write ETB integration test**

Add to `PlaneswalkerCoreTests.cs`:

```csharp
[Fact]
public void Planeswalker_ETB_GetsStartingLoyalty()
{
    // Register a test planeswalker
    CardDefinitions.Register(new CardDefinition(
        ManaCost.Parse("{2}{U}{B}"), null, null, null, CardType.Planeswalker)
    {
        Name = "Test Planeswalker",
        StartingLoyalty = 4,
    });

    try
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var pw = GameCard.Create("Test Planeswalker");
        p1.Battlefield.Add(pw);
        engine.ApplyEntersWithCounters(pw);

        pw.Loyalty.Should().Be(4);
    }
    finally
    {
        CardDefinitions.Unregister("Test Planeswalker");
    }
}
```

**Note:** `ApplyEntersWithCounters` is `private`. You may need to make it `internal` for testing (check if InternalsVisibleTo is set up), or test indirectly through spell resolution. Check how existing tests access internal methods — look at the `.csproj` for InternalsVisibleTo.

**Step 6: Run all tests, verify pass**

**Step 7: Commit**
```bash
git commit -m "feat(engine): add loyalty counter system and CardDefinition.StartingLoyalty"
```

---

## Task 3: Planeswalker SBA (loyalty ≤ 0 → graveyard)

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (CheckStateBasedActionsAsync)
- Test: `tests/MtgDecker.Engine.Tests/PlaneswalkerCoreTests.cs` (add tests)

**Step 1: Write failing test**

```csharp
[Fact]
public async Task SBA_Planeswalker_ZeroLoyalty_MovesToGraveyard()
{
    var h1 = new TestDecisionHandler();
    var h2 = new TestDecisionHandler();
    var p1 = new Player(Guid.NewGuid(), "P1", h1);
    var p2 = new Player(Guid.NewGuid(), "P2", h2);
    var state = new GameState(p1, p2);
    var engine = new GameEngine(state);

    var pw = new GameCard
    {
        Name = "Dying Planeswalker",
        CardTypes = CardType.Planeswalker,
    };
    // No loyalty counters = 0 loyalty
    p1.Battlefield.Add(pw);

    await engine.CheckStateBasedActionsAsync();

    p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Dying Planeswalker");
    p1.Graveyard.Cards.Should().Contain(c => c.Name == "Dying Planeswalker");
}

[Fact]
public async Task SBA_Planeswalker_PositiveLoyalty_StaysOnBattlefield()
{
    var h1 = new TestDecisionHandler();
    var h2 = new TestDecisionHandler();
    var p1 = new Player(Guid.NewGuid(), "P1", h1);
    var p2 = new Player(Guid.NewGuid(), "P2", h2);
    var state = new GameState(p1, p2);
    var engine = new GameEngine(state);

    var pw = new GameCard
    {
        Name = "Healthy Planeswalker",
        CardTypes = CardType.Planeswalker,
    };
    pw.AddCounters(CounterType.Loyalty, 3);
    p1.Battlefield.Add(pw);

    await engine.CheckStateBasedActionsAsync();

    p1.Battlefield.Cards.Should().Contain(c => c.Name == "Healthy Planeswalker");
}
```

**Step 2: Run to verify failure**

**Step 3: Implement**

In `src/MtgDecker.Engine/GameEngine.cs`, in `CheckStateBasedActionsAsync()`, add a new SBA check in the main loop. Find the section that checks for lethal damage on creatures and add nearby:

```csharp
// SBA: Planeswalker with 0 or less loyalty → graveyard (MTG 704.5i)
foreach (var player in new[] { _state.Player1, _state.Player2 })
{
    var dyingPws = player.Battlefield.Cards
        .Where(c => c.IsPlaneswalker && c.Loyalty <= 0)
        .ToList();

    foreach (var pw in dyingPws)
    {
        player.Battlefield.Remove(pw);
        player.Graveyard.Add(pw);
        _state.Log($"{pw.Name} is put into {player.Name}'s graveyard (0 loyalty).");
        changed = true;
    }
}
```

**Step 4: Run tests, verify pass**

**Step 5: Commit**
```bash
git commit -m "feat(engine): add planeswalker SBA — loyalty ≤ 0 goes to graveyard"
```

---

## Task 4: LoyaltyAbility record + CardDefinition.LoyaltyAbilities + ActionType

**Files:**
- Create: `src/MtgDecker.Engine/LoyaltyAbility.cs`
- Modify: `src/MtgDecker.Engine/CardDefinition.cs`
- Modify: `src/MtgDecker.Engine/Enums/ActionType.cs`
- Modify: `src/MtgDecker.Engine/GameAction.cs`
- Test: `tests/MtgDecker.Engine.Tests/PlaneswalkerCoreTests.cs` (add tests)

**Step 1: Write failing tests**

```csharp
[Fact]
public void LoyaltyAbility_Record_StoresCorrectValues()
{
    var effect = new DealDamageEffect(1);
    var ability = new LoyaltyAbility(-2, effect, "Deal 1 damage");
    ability.LoyaltyCost.Should().Be(-2);
    ability.Effect.Should().Be(effect);
    ability.Description.Should().Be("Deal 1 damage");
}

[Fact]
public void CardDefinition_LoyaltyAbilities_CanBeSet()
{
    var def = new CardDefinition(null, null, null, null, CardType.Planeswalker)
    {
        LoyaltyAbilities =
        [
            new LoyaltyAbility(1, new DealDamageEffect(1), "+1: Deal 1"),
            new LoyaltyAbility(-2, new DealDamageEffect(2), "-2: Deal 2"),
        ],
    };
    def.LoyaltyAbilities.Should().HaveCount(2);
}

[Fact]
public void GameAction_ActivateLoyaltyAbility_HasCorrectProperties()
{
    var action = GameAction.ActivateLoyaltyAbility(Guid.NewGuid(), Guid.NewGuid(), 1);
    action.Type.Should().Be(ActionType.ActivateLoyaltyAbility);
    action.AbilityIndex.Should().Be(1);
}
```

**Step 2: Run to verify failure**

**Step 3: Implement**

Create `src/MtgDecker.Engine/LoyaltyAbility.cs`:
```csharp
using MtgDecker.Engine.Triggers;

namespace MtgDecker.Engine;

/// <summary>
/// A planeswalker loyalty ability. LoyaltyCost is positive for +N, negative for -N, zero for 0.
/// </summary>
public record LoyaltyAbility(int LoyaltyCost, IEffect Effect, string Description);
```

In `src/MtgDecker.Engine/CardDefinition.cs`, add after `StartingLoyalty`:
```csharp
public IReadOnlyList<LoyaltyAbility>? LoyaltyAbilities { get; init; }
```

In `src/MtgDecker.Engine/Enums/ActionType.cs`, add:
```csharp
ActivateLoyaltyAbility,
```

In `src/MtgDecker.Engine/GameAction.cs`, add property:
```csharp
public int? AbilityIndex { get; init; }
```

Add factory method:
```csharp
public static GameAction ActivateLoyaltyAbility(Guid playerId, Guid cardId, int abilityIndex)
    => new() { Type = ActionType.ActivateLoyaltyAbility, PlayerId = playerId, CardId = cardId, AbilityIndex = abilityIndex };
```

**Step 4: Run tests, verify pass**

**Step 5: Commit**
```bash
git commit -m "feat(engine): add LoyaltyAbility record, ActionType, and GameAction factory"
```

---

## Task 5: Loyalty ability activation handler

This is the core of the planeswalker system: the engine handler that validates and executes loyalty ability activation.

**Files:**
- Create: `src/MtgDecker.Engine/ActivatedLoyaltyAbilityStackObject.cs`
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (ExecuteAction handler)
- Modify: `src/MtgDecker.Engine/Player.cs` (per-PW tracking)
- Test: `tests/MtgDecker.Engine.Tests/LoyaltyAbilityTests.cs`

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/LoyaltyAbilityTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class LoyaltyAbilityTests : IDisposable
{
    private const string TestPwName = "Test Loyalty PW";

    public LoyaltyAbilityTests()
    {
        CardDefinitions.Register(new CardDefinition(
            ManaCost.Parse("{2}{U}{B}"), null, null, null, CardType.Planeswalker)
        {
            Name = TestPwName,
            StartingLoyalty = 4,
            LoyaltyAbilities =
            [
                new LoyaltyAbility(1, new DealDamageEffect(1), "+1: Deal 1"),
                new LoyaltyAbility(0, new DealDamageEffect(2), "0: Deal 2"),
                new LoyaltyAbility(-2, new DealDamageEffect(3), "-2: Deal 3"),
            ],
        });
    }

    public void Dispose() => CardDefinitions.Unregister(TestPwName);

    private (GameEngine engine, GameState state, Player p1, Player p2,
        TestDecisionHandler h1, TestDecisionHandler h2) SetupWithPW()
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
        return (engine, state, p1, p2, h1, h2);
    }

    [Fact]
    public async Task ActivateLoyaltyAbility_Plus1_IncreasesLoyaltyAndPushesOnStack()
    {
        var (engine, state, p1, p2, h1, h2) = SetupWithPW();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var pw = GameCard.Create(TestPwName);
        pw.AddCounters(CounterType.Loyalty, 4);
        pw.TurnEnteredBattlefield = state.TurnNumber - 1;
        p1.Battlefield.Add(pw);

        await engine.ExecuteAction(GameAction.ActivateLoyaltyAbility(p1.Id, pw.Id, 0));

        pw.Loyalty.Should().Be(5); // 4 + 1 (cost paid immediately)
        state.StackCount.Should().Be(1);
    }

    [Fact]
    public async Task ActivateLoyaltyAbility_Minus2_DecreasesLoyalty()
    {
        var (engine, state, p1, p2, h1, h2) = SetupWithPW();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var pw = GameCard.Create(TestPwName);
        pw.AddCounters(CounterType.Loyalty, 4);
        pw.TurnEnteredBattlefield = state.TurnNumber - 1;
        p1.Battlefield.Add(pw);

        await engine.ExecuteAction(GameAction.ActivateLoyaltyAbility(p1.Id, pw.Id, 2));

        pw.Loyalty.Should().Be(2); // 4 - 2
        state.StackCount.Should().Be(1);
    }

    [Fact]
    public async Task ActivateLoyaltyAbility_RejectedIfNotSorcerySpeed()
    {
        var (engine, state, p1, p2, h1, h2) = SetupWithPW();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.Combat; // Not main phase

        var pw = GameCard.Create(TestPwName);
        pw.AddCounters(CounterType.Loyalty, 4);
        pw.TurnEnteredBattlefield = state.TurnNumber - 1;
        p1.Battlefield.Add(pw);

        await engine.ExecuteAction(GameAction.ActivateLoyaltyAbility(p1.Id, pw.Id, 0));

        state.StackCount.Should().Be(0); // Rejected
        pw.Loyalty.Should().Be(4); // Unchanged
    }

    [Fact]
    public async Task ActivateLoyaltyAbility_RejectedIfAlreadyUsedThisTurn()
    {
        var (engine, state, p1, p2, h1, h2) = SetupWithPW();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var pw = GameCard.Create(TestPwName);
        pw.AddCounters(CounterType.Loyalty, 4);
        pw.TurnEnteredBattlefield = state.TurnNumber - 1;
        p1.Battlefield.Add(pw);

        // First activation succeeds
        await engine.ExecuteAction(GameAction.ActivateLoyaltyAbility(p1.Id, pw.Id, 0));
        state.StackCount.Should().Be(1);

        // Second activation on same PW rejected
        await engine.ExecuteAction(GameAction.ActivateLoyaltyAbility(p1.Id, pw.Id, 1));
        state.StackCount.Should().Be(1); // Still 1
    }

    [Fact]
    public async Task ActivateLoyaltyAbility_RejectedIfLoyaltyWouldGoBelowZero()
    {
        var (engine, state, p1, p2, h1, h2) = SetupWithPW();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var pw = GameCard.Create(TestPwName);
        pw.AddCounters(CounterType.Loyalty, 1); // Only 1 loyalty
        pw.TurnEnteredBattlefield = state.TurnNumber - 1;
        p1.Battlefield.Add(pw);

        // -2 ability would put loyalty at -1, should be rejected
        await engine.ExecuteAction(GameAction.ActivateLoyaltyAbility(p1.Id, pw.Id, 2));

        state.StackCount.Should().Be(0);
        pw.Loyalty.Should().Be(1);
    }
}
```

**Step 2: Run to verify failure**

**Step 3: Implement**

Create `src/MtgDecker.Engine/ActivatedLoyaltyAbilityStackObject.cs`:
```csharp
using MtgDecker.Engine.Triggers;

namespace MtgDecker.Engine;

public class ActivatedLoyaltyAbilityStackObject : IStackObject
{
    public Guid Id { get; } = Guid.NewGuid();
    public GameCard Source { get; }
    public Guid ControllerId { get; }
    public IEffect Effect { get; }
    public string Description { get; }

    public ActivatedLoyaltyAbilityStackObject(GameCard source, Guid controllerId, IEffect effect, string description)
    {
        Source = source;
        ControllerId = controllerId;
        Effect = effect;
        Description = description;
    }
}
```

In `src/MtgDecker.Engine/Player.cs`, add after `DrawStepDrawExempted`:
```csharp
public HashSet<Guid> PlaneswalkerAbilitiesUsedThisTurn { get; } = [];
```

In `src/MtgDecker.Engine/GameEngine.cs`, add reset in `RunTurnAsync()` alongside existing resets:
```csharp
_state.Player1.PlaneswalkerAbilitiesUsedThisTurn.Clear();
_state.Player2.PlaneswalkerAbilitiesUsedThisTurn.Clear();
```

Add the handler in `ExecuteAction()`, in the switch statement alongside existing cases:
```csharp
case ActionType.ActivateLoyaltyAbility:
{
    var pwCard = FindCard(action.CardId!.Value);
    if (pwCard == null) break;

    var pwPlayer = _state.GetPlayer(action.PlayerId);

    // Must be at sorcery speed
    if (!CanCastSorcery(action.PlayerId))
    {
        _state.Log($"Cannot activate loyalty ability — not at sorcery speed.");
        break;
    }

    // Must not have already activated this planeswalker this turn
    if (pwPlayer.PlaneswalkerAbilitiesUsedThisTurn.Contains(pwCard.Id))
    {
        _state.Log($"Cannot activate {pwCard.Name} — already used a loyalty ability this turn.");
        break;
    }

    // Get definition
    if (!CardDefinitions.TryGet(pwCard.Name, out var pwDef) || pwDef.LoyaltyAbilities == null)
    {
        _state.Log($"{pwCard.Name} has no loyalty abilities.");
        break;
    }

    var abilityIndex = action.AbilityIndex ?? 0;
    if (abilityIndex < 0 || abilityIndex >= pwDef.LoyaltyAbilities.Count)
    {
        _state.Log($"Invalid ability index {abilityIndex} for {pwCard.Name}.");
        break;
    }

    var ability = pwDef.LoyaltyAbilities[abilityIndex];

    // Check loyalty cost is payable (for negative costs, loyalty must not go below 0)
    if (ability.LoyaltyCost < 0 && pwCard.Loyalty + ability.LoyaltyCost < 0)
    {
        _state.Log($"Cannot activate {pwCard.Name} ability — insufficient loyalty ({pwCard.Loyalty}).");
        break;
    }

    // Pay loyalty cost (add or remove counters)
    if (ability.LoyaltyCost > 0)
    {
        pwCard.AddCounters(CounterType.Loyalty, ability.LoyaltyCost);
    }
    else if (ability.LoyaltyCost < 0)
    {
        for (int i = 0; i < -ability.LoyaltyCost; i++)
            pwCard.RemoveCounter(CounterType.Loyalty);
    }

    // Mark as used this turn
    pwPlayer.PlaneswalkerAbilitiesUsedThisTurn.Add(pwCard.Id);

    // Push ability on stack
    _state.StackPush(new ActivatedLoyaltyAbilityStackObject(
        pwCard, action.PlayerId, ability.Effect, ability.Description));

    _state.Log($"{pwPlayer.Name} activates {pwCard.Name}: {ability.Description} (loyalty now {pwCard.Loyalty})");
    break;
}
```

**Note:** `FindCard` is an existing helper. Check its signature — it may search all zones. If it only searches specific zones, you may need to search the player's battlefield directly. Verify by reading the existing `FindCard` implementation.

**Step 4: Run tests, verify pass**

**Step 5: Run full engine suite**

**Step 6: Commit**
```bash
git commit -m "feat(engine): loyalty ability activation with sorcery-speed and per-turn limits"
```

---

## Task 6: Loyalty ability stack resolution

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (ResolveTopOfStack)
- Test: `tests/MtgDecker.Engine.Tests/LoyaltyAbilityTests.cs` (add resolution tests)

**Step 1: Write failing test**

Add to `LoyaltyAbilityTests.cs`:

```csharp
[Fact]
public async Task LoyaltyAbility_Resolves_ExecutesEffect()
{
    var (engine, state, p1, p2, h1, h2) = SetupWithPW();
    await engine.StartGameAsync();
    state.CurrentPhase = Phase.MainPhase1;

    var pw = GameCard.Create(TestPwName);
    pw.AddCounters(CounterType.Loyalty, 4);
    pw.TurnEnteredBattlefield = state.TurnNumber - 1;
    p1.Battlefield.Add(pw);

    // Activate +1 ability (DealDamageEffect(1) — targets opponent by default)
    await engine.ExecuteAction(GameAction.ActivateLoyaltyAbility(p1.Id, pw.Id, 0));

    // Resolve the stack
    h1.EnqueueAction(GameAction.Pass(p1.Id));
    h2.EnqueueAction(GameAction.Pass(p2.Id));
    await engine.ResolveAllTriggersAsync();

    state.StackCount.Should().Be(0);
}
```

**Step 2: Run to verify failure** (ActivatedLoyaltyAbilityStackObject not handled in resolution)

**Step 3: Implement**

In the stack resolution logic in `GameEngine.cs`, find where `TriggeredAbilityStackObject` is handled. Add a case for `ActivatedLoyaltyAbilityStackObject`:

```csharp
if (topItem is ActivatedLoyaltyAbilityStackObject loyaltyAbility)
{
    var controller = _state.GetPlayer(loyaltyAbility.ControllerId);
    var context = new EffectContext(_state, controller, loyaltyAbility.Source, controller.DecisionHandler);
    await loyaltyAbility.Effect.Execute(context, ct);
    _state.Log($"Resolved {loyaltyAbility.Source.Name} loyalty ability: {loyaltyAbility.Description}");
}
```

**Note:** The exact location depends on how `ResolveTopOfStack` or the resolution loop is structured. Read the existing code carefully — find where `TriggeredAbilityStackObject` is resolved and add the `ActivatedLoyaltyAbilityStackObject` handling nearby, following the same pattern.

**Step 4: Run tests, verify pass**

**Step 5: Commit**
```bash
git commit -m "feat(engine): resolve ActivatedLoyaltyAbilityStackObject on stack"
```

---

## Task 7: Planeswalker combat (attacker targeting + damage)

This is the most complex task — extending combat to support attacking planeswalkers.

**Files:**
- Modify: `src/MtgDecker.Engine/CombatState.cs`
- Modify: `src/MtgDecker.Engine/IPlayerDecisionHandler.cs`
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (combat flow)
- Modify: `tests/MtgDecker.Engine.Tests/Helpers/TestDecisionHandler.cs`
- Test: `tests/MtgDecker.Engine.Tests/PlaneswalkerCombatTests.cs`

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/PlaneswalkerCombatTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class PlaneswalkerCombatTests
{
    [Fact]
    public void CombatState_AttackerTargets_TracksPerAttacker()
    {
        var combat = new CombatState(Guid.NewGuid(), Guid.NewGuid());
        var attackerId = Guid.NewGuid();
        var pwId = Guid.NewGuid();

        combat.DeclareAttacker(attackerId);
        combat.SetAttackerTarget(attackerId, pwId);

        combat.GetAttackerTarget(attackerId).Should().Be(pwId);
    }

    [Fact]
    public void CombatState_AttackerTarget_DefaultsToNull()
    {
        var combat = new CombatState(Guid.NewGuid(), Guid.NewGuid());
        var attackerId = Guid.NewGuid();

        combat.DeclareAttacker(attackerId);

        combat.GetAttackerTarget(attackerId).Should().BeNull();
    }

    [Fact]
    public async Task Combat_UnblockedAttacker_DealsDamageToPlaneswalker()
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

        // P1 has an attacker
        var attacker = new GameCard
        {
            Name = "Bear",
            BasePower = 2,
            BaseToughness = 2,
            CardTypes = CardType.Creature,
            TurnEnteredBattlefield = state.TurnNumber - 1,
        };
        p1.Battlefield.Add(attacker);

        // P2 has a planeswalker with 4 loyalty
        var pw = new GameCard
        {
            Name = "Target PW",
            CardTypes = CardType.Planeswalker,
        };
        pw.AddCounters(CounterType.Loyalty, 4);
        p2.Battlefield.Add(pw);

        // P1 attacks with bear, targeting the planeswalker
        h1.EnqueueAttackers([attacker.Id]);
        h1.EnqueueAttackerTarget(attacker.Id, pw.Id); // New method

        // P2 doesn't block
        h2.EnqueueBlockers(new Dictionary<Guid, Guid>());

        state.CurrentPhase = Phase.Combat;
        await engine.RunCombatAsync(CancellationToken.None);

        // Planeswalker should have lost 2 loyalty (bear's power)
        pw.Loyalty.Should().Be(2);
        // Player should NOT have taken damage
        p2.Life.Should().Be(20);
    }
}
```

**Step 2: Run to verify failure**

**Step 3: Implement**

In `src/MtgDecker.Engine/CombatState.cs`, add attacker target tracking:
```csharp
private readonly Dictionary<Guid, Guid?> _attackerTargets = new();

public void SetAttackerTarget(Guid attackerId, Guid? planeswalkerTargetId)
{
    _attackerTargets[attackerId] = planeswalkerTargetId;
}

public Guid? GetAttackerTarget(Guid attackerId)
{
    return _attackerTargets.TryGetValue(attackerId, out var target) ? target : null;
}
```

In `IPlayerDecisionHandler.cs`, add:
```csharp
Task<Dictionary<Guid, Guid?>> ChooseAttackerTargets(
    IReadOnlyList<GameCard> attackers,
    IReadOnlyList<GameCard> planeswalkers,
    CancellationToken ct = default);
```

In `TestDecisionHandler.cs`, add:
```csharp
private readonly Queue<Dictionary<Guid, Guid?>> _attackerTargetQueue = new();

public void EnqueueAttackerTarget(Guid attackerId, Guid? targetId)
{
    _attackerTargetQueue.Enqueue(new Dictionary<Guid, Guid?> { { attackerId, targetId } });
}

public void EnqueueAttackerTargets(Dictionary<Guid, Guid?> targets)
{
    _attackerTargetQueue.Enqueue(targets);
}

public Task<Dictionary<Guid, Guid?>> ChooseAttackerTargets(
    IReadOnlyList<GameCard> attackers, IReadOnlyList<GameCard> planeswalkers,
    CancellationToken ct = default)
{
    if (_attackerTargetQueue.Count > 0) return Task.FromResult(_attackerTargetQueue.Dequeue());
    // Default: all attack player
    return Task.FromResult(attackers.ToDictionary(a => a.Id, _ => (Guid?)null));
}
```

Also update `InteractiveDecisionHandler` and `AiBotDecisionHandler` with the new method. For AiBot, default to attacking the player. For Interactive, delegate to the UI (return empty dict for now — the UI will need updating in a separate PR).

In `GameEngine.cs`, in `RunCombatAsync()`, after declaring attackers and before declaring blockers, add planeswalker target assignment:

```csharp
// After declaring attackers, check if defending player has planeswalkers
var defenderPWs = defendingPlayer.Battlefield.Cards
    .Where(c => c.IsPlaneswalker)
    .ToList();

if (defenderPWs.Count > 0 && combat.Attackers.Count > 0)
{
    var attackerCards = combat.Attackers
        .Select(id => FindCard(id))
        .Where(c => c != null)
        .ToList();

    var targets = await attackingPlayer.DecisionHandler.ChooseAttackerTargets(
        attackerCards!, defenderPWs, ct);

    foreach (var (attackerId, targetId) in targets)
    {
        combat.SetAttackerTarget(attackerId, targetId);
    }
}
```

In the combat damage step, modify unblocked attacker damage to check for planeswalker targets:

```csharp
// For unblocked attackers:
var pwTargetId = combat.GetAttackerTarget(attackerId);
if (pwTargetId.HasValue)
{
    var targetPw = FindCard(pwTargetId.Value);
    if (targetPw != null && targetPw.IsPlaneswalker)
    {
        // Remove loyalty counters instead of dealing player damage
        var loyaltyToRemove = Math.Min(attackerCard.Power ?? 0, targetPw.Loyalty);
        for (int i = 0; i < loyaltyToRemove; i++)
            targetPw.RemoveCounter(CounterType.Loyalty);
        _state.Log($"{attackerCard.Name} deals {attackerCard.Power} damage to {targetPw.Name} (loyalty now {targetPw.Loyalty}).");
    }
}
else
{
    // Original: damage to player
    defendingPlayer.AdjustLife(-(attackerCard.Power ?? 0));
    // ... existing damage logic
}
```

**Note:** The exact combat damage logic in the engine needs careful reading. Look at how unblocked attackers currently deal damage and modify that specific code path. Don't break existing creature combat tests.

**Step 4: Run all tests, verify pass**

**Step 5: Commit**
```bash
git commit -m "feat(engine): planeswalker combat targeting — attackers can target planeswalkers"
```

---

## Task 8: Keyword.Hexproof

**Files:**
- Modify: `src/MtgDecker.Engine/Enums/Keyword.cs`
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (targeting check)
- Test: `tests/MtgDecker.Engine.Tests/HexproofTests.cs`

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/HexproofTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class HexproofTests
{
    [Fact]
    public void Keyword_Hexproof_Exists()
    {
        Keyword.Hexproof.Should().BeDefined();
    }

    [Fact]
    public void HexproofCreature_IsNotTargetableByOpponent()
    {
        // This test validates that targeting logic respects hexproof.
        // The engine's targeting filter should exclude hexproof creatures
        // when the targeting player is not the creature's controller.
        var creature = new GameCard
        {
            Name = "Hexproof Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        creature.ActiveKeywords.Add(Keyword.Hexproof);

        // Hexproof should not appear as Shroud
        creature.ActiveKeywords.Should().NotContain(Keyword.Shroud);
        creature.ActiveKeywords.Should().Contain(Keyword.Hexproof);
    }
}
```

**Step 2: Run to verify failure**

**Step 3: Implement**

In `src/MtgDecker.Engine/Enums/Keyword.cs`, add:
```csharp
Hexproof,
```

In `src/MtgDecker.Engine/GameEngine.cs`, find the targeting validation logic (where Shroud is checked). Hexproof should prevent targeting by opponents only. Look for where eligible targets are filtered (e.g., in CastSpell, BowmastersEffect, or general targeting). Add hexproof check:

```csharp
// When filtering eligible targets for a spell/ability controlled by controllerId:
// Shroud: can't be targeted by anything
// Hexproof: can't be targeted by opponents
.Where(c => !c.ActiveKeywords.Contains(Keyword.Shroud)
    && !(c.ActiveKeywords.Contains(Keyword.Hexproof) && IsOpponentControlled(c, controllerId)))
```

**Note:** The exact targeting logic depends on how the engine currently filters targets. Study the existing Shroud filtering and add Hexproof alongside it. The key difference: Shroud blocks ALL targeting, Hexproof only blocks opponent targeting. You'll need to know who controls the target vs who's doing the targeting.

**Step 4: Run tests, verify pass**

**Step 5: Commit**
```bash
git commit -m "feat(engine): add Keyword.Hexproof with opponent-only targeting restriction"
```

---

## Task 9: CounterType.Stun + untap step modification

**Files:**
- Modify: `src/MtgDecker.Engine/Enums/CounterType.cs`
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (untap step)
- Test: `tests/MtgDecker.Engine.Tests/StunCounterTests.cs`

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/StunCounterTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class StunCounterTests
{
    [Fact]
    public void StunCounter_ExistsInEnum()
    {
        CounterType.Stun.Should().BeDefined();
    }

    [Fact]
    public void Creature_WithStunCounter_DoesNotUntap()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var creature = new GameCard
        {
            Name = "Stunned Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
            IsTapped = true,
        };
        creature.AddCounters(CounterType.Stun, 2);
        p1.Battlefield.Add(creature);

        // Simulate untap step
        engine.UntapStep(p1);

        // Should still be tapped, but lost one stun counter
        creature.IsTapped.Should().BeTrue();
        creature.GetCounters(CounterType.Stun).Should().Be(1);
    }

    [Fact]
    public void Creature_WithOneStunCounter_UntapsNextTurn()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var creature = new GameCard
        {
            Name = "Almost Free Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
            IsTapped = true,
        };
        creature.AddCounters(CounterType.Stun, 1);
        p1.Battlefield.Add(creature);

        // First untap: removes stun counter, stays tapped
        engine.UntapStep(p1);
        creature.IsTapped.Should().BeTrue();
        creature.GetCounters(CounterType.Stun).Should().Be(0);

        // Second untap: no stun counters, untaps normally
        engine.UntapStep(p1);
        creature.IsTapped.Should().BeFalse();
    }
}
```

**Note:** `UntapStep` may be `private` — check if you need to make it `internal` or test via turn progression.

**Step 2: Run to verify failure**

**Step 3: Implement**

In `src/MtgDecker.Engine/Enums/CounterType.cs`, add:
```csharp
Stun,
```

In `src/MtgDecker.Engine/GameEngine.cs`, find the untap logic in `RunTurnAsync` (Phase.Untap case). Modify it:

```csharp
case Phase.Untap:
    foreach (var card in _state.ActivePlayer.Battlefield.Cards)
    {
        if (card.GetCounters(CounterType.Stun) > 0)
        {
            // Instead of untapping, remove one stun counter (MTG 701.21e)
            card.RemoveCounter(CounterType.Stun);
            _state.Log($"Removed a stun counter from {card.Name} (instead of untapping).");
        }
        else if (card.IsTapped)
        {
            card.IsTapped = false;
        }
    }
    break;
```

**Step 4: Run tests, verify pass**

**Step 5: Commit**
```bash
git commit -m "feat(engine): add stun counters — prevent untap, remove one per untap step"
```

---

## Task 10: SurveilEffect

**Files:**
- Create: `src/MtgDecker.Engine/Triggers/Effects/SurveilEffect.cs`
- Test: `tests/MtgDecker.Engine.Tests/SurveilEffectTests.cs`

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/SurveilEffectTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class SurveilEffectTests
{
    private static (EffectContext context, Player player, GameState state, TestDecisionHandler handler)
        CreateContext()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var source = new GameCard { Name = "Surveyor" };
        var context = new EffectContext(state, p1, source, h1);
        return (context, p1, state, h1);
    }

    [Fact]
    public async Task Surveil1_ChooseGraveyard_CardGoesToGraveyard()
    {
        var (context, player, state, handler) = CreateContext();

        var topCard = new GameCard { Name = "Top Card" };
        player.Library.AddToTop(topCard);

        // Choose to put top card to graveyard
        handler.EnqueueCardChoice(topCard.Id);

        var effect = new SurveilEffect(1);
        await effect.Execute(context);

        player.Graveyard.Cards.Should().Contain(c => c.Name == "Top Card");
        player.Library.Cards.Should().NotContain(c => c.Name == "Top Card");
    }

    [Fact]
    public async Task Surveil1_ChooseKeep_CardStaysOnTop()
    {
        var (context, player, state, handler) = CreateContext();

        var topCard = new GameCard { Name = "Top Card" };
        player.Library.AddToTop(topCard);

        // Decline to put to graveyard (keep on top)
        handler.EnqueueCardChoice(null);

        var effect = new SurveilEffect(1);
        await effect.Execute(context);

        player.Library.Cards.Should().Contain(c => c.Name == "Top Card");
        player.Graveyard.Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task Surveil2_ChooseOneToGraveyard_OneStaysOnTop()
    {
        var (context, player, state, handler) = CreateContext();

        var card1 = new GameCard { Name = "Card A" };
        var card2 = new GameCard { Name = "Card B" };
        player.Library.AddToTop(card2); // card2 will be 2nd from top
        player.Library.AddToTop(card1); // card1 will be on top

        // Choose card1 to graveyard, keep card2
        handler.EnqueueCardChoice(card1.Id);
        handler.EnqueueCardChoice(null); // keep card2

        var effect = new SurveilEffect(2);
        await effect.Execute(context);

        player.Graveyard.Cards.Should().Contain(c => c.Name == "Card A");
        player.Library.Cards.Should().Contain(c => c.Name == "Card B");
    }
}
```

**Step 2: Run to verify failure**

**Step 3: Implement**

Create `src/MtgDecker.Engine/Triggers/Effects/SurveilEffect.cs`:

```csharp
namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Surveil N: look at top N cards, for each choose to put into graveyard or back on top.
/// </summary>
public class SurveilEffect(int amount) : IEffect
{
    public int Amount { get; } = amount;

    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var topCards = context.Controller.Library.PeekTop(Amount);
        if (topCards.Count == 0) return;

        context.State.Log($"{context.Controller.Name} surveils {topCards.Count}.");

        var toGraveyard = new List<GameCard>();
        var toKeep = new List<GameCard>();

        foreach (var card in topCards)
        {
            var choice = await context.DecisionHandler.ChooseCard(
                [card],
                $"Surveil: Put {card.Name} into graveyard?",
                optional: true, ct);

            if (choice.HasValue)
                toGraveyard.Add(card);
            else
                toKeep.Add(card);
        }

        // Remove from library and place
        foreach (var card in toGraveyard)
        {
            context.Controller.Library.Remove(card);
            context.Controller.Graveyard.Add(card);
            context.State.Log($"{context.Controller.Name} puts {card.Name} into graveyard (surveil).");
        }

        // Cards kept stay on top in their original order (already there via PeekTop)
        // No action needed — PeekTop doesn't remove cards
    }
}
```

**Step 4: Run tests, verify pass**

**Step 5: Commit**
```bash
git commit -m "feat(engine): implement SurveilEffect (surveil N — graveyard or keep)"
```

---

## Task 11: Player.LifeLostThisTurn tracking

**Files:**
- Modify: `src/MtgDecker.Engine/Player.cs`
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (turn reset)
- Test: `tests/MtgDecker.Engine.Tests/LifeLostTrackingTests.cs`

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/LifeLostTrackingTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class LifeLostTrackingTests
{
    [Fact]
    public void Player_LifeLostThisTurn_StartsAtZero()
    {
        var handler = new TestDecisionHandler();
        var player = new Player(Guid.NewGuid(), "Test", handler);
        player.LifeLostThisTurn.Should().Be(0);
    }

    [Fact]
    public void Player_AdjustLife_NegativeDelta_TracksLifeLost()
    {
        var handler = new TestDecisionHandler();
        var player = new Player(Guid.NewGuid(), "Test", handler);

        player.AdjustLife(-3);

        player.LifeLostThisTurn.Should().Be(3);
        player.Life.Should().Be(17);
    }

    [Fact]
    public void Player_AdjustLife_PositiveDelta_DoesNotTrackLifeLost()
    {
        var handler = new TestDecisionHandler();
        var player = new Player(Guid.NewGuid(), "Test", handler);

        player.AdjustLife(-5);
        player.AdjustLife(3); // Gain life

        player.LifeLostThisTurn.Should().Be(5); // Only loss tracked
        player.Life.Should().Be(18);
    }

    [Fact]
    public void Player_LifeLostThisTurn_AccumulatesAcrossMultipleLosses()
    {
        var handler = new TestDecisionHandler();
        var player = new Player(Guid.NewGuid(), "Test", handler);

        player.AdjustLife(-2);
        player.AdjustLife(-3);

        player.LifeLostThisTurn.Should().Be(5);
    }
}
```

**Step 2: Run to verify failure**

**Step 3: Implement**

In `src/MtgDecker.Engine/Player.cs`, add property:
```csharp
public int LifeLostThisTurn { get; set; }
```

Modify `AdjustLife`:
```csharp
public void AdjustLife(int delta)
{
    Life += delta;
    if (delta < 0)
        LifeLostThisTurn += -delta;
}
```

In `GameEngine.cs`, in `RunTurnAsync()`, add reset alongside existing resets:
```csharp
_state.Player1.LifeLostThisTurn = 0;
_state.Player2.LifeLostThisTurn = 0;
```

**Step 4: Run tests, verify pass**

**Step 5: Commit**
```bash
git commit -m "feat(engine): track LifeLostThisTurn on Player for Kaito's draw ability"
```

---

## Task 12: Emblem system

**Files:**
- Create: `src/MtgDecker.Engine/Emblem.cs`
- Modify: `src/MtgDecker.Engine/Player.cs`
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (RecalculateState — apply emblem effects)
- Test: `tests/MtgDecker.Engine.Tests/EmblemTests.cs`

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/EmblemTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class EmblemTests
{
    [Fact]
    public void Emblem_CanBeCreated()
    {
        var emblem = new Emblem("Ninjas you control get +1/+1.",
            new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyPowerToughness,
                (card, _) => card.Subtypes.Contains("Ninja"),
                PowerMod: 1, ToughnessMod: 1));

        emblem.Description.Should().Be("Ninjas you control get +1/+1.");
        emblem.Effect.Should().NotBeNull();
    }

    [Fact]
    public void Player_Emblems_StartsEmpty()
    {
        var handler = new TestDecisionHandler();
        var player = new Player(Guid.NewGuid(), "Test", handler);
        player.Emblems.Should().BeEmpty();
    }

    [Fact]
    public void Emblem_Effect_ModifiesCreatures()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P1 has a Ninja creature
        var ninja = new GameCard
        {
            Name = "Test Ninja",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
            Subtypes = ["Ninja"],
        };
        p1.Battlefield.Add(ninja);

        // P1 gets an emblem granting Ninjas +1/+1
        p1.Emblems.Add(new Emblem("Ninjas you control get +1/+1.",
            new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyPowerToughness,
                (card, _) => card.Subtypes.Contains("Ninja"),
                PowerMod: 1, ToughnessMod: 1,
                ControllerOnly: true)));

        engine.RecalculateState();

        ninja.Power.Should().Be(3);
        ninja.Toughness.Should().Be(3);
    }
}
```

**Step 2: Run to verify failure**

**Step 3: Implement**

Create `src/MtgDecker.Engine/Emblem.cs`:
```csharp
namespace MtgDecker.Engine;

/// <summary>
/// A permanent emblem created by a planeswalker ability.
/// Emblems exist in the command zone and cannot be removed.
/// </summary>
public record Emblem(string Description, ContinuousEffect Effect);
```

In `src/MtgDecker.Engine/Player.cs`, add:
```csharp
public List<Emblem> Emblems { get; } = [];
```

In `src/MtgDecker.Engine/GameEngine.cs`, in `RecalculateState()`, find where `ActiveEffects` are gathered. Add emblem effects to the active effects pool. Find the section that collects effects from battlefield cards and add:

```csharp
// Collect emblem effects
foreach (var player in new[] { _state.Player1, _state.Player2 })
{
    foreach (var emblem in player.Emblems)
    {
        var effect = emblem.Effect with { SourceId = Guid.Empty };
        allEffects.Add(effect);
    }
}
```

**Note:** The exact integration depends on how RecalculateState collects effects. Read the existing code carefully. Emblem effects should be treated like regular ContinuousEffects but they apply to their controller's creatures only (when `ControllerOnly = true`). The `ControllerOnly` check needs to know which player owns the emblem. You may need to adjust the `Applies` lambda or add player context.

**Step 4: Run tests, verify pass**

**Step 5: Commit**
```bash
git commit -m "feat(engine): add emblem system — persistent planeswalker effects"
```

---

## Task 13: Kaito's loyalty ability effects

**Files:**
- Create: `src/MtgDecker.Engine/Triggers/Effects/TapAndStunEffect.cs`
- Create: `src/MtgDecker.Engine/Triggers/Effects/SurveilAndDrawEffect.cs`
- Create: `src/MtgDecker.Engine/Triggers/Effects/CreateNinjaEmblemEffect.cs`
- Test: `tests/MtgDecker.Engine.Tests/KaitoAbilityEffectsTests.cs`

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/KaitoAbilityEffectsTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class KaitoAbilityEffectsTests
{
    private static (EffectContext context, Player controller, Player opponent, GameState state,
        TestDecisionHandler handler) CreateContext()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var source = new GameCard { Name = "Kaito" };
        var context = new EffectContext(state, p1, source, h1);
        return (context, p1, p2, state, h1);
    }

    // --- TapAndStunEffect (-2) ---

    [Fact]
    public async Task TapAndStunEffect_TapsTargetAndAddsStunCounters()
    {
        var (context, controller, opponent, state, handler) = CreateContext();

        var creature = new GameCard
        {
            Name = "Target Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        opponent.Battlefield.Add(creature);

        handler.EnqueueCardChoice(creature.Id);

        var effect = new TapAndStunEffect(2);
        await effect.Execute(context);

        creature.IsTapped.Should().BeTrue();
        creature.GetCounters(CounterType.Stun).Should().Be(2);
    }

    // --- SurveilAndDrawEffect (0) ---

    [Fact]
    public async Task SurveilAndDrawEffect_Surveils2ThenDrawsForLifeLost()
    {
        var (context, controller, opponent, state, handler) = CreateContext();

        // Put cards on top of library
        var topCard1 = new GameCard { Name = "Top1" };
        var topCard2 = new GameCard { Name = "Top2" };
        var drawCard = new GameCard { Name = "DrawMe" };
        controller.Library.AddToTop(drawCard);
        controller.Library.AddToTop(topCard2);
        controller.Library.AddToTop(topCard1);

        // Opponent lost 2 life this turn
        opponent.LifeLostThisTurn = 2;

        // Surveil choices: put both to graveyard
        handler.EnqueueCardChoice(topCard1.Id);
        handler.EnqueueCardChoice(topCard2.Id);

        var effect = new SurveilAndDrawEffect();
        await effect.Execute(context);

        // Both cards surveiled to graveyard
        controller.Graveyard.Cards.Should().Contain(c => c.Name == "Top1");
        controller.Graveyard.Cards.Should().Contain(c => c.Name == "Top2");

        // Drew 1 card (opponent lost life this turn = true in 2-player, so draw 1)
        controller.Hand.Cards.Should().Contain(c => c.Name == "DrawMe");
    }

    [Fact]
    public async Task SurveilAndDrawEffect_NoLifeLost_NoDraw()
    {
        var (context, controller, opponent, state, handler) = CreateContext();

        var topCard1 = new GameCard { Name = "Top1" };
        controller.Library.AddToTop(topCard1);

        // Opponent did NOT lose life
        opponent.LifeLostThisTurn = 0;

        // Surveil: keep on top
        handler.EnqueueCardChoice(null);

        var effect = new SurveilAndDrawEffect();
        await effect.Execute(context);

        controller.Hand.Cards.Should().BeEmpty();
    }

    // --- CreateNinjaEmblemEffect (+1) ---

    [Fact]
    public async Task CreateNinjaEmblemEffect_AddsEmblemToPlayer()
    {
        var (context, controller, opponent, state, handler) = CreateContext();

        var effect = new CreateNinjaEmblemEffect();
        await effect.Execute(context);

        controller.Emblems.Should().HaveCount(1);
        controller.Emblems[0].Description.Should().Contain("Ninja");
    }
}
```

**Step 2: Run to verify failure**

**Step 3: Implement**

Create `src/MtgDecker.Engine/Triggers/Effects/TapAndStunEffect.cs`:
```csharp
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Tap target creature and put N stun counters on it. (Kaito -2)
/// </summary>
public class TapAndStunEffect(int stunCounters) : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var eligibleCreatures = context.State.Player1.Battlefield.Cards
            .Concat(context.State.Player2.Battlefield.Cards)
            .Where(c => c.IsCreature && !c.ActiveKeywords.Contains(Keyword.Shroud)
                && !c.ActiveKeywords.Contains(Keyword.Hexproof))
            .ToList();

        if (eligibleCreatures.Count == 0) return;

        var chosenId = await context.DecisionHandler.ChooseCard(
            eligibleCreatures, "Choose target creature to tap and stun", optional: false, ct);

        if (!chosenId.HasValue) return;

        var target = eligibleCreatures.FirstOrDefault(c => c.Id == chosenId.Value);
        if (target == null) return;

        target.IsTapped = true;
        target.AddCounters(CounterType.Stun, stunCounters);
        context.State.Log($"{context.Source.Name} taps {target.Name} and puts {stunCounters} stun counter(s) on it.");
    }
}
```

**Note:** The hexproof check here should actually check if the target's controller is the opponent, not just the keyword presence. For Kaito's -2, it targets any creature — but hexproof on your own creature wouldn't prevent YOU from targeting it. Adjust the filter accordingly. Check who controls each creature relative to the effect's controller.

Create `src/MtgDecker.Engine/Triggers/Effects/SurveilAndDrawEffect.cs`:
```csharp
namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Surveil 2. Then draw a card for each opponent who lost life this turn. (Kaito 0)
/// In 2-player: draw 1 if opponent lost life this turn.
/// </summary>
public class SurveilAndDrawEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        // Surveil 2
        var surveil = new SurveilEffect(2);
        await surveil.Execute(context, ct);

        // Draw for each opponent who lost life this turn
        var opponent = context.State.GetOpponent(context.Controller);
        if (opponent.LifeLostThisTurn > 0)
        {
            var engine = GetEngine(context.State);
            engine?.DrawCards(context.Controller, 1);
            context.State.Log($"{context.Controller.Name} draws 1 card (opponent lost life this turn).");
        }
    }

    private static GameEngine? GetEngine(GameState state)
    {
        // Need access to engine for DrawCards — check if there's a pattern for this
        // Alternative: draw directly by moving card from library to hand
        return null; // TODO: implement based on existing patterns
    }
}
```

**IMPORTANT NOTE:** The `SurveilAndDrawEffect` needs to call `DrawCards` on the engine to properly trigger draw-related effects (like Orcish Bowmasters' draw trigger). Check how other effects access the engine or draw cards. Options:
1. Add `GameEngine` to `EffectContext` (if not already there)
2. Draw directly: `controller.Library.DrawFromTop()` → `controller.Hand.Add()`
3. Find another pattern used by existing effects

Study how effects that draw cards currently work in the codebase (e.g., search for "DrawFromTop" or "Hand.Add" in existing effects).

Create `src/MtgDecker.Engine/Triggers/Effects/CreateNinjaEmblemEffect.cs`:
```csharp
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// You get an emblem with "Ninjas you control get +1/+1." (Kaito +1)
/// </summary>
public class CreateNinjaEmblemEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var emblem = new Emblem(
            "Ninjas you control get +1/+1.",
            new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyPowerToughness,
                (card, _) => card.Subtypes.Contains("Ninja"),
                PowerMod: 1, ToughnessMod: 1,
                ControllerOnly: true));

        context.Controller.Emblems.Add(emblem);
        context.State.Log($"{context.Controller.Name} gets an emblem: Ninjas you control get +1/+1.");
        return Task.CompletedTask;
    }
}
```

**Step 4: Run tests, verify pass**

**Step 5: Commit**
```bash
git commit -m "feat(engine): implement Kaito's loyalty ability effects (tap+stun, surveil+draw, ninja emblem)"
```

---

## Task 14: Kaito's conditional creature mode

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinition.cs` (or CardDefinitions.cs for Kaito-specific effect)
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (RecalculateState for creature-mode)
- Test: `tests/MtgDecker.Engine.Tests/KaitoCreatureModeTests.cs`

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/KaitoCreatureModeTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class KaitoCreatureModeTests
{
    [Fact]
    public void Kaito_DuringYourTurn_WithLoyalty_IsCreature()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        state.SetActivePlayer(p1); // P1's turn

        var kaito = new GameCard
        {
            Name = "Kaito, Bane of Nightmares",
            CardTypes = CardType.Planeswalker,
            IsLegendary = true,
            Subtypes = ["Kaito"],
        };
        kaito.AddCounters(CounterType.Loyalty, 4);
        p1.Battlefield.Add(kaito);

        // Kaito has a ContinuousEffect that makes him a 3/4 creature during your turn
        // This should be registered via CardDefinitions — for this test, add it directly
        kaito.ContinuousEffects =
        [
            new ContinuousEffect(kaito.Id, ContinuousEffectType.BecomeCreature,
                (card, _) => card.Id == kaito.Id,
                SetPower: 3, SetToughness: 4,
                Layer: EffectLayer.Layer4_TypeChanging),
        ];

        // NOTE: This test needs the ContinuousEffect to be conditional on:
        // 1. It being the controller's turn
        // 2. Kaito having loyalty > 0
        // The Applies lambda needs access to GameState, which it currently doesn't have
        // in its (GameCard, Player) signature. This will need design work.

        engine.RecalculateState();

        // During P1's turn, Kaito should be a creature
        kaito.IsCreature.Should().BeTrue();
        kaito.IsPlaneswalker.Should().BeTrue();
        kaito.Power.Should().Be(3);
        kaito.Toughness.Should().Be(4);
    }

    [Fact]
    public void Kaito_DuringOpponentsTurn_IsNotCreature()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        state.SetActivePlayer(p2); // P2's turn — not Kaito's controller

        var kaito = new GameCard
        {
            Name = "Kaito, Bane of Nightmares",
            CardTypes = CardType.Planeswalker,
            IsLegendary = true,
        };
        kaito.AddCounters(CounterType.Loyalty, 4);
        p1.Battlefield.Add(kaito);

        engine.RecalculateState();

        kaito.IsPlaneswalker.Should().BeTrue();
        kaito.IsCreature.Should().BeFalse();
        kaito.Power.Should().BeNull();
    }
}
```

**IMPORTANT DESIGN NOTE:** The current `ContinuousEffect.Applies` signature is `Func<GameCard, Player, bool>`. To check "during your turn", we need access to `GameState` to check `ActivePlayer`. Options:
1. Change `Applies` to `Func<GameCard, Player, GameState, bool>` — breaking change
2. Add a separate `ConditionalApplies` with GameState access — complex
3. Check the condition in RecalculateState before applying the effect — cleanest

**Recommended approach:** In RecalculateState, when applying `BecomeCreature` effects for planeswalkers, check if it's the controller's turn and the card has loyalty. This is Kaito-specific logic but can be generalized.

Actually, the cleanest approach: add an optional `Func<GameState, bool>? StateCondition` to `ContinuousEffect`. If set, the effect only applies when the condition is true. This is a general mechanism that can be reused.

**Step 2: Run to verify failure**

**Step 3: Implement**

Option: Add `StateCondition` to ContinuousEffect:
```csharp
public record ContinuousEffect(
    // ... existing params ...
    Func<GameState, bool>? StateCondition = null
);
```

In RecalculateState, when evaluating whether an effect applies:
```csharp
if (effect.StateCondition != null && !effect.StateCondition(_state))
    continue;
```

Kaito's creature-mode effect:
```csharp
new ContinuousEffect(Guid.Empty, ContinuousEffectType.BecomeCreature,
    (card, _) => card.Name == "Kaito, Bane of Nightmares"
        && card.GetCounters(CounterType.Loyalty) > 0,
    SetPower: 3, SetToughness: 4,
    Layer: EffectLayer.Layer4_TypeChanging,
    StateCondition: state => state.ActivePlayer.Battlefield.Cards
        .Any(c => c.Name == "Kaito, Bane of Nightmares"))
```

Wait, that checks if Kaito is on the active player's battlefield. That's the "during your turn" check. Also need to add Ninja subtype and Hexproof keyword.

This is complex — the implementer will need to study how `BecomeCreature` currently works in RecalculateState (it exists for Opalescence). The creature-mode needs to:
1. Change card type to include Creature
2. Set base P/T to 3/4
3. Add Ninja subtype
4. Grant Hexproof keyword

This may require multiple ContinuousEffects or extending the BecomeCreature handler.

**Step 4: Run tests, verify pass**

**Step 5: Commit**
```bash
git commit -m "feat(engine): Kaito conditional creature mode — 3/4 Ninja with hexproof during your turn"
```

---

## Task 15: Ninjutsu mechanic

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinition.cs` (NinjutsuCost)
- Modify: `src/MtgDecker.Engine/Enums/ActionType.cs`
- Modify: `src/MtgDecker.Engine/GameAction.cs`
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (Ninjutsu handler + combat integration)
- Test: `tests/MtgDecker.Engine.Tests/NinjutsuTests.cs`

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/NinjutsuTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class NinjutsuTests : IDisposable
{
    private const string TestNinjaName = "Test Ninjutsu Creature";

    public NinjutsuTests()
    {
        CardDefinitions.Register(new CardDefinition(
            ManaCost.Parse("{2}{U}{B}"), null, null, null, CardType.Planeswalker)
        {
            Name = TestNinjaName,
            NinjutsuCost = ManaCost.Parse("{1}{U}{B}"),
            StartingLoyalty = 4,
        });
    }

    public void Dispose() => CardDefinitions.Unregister(TestNinjaName);

    [Fact]
    public async Task Ninjutsu_ReturnsUnblockedAttacker_PutsNinjaOnBattlefield()
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
        state.SetActivePlayer(p1);
        state.CurrentPhase = Phase.Combat;

        // P1 has an unblocked attacker
        var attacker = new GameCard
        {
            Name = "Unblocked Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
            TurnEnteredBattlefield = state.TurnNumber - 1,
        };
        p1.Battlefield.Add(attacker);

        // P1 has ninja in hand
        var ninja = GameCard.Create(TestNinjaName);
        p1.Hand.Add(ninja);

        // P1 has mana
        p1.ManaPool.Add(ManaColor.Blue, 1);
        p1.ManaPool.Add(ManaColor.Black, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 1);

        // Set up combat with unblocked attacker
        state.Combat = new CombatState(p1.Id, p2.Id);
        state.Combat.DeclareAttacker(attacker.Id);
        state.CombatStep = CombatStep.DeclareBlockers;

        // Activate ninjutsu: return bear, put ninja on battlefield
        await engine.ExecuteAction(
            GameAction.Ninjutsu(p1.Id, ninja.Id, attacker.Id));

        // Bear returned to hand
        p1.Hand.Cards.Should().Contain(c => c.Name == "Unblocked Bear");
        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Unblocked Bear");

        // Ninja on battlefield, tapped, attacking
        p1.Battlefield.Cards.Should().Contain(c => c.Name == TestNinjaName);
        var ninjaOnField = p1.Battlefield.Cards.First(c => c.Name == TestNinjaName);
        ninjaOnField.IsTapped.Should().BeTrue();
    }
}
```

**Step 2: Run to verify failure**

**Step 3: Implement**

In `src/MtgDecker.Engine/CardDefinition.cs`, add:
```csharp
public ManaCost? NinjutsuCost { get; init; }
```

In `src/MtgDecker.Engine/Enums/ActionType.cs`, add:
```csharp
Ninjutsu,
```

In `src/MtgDecker.Engine/GameAction.cs`, add:
```csharp
public Guid? ReturnCardId { get; init; }

public static GameAction Ninjutsu(Guid playerId, Guid ninjutsuCardId, Guid returnCreatureId)
    => new() { Type = ActionType.Ninjutsu, PlayerId = playerId, CardId = ninjutsuCardId, ReturnCardId = returnCreatureId };
```

In `GameEngine.cs`, add handler:
```csharp
case ActionType.Ninjutsu:
{
    var ninjutsuPlayer = _state.GetPlayer(action.PlayerId);
    var ninjutsuCard = ninjutsuPlayer.Hand.Cards.FirstOrDefault(c => c.Id == action.CardId);
    if (ninjutsuCard == null) break;

    if (!CardDefinitions.TryGet(ninjutsuCard.Name, out var nDef) || nDef.NinjutsuCost == null)
    {
        _state.Log($"{ninjutsuCard.Name} does not have ninjutsu.");
        break;
    }

    // Must be during combat after blockers declared
    if (_state.CombatStep < CombatStep.DeclareBlockers || _state.Combat == null)
    {
        _state.Log("Ninjutsu can only be activated after blockers are declared.");
        break;
    }

    // The returned creature must be an unblocked attacker controlled by this player
    var returnCardId = action.ReturnCardId;
    if (!returnCardId.HasValue) break;

    var returnCard = ninjutsuPlayer.Battlefield.Cards.FirstOrDefault(c => c.Id == returnCardId.Value);
    if (returnCard == null || !_state.Combat.Attackers.Contains(returnCardId.Value)
        || _state.Combat.IsBlocked(returnCardId.Value))
    {
        _state.Log("Must return an unblocked attacker you control.");
        break;
    }

    // Pay mana cost
    if (!ninjutsuPlayer.ManaPool.CanPay(nDef.NinjutsuCost))
    {
        _state.Log("Cannot pay ninjutsu cost.");
        break;
    }
    ninjutsuPlayer.ManaPool.Pay(nDef.NinjutsuCost);

    // Return the unblocked attacker to hand
    ninjutsuPlayer.Battlefield.Remove(returnCard);
    returnCard.IsTapped = false;
    ninjutsuPlayer.Hand.Add(returnCard);
    _state.Log($"{returnCard.Name} returned to {ninjutsuPlayer.Name}'s hand.");

    // Put ninjutsu card onto battlefield tapped and attacking
    ninjutsuPlayer.Hand.Remove(ninjutsuCard);
    ninjutsuCard.IsTapped = true;
    ninjutsuCard.TurnEnteredBattlefield = _state.TurnNumber;
    ninjutsuPlayer.Battlefield.Add(ninjutsuCard);

    // Apply ETB effects (loyalty for planeswalkers)
    ApplyEntersWithCounters(ninjutsuCard);

    // Add to attackers list (replace the returned creature)
    _state.Combat.DeclareAttacker(ninjutsuCard.Id);
    // Copy the attack target from the returned creature
    var originalTarget = _state.Combat.GetAttackerTarget(returnCardId.Value);
    _state.Combat.SetAttackerTarget(ninjutsuCard.Id, originalTarget);

    _state.Log($"{ninjutsuCard.Name} enters the battlefield tapped and attacking via ninjutsu.");

    // Fire ETB triggers
    await QueueSelfTriggersOnStackAsync(GameEvent.EnterBattlefield, ninjutsuCard, ninjutsuPlayer);

    break;
}
```

**Note:** ManaPool.CanPay and ManaPool.Pay may not exist with this exact signature. Check the existing mana payment flow in CastSpell handler — it uses a more complex payment process with ChooseGenericPayment. You may need to simplify for ninjutsu or follow the same pattern. The key is: pay the NinjutsuCost mana.

**Step 4: Run tests, verify pass**

**Step 5: Commit**
```bash
git commit -m "feat(engine): implement ninjutsu mechanic — return unblocked attacker, put onto battlefield attacking"
```

---

## Task 16: Register Kaito, Bane of Nightmares in CardDefinitions

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Test: `tests/MtgDecker.Engine.Tests/KaitoRegistrationTests.cs`

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/KaitoRegistrationTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class KaitoRegistrationTests
{
    [Fact]
    public void CardDefinition_Kaito_IsRegistered()
    {
        CardDefinitions.TryGet("Kaito, Bane of Nightmares", out var def).Should().BeTrue();
        def!.ManaCost!.ConvertedManaCost.Should().Be(4); // {2}{U}{B}
        def.CardTypes.Should().Be(CardType.Planeswalker);
        def.StartingLoyalty.Should().Be(4);
        def.IsLegendary.Should().BeTrue();
        def.Subtypes.Should().Contain("Kaito");
    }

    [Fact]
    public void CardDefinition_Kaito_HasThreeLoyaltyAbilities()
    {
        CardDefinitions.TryGet("Kaito, Bane of Nightmares", out var def).Should().BeTrue();
        def!.LoyaltyAbilities.Should().HaveCount(3);

        // +1: Ninja emblem
        def.LoyaltyAbilities![0].LoyaltyCost.Should().Be(1);
        def.LoyaltyAbilities[0].Effect.Should().BeOfType<CreateNinjaEmblemEffect>();

        // 0: Surveil + draw
        def.LoyaltyAbilities[1].LoyaltyCost.Should().Be(0);
        def.LoyaltyAbilities[1].Effect.Should().BeOfType<SurveilAndDrawEffect>();

        // -2: Tap + stun
        def.LoyaltyAbilities[2].LoyaltyCost.Should().Be(-2);
        def.LoyaltyAbilities[2].Effect.Should().BeOfType<TapAndStunEffect>();
    }

    [Fact]
    public void CardDefinition_Kaito_HasNinjutsuCost()
    {
        CardDefinitions.TryGet("Kaito, Bane of Nightmares", out var def).Should().BeTrue();
        def!.NinjutsuCost.Should().NotBeNull();
        def.NinjutsuCost!.ConvertedManaCost.Should().Be(3); // {1}{U}{B}
    }

    [Fact]
    public void CardDefinition_Kaito_HasCreatureModeEffect()
    {
        CardDefinitions.TryGet("Kaito, Bane of Nightmares", out var def).Should().BeTrue();
        // Should have continuous effects for creature mode
        def!.ContinuousEffects.Should().NotBeEmpty();
    }
}
```

**Step 2: Run to verify failure**

**Step 3: Register Kaito**

In `src/MtgDecker.Engine/CardDefinitions.cs`, in the "Legacy Dimir Tempo" section:

```csharp
["Kaito, Bane of Nightmares"] = new(ManaCost.Parse("{2}{U}{B}"), null, null, null, CardType.Planeswalker)
{
    IsLegendary = true,
    StartingLoyalty = 4,
    Subtypes = ["Kaito"],
    NinjutsuCost = ManaCost.Parse("{1}{U}{B}"),
    LoyaltyAbilities =
    [
        new LoyaltyAbility(1, new CreateNinjaEmblemEffect(), "+1: Ninja emblem"),
        new LoyaltyAbility(0, new SurveilAndDrawEffect(), "0: Surveil 2, draw for life lost"),
        new LoyaltyAbility(-2, new TapAndStunEffect(2), "-2: Tap, 2 stun counters"),
    ],
    ContinuousEffects =
    [
        // During your turn, with loyalty > 0, Kaito is a 3/4 Ninja creature with hexproof
        // Implementation depends on how Task 14 resolved the conditional creature mode
    ],
},
```

**Step 4: Run tests, verify pass**

**Step 5: Commit**
```bash
git commit -m "feat(engine): register Kaito, Bane of Nightmares with all abilities"
```

---

## Task 17: Integration tests — full Kaito gameplay

**Files:**
- Test: `tests/MtgDecker.Engine.Tests/KaitoIntegrationTests.cs`

Write integration tests covering:

1. **Cast Kaito, activate +1 ability** — verify loyalty goes to 5, emblem created
2. **Activate 0 ability with opponent life loss** — verify surveil + draw
3. **Activate -2 ability** — verify target tapped with 2 stun counters
4. **Ninjutsu during combat** — verify attacker returned, Kaito enters attacking
5. **Creature mode during your turn** — verify Kaito is 3/4 creature
6. **SBA removes Kaito at 0 loyalty** — verify graveyard
7. **Combat damage to Kaito** — verify loyalty decremented

Each test should use the real CardDefinitions entry for Kaito. Follow existing integration test patterns (e.g., `OrcishBowmastersTests` from PR 1).

**Commit:**
```bash
git commit -m "test(engine): add Kaito integration tests — loyalty, combat, ninjutsu, creature mode"
```

---

## Task 18: Full test suite verification + PR

**Step 1: Run all test suites**
```bash
dotnet test tests/MtgDecker.Engine.Tests/
dotnet test tests/MtgDecker.Domain.Tests/
dotnet test tests/MtgDecker.Application.Tests/
dotnet test tests/MtgDecker.Infrastructure.Tests/
dotnet build src/MtgDecker.Web/
```

**Step 2: Push and create PR**
```bash
git push -u origin feat/planeswalker-kaito
gh pr create --title "feat(engine): planeswalker system + Kaito, Bane of Nightmares" --body "..."
```

---

## Summary

| Task | What | Key Files |
|------|------|-----------|
| 1 | CardType.Planeswalker + IsPlaneswalker | CardType.cs, GameCard.cs |
| 2 | Loyalty counters + StartingLoyalty + ETB | CounterType.cs, GameCard.cs, CardDefinition.cs, GameEngine.cs |
| 3 | Planeswalker SBA (loyalty ≤ 0) | GameEngine.cs |
| 4 | LoyaltyAbility record + ActionType | LoyaltyAbility.cs, ActionType.cs, GameAction.cs |
| 5 | Loyalty ability activation handler | GameEngine.cs, Player.cs, ActivatedLoyaltyAbilityStackObject.cs |
| 6 | Loyalty ability stack resolution | GameEngine.cs |
| 7 | Planeswalker combat | CombatState.cs, IPlayerDecisionHandler.cs, GameEngine.cs |
| 8 | Keyword.Hexproof | Keyword.cs, GameEngine.cs |
| 9 | Stun counters + untap | CounterType.cs, GameEngine.cs |
| 10 | SurveilEffect | Effects/SurveilEffect.cs |
| 11 | Player.LifeLostThisTurn | Player.cs, GameEngine.cs |
| 12 | Emblem system | Emblem.cs, Player.cs, GameEngine.cs |
| 13 | Kaito's loyalty effects | 3 effect files |
| 14 | Kaito's creature mode | GameEngine.cs, ContinuousEffect |
| 15 | Ninjutsu mechanic | CardDefinition.cs, GameEngine.cs |
| 16 | Register Kaito | CardDefinitions.cs |
| 17 | Integration tests | KaitoIntegrationTests.cs |
| 18 | Full verification + PR | — |
