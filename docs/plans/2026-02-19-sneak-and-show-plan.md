# Legacy Sneak and Show Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement all cards for a Legacy Sneak and Show deck in the `CardDefinitions` registry, adding new engine mechanics (extra turns, annihilator, put-into-play effects, pay-life costs, self-damage lands, Blood Moon) along the way.

**Architecture:** New `SpellEffect` and `IEffect` subclasses for each card's mechanics, new enum values for keywords/triggers, and targeted engine changes in `GameEngine.cs` (turn management, activated ability cost validation, mana production). All work in worktree `.worktrees/sneak-and-show/` on branch `feat/sneak-and-show`.

**Tech Stack:** .NET 10, C# 14, xUnit + FluentAssertions. Engine-only changes (no EF Core, no Web).

---

## Conventions

- All engine source: `src/MtgDecker.Engine/`
- All tests: `tests/MtgDecker.Engine.Tests/`
- Test class naming: `{CardName}Tests.cs` or `{MechanicName}Tests.cs`
- Use `TestDecisionHandler` from `tests/MtgDecker.Engine.Tests/Helpers/TestDecisionHandler.cs`
- `GameCard.Create(name, typeLine)` for test cards
- Build: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/`
- Run single test: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~ClassName.MethodName"`
- Commit after each task

---

## Task 1: Engine Infrastructure — New Enums and Properties

**Files:**
- Modify: `src/MtgDecker.Engine/Enums/Keyword.cs`
- Modify: `src/MtgDecker.Engine/Enums/GameEvent.cs`
- Modify: `src/MtgDecker.Engine/Triggers/TriggerCondition.cs`
- Modify: `src/MtgDecker.Engine/ActivatedAbility.cs`
- Modify: `src/MtgDecker.Engine/Mana/ManaAbility.cs`
- Modify: `src/MtgDecker.Engine/GameState.cs`
- Modify: `src/MtgDecker.Engine/CardDefinition.cs`
- Test: `tests/MtgDecker.Engine.Tests/SneakShowInfraTests.cs`

**Step 1: Add new enum values and properties**

Add to `Keyword.cs`:
```csharp
ProtectionFromColoredSpells,
```

Add to `GameEvent.cs`:
```csharp
LandPlayed,
```

Add to `TriggerCondition.cs`:
```csharp
SelfIsCast,
ControllerPlaysAnotherLand,
```

Add to `ActivatedAbilityCost` in `ActivatedAbility.cs` — new optional parameter:
```csharp
public record ActivatedAbilityCost(
    bool TapSelf = false,
    bool SacrificeSelf = false,
    string? SacrificeSubtype = null,
    ManaCost? ManaCost = null,
    CounterType? RemoveCounterType = null,
    CardType? SacrificeCardType = null,
    CardType? DiscardCardType = null,
    int PayLife = 0);
```

Add `SelfDamage` property and `ProduceCount` to `ManaAbility.cs`:
```csharp
public int SelfDamage { get; }
public int ProduceCount { get; }

// Update private constructor to accept these:
private ManaAbility(ManaAbilityType type, ManaColor? fixedColor, IReadOnlyList<ManaColor>? choiceColors,
    ManaColor? dynamicColor = null, Func<Player, int>? countFunc = null,
    IReadOnlySet<ManaColor>? painColors = null, CounterType? removesCounterOnTap = null,
    int selfDamage = 0, int produceCount = 1)
{
    // ... existing assignments ...
    SelfDamage = selfDamage;
    ProduceCount = produceCount;
}

// New factory method for Ancient Tomb / City of Traitors:
public static ManaAbility FixedMultiple(ManaColor color, int count, int selfDamage = 0) =>
    new(ManaAbilityType.Fixed, color, null, selfDamage: selfDamage, produceCount: count);
```

Add to `GameState.cs`:
```csharp
public Queue<Guid> ExtraTurns { get; } = new();
```

Add to `CardDefinition.cs`:
```csharp
public bool ShuffleGraveyardOnDeath { get; init; }
```

**Step 2: Write infrastructure tests**

Create `tests/MtgDecker.Engine.Tests/SneakShowInfraTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests;

public class SneakShowInfraTests
{
    [Fact]
    public void ManaAbility_FixedMultiple_ProducesCorrectCount()
    {
        var ability = ManaAbility.FixedMultiple(ManaColor.Colorless, 2);
        ability.FixedColor.Should().Be(ManaColor.Colorless);
        ability.ProduceCount.Should().Be(2);
        ability.SelfDamage.Should().Be(0);
    }

    [Fact]
    public void ManaAbility_FixedMultipleWithDamage_HasSelfDamage()
    {
        var ability = ManaAbility.FixedMultiple(ManaColor.Colorless, 2, selfDamage: 2);
        ability.SelfDamage.Should().Be(2);
        ability.ProduceCount.Should().Be(2);
    }

    [Fact]
    public void GameState_ExtraTurns_StartsEmpty()
    {
        var state = TestHelper.CreateState();
        state.ExtraTurns.Should().BeEmpty();
    }

    [Fact]
    public void ActivatedAbilityCost_PayLife_DefaultsToZero()
    {
        var cost = new ActivatedAbilityCost();
        cost.PayLife.Should().Be(0);
    }

    [Fact]
    public void ActivatedAbilityCost_PayLife_CanBeSet()
    {
        var cost = new ActivatedAbilityCost(PayLife: 7);
        cost.PayLife.Should().Be(7);
    }

    [Fact]
    public void CardDefinition_ShuffleGraveyardOnDeath_DefaultsFalse()
    {
        var def = new CardDefinition(null, null, null, null, CardType.Creature);
        def.ShuffleGraveyardOnDeath.Should().BeFalse();
    }
}
```

Also create a small `tests/MtgDecker.Engine.Tests/TestHelper.cs` if it doesn't already exist (check first — if something similar exists in `Helpers/`, use that):
```csharp
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public static class TestHelper
{
    public static GameState CreateState()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        return new GameState(p1, p2);
    }

    public static (GameState state, TestDecisionHandler h1, TestDecisionHandler h2) CreateStateWithHandlers()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        return (new GameState(p1, p2), h1, h2);
    }
}
```

**Step 3: Run tests to verify**

```bash
export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~SneakShowInfraTests"
```

Expected: All pass.

**Step 4: Commit**

```bash
git add -A && git commit -m "feat(engine): add infrastructure for Sneak and Show — new enums, ManaAbility.FixedMultiple, PayLife cost, ExtraTurns queue"
```

---

## Task 2: Engine — TapCard Multi-Mana + Self-Damage

