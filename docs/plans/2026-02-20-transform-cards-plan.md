# Transform Cards + Tamiyo Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add transform card engine mechanic and register Tamiyo, Inquisitive Student // Tamiyo, Seasoned Scholar with full mechanics (investigate, 3rd-draw transform trigger, planeswalker back face with loyalty abilities).

**Architecture:** GameCard gets IsTransformed + BackFaceDefinition; when transformed, properties delegate to back face. CardDefinition gets TransformInto for back face definition. New effects: TransformExileReturnEffect, InvestigateEffect (Clue tokens), TamiyoDefenseEffect (+2), TamiyoRecoverEffect (-3), TamiyoUltimateEffect (-7). New "until next turn" duration on ContinuousEffect.

**Tech Stack:** .NET 10, C# 14, xUnit, FluentAssertions

---

### Task 1: GameCard Transform Infrastructure

**Files:**
- Modify: `src/MtgDecker.Engine/GameCard.cs`
- Test: `tests/MtgDecker.Engine.Tests/TransformTests.cs`

**Step 1: Write failing tests**

```csharp
// TransformTests.cs
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class TransformTests
{
    [Fact]
    public void GameCard_IsTransformed_DefaultsFalse()
    {
        var card = new GameCard { Name = "Test" };
        card.IsTransformed.Should().BeFalse();
    }

    [Fact]
    public void GameCard_BackFaceDefinition_DefaultsNull()
    {
        var card = new GameCard { Name = "Test" };
        card.BackFaceDefinition.Should().BeNull();
    }

    [Fact]
    public void GameCard_WhenTransformed_NameFromBackFace()
    {
        var backFace = new CardDefinition(null, null, 2, 3, CardType.Creature)
        { Name = "Back Face" };
        var card = new GameCard
        {
            Name = "Front Face",
            CardTypes = CardType.Creature,
            BasePower = 1,
            BaseToughness = 1,
            BackFaceDefinition = backFace,
        };

        card.IsTransformed = true;

        card.Name.Should().Be("Back Face");
    }

    [Fact]
    public void GameCard_WhenTransformed_CardTypesFromBackFace()
    {
        var backFace = new CardDefinition(null, null, null, null, CardType.Planeswalker)
        { Name = "PW Back", StartingLoyalty = 3 };
        var card = new GameCard
        {
            Name = "Creature Front",
            CardTypes = CardType.Creature,
            BasePower = 0,
            BaseToughness = 3,
            BackFaceDefinition = backFace,
        };

        card.IsTransformed = true;

        card.IsPlaneswalker.Should().BeTrue();
        card.IsCreature.Should().BeFalse();
    }

    [Fact]
    public void GameCard_WhenTransformed_PowerToughnessFromBackFace()
    {
        var backFace = new CardDefinition(null, null, 5, 5, CardType.Creature)
        { Name = "Big Back" };
        var card = new GameCard
        {
            Name = "Small Front",
            CardTypes = CardType.Creature,
            BasePower = 1,
            BaseToughness = 1,
            BackFaceDefinition = backFace,
        };

        card.IsTransformed = true;

        card.BasePower.Should().Be(5);
        card.BaseToughness.Should().Be(5);
    }

    [Fact]
    public void GameCard_WhenNotTransformed_UsesOwnProperties()
    {
        var backFace = new CardDefinition(null, null, 5, 5, CardType.Creature)
        { Name = "Back" };
        var card = new GameCard
        {
            Name = "Front",
            CardTypes = CardType.Creature,
            BasePower = 1,
            BaseToughness = 1,
            BackFaceDefinition = backFace,
        };

        card.Name.Should().Be("Front");
        card.BasePower.Should().Be(1);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "TransformTests" -v minimal`
Expected: FAIL — properties don't exist yet

**Step 3: Implement transform properties on GameCard**

Add to `GameCard.cs`:
- `public bool IsTransformed { get; set; }`
- `public CardDefinition? BackFaceDefinition { get; set; }`
- Rename existing `Name` to `_frontName` backing field; make `Name` a property that returns back face name when transformed
- Similarly for `CardTypes`, `BasePower`, `BaseToughness`

The key design: GameCard stores front-face values directly. When `IsTransformed && BackFaceDefinition != null`, property getters return back face values instead.