The engine's `TapCard` handler currently produces 1 mana for `ManaAbilityType.Fixed`. Extend it to support `ProduceCount > 1` and `SelfDamage > 0`.

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (TapCard handler, ~lines 258-318)
- Test: `tests/MtgDecker.Engine.Tests/AncientTombManaTests.cs`

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/AncientTombManaTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class AncientTombManaTests
{
    [Fact]
    public async Task TapCard_FixedMultiple_ProducesMultipleMana()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);

        var tomb = new GameCard
        {
            Name = "Ancient Tomb",
            CardTypes = CardType.Land,
            ManaAbility = ManaAbility.FixedMultiple(ManaColor.Colorless, 2, selfDamage: 2),
        };
        state.Player1.Battlefield.Add(tomb);

        h1.EnqueueAction(GameAction.Tap(state.Player1.Id, tomb.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));

        state.CurrentPhase = Phase.Main1;
        await engine.ExecuteAction(
            new GameAction { Type = ActionType.TapCard, PlayerId = state.Player1.Id, CardId = tomb.Id },
            state.Player1, CancellationToken.None);

        state.Player1.ManaPool.GetAmount(ManaColor.Colorless).Should().Be(2);
    }

    [Fact]
    public async Task TapCard_FixedMultipleWithDamage_DealsDamageToController()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);

        var tomb = new GameCard
        {
            Name = "Ancient Tomb",
            CardTypes = CardType.Land,
            ManaAbility = ManaAbility.FixedMultiple(ManaColor.Colorless, 2, selfDamage: 2),
        };
        state.Player1.Battlefield.Add(tomb);

        await engine.ExecuteAction(
            new GameAction { Type = ActionType.TapCard, PlayerId = state.Player1.Id, CardId = tomb.Id },
            state.Player1, CancellationToken.None);

        state.Player1.Life.Should().Be(18); // 20 - 2
    }

    [Fact]
    public async Task TapCard_FixedMultipleNoDamage_DoesNotDealDamage()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);

        var city = new GameCard
        {
            Name = "City of Traitors",
            CardTypes = CardType.Land,
            ManaAbility = ManaAbility.FixedMultiple(ManaColor.Colorless, 2),
        };
        state.Player1.Battlefield.Add(city);

        await engine.ExecuteAction(
            new GameAction { Type = ActionType.TapCard, PlayerId = state.Player1.Id, CardId = city.Id },
            state.Player1, CancellationToken.None);

        state.Player1.ManaPool.GetAmount(ManaColor.Colorless).Should().Be(2);
        state.Player1.Life.Should().Be(20); // no damage
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~AncientTombManaTests"
```

Expected: Fail (ProduceCount not used in engine yet).

**Step 3: Modify GameEngine.cs TapCard handler**

In `GameEngine.cs`, find the `case ActionType.TapCard` section, specifically the `ManaAbilityType.Fixed` branch (~line 283-288). Change:

```csharp
if (ability.Type == ManaAbilityType.Fixed)
{
    player.ManaPool.Add(ability.FixedColor!.Value);
    action.ManaProduced = ability.FixedColor!.Value;
    _state.Log($"{player.Name} taps {tapTarget.Name} for {ability.FixedColor}.");
}
```

To:

```csharp
if (ability.Type == ManaAbilityType.Fixed)
{
    var count = ability.ProduceCount;
    player.ManaPool.Add(ability.FixedColor!.Value, count);
    action.ManaProduced = ability.FixedColor!.Value;
    action.ManaProducedAmount = count;
    if (ability.SelfDamage > 0)
    {
        player.AdjustLife(-ability.SelfDamage);
        _state.Log($"{tapTarget.Name} deals {ability.SelfDamage} damage to {player.Name}.");
    }
    var manaDesc = count > 1 ? $"{count} {ability.FixedColor}" : $"{ability.FixedColor}";
    _state.Log($"{player.Name} taps {tapTarget.Name} for {manaDesc}.");
}
```

Also ensure `ManaPool.Add(color, count)` supports a count parameter. Check `ManaPool.cs` — if it only has `Add(ManaColor)`, add an overload:

```csharp
public void Add(ManaColor color, int count)
{
    for (int i = 0; i < count; i++)
        Add(color);
}
```

**Step 4: Run tests to verify they pass**

```bash
export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~AncientTombManaTests"
```

Expected: All pass.

**Step 5: Run full test suite**

```bash
export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/
```

Expected: All existing tests still pass (1333 pass, 1 pre-existing failure).

**Step 6: Commit**

```bash
git add -A && git commit -m "feat(engine): support multi-mana production and self-damage on TapCard (Ancient Tomb)"
```

---

## Task 3: Engine — PayLife Activated Ability Cost

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (ActivateAbility handler, around line 580-740)
- Test: `tests/MtgDecker.Engine.Tests/PayLifeAbilityTests.cs`

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/PayLifeAbilityTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class PayLifeAbilityTests
{
    [Fact]
    public async Task ActivateAbility_PayLifeCost_DeductsLife()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);

        // Register a test card with PayLife cost
        CardDefinitions.Register(new CardDefinition(
            ManaCost.Parse("{4}{B}{B}{B}{B}"), null, 7, 7, CardType.Creature)
        {
            Name = "TestPayLifeCard",
            ActivatedAbility = new(
                new ActivatedAbilityCost(PayLife: 7),
                new DrawCardsActivatedEffect(7)),
        });

        var card = new GameCard
        {
            Name = "TestPayLifeCard",
            CardTypes = CardType.Creature,
            TurnEnteredBattlefield = 0, // no summoning sickness
        };
        state.Player1.Battlefield.Add(card);
        state.TurnNumber = 2;

        // Add 7 cards to library so draws succeed
        for (int i = 0; i < 7; i++)
            state.Player1.Library.AddToTop(GameCard.Create($"Card{i}"));

        await engine.ExecuteAction(
            new GameAction { Type = ActionType.ActivateAbility, PlayerId = state.Player1.Id, CardId = card.Id },
            state.Player1, CancellationToken.None);

        state.Player1.Life.Should().Be(13); // 20 - 7

        CardDefinitions.Unregister("TestPayLifeCard");
    }

    [Fact]
    public async Task ActivateAbility_PayLifeCost_CannotActivateBelowThreshold()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);

        CardDefinitions.Register(new CardDefinition(
            ManaCost.Parse("{4}{B}{B}{B}{B}"), null, 7, 7, CardType.Creature)
        {
            Name = "TestPayLifeCard2",
            ActivatedAbility = new(
                new ActivatedAbilityCost(PayLife: 7),
                new DrawCardsActivatedEffect(7)),
        });

        var card = new GameCard
        {
            Name = "TestPayLifeCard2",
            CardTypes = CardType.Creature,
            TurnEnteredBattlefield = 0,
        };
        state.Player1.Battlefield.Add(card);
        state.TurnNumber = 2;
        state.Player1.AdjustLife(-15); // Life = 5, can't pay 7

        await engine.ExecuteAction(
            new GameAction { Type = ActionType.ActivateAbility, PlayerId = state.Player1.Id, CardId = card.Id },
            state.Player1, CancellationToken.None);

        state.Player1.Life.Should().Be(5); // unchanged
        state.GameLog.Should().Contain(msg => msg.Contains("not enough life"));

        CardDefinitions.Unregister("TestPayLifeCard2");
    }
}
```

**Step 2: Create `DrawCardsActivatedEffect`**

Create `src/MtgDecker.Engine/Triggers/Effects/DrawCardsActivatedEffect.cs`:

```csharp
namespace MtgDecker.Engine.Triggers.Effects;

public class DrawCardsActivatedEffect(int count) : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        for (int i = 0; i < count; i++)
        {
            var drawn = context.Controller.Library.DrawFromTop();
            if (drawn != null)
            {
                context.Controller.Hand.Add(drawn);
            }
            else
            {
                // Deck-out: drawing from empty library = game loss (MTG 104.3c)
                context.State.IsGameOver = true;
                context.State.Winner = context.State.GetOpponent(context.Controller).Name;
                context.State.Log($"{context.Controller.Name} cannot draw — loses the game.");
                return Task.CompletedTask;
            }
        }
        context.State.Log($"{context.Controller.Name} pays {count} life to draw {count} cards.");
        return Task.CompletedTask;
    }
}
```

**Step 3: Add PayLife validation and cost payment to GameEngine.cs**

In the `case ActionType.ActivateAbility` handler, after the summoning sickness check (~line 620) and before the mana cost check (~line 622), add:

```csharp
// Validate: pay life cost
if (cost.PayLife > 0 && player.Life < cost.PayLife)
{
    _state.Log($"Cannot activate {abilitySource.Name} — not enough life (need {cost.PayLife}, have {player.Life}).");
    break;
}
```

Then in the "Pay costs" section (~after line 738), add:

```csharp
// Pay costs: life
if (cost.PayLife > 0)
{
    player.AdjustLife(-cost.PayLife);
    _state.Log($"{player.Name} pays {cost.PayLife} life.");
}
```

**Step 4: Run tests**

```bash
export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~PayLifeAbilityTests"
```

**Step 5: Run full suite, commit**

```bash
export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/
git add -A && git commit -m "feat(engine): add PayLife activated ability cost and DrawCardsActivatedEffect"
```

---

## Task 4: Engine — Extra Turns

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (end of `RunTurnAsync`, ~lines 100-103)
- Create: `src/MtgDecker.Engine/Triggers/Effects/ExtraTurnEffect.cs`
- Test: `tests/MtgDecker.Engine.Tests/ExtraTurnTests.cs`

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/ExtraTurnTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers.Effects;
using MtgDecker.Engine.Triggers;

namespace MtgDecker.Engine.Tests;

public class ExtraTurnTests
{
    [Fact]
    public async Task ExtraTurn_QueuedPlayer_TakesNextTurn()
    {
        var (state, h1, h2) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);
        state.TurnNumber = 1;
        state.ActivePlayer = state.Player1;

        // Queue extra turn for Player1
        state.ExtraTurns.Enqueue(state.Player1.Id);

        // Fill libraries so draws don't fail
        for (int i = 0; i < 20; i++)
        {
            state.Player1.Library.AddToTop(GameCard.Create($"Card{i}"));
            state.Player2.Library.AddToTop(GameCard.Create($"Card{i}"));
        }

        // Run P1's turn — both players pass immediately
        await engine.RunTurnAsync();

        // After P1's turn, extra turn was queued so P1 should still be active
        state.ActivePlayer.Should().Be(state.Player1);
    }

    [Fact]
    public async Task ExtraTurn_AfterExtraTurnEnds_NormalPlayerRotation()
    {
        var (state, h1, h2) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);
        state.TurnNumber = 1;
        state.ActivePlayer = state.Player1;

        state.ExtraTurns.Enqueue(state.Player1.Id);

        for (int i = 0; i < 20; i++)
        {
            state.Player1.Library.AddToTop(GameCard.Create($"Card{i}"));
            state.Player2.Library.AddToTop(GameCard.Create($"Card{i}"));
        }

        // Turn 1 (P1), then extra turn (P1 again)
        await engine.RunTurnAsync();
        state.ActivePlayer.Should().Be(state.Player1); // extra turn

        await engine.RunTurnAsync();
        state.ActivePlayer.Should().Be(state.Player2); // normal rotation
    }

    [Fact]
    public void ExtraTurnEffect_QueuesExtraTurn()
    {
        var (state, _, _) = TestHelper.CreateStateWithHandlers();
        var source = GameCard.Create("Emrakul, the Aeons Torn");
        var ctx = new EffectContext(state, state.Player1, source, state.Player1.DecisionHandler);

        var effect = new ExtraTurnEffect();
        effect.Execute(ctx);

        state.ExtraTurns.Should().ContainSingle()
            .Which.Should().Be(state.Player1.Id);
    }
}
```

**Step 2: Create ExtraTurnEffect**

Create `src/MtgDecker.Engine/Triggers/Effects/ExtraTurnEffect.cs`:

```csharp
namespace MtgDecker.Engine.Triggers.Effects;

public class ExtraTurnEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        context.State.ExtraTurns.Enqueue(context.Controller.Id);
        context.State.Log($"{context.Controller.Name} will take an extra turn.");
        return Task.CompletedTask;
    }
}
```

**Step 3: Modify RunTurnAsync to check extra turns**

At the end of `RunTurnAsync` in `GameEngine.cs`, replace lines 101-103:

```csharp
_state.IsFirstTurn = false;
_state.TurnNumber++;
_state.ActivePlayer = _state.GetOpponent(_state.ActivePlayer);
```

With:

```csharp
_state.IsFirstTurn = false;
_state.TurnNumber++;

if (_state.ExtraTurns.Count > 0)
{
    var extraPlayerId = _state.ExtraTurns.Dequeue();
    _state.ActivePlayer = _state.GetPlayer(extraPlayerId);
    _state.Log($"Extra turn for {_state.ActivePlayer.Name}!");
}
else
{
    _state.ActivePlayer = _state.GetOpponent(_state.ActivePlayer);
}
```

**Step 4: Run tests**

```bash
export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~ExtraTurnTests"
```

**Step 5: Full suite + commit**

```bash
export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/
git add -A && git commit -m "feat(engine): add extra turn mechanic with ExtraTurnEffect and GameState.ExtraTurns queue"
```

---

## Task 5: Engine — LandPlayed Event + City of Traitors Trigger

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (PlayCard land handler, ~line 185)
- Create: `src/MtgDecker.Engine/Triggers/Effects/SacrificeSelfOnLandEffect.cs`
- Test: `tests/MtgDecker.Engine.Tests/CityOfTraitorsTests.cs`

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/CityOfTraitorsTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class CityOfTraitorsTests
{
    [Fact]
    public async Task CityOfTraitors_WhenAnotherLandPlayed_SacrificesSelf()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);
        state.ActivePlayer = state.Player1;
        state.TurnNumber = 1;

        // City is on battlefield
        var city = new GameCard
        {
            Name = "City of Traitors",
            CardTypes = CardType.Land,
            ManaAbility = ManaAbility.FixedMultiple(ManaColor.Colorless, 2),
            Triggers = [new Trigger(GameEvent.LandPlayed, TriggerCondition.ControllerPlaysAnotherLand, new SacrificeSelfOnLandEffect())],
        };
        state.Player1.Battlefield.Add(city);

        // Play another land
        var island = GameCard.Create("Island", "Basic Land — Island");
        island.ManaAbility = ManaAbility.Fixed(ManaColor.Blue);
        state.Player1.Hand.Add(island);

        h1.EnqueueAction(new GameAction { Type = ActionType.PlayCard, PlayerId = state.Player1.Id, CardId = island.Id });
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));

        state.CurrentPhase = Phase.Main1;
        await engine.ExecuteAction(
            new GameAction { Type = ActionType.PlayCard, PlayerId = state.Player1.Id, CardId = island.Id },
            state.Player1, CancellationToken.None);

        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Name == "City of Traitors");
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Name == "City of Traitors");
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Name == "Island");
    }

    [Fact]
    public async Task CityOfTraitors_FirstLandPlayed_DoesNotSacrifice()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);
        state.ActivePlayer = state.Player1;

        // City is on battlefield with no other lands
        var city = new GameCard
        {
            Name = "City of Traitors",
            CardTypes = CardType.Land,
            ManaAbility = ManaAbility.FixedMultiple(ManaColor.Colorless, 2),
            Triggers = [new Trigger(GameEvent.LandPlayed, TriggerCondition.ControllerPlaysAnotherLand, new SacrificeSelfOnLandEffect())],
        };
        // City is already on battlefield — it was played previously
        state.Player1.Battlefield.Add(city);

        // Tap city for mana — should work fine
        await engine.ExecuteAction(
            new GameAction { Type = ActionType.TapCard, PlayerId = state.Player1.Id, CardId = city.Id },
            state.Player1, CancellationToken.None);

        state.Player1.Battlefield.Cards.Should().Contain(c => c.Name == "City of Traitors");
        state.Player1.ManaPool.GetAmount(ManaColor.Colorless).Should().Be(2);
    }
}
```

**Step 2: Create SacrificeSelfOnLandEffect**

Create `src/MtgDecker.Engine/Triggers/Effects/SacrificeSelfOnLandEffect.cs`:

```csharp
namespace MtgDecker.Engine.Triggers.Effects;

public class SacrificeSelfOnLandEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var card = context.Controller.Battlefield.Cards.FirstOrDefault(c => c.Id == context.Source.Id);
        if (card == null) return Task.CompletedTask;

        context.Controller.Battlefield.RemoveById(card.Id);
        context.Controller.Graveyard.Add(card);
        context.State.Log($"{context.Controller.Name} sacrifices {card.Name} (another land was played).");
        return Task.CompletedTask;
    }
}
```

**Step 3: Fire LandPlayed event in GameEngine**

In `GameEngine.cs`, after a land is played and ETB triggers are queued (~line 188-189), add:

```csharp
// Fire LandPlayed triggers (e.g., City of Traitors)
await QueueBoardTriggersOnStackAsync(GameEvent.LandPlayed, playCard, ct);
```

**Step 4: Handle TriggerCondition.ControllerPlaysAnotherLand in trigger matching**

Find where board triggers are evaluated (the `QueueBoardTriggersOnStackAsync` method). The `ControllerPlaysAnotherLand` condition should match when:
- The trigger's event matches `GameEvent.LandPlayed`
- The source card (with the trigger) is NOT the card that was just played
- The controller of the source card is the same as the player who played the land

Look at how `QueueBoardTriggersOnStackAsync` evaluates conditions and add a case for `TriggerCondition.ControllerPlaysAnotherLand`.

**Step 5: Run tests + full suite + commit**

```bash
export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~CityOfTraitorsTests"
dotnet test tests/MtgDecker.Engine.Tests/
git add -A && git commit -m "feat(engine): add LandPlayed event and City of Traitors sacrifice trigger"
```

---