Implementation approach — add backing fields for front-face values and make public properties delegate:
```csharp
private string _frontName = string.Empty;
public string Name
{
    get => IsTransformed && BackFaceDefinition != null ? BackFaceDefinition.Name : _frontName;
    set => _frontName = value;
}

private CardType _frontCardTypes;
public CardType CardTypes
{
    get => IsTransformed && BackFaceDefinition != null ? BackFaceDefinition.CardTypes : _frontCardTypes;
    set => _frontCardTypes = value;
}

private int? _frontBasePower;
public int? BasePower
{
    get => IsTransformed && BackFaceDefinition != null ? BackFaceDefinition.Power : _frontBasePower;
    set => _frontBasePower = value;
}

private int? _frontBaseToughness;
public int? BaseToughness
{
    get => IsTransformed && BackFaceDefinition != null ? BackFaceDefinition.Toughness : _frontBaseToughness;
    set => _frontBaseToughness = value;
}
```

Note: `FrontName` property needed for zones other than battlefield (graveyard, hand show front face name).

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "TransformTests" -v minimal`
Expected: PASS

**Step 5: Commit**

```bash
git add tests/MtgDecker.Engine.Tests/TransformTests.cs src/MtgDecker.Engine/GameCard.cs
git commit -m "feat(engine): add transform infrastructure — IsTransformed, BackFaceDefinition, property delegation"
```

---

### Task 2: CardDefinition.TransformInto + GameCard.Create Integration

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinition.cs`
- Modify: `src/MtgDecker.Engine/GameCard.cs` (Create method)
- Test: `tests/MtgDecker.Engine.Tests/TransformTests.cs` (add tests)

**Step 1: Write failing tests**

```csharp
[Fact]
public void GameCardCreate_WithTransformDefinition_SetsBackFaceDefinition()
{
    // Register a test transform card
    var backFace = new CardDefinition(null, null, 5, 5, CardType.Planeswalker)
    { Name = "Test Back Face", StartingLoyalty = 3 };
    CardDefinitions.Register(new CardDefinition(null, null, 0, 3, CardType.Creature)
    { Name = "Test Transform Card", TransformInto = backFace });

    try
    {
        var card = GameCard.Create("Test Transform Card");
        card.BackFaceDefinition.Should().NotBeNull();
        card.BackFaceDefinition!.Name.Should().Be("Test Back Face");
    }
    finally
    {
        CardDefinitions.Unregister("Test Transform Card");
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "GameCardCreate_WithTransformDefinition" -v minimal`

**Step 3: Implement**

Add to `CardDefinition.cs`:
```csharp
public CardDefinition? TransformInto { get; init; }
```

In `GameCard.Create()` (the factory method that reads from CardDefinitions), set `BackFaceDefinition = def.TransformInto` when creating the card.

**Step 4: Run tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "TransformTests" -v minimal`

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/CardDefinition.cs src/MtgDecker.Engine/GameCard.cs tests/MtgDecker.Engine.Tests/TransformTests.cs
git commit -m "feat(engine): add CardDefinition.TransformInto and wire up in GameCard.Create"
```

---

### Task 3: TransformExileReturnEffect

**Files:**
- Create: `src/MtgDecker.Engine/Triggers/Effects/TransformExileReturnEffect.cs`
- Modify: `tests/MtgDecker.Engine.Tests/TransformTests.cs` (add tests)

**Step 1: Write failing tests**

```csharp
[Fact]
public async Task TransformExileReturnEffect_ExilesAndReturnsTransformed()
{
    var h1 = new TestDecisionHandler();
    var h2 = new TestDecisionHandler();
    var p1 = new Player(Guid.NewGuid(), "P1", h1);
    var p2 = new Player(Guid.NewGuid(), "P2", h2);
    var state = new GameState(p1, p2);
    var engine = new GameEngine(state);

    var backFace = new CardDefinition(null, null, null, null, CardType.Planeswalker)
    { Name = "PW Back", StartingLoyalty = 3 };
    var card = new GameCard
    {
        Name = "Creature Front",
        CardTypes = CardType.Creature,
        BasePower = 0,
        BaseToughness = 3,
        BackFaceDefinition = backFace,
    };
    p1.Battlefield.Add(card);

    var effect = new TransformExileReturnEffect();
    var context = new EffectContext(state, engine, card, p1, h1);
    await effect.Execute(context);

    // Card should be on battlefield, transformed
    p1.Battlefield.Cards.Should().Contain(c => c.Id == card.Id);
    card.IsTransformed.Should().BeTrue();
    card.IsPlaneswalker.Should().BeTrue();
}

[Fact]
public async Task TransformExileReturnEffect_GainsLoyaltyCounters()
{
    // Same setup as above
    // After transform, if back face is planeswalker with StartingLoyalty,
    // should have loyalty counters
    var h1 = new TestDecisionHandler();
    var h2 = new TestDecisionHandler();
    var p1 = new Player(Guid.NewGuid(), "P1", h1);
    var p2 = new Player(Guid.NewGuid(), "P2", h2);
    var state = new GameState(p1, p2);
    var engine = new GameEngine(state);

    var backFace = new CardDefinition(null, null, null, null, CardType.Planeswalker)
    { Name = "PW Back", StartingLoyalty = 2 };
    var card = new GameCard
    {
        Name = "Creature Front",
        CardTypes = CardType.Creature,
        BasePower = 0,
        BaseToughness = 3,
        BackFaceDefinition = backFace,
    };
    p1.Battlefield.Add(card);

    var effect = new TransformExileReturnEffect();
    var context = new EffectContext(state, engine, card, p1, h1);
    await effect.Execute(context);

    card.Loyalty.Should().Be(2);
}
```

**Step 2: Run tests to verify failure**

**Step 3: Implement TransformExileReturnEffect**

```csharp
public class TransformExileReturnEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var card = context.Source;
        var controller = context.Controller;
        var state = context.State;

        // Remove from battlefield
        controller.Battlefield.Remove(card);

        // Transform
        card.IsTransformed = true;

        // Return to battlefield
        controller.Battlefield.Add(card);

        // If back face is a planeswalker with starting loyalty, add loyalty counters
        if (card.BackFaceDefinition?.StartingLoyalty is int loyalty)
        {
            card.AddCounters(CounterType.Loyalty, loyalty);
        }

        state.Log($"{card.Name} transforms!");

        // Recalculate (handles ETB for transformed card)
        context.Engine.RecalculateState();

        return Task.CompletedTask;
    }
}
```

Note: We skip exile zone (we don't have an explicit exile zone per player currently) — just remove and re-add to battlefield. The key is setting IsTransformed and adding loyalty counters.

**Step 4: Run tests**
**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/Triggers/Effects/TransformExileReturnEffect.cs tests/MtgDecker.Engine.Tests/TransformTests.cs
git commit -m "feat(engine): add TransformExileReturnEffect — exile and return transformed"
```

---

### Task 4: InvestigateEffect (Create Clue Token)

**Files:**
- Create: `src/MtgDecker.Engine/Triggers/Effects/InvestigateEffect.cs`
- Test: `tests/MtgDecker.Engine.Tests/InvestigateTests.cs`

**Step 1: Write failing tests**

```csharp
// InvestigateTests.cs
public class InvestigateTests
{
    [Fact]
    public async Task InvestigateEffect_CreatesClueToken()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        var source = new GameCard { Name = "Investigator" };
        p1.Battlefield.Add(source);

        var effect = new InvestigateEffect();
        var context = new EffectContext(state, engine, source, p1, h1);
        await effect.Execute(context);

        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Clue" && c.IsToken);
    }

    [Fact]
    public async Task InvestigateEffect_ClueIsArtifact()
    {
        // setup...
        var effect = new InvestigateEffect();
        // execute...
        var clue = p1.Battlefield.Cards.First(c => c.Name == "Clue");
        clue.CardTypes.Should().Be(CardType.Artifact);
    }
}
```

**Step 2: Run tests to verify failure**

**Step 3: Implement InvestigateEffect**

```csharp
public class InvestigateEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var controller = context.Controller;
        var state = context.State;

        var clue = new GameCard
        {
            Name = "Clue",
            CardTypes = CardType.Artifact,
            IsToken = true,
        };
        controller.Battlefield.Add(clue);
        state.Log($"{controller.Name} investigates and creates a Clue token.");

        return Task.CompletedTask;
    }
}
```

Note: For now, Clue tokens don't have the {2} sacrifice-to-draw ability. We'll add that as a separate task since it requires the ActivateAbility action handler.

**Step 4: Run tests**
**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/Triggers/Effects/InvestigateEffect.cs tests/MtgDecker.Engine.Tests/InvestigateTests.cs
git commit -m "feat(engine): add InvestigateEffect — creates Clue artifact token"
```