## Task 6: Engine — SelfIsCast Trigger Condition

Emrakul's extra turn triggers on CAST, not on ETB. Need `TriggerCondition.SelfIsCast` that fires when the card itself is cast as a spell.

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (CastSpell handler — fire self-cast triggers)
- Test: `tests/MtgDecker.Engine.Tests/SelfIsCastTests.cs`

**Step 1: Write test**

```csharp
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class SelfIsCastTests
{
    [Fact]
    public async Task SelfIsCast_WhenCardCast_TriggerFires()
    {
        var (state, h1, h2) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);
        state.ActivePlayer = state.Player1;
        state.TurnNumber = 2;
        state.CurrentPhase = Phase.Main1;

        // Register test card with SelfIsCast trigger
        CardDefinitions.Register(new CardDefinition(
            ManaCost.Parse("{3}"), null, 3, 3, CardType.Creature)
        {
            Name = "TestCastTrigger",
            Triggers = [new Trigger(GameEvent.SpellCast, TriggerCondition.SelfIsCast, new ExtraTurnEffect())],
        });

        var card = GameCard.Create("TestCastTrigger");
        card.ManaCost = ManaCost.Parse("{3}");
        card.CardTypes = CardType.Creature;
        state.Player1.Hand.Add(card);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 3);

        await engine.ExecuteAction(
            new GameAction { Type = ActionType.CastSpell, PlayerId = state.Player1.Id, CardId = card.Id },
            state.Player1, CancellationToken.None);

        // Extra turn should be queued from the SelfIsCast trigger
        state.ExtraTurns.Should().ContainSingle();

        CardDefinitions.Unregister("TestCastTrigger");
    }

    [Fact]
    public async Task SelfIsCast_WhenPutIntoPlayDirectly_TriggerDoesNotFire()
    {
        var (state, h1, h2) = TestHelper.CreateStateWithHandlers();

        // Directly add card to battlefield (simulating Show and Tell)
        var card = new GameCard
        {
            Name = "TestCastTrigger2",
            CardTypes = CardType.Creature,
            Triggers = [new Trigger(GameEvent.SpellCast, TriggerCondition.SelfIsCast, new ExtraTurnEffect())],
        };
        state.Player1.Battlefield.Add(card);

        // Extra turn should NOT be queued (wasn't cast)
        state.ExtraTurns.Should().BeEmpty();
    }
}
```

**Step 2: Implement SelfIsCast handling**

In `GameEngine.cs`, in the `CastSpell` handler, after the spell is pushed onto the stack and SpellCast board triggers fire (~line 576), add self-cast trigger checking:

```csharp
// Fire SelfIsCast triggers (e.g., Emrakul extra turn on cast)
await QueueSelfCastTriggersAsync(castCard, castPlayer, ct);
```

Create a helper method:

```csharp
private async Task QueueSelfCastTriggersAsync(GameCard card, Player controller, CancellationToken ct)
{
    if (!CardDefinitions.TryGet(card.Name, out var def)) return;
    foreach (var trigger in def.Triggers)
    {
        if (trigger.Event == GameEvent.SpellCast && trigger.Condition == TriggerCondition.SelfIsCast)
        {
            var ctx = new EffectContext(_state, controller, card, controller.DecisionHandler);
            await trigger.Effect.Execute(ctx, ct);
        }
    }
}
```

Note: This fires immediately (not on stack) because Emrakul's extra turn trigger isn't a triggered ability that uses the stack — it's a cast trigger that resolves before the spell itself. Actually in real MTG it IS a triggered ability that goes on the stack. But for simplicity, we can have it fire immediately or properly put it on the stack. For correctness, put it on the stack:

```csharp
private Task QueueSelfCastTriggersAsync(GameCard card, Player controller, CancellationToken ct)
{
    if (!CardDefinitions.TryGet(card.Name, out var def)) return Task.CompletedTask;
    foreach (var trigger in def.Triggers)
    {
        if (trigger.Event == GameEvent.SpellCast && trigger.Condition == TriggerCondition.SelfIsCast)
        {
            _state.StackPush(new TriggeredAbilityStackObject(card, controller.Id, trigger.Effect));
            _state.Log($"{card.Name}'s cast trigger goes on the stack.");
        }
    }
    return Task.CompletedTask;
}
```

**Step 3: Run tests + commit**

```bash
export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~SelfIsCastTests"
dotnet test tests/MtgDecker.Engine.Tests/
git add -A && git commit -m "feat(engine): add SelfIsCast trigger condition for Emrakul cast triggers"
```

---

## Task 7: Engine — Graveyard Shuffle Replacement + Protection from Colored Spells

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (zone transitions to graveyard, targeting validation)
- Test: `tests/MtgDecker.Engine.Tests/EmrakulMechanicsTests.cs`

**Step 1: Write tests**

Create `tests/MtgDecker.Engine.Tests/EmrakulMechanicsTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class EmrakulMechanicsTests
{
    [Fact]
    public void ShuffleGraveyardOnDeath_WhenDiscarded_ShufflesIntoLibrary()
    {
        var (state, _, _) = TestHelper.CreateStateWithHandlers();

        var emrakul = new GameCard
        {
            Name = "Emrakul",
            CardTypes = CardType.Creature,
        };
        // Simulate: Emrakul would go to graveyard
        state.Player1.Hand.Add(emrakul);
        state.Player1.Graveyard.Add(GameCard.Create("Other Card"));

        // Register with ShuffleGraveyardOnDeath
        CardDefinitions.Register(new CardDefinition(
            ManaCost.Parse("{15}"), null, 15, 15, CardType.Creature)
        {
            Name = "Emrakul",
            ShuffleGraveyardOnDeath = true,
        });

        // Use the helper that should check the flag
        var engine = new GameEngine(state);
        engine.MoveToGraveyardWithReplacement(emrakul, state.Player1);

        // Emrakul should NOT be in graveyard
        state.Player1.Graveyard.Cards.Should().NotContain(c => c.Name == "Emrakul");
        // Graveyard should have been shuffled into library (including "Other Card")
        state.Player1.Graveyard.Cards.Should().BeEmpty();
        // Library should contain both Emrakul and Other Card
        state.Player1.Library.Cards.Should().Contain(c => c.Name == "Emrakul");
        state.Player1.Library.Cards.Should().Contain(c => c.Name == "Other Card");

        CardDefinitions.Unregister("Emrakul");
    }

    [Fact]
    public void ProtectionFromColoredSpells_BlocksColoredTargeting()
    {
        var (state, _, _) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);

        var emrakul = new GameCard
        {
            Name = "Emrakul",
            CardTypes = CardType.Creature,
            ActiveKeywords = { Keyword.ProtectionFromColoredSpells },
        };
        state.Player2.Battlefield.Add(emrakul);

        // Swords to Plowshares costs {W} — it's colored
        var swords = GameCard.Create("Swords to Plowshares");
        swords.ManaCost = ManaCost.Parse("{W}");

        // Engine targeting check should reject this
        var canTarget = engine.CanTargetWithSpell(emrakul, swords);
        canTarget.Should().BeFalse();
    }

    [Fact]
    public void ProtectionFromColoredSpells_AllowsColorlessTargeting()
    {
        var (state, _, _) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);

        var emrakul = new GameCard
        {
            Name = "Emrakul",
            CardTypes = CardType.Creature,
            ActiveKeywords = { Keyword.ProtectionFromColoredSpells },
        };
        state.Player2.Battlefield.Add(emrakul);

        // A colorless spell should be able to target
        var colorlessSpell = GameCard.Create("All Is Dust");
        colorlessSpell.ManaCost = ManaCost.Parse("{7}");

        var canTarget = engine.CanTargetWithSpell(emrakul, colorlessSpell);
        canTarget.Should().BeTrue();
    }
}
```

**Step 2: Implement graveyard replacement and targeting checks**