---

### Task 5: Clue Token Sacrifice-to-Draw (ActionType.ActivateAbility)

**Files:**
- Modify: `src/MtgDecker.Engine/Enums/ActionType.cs`
- Modify: `src/MtgDecker.Engine/GameAction.cs`
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (ExecuteAction handler)
- Modify: `src/MtgDecker.Engine/Triggers/Effects/InvestigateEffect.cs` (add ActivatedAbility to Clue)
- Modify: `src/MtgDecker.Engine/CardDefinition.cs` (if ActivatedAbility needs enhancement)
- Test: `tests/MtgDecker.Engine.Tests/InvestigateTests.cs` (add sacrifice test)

**Step 1: Write failing tests**

```csharp
[Fact]
public async Task ClueToken_CanBeSacrificedToDraw()
{
    // Setup with game started, clue on battlefield, mana available
    // Execute ActivateAbility action on the Clue
    // Assert: Clue removed from battlefield, player drew a card
}

[Fact]
public async Task ClueToken_RequiresTwoManaToSacrifice()
{
    // Setup with no mana available
    // Execute ActivateAbility on Clue
    // Assert: nothing happens (can't pay cost)
}
```

**Step 2: Run tests to verify failure**

**Step 3: Implement**

Add `ActionType.ActivateAbility` to the enum.

Add to `GameAction`:
```csharp
public static GameAction ActivateAbility(Guid playerId, Guid cardId)
    => new() { Type = ActionType.ActivateAbility, PlayerId = playerId, CardId = cardId };
```

Add `ActivatedAbility` property to `GameCard`:
```csharp
public ActivatedAbility? TokenActivatedAbility { get; set; }
```

In `GameEngine.ExecuteAction`, add handler for `ActionType.ActivateAbility`:
- Find card on player's battlefield
- Check if card has an activated ability (from CardDefinition or TokenActivatedAbility)
- Check if player can pay the mana cost
- Pay mana, execute the ability effect
- For Clue specifically: sacrifice (remove from battlefield, tokens cease to exist), draw a card

The ActivatedAbility record already exists. For Clue tokens, set `TokenActivatedAbility` on the GameCard when creating it in InvestigateEffect. The ability effect is a `SacrificeAndDrawEffect`.

Create `SacrificeAndDrawEffect` that removes the source from battlefield and draws a card.

Update InvestigateEffect to set `TokenActivatedAbility` on the Clue token with ManaCost `{2}` and SacrificeAndDrawEffect.

**Step 4: Run tests**
**Step 5: Commit**

```bash
git add -A  # Multiple files changed
git commit -m "feat(engine): add ActionType.ActivateAbility and Clue sacrifice-to-draw"
```

---

### Task 6: TriggerCondition.ThirdDrawInTurn

**Files:**
- Modify: `src/MtgDecker.Engine/Triggers/TriggerCondition.cs`
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (trigger matching)
- Test: `tests/MtgDecker.Engine.Tests/TransformTests.cs` (add tests)

**Step 1: Write failing tests**

```csharp
[Fact]
public async Task ThirdDrawTrigger_FiresOnThirdDraw()
{
    // Setup: card with trigger on GameEvent.DrawCard + TriggerCondition.ThirdDrawInTurn
    // Draw 3 cards
    // Assert: trigger fired (effect executed)
}

[Fact]
public async Task ThirdDrawTrigger_DoesNotFireOnFirstOrSecondDraw()
{
    // Setup same card
    // Draw 2 cards
    // Assert: trigger NOT fired
}
```

**Step 2: Run tests to verify failure**

**Step 3: Implement**

Add `ThirdDrawInTurn` to `TriggerCondition` enum.