Add `MoveToGraveyardWithReplacement` to `GameEngine.cs`:
```csharp
/// <summary>
/// Moves a card to graveyard, checking for replacement effects (e.g., Emrakul shuffle).
/// </summary>
public void MoveToGraveyardWithReplacement(GameCard card, Player owner)
{
    if (CardDefinitions.TryGet(card.Name, out var def) && def.ShuffleGraveyardOnDeath)
    {
        // Shuffle this card + entire graveyard into library
        owner.Library.AddToTop(card);
        foreach (var graveyardCard in owner.Graveyard.Cards.ToList())
        {
            owner.Graveyard.Remove(graveyardCard);
            owner.Library.AddToTop(graveyardCard);
        }
        owner.Library.Shuffle();
        _state.Log($"{card.Name}'s graveyard replacement — {owner.Name} shuffles their graveyard into their library.");
    }
    else
    {
        owner.Graveyard.Add(card);
    }
}
```

Add `CanTargetWithSpell` to `GameEngine.cs`:
```csharp
public bool CanTargetWithSpell(GameCard target, GameCard spell)
{
    if (target.ActiveKeywords.Contains(Keyword.ProtectionFromColoredSpells))
    {
        // Check if spell is colored (has non-colorless, non-generic mana in cost)
        if (spell.ManaCost != null && spell.ManaCost.IsColored)
            return false;
    }
    return true;
}
```

You'll also need to ensure `ManaCost.IsColored` property exists. Check `ManaCost.cs` — add if missing:
```csharp
public bool IsColored => ColorRequirements.Keys.Any(c => c != ManaColor.Colorless);
```

Then integrate these checks into the existing targeting validation in `CastSpell` and `ActivateAbility` handlers.

**Step 3: Run tests + commit**

```bash
export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~EmrakulMechanicsTests"
dotnet test tests/MtgDecker.Engine.Tests/
git add -A && git commit -m "feat(engine): add Emrakul graveyard shuffle replacement and protection from colored spells"
```

---

## Task 8: Effects — AnnihilatorEffect

**Files:**
- Create: `src/MtgDecker.Engine/Triggers/Effects/AnnihilatorEffect.cs`
- Test: `tests/MtgDecker.Engine.Tests/AnnihilatorTests.cs`

**Step 1: Write tests**

Create `tests/MtgDecker.Engine.Tests/AnnihilatorTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class AnnihilatorTests
{
    [Fact]
    public async Task Annihilator_DefenderSacrificesPermanents()
    {
        var (state, h1, h2) = TestHelper.CreateStateWithHandlers();

        // Defender has 3 permanents
        var land1 = GameCard.Create("Island", "Basic Land — Island");
        var land2 = GameCard.Create("Mountain", "Basic Land — Mountain");
        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature, Power = 2, Toughness = 2 };
        state.Player2.Battlefield.Add(land1);
        state.Player2.Battlefield.Add(land2);
        state.Player2.Battlefield.Add(creature);

        // Queue choices for defender: sacrifice all 3 (annihilator 3)
        h2.EnqueueCardChoice(land1.Id);
        h2.EnqueueCardChoice(land2.Id);
        h2.EnqueueCardChoice(creature.Id);

        var source = new GameCard { Name = "Emrakul", CardTypes = CardType.Creature };
        var ctx = new EffectContext(state, state.Player1, source, h1)
        {
            FireLeaveBattlefieldTriggers = _ => Task.CompletedTask,
        };

        var effect = new AnnihilatorEffect(3);
        await effect.Execute(ctx);

        state.Player2.Battlefield.Cards.Should().BeEmpty();
        state.Player2.Graveyard.Cards.Should().HaveCount(3);
    }

    [Fact]
    public async Task Annihilator_DefenderHasFewerPermanentsThanN_SacrificesAll()
    {
        var (state, h1, h2) = TestHelper.CreateStateWithHandlers();

        var land = GameCard.Create("Island", "Basic Land — Island");
        state.Player2.Battlefield.Add(land);

        h2.EnqueueCardChoice(land.Id);

        var source = new GameCard { Name = "Emrakul", CardTypes = CardType.Creature };
        var ctx = new EffectContext(state, state.Player1, source, h1)
        {
            FireLeaveBattlefieldTriggers = _ => Task.CompletedTask,
        };

        var effect = new AnnihilatorEffect(6);
        await effect.Execute(ctx);

        state.Player2.Battlefield.Cards.Should().BeEmpty();
        state.Player2.Graveyard.Cards.Should().HaveCount(1);
    }
}
```

**Step 2: Create AnnihilatorEffect**

Create `src/MtgDecker.Engine/Triggers/Effects/AnnihilatorEffect.cs`:

```csharp
namespace MtgDecker.Engine.Triggers.Effects;

public class AnnihilatorEffect(int count) : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var defender = context.State.GetOpponent(context.Controller);
        var sacrificed = 0;

        context.State.Log($"Annihilator {count} — {defender.Name} must sacrifice {count} permanent(s).");

        while (sacrificed < count && defender.Battlefield.Cards.Count > 0)
        {
            var eligible = defender.Battlefield.Cards.ToList();
            var chosenId = await defender.DecisionHandler.ChooseCard(
                eligible, $"Sacrifice a permanent (annihilator — {count - sacrificed} remaining)",
                optional: false, ct);

            if (!chosenId.HasValue) break;

            var card = defender.Battlefield.Cards.FirstOrDefault(c => c.Id == chosenId.Value);
            if (card == null) break;

            if (context.FireLeaveBattlefieldTriggers != null)
                await context.FireLeaveBattlefieldTriggers(card);

            defender.Battlefield.RemoveById(card.Id);
            defender.Graveyard.Add(card);
            context.State.Log($"{defender.Name} sacrifices {card.Name}.");
            sacrificed++;
        }
    }
}
```

**Step 3: Run tests + commit**

```bash
export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~AnnihilatorTests"
dotnet test tests/MtgDecker.Engine.Tests/
git add -A && git commit -m "feat(engine): add AnnihilatorEffect for Emrakul"
```

---

## Task 9: Effects — ShowAndTellEffect

**Files:**
- Create: `src/MtgDecker.Engine/Effects/ShowAndTellEffect.cs`
- Test: `tests/MtgDecker.Engine.Tests/ShowAndTellTests.cs`

**Step 1: Write tests**

```csharp
using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class ShowAndTellTests
{
    [Fact]
    public async Task ShowAndTell_BothPlayersChoosePermanent_BothEnterBattlefield()
    {
        var (state, h1, h2) = TestHelper.CreateStateWithHandlers();

        var emrakul = new GameCard { Name = "Emrakul", CardTypes = CardType.Creature, Power = 15, Toughness = 15 };
        state.Player1.Hand.Add(emrakul);

        var bear = new GameCard { Name = "Grizzly Bears", CardTypes = CardType.Creature, Power = 2, Toughness = 2 };
        state.Player2.Hand.Add(bear);

        h1.EnqueueCardChoice(emrakul.Id); // Caster chooses Emrakul
        h2.EnqueueCardChoice(bear.Id);     // Opponent chooses Bear

        var showAndTell = GameCard.Create("Show and Tell");
        var spell = new StackObject(showAndTell, state.Player1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);

        var effect = new ShowAndTellEffect();
        await effect.ResolveAsync(state, spell, h1);

        state.Player1.Battlefield.Cards.Should().Contain(c => c.Name == "Emrakul");
        state.Player1.Hand.Cards.Should().NotContain(c => c.Name == "Emrakul");
        state.Player2.Battlefield.Cards.Should().Contain(c => c.Name == "Grizzly Bears");
        state.Player2.Hand.Cards.Should().NotContain(c => c.Name == "Grizzly Bears");
    }

    [Fact]
    public async Task ShowAndTell_PlayerDeclinesChoosing_OnlyOtherCardEnters()
    {
        var (state, h1, h2) = TestHelper.CreateStateWithHandlers();

        var emrakul = new GameCard { Name = "Emrakul", CardTypes = CardType.Creature, Power = 15, Toughness = 15 };
        state.Player1.Hand.Add(emrakul);

        h1.EnqueueCardChoice(emrakul.Id);
        h2.EnqueueCardChoice((Guid?)null); // Opponent declines

        var showAndTell = GameCard.Create("Show and Tell");
        var spell = new StackObject(showAndTell, state.Player1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);

        var effect = new ShowAndTellEffect();
        await effect.ResolveAsync(state, spell, h1);

        state.Player1.Battlefield.Cards.Should().Contain(c => c.Name == "Emrakul");
        state.Player2.Battlefield.Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task ShowAndTell_OnlyPermanentsEligible_InstantsNotOffered()
    {
        var (state, h1, h2) = TestHelper.CreateStateWithHandlers();

        var bolt = new GameCard { Name = "Lightning Bolt", CardTypes = CardType.Instant };
        var bear = new GameCard { Name = "Bear", CardTypes = CardType.Creature, Power = 2, Toughness = 2 };
        state.Player1.Hand.Add(bolt);
        state.Player1.Hand.Add(bear);

        h1.EnqueueCardChoice(bear.Id); // Should only see Bear, not Bolt
        h2.EnqueueCardChoice((Guid?)null);

        var showAndTell = GameCard.Create("Show and Tell");
        var spell = new StackObject(showAndTell, state.Player1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);

        var effect = new ShowAndTellEffect();
        await effect.ResolveAsync(state, spell, h1);

        state.Player1.Battlefield.Cards.Should().Contain(c => c.Name == "Bear");
        state.Player1.Hand.Cards.Should().Contain(c => c.Name == "Lightning Bolt");
    }
}
```