In `GameEngine`, where triggers are matched after draw events, add handling for this condition:
- Check if the drawing player's `DrawsThisTurn == 3` (exactly 3, fires once)
- Only fire for self triggers (the creature with this trigger condition)

**Step 4: Run tests**
**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/Triggers/TriggerCondition.cs src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/TransformTests.cs
git commit -m "feat(engine): add TriggerCondition.ThirdDrawInTurn for transform triggers"
```

---

### Task 7: "Until Next Turn" Duration for ContinuousEffect

**Files:**
- Modify: `src/MtgDecker.Engine/ContinuousEffect.cs`
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (cleanup at turn start)
- Test: `tests/MtgDecker.Engine.Tests/TransformTests.cs` or new file

**Step 1: Write failing tests**

```csharp
[Fact]
public void UntilNextTurn_EffectSurvivesOpponentTurn()
{
    // Create effect with ExpiresOnTurnNumber = current + 2
    // Simulate opponent's turn
    // Assert: effect still active
}

[Fact]
public void UntilNextTurn_EffectExpiresAtControllerNextTurn()
{
    // Create effect with ExpiresOnTurnNumber
    // Advance to controller's next turn
    // Assert: effect removed
}
```

**Step 2: Run tests to verify failure**

**Step 3: Implement**

Add to `ContinuousEffect`:
```csharp
int? ExpiresOnTurnNumber = null
```

In `GameEngine` turn-start logic, remove ActiveEffects where `ExpiresOnTurnNumber != null && ExpiresOnTurnNumber <= state.TurnNumber`.

**Step 4: Run tests**
**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/ContinuousEffect.cs src/MtgDecker.Engine/GameEngine.cs tests/
git commit -m "feat(engine): add ExpiresOnTurnNumber to ContinuousEffect for 'until next turn' duration"
```

---

### Task 8: Tamiyo Back Face Loyalty Effects

**Files:**
- Create: `src/MtgDecker.Engine/Triggers/Effects/TamiyoDefenseEffect.cs` (+2)
- Create: `src/MtgDecker.Engine/Triggers/Effects/TamiyoRecoverEffect.cs` (-3)
- Create: `src/MtgDecker.Engine/Triggers/Effects/TamiyoUltimateEffect.cs` (-7)
- Test: `tests/MtgDecker.Engine.Tests/TamiyoEffectTests.cs`

**Step 1: Write failing tests for each effect**

**+2 TamiyoDefenseEffect:** Creates a ContinuousEffect "until your next turn" that gives -1/-0 to creatures attacking you or your PWs.
- Test: after executing, attacking creatures get -1/-0

**-3 TamiyoRecoverEffect:** Return target instant or sorcery from graveyard to hand.
- Test: instant card in graveyard moves to hand
- Test: creature card in graveyard NOT eligible

**-7 TamiyoUltimateEffect:** Draw cards equal to half library rounded up. Create emblem "no maximum hand size."
- Test: draws correct number of cards
- Test: emblem created on player

**Step 2: Run tests to verify failure**

**Step 3: Implement each effect**

TamiyoDefenseEffect:
```csharp
public class TamiyoDefenseEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var state = context.State;
        var controllerId = context.Controller.Id;

        // Create a continuous effect that lasts until controller's next turn
        // -1/-0 to creatures attacking controller or controller's planeswalkers
        var effect = new ContinuousEffect(
            context.Source.Id,
            ContinuousEffectType.ModifyPowerToughness,
            (card, player) => card.IsCreature, // Applies broadly, but only when attacking
            PowerMod: -1,
            ExpiresOnTurnNumber: state.TurnNumber + 2, // Until my next turn
            Layer: EffectLayer.Layer7c_ModifyPT,
            // We need a way to scope this to "attackers targeting controller"
            // Simplification: applies to all creatures that attacked this turn
            StateCondition: s => s.CombatState != null
        );
        state.ActiveEffects.Add(effect);
        state.Log("Until your next turn, creatures attacking you or your planeswalkers get -1/-0.");

        return Task.CompletedTask;
    }
}
```

Note: The exact scoping of "creatures attacking you" may need refinement. For a first pass, we can make this apply during combat to declared attackers of the controller.