**Step 2: Create ShowAndTellEffect**

Create `src/MtgDecker.Engine/Effects/ShowAndTellEffect.cs`:

```csharp
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Effects;

public class ShowAndTellEffect : SpellEffect
{
    private static bool IsPermanent(GameCard card) =>
        card.CardTypes.HasFlag(CardType.Creature) ||
        card.CardTypes.HasFlag(CardType.Artifact) ||
        card.CardTypes.HasFlag(CardType.Enchantment) ||
        card.CardTypes.HasFlag(CardType.Land) ||
        card.CardTypes.HasFlag(CardType.Planeswalker);

    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var caster = state.GetPlayer(spell.ControllerId);
        var opponent = state.GetOpponent(caster);

        // Caster chooses first
        GameCard? casterCard = null;
        var casterPermanents = caster.Hand.Cards.Where(IsPermanent).ToList();
        if (casterPermanents.Count > 0)
        {
            var casterChoice = await caster.DecisionHandler.ChooseCard(
                casterPermanents, "Choose a permanent card to put onto the battlefield", optional: true, ct);
            if (casterChoice.HasValue)
                casterCard = caster.Hand.Cards.FirstOrDefault(c => c.Id == casterChoice.Value);
        }

        // Opponent chooses
        GameCard? opponentCard = null;
        var opponentPermanents = opponent.Hand.Cards.Where(IsPermanent).ToList();
        if (opponentPermanents.Count > 0)
        {
            var opponentChoice = await opponent.DecisionHandler.ChooseCard(
                opponentPermanents, "Choose a permanent card to put onto the battlefield", optional: true, ct);
            if (opponentChoice.HasValue)
                opponentCard = opponent.Hand.Cards.FirstOrDefault(c => c.Id == opponentChoice.Value);
        }

        // Put both onto the battlefield simultaneously
        if (casterCard != null)
        {
            caster.Hand.RemoveById(casterCard.Id);
            caster.Battlefield.Add(casterCard);
            casterCard.TurnEnteredBattlefield = state.TurnNumber;
            if (casterCard.EntersTapped) casterCard.IsTapped = true;
            state.Log($"{caster.Name} puts {casterCard.Name} onto the battlefield.");
        }
        else
        {
            state.Log($"{caster.Name} chooses not to put a card onto the battlefield.");
        }

        if (opponentCard != null)
        {
            opponent.Hand.RemoveById(opponentCard.Id);
            opponent.Battlefield.Add(opponentCard);
            opponentCard.TurnEnteredBattlefield = state.TurnNumber;
            if (opponentCard.EntersTapped) opponentCard.IsTapped = true;
            state.Log($"{opponent.Name} puts {opponentCard.Name} onto the battlefield.");
        }
        else
        {
            state.Log($"{opponent.Name} chooses not to put a card onto the battlefield.");
        }
    }
}
```

**Note:** ETB triggers for cards put into play by Show and Tell will be handled by the engine's post-spell-resolution logic if it already checks for new permanents. If not, we'll need to add explicit ETB trigger firing here. Verify during implementation.

**Step 3: Run tests + commit**

```bash
export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~ShowAndTellTests"
dotnet test tests/MtgDecker.Engine.Tests/
git add -A && git commit -m "feat(engine): add ShowAndTellEffect — each player puts a permanent from hand"
```

---

## Task 10: Effects — SneakAttackPutEffect

**Files:**
- Create: `src/MtgDecker.Engine/Triggers/Effects/SneakAttackPutEffect.cs`
- Test: `tests/MtgDecker.Engine.Tests/SneakAttackTests.cs`

**Step 1: Write tests**

```csharp
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class SneakAttackTests
{
    [Fact]
    public async Task SneakAttack_PutsCreatureFromHand()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();

        var creature = new GameCard { Name = "Emrakul", CardTypes = CardType.Creature, Power = 15, Toughness = 15 };
        state.Player1.Hand.Add(creature);

        h1.EnqueueCardChoice(creature.Id);

        var source = new GameCard { Name = "Sneak Attack", CardTypes = CardType.Enchantment };
        var ctx = new EffectContext(state, state.Player1, source, h1);

        var effect = new SneakAttackPutEffect();
        await effect.Execute(ctx);

        state.Player1.Battlefield.Cards.Should().Contain(c => c.Name == "Emrakul");
        state.Player1.Hand.Cards.Should().NotContain(c => c.Name == "Emrakul");
    }

    [Fact]
    public async Task SneakAttack_CreatureGainsHaste()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();

        var creature = new GameCard { Name = "Emrakul", CardTypes = CardType.Creature };
        state.Player1.Hand.Add(creature);
        h1.EnqueueCardChoice(creature.Id);

        var source = new GameCard { Name = "Sneak Attack", CardTypes = CardType.Enchantment };
        var ctx = new EffectContext(state, state.Player1, source, h1);

        var effect = new SneakAttackPutEffect();
        await effect.Execute(ctx);

        // Haste should be granted via continuous effect
        state.ActiveEffects.Should().Contain(e =>
            e.Type == ContinuousEffectType.GrantKeyword
            && e.GrantedKeyword == Keyword.Haste);
    }

    [Fact]
    public async Task SneakAttack_RegistersEndOfTurnSacrifice()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();

        var creature = new GameCard { Name = "Emrakul", CardTypes = CardType.Creature };
        state.Player1.Hand.Add(creature);
        h1.EnqueueCardChoice(creature.Id);

        var source = new GameCard { Name = "Sneak Attack", CardTypes = CardType.Enchantment };
        var ctx = new EffectContext(state, state.Player1, source, h1);

        var effect = new SneakAttackPutEffect();
        await effect.Execute(ctx);

        state.DelayedTriggers.Should().ContainSingle(d => d.FireOn == GameEvent.EndStep);
    }

    [Fact]
    public async Task SneakAttack_NoCreaturesInHand_DoesNothing()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();

        // Only a non-creature in hand
        var sorcery = new GameCard { Name = "Ponder", CardTypes = CardType.Sorcery };
        state.Player1.Hand.Add(sorcery);

        var source = new GameCard { Name = "Sneak Attack", CardTypes = CardType.Enchantment };
        var ctx = new EffectContext(state, state.Player1, source, h1);

        var effect = new SneakAttackPutEffect();
        await effect.Execute(ctx);

        state.Player1.Battlefield.Cards.Should().BeEmpty();
    }
}
```

**Step 2: Create SneakAttackPutEffect**

Create `src/MtgDecker.Engine/Triggers/Effects/SneakAttackPutEffect.cs`:

```csharp
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class SneakAttackPutEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var creatures = context.Controller.Hand.Cards
            .Where(c => c.IsCreature)
            .ToList();

        if (creatures.Count == 0)
        {
            context.State.Log("No creature cards in hand.");
            return;
        }

        var chosenId = await context.DecisionHandler.ChooseCard(
            creatures, "Put a creature card onto the battlefield", optional: true, ct);

        if (!chosenId.HasValue) return;

        var chosen = context.Controller.Hand.RemoveById(chosenId.Value);
        if (chosen == null) return;

        context.Controller.Battlefield.Add(chosen);
        chosen.TurnEnteredBattlefield = context.State.TurnNumber;
        context.State.Log($"{context.Controller.Name} puts {chosen.Name} onto the battlefield with Sneak Attack.");

        // Grant haste
        context.State.ActiveEffects.Add(new ContinuousEffect(
            chosen.Id,
            ContinuousEffectType.GrantKeyword,
            (card, _) => card.Id == chosen.Id,
            GrantedKeyword: Keyword.Haste,
            Layer: EffectLayer.Layer6_AbilityAddRemove));

        // Register end-of-turn sacrifice
        context.State.DelayedTriggers.Add(new DelayedTrigger(
            GameEvent.EndStep,
            new SacrificeSpecificCardEffect(chosen.Id),
            context.Controller.Id));
    }
}
```

**Step 3: Run tests + commit**

```bash
export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~SneakAttackTests"
dotnet test tests/MtgDecker.Engine.Tests/
git add -A && git commit -m "feat(engine): add SneakAttackPutEffect — put creature, haste, EOT sacrifice"
```

---

## Task 11: Effects — AddAnyManaEffect + BounceTargetEffect + NoncreatureSpell TargetFilter

**Files:**
- Create: `src/MtgDecker.Engine/Triggers/Effects/AddAnyManaEffect.cs`
- Create: `src/MtgDecker.Engine/Effects/BounceTargetEffect.cs`
- Modify: `src/MtgDecker.Engine/TargetFilter.cs`
- Test: `tests/MtgDecker.Engine.Tests/UtilityEffectTests.cs`

**Step 1: Write tests + implement**

Create `AddAnyManaEffect`:
```csharp
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class AddAnyManaEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var colors = new List<ManaColor>
            { ManaColor.White, ManaColor.Blue, ManaColor.Black, ManaColor.Red, ManaColor.Green };

        var chosen = await context.DecisionHandler.ChooseManaColor(colors, ct);
        context.Controller.ManaPool.Add(chosen);
        context.State.Log($"{context.Controller.Name} adds {chosen} mana.");
    }
}
```

Create `BounceTargetEffect`:
```csharp
namespace MtgDecker.Engine.Effects;

public class BounceTargetEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        if (spell.Targets.Count == 0) return;
        var target = spell.Targets[0];

        var owner = state.GetPlayer(target.OwnerId);
        var card = owner.Battlefield.Cards.FirstOrDefault(c => c.Id == target.CardId);
        if (card == null)
        {
            state.Log($"{spell.Card.Name} fizzles (target no longer on battlefield).");
            return;
        }

        owner.Battlefield.RemoveById(card.Id);
        owner.Hand.Add(card);
        card.IsTapped = false;
        state.Log($"{card.Name} is returned to {owner.Name}'s hand.");
    }
}
```

Add to `TargetFilter.cs`:
```csharp
public static TargetFilter NoncreatureSpell() => new((card, zone) =>
    zone == ZoneType.Stack && !card.CardTypes.HasFlag(CardType.Creature));

public static TargetFilter InstantOrSorcerySpell() => new((card, zone) =>
    zone == ZoneType.Stack &&
    (card.CardTypes.HasFlag(CardType.Instant) || card.CardTypes.HasFlag(CardType.Sorcery)));
```

Write tests, run them, commit.

```bash
git add -A && git commit -m "feat(engine): add AddAnyManaEffect, BounceTargetEffect, NoncreatureSpell TargetFilter"
```

---

## Task 12: CardDefinitions — Register All Main Deck Cards

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Test: `tests/MtgDecker.Engine.Tests/SneakShowCardRegistrationTests.cs`

**Step 1: Register all new cards**

Add a new section in `CardDefinitions.cs` after the Dimir Tempo section:

```csharp
// ─── Legacy Sneak and Show ──────────────────────────────────────────

["Show and Tell"] = new(ManaCost.Parse("{1}{U}{U}"), null, null, null, CardType.Sorcery,
    Effect: new ShowAndTellEffect()),

["Sneak Attack"] = new(ManaCost.Parse("{3}{R}"), null, null, null, CardType.Enchantment)
{
    ActivatedAbility = new(
        new ActivatedAbilityCost(ManaCost: ManaCost.Parse("{R}")),
        new SneakAttackPutEffect()),
},

["Emrakul, the Aeons Torn"] = new(ManaCost.Parse("{15}"), null, 15, 15, CardType.Creature)
{
    IsLegendary = true,
    Subtypes = ["Eldrazi"],
    ShuffleGraveyardOnDeath = true,
    ContinuousEffects =
    [
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
            (card, _) => card.Name == "Emrakul, the Aeons Torn",
            GrantedKeyword: Keyword.Flying,
            Layer: EffectLayer.Layer6_AbilityAddRemove),
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
            (card, _) => card.Name == "Emrakul, the Aeons Torn",
            GrantedKeyword: Keyword.ProtectionFromColoredSpells,
            Layer: EffectLayer.Layer6_AbilityAddRemove),
    ],
    Triggers =
    [
        new Trigger(GameEvent.SpellCast, TriggerCondition.SelfIsCast, new ExtraTurnEffect()),
        new Trigger(GameEvent.BeginCombat, TriggerCondition.SelfAttacks, new AnnihilatorEffect(6)),
    ],
},

["Griselbrand"] = new(ManaCost.Parse("{4}{B}{B}{B}{B}"), null, 7, 7, CardType.Creature)
{
    IsLegendary = true,
    Subtypes = ["Demon"],
    ContinuousEffects =
    [
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
            (card, _) => card.Name == "Griselbrand",
            GrantedKeyword: Keyword.Flying,
            Layer: EffectLayer.Layer6_AbilityAddRemove),
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
            (card, _) => card.Name == "Griselbrand",
            GrantedKeyword: Keyword.Lifelink,
            Layer: EffectLayer.Layer6_AbilityAddRemove),
    ],
    ActivatedAbility = new(
        new ActivatedAbilityCost(PayLife: 7),
        new DrawCardsActivatedEffect(7)),
},

["Lotus Petal"] = new(ManaCost.Parse("{0}"), null, null, null, CardType.Artifact)
{
    ActivatedAbility = new(
        new ActivatedAbilityCost(TapSelf: true, SacrificeSelf: true),
        new AddAnyManaEffect()),
},

["Spell Pierce"] = new(ManaCost.Parse("{U}"), null, null, null, CardType.Instant,
    TargetFilter.NoncreatureSpell(), new ConditionalCounterEffect(2)),

["Ancient Tomb"] = new(null, ManaAbility.FixedMultiple(ManaColor.Colorless, 2, selfDamage: 2),
    null, null, CardType.Land),

["City of Traitors"] = new(null, ManaAbility.FixedMultiple(ManaColor.Colorless, 2),
    null, null, CardType.Land)
{
    Triggers =
    [
        new Trigger(GameEvent.LandPlayed, TriggerCondition.ControllerPlaysAnotherLand,
            new SacrificeSelfOnLandEffect()),
    ],
},

["Intuition"] = new(ManaCost.Parse("{2}{U}"), null, null, null, CardType.Instant,
    Effect: new IntuitionEffect()),
```

**Step 2: Write registration tests**

```csharp
using FluentAssertions;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class SneakShowCardRegistrationTests
{
    [Theory]
    [InlineData("Show and Tell")]
    [InlineData("Sneak Attack")]
    [InlineData("Emrakul, the Aeons Torn")]
    [InlineData("Griselbrand")]
    [InlineData("Lotus Petal")]
    [InlineData("Spell Pierce")]
    [InlineData("Ancient Tomb")]
    [InlineData("City of Traitors")]
    [InlineData("Intuition")]
    public void Card_IsRegistered(string cardName)
    {
        CardDefinitions.TryGet(cardName, out var def).Should().BeTrue($"'{cardName}' should be registered");
        def.Should().NotBeNull();
    }

    [Fact]
    public void Emrakul_HasCorrectProperties()
    {
        CardDefinitions.TryGet("Emrakul, the Aeons Torn", out var def).Should().BeTrue();
        def!.Power.Should().Be(15);
        def.Toughness.Should().Be(15);
        def.IsLegendary.Should().BeTrue();
        def.ShuffleGraveyardOnDeath.Should().BeTrue();
        def.Subtypes.Should().Contain("Eldrazi");
        def.Triggers.Should().HaveCount(2);
        def.ContinuousEffects.Should().HaveCount(2);
    }

    [Fact]
    public void Griselbrand_HasPayLifeAbility()
    {
        CardDefinitions.TryGet("Griselbrand", out var def).Should().BeTrue();
        def!.ActivatedAbility.Should().NotBeNull();
        def.ActivatedAbility!.Cost.PayLife.Should().Be(7);
    }

    [Fact]
    public void AncientTomb_ProducesTwoColorlessWith2Damage()
    {
        CardDefinitions.TryGet("Ancient Tomb", out var def).Should().BeTrue();
        def!.ManaAbility.Should().NotBeNull();
        def.ManaAbility!.ProduceCount.Should().Be(2);
        def.ManaAbility.SelfDamage.Should().Be(2);
    }

    [Fact]
    public void CityOfTraitors_HasLandPlayedTrigger()
    {
        CardDefinitions.TryGet("City of Traitors", out var def).Should().BeTrue();
        def!.Triggers.Should().ContainSingle();
        def.Triggers[0].Event.Should().Be(GameEvent.LandPlayed);
    }
}
```