TamiyoRecoverEffect:
```csharp
public class TamiyoRecoverEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var controller = context.Controller;
        var state = context.State;

        var eligible = controller.Graveyard.Cards
            .Where(c => c.CardTypes.HasFlag(CardType.Instant) || c.CardTypes.HasFlag(CardType.Sorcery))
            .ToList();

        if (eligible.Count == 0)
        {
            state.Log("No instant or sorcery cards in graveyard.");
            return;
        }

        var targetId = await context.DecisionHandler.ChooseCard(
            eligible, "Choose an instant or sorcery to return to hand.", optional: true, ct);

        if (!targetId.HasValue) return;

        var target = eligible.FirstOrDefault(c => c.Id == targetId.Value);
        if (target == null) return;

        controller.Graveyard.Remove(target);
        controller.Hand.Add(target);
        state.Log($"{target.Name} returned from graveyard to hand.");
    }
}
```

TamiyoUltimateEffect:
```csharp
public class TamiyoUltimateEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var controller = context.Controller;
        var state = context.State;

        int cardsToDraw = (controller.Library.Count + 1) / 2; // Round up
        context.Engine.DrawCards(controller, cardsToDraw);
        state.Log($"{controller.Name} draws {cardsToDraw} cards.");

        // Create emblem: no maximum hand size
        // (Cosmetic in our engine since we don't enforce hand size)
        controller.Emblems.Add(new Emblem(
            "You have no maximum hand size.",
            new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantPlayerShroud,
                (_, _) => false) // No-op effect, emblem is cosmetic
        ));
        state.Log($"{controller.Name} gets an emblem: No maximum hand size.");

        return Task.CompletedTask;
    }
}
```

**Step 4: Run tests**
**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/Triggers/Effects/TamiyoDefenseEffect.cs src/MtgDecker.Engine/Triggers/Effects/TamiyoRecoverEffect.cs src/MtgDecker.Engine/Triggers/Effects/TamiyoUltimateEffect.cs tests/MtgDecker.Engine.Tests/TamiyoEffectTests.cs
git commit -m "feat(engine): implement Tamiyo back face loyalty effects (+2, -3, -7)"
```

---

### Task 9: Register Tamiyo in CardDefinitions

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Test: `tests/MtgDecker.Engine.Tests/TamiyoRegistrationTests.cs`

**Step 1: Write failing tests**

```csharp
public class TamiyoRegistrationTests
{
    [Fact]
    public void Tamiyo_FrontFace_IsRegistered()
    {
        var def = CardDefinitions.Get("Tamiyo, Inquisitive Student");
        def.Should().NotBeNull();
        def!.CardTypes.Should().Be(CardType.Creature);
        def.Power.Should().Be(0);
        def.Toughness.Should().Be(3);
    }

    [Fact]
    public void Tamiyo_FrontFace_HasTransformInto()
    {
        var def = CardDefinitions.Get("Tamiyo, Inquisitive Student");
        def!.TransformInto.Should().NotBeNull();
        def.TransformInto!.Name.Should().Be("Tamiyo, Seasoned Scholar");
    }

    [Fact]
    public void Tamiyo_BackFace_IsPlaneswalker()
    {
        var def = CardDefinitions.Get("Tamiyo, Inquisitive Student");
        def!.TransformInto!.CardTypes.Should().Be(CardType.Planeswalker);
        def.TransformInto.StartingLoyalty.Should().Be(2);
    }

    [Fact]
    public void Tamiyo_BackFace_HasThreeLoyaltyAbilities()
    {
        var def = CardDefinitions.Get("Tamiyo, Inquisitive Student");
        def!.TransformInto!.LoyaltyAbilities.Should().HaveCount(3);
    }

    [Fact]
    public void Tamiyo_FrontFace_HasFlyingKeyword()
    {
        var def = CardDefinitions.Get("Tamiyo, Inquisitive Student");
        // Check that the card has flying via keyword or continuous effects
    }
}
```

**Step 2: Run tests to verify failure**

**Step 3: Register Tamiyo**

In `CardDefinitions.cs`, in the Legacy Dimir Tempo section:
```csharp
["Tamiyo, Inquisitive Student"] = new(ManaCost.Parse("{U}"), null, 0, 3, CardType.Creature)
{
    Name = "Tamiyo, Inquisitive Student",
    IsLegendary = true,
    Subtypes = ["Moonfolk", "Wizard"],
    // Flying keyword (via continuous effect or built-in keyword handling)
    ContinuousEffects = [
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
            (card, _) => card.Name == "Tamiyo, Inquisitive Student" || card.Name == "Tamiyo, Seasoned Scholar",
            GrantedKeyword: Keyword.Flying,
            Layer: EffectLayer.Layer6_AbilityAddRemove,
            ApplyToSelf: true),
    ],
    Triggers = [
        // Attack trigger: investigate
        new Trigger(GameEvent.CombatDamageDealt, TriggerCondition.SelfAttacks, new InvestigateEffect()),
        // Transform trigger: when 3rd card drawn
        new Trigger(GameEvent.DrawCard, TriggerCondition.ThirdDrawInTurn, new TransformExileReturnEffect()),
    ],
    TransformInto = new CardDefinition(null, null, null, null, CardType.Planeswalker)
    {
        Name = "Tamiyo, Seasoned Scholar",
        IsLegendary = true,
        Subtypes = ["Tamiyo"],
        StartingLoyalty = 2,
        LoyaltyAbilities = [
            new LoyaltyAbility(2, new TamiyoDefenseEffect(), "+2: Creatures attacking you get -1/-0 until next turn"),
            new LoyaltyAbility(-3, new TamiyoRecoverEffect(), "-3: Return instant or sorcery from graveyard to hand"),
            new LoyaltyAbility(-7, new TamiyoUltimateEffect(), "-7: Draw half library, emblem no max hand size"),
        ],
    },
},
```

**Step 4: Run tests**
**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/CardDefinitions.cs tests/MtgDecker.Engine.Tests/TamiyoRegistrationTests.cs
git commit -m "feat(engine): register Tamiyo, Inquisitive Student with transform and loyalty abilities"
```

---

### Task 10: Tamiyo Integration Tests

**Files:**
- Create: `tests/MtgDecker.Engine.Tests/TamiyoIntegrationTests.cs`

**Step 1: Write integration tests**

```csharp
public class TamiyoIntegrationTests
{
    [Fact]
    public async Task Tamiyo_CreatedFromRegistry_HasCorrectFrontFace()
    {
        var card = GameCard.Create("Tamiyo, Inquisitive Student");
        card.IsCreature.Should().BeTrue();
        card.BasePower.Should().Be(0);
        card.BaseToughness.Should().Be(3);
        card.IsTransformed.Should().BeFalse();
    }

    [Fact]
    public async Task Tamiyo_AfterTransform_IsPlaneswalkerWithLoyalty()
    {
        // Setup game, put Tamiyo on battlefield
        // Trigger 3rd draw
        // Assert: Tamiyo is now a planeswalker with loyalty 2
    }

    [Fact]
    public async Task Tamiyo_ThirdDraw_TriggersTransform()
    {
        // Full game setup
        // Draw 3 cards (simulating Brainstorm or Clue sacrifice)
        // Assert: transform trigger fires
    }

    [Fact]
    public async Task Tamiyo_TransformedBack_CanActivateLoyaltyAbilities()
    {
        // After transforming, activate -3 to return instant from graveyard
    }
}
```

**Step 2: Run tests**
**Step 3: Fix any issues discovered during integration testing**
**Step 4: Commit**

```bash
git add tests/MtgDecker.Engine.Tests/TamiyoIntegrationTests.cs
git commit -m "test(engine): add Tamiyo integration tests — transform, investigate, loyalty"
```

---

### Task 11: Full Verification + PR

**Step 1: Run all engine tests**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ -v minimal
```

Expected: All tests pass

**Step 2: Run all other test projects**

```bash
dotnet test tests/MtgDecker.Domain.Tests/ -v minimal
dotnet test tests/MtgDecker.Application.Tests/ -v minimal
dotnet test tests/MtgDecker.Infrastructure.Tests/ -v minimal
```

**Step 3: Push and create PR**

```bash
git push -u origin feat/transform-cards
gh pr create --title "feat(engine): transform cards + Tamiyo, Inquisitive Student"
```