**Step 3: Run tests + commit**

```bash
export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~SneakShowCardRegistrationTests"
dotnet test tests/MtgDecker.Engine.Tests/
git add -A && git commit -m "feat(engine): register all Sneak and Show main deck cards in CardDefinitions"
```

---

## Task 13: Effects — IntuitionEffect

**Files:**
- Create: `src/MtgDecker.Engine/Effects/IntuitionEffect.cs`
- Test: `tests/MtgDecker.Engine.Tests/IntuitionTests.cs`

**Step 1: Write tests + implement**

`IntuitionEffect` searches library for 3 cards, opponent picks 1 for caster's hand, rest go to graveyard. This needs multiple `ChooseCard` calls. The implementation should:
1. Let caster search library and pick 3 cards (3 successive `ChooseCard` calls)
2. Reveal all 3 to opponent
3. Opponent chooses 1 of the 3
4. Chosen card → caster's hand, other 2 → caster's graveyard

```bash
git add -A && git commit -m "feat(engine): add IntuitionEffect — search 3, opponent picks 1"
```

---

## Task 14: Sideboard Cards — Blood Moon + Simple Stubs

**Files:**
- Create: `src/MtgDecker.Engine/Effects/PyroblastEffect.cs`
- Create: `src/MtgDecker.Engine/Effects/SurgicalExtractionEffect.cs`
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Modify: `src/MtgDecker.Engine/ContinuousEffect.cs` (add OverrideLandType)
- Test: `tests/MtgDecker.Engine.Tests/BloodMoonTests.cs`
- Test: `tests/MtgDecker.Engine.Tests/SneakShowSideboardTests.cs`

**Step 1: Blood Moon continuous effect**

Add `OverrideLandType` to `ContinuousEffectType`:
```csharp
OverrideLandType,
```

Blood Moon registration:
```csharp
["Blood Moon"] = new(ManaCost.Parse("{2}{R}"), null, null, null, CardType.Enchantment)
{
    ContinuousEffects =
    [
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.OverrideLandType,
            (card, _) => card.IsLand && !card.IsBasicLand,
            Layer: EffectLayer.Layer4_TypeChanging),
    ],
},
```

Engine change: In `RecalculateState()` or wherever continuous effects are applied, handle `OverrideLandType` by setting affected lands' `ManaAbility` to `ManaAbility.Fixed(ManaColor.Red)` and adding "Mountain" subtype.

**Step 2: Register remaining sideboard cards**

```csharp
["Pyroclasm"] = new(ManaCost.Parse("{1}{R}"), null, null, null, CardType.Sorcery,
    Effect: new DamageAllCreaturesEffect(2)),

["Flusterstorm"] = new(ManaCost.Parse("{U}"), null, null, null, CardType.Instant,
    TargetFilter.InstantOrSorcerySpell(), new ConditionalCounterEffect(1)),
    // Note: Storm keyword not implemented — simplified as single counter

["Pyroblast"] = new(ManaCost.Parse("{R}"), null, null, null, CardType.Instant,
    TargetFilter.Spell(), new PyroblastEffect()),

["Surgical Extraction"] = new(ManaCost.Parse("{B}"), null, null, null, CardType.Instant)
{
    AlternateCost = new AlternateCost(LifeCost: 2),
    // Stub: targeting and exile logic simplified
},

["Grafdigger's Cage"] = new(ManaCost.Parse("{1}"), null, null, null, CardType.Artifact),
    // Stub: continuous effect preventing graveyard/library creature entry not implemented

["Wipe Away"] = new(ManaCost.Parse("{1}{U}{U}"), null, null, null, CardType.Instant,
    TargetFilter.AnyPermanent(), new BounceTargetEffect()),
    // Note: Split second not implemented
```

**Step 3: Write tests for Blood Moon + sideboard registration**

Test Blood Moon: nonbasic land becomes Mountain (produces red only). Basic lands unaffected.

Test sideboard cards are registered.

**Step 4: Run tests + commit**

```bash
git add -A && git commit -m "feat(engine): add Blood Moon, sideboard cards (Pyroclasm, Flusterstorm, Pyroblast, Surgical, Cage, Wipe Away)"
```

---

## Task 15: Integration Tests — Full Sneak and Show Scenarios

**Files:**
- Test: `tests/MtgDecker.Engine.Tests/SneakShowIntegrationTests.cs`

Write integration tests covering:
1. **Show and Tell into Emrakul** — cast Show and Tell, choose Emrakul, verify on battlefield (no extra turn since not cast)
2. **Sneak Attack + Emrakul** — activate Sneak Attack, choose Emrakul, attack with annihilator 6, verify sacrifice + EOT sacrifice
3. **Cast Emrakul naturally** — pay 15 mana, verify extra turn trigger
4. **Griselbrand draw 7** — Sneak Attack → Griselbrand, activate ability, verify 7 cards drawn and 7 life paid
5. **Lotus Petal into Show and Tell** — T1 Island, Lotus Petal, sacrifice for U, cast Show and Tell with {1}{U}{U}
6. **City of Traitors sacrifice** — play City, next turn play Island, City sacrificed
7. **Ancient Tomb damage** — tap for 2 colorless, take 2 damage

```bash
git add -A && git commit -m "test(engine): add Sneak and Show integration tests"
```

---

## Task 16: Copy design doc to worktree + final verification

**Step 1:** Copy the design doc from the main repo to the worktree (it was written before the worktree was created):

```bash
cp docs/plans/2026-02-19-sneak-and-show-design.md .worktrees/sneak-and-show/docs/plans/
```

**Step 2:** Run full test suite one final time:

```bash
cd .worktrees/sneak-and-show && export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/
```

**Step 3:** Commit the design doc:

```bash
git add docs/plans/2026-02-19-sneak-and-show-design.md && git commit -m "docs: add Sneak and Show design document"
```

---

## Summary

| Task | What | Key Files |
|------|------|-----------|
| 1 | Infrastructure enums + properties | Keyword, GameEvent, TriggerCondition, ActivatedAbilityCost, ManaAbility, GameState, CardDefinition |
| 2 | Multi-mana + self-damage on TapCard | GameEngine.cs |
| 3 | PayLife activated ability cost | GameEngine.cs, DrawCardsActivatedEffect |
| 4 | Extra turns | GameEngine.cs, ExtraTurnEffect |
| 5 | LandPlayed event + City of Traitors | GameEngine.cs, SacrificeSelfOnLandEffect |
| 6 | SelfIsCast trigger condition | GameEngine.cs |
| 7 | Graveyard shuffle + protection colored | GameEngine.cs, ManaCost |
| 8 | AnnihilatorEffect | New effect |
| 9 | ShowAndTellEffect | New SpellEffect |
| 10 | SneakAttackPutEffect | New effect |
| 11 | Utility effects + TargetFilters | AddAnyManaEffect, BounceTargetEffect, TargetFilter |
| 12 | CardDefinitions registration | CardDefinitions.cs |
| 13 | IntuitionEffect | New SpellEffect |
| 14 | Blood Moon + sideboard | ContinuousEffect, CardDefinitions |
| 15 | Integration tests | Full scenarios |
| 16 | Final verification | Design doc, full suite |
