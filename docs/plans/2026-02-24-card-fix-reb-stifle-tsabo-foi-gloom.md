# Card Implementation Fixes: REB/BEB/Hydroblast, Stifle, Tsabo's Web, Flash of Insight, Gloom

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fix five card implementation issues found during Oracle accuracy review — modal targeting for elemental blasts, proper Stifle targeting, Tsabo's Web untap prevention, Flash of Insight dynamic flashback X, and Gloom activated ability cost increase.

**Architecture:** Each fix modifies Engine source files (TargetFilter, Effects, CardDefinitions, ContinuousEffect, ActivateAbilityHandler) with corresponding test coverage in Engine.Tests. All changes are isolated to the `MtgDecker.Engine` project and its test project.

**Tech Stack:** C# 14, .NET 10, xUnit + FluentAssertions, MtgDecker.Engine

---

## Task 1: Add `TargetFilter.SpellOrPermanent()` for REB/BEB/Hydroblast

The three elemental blast cards are modal — they can counter a spell OR destroy a permanent of the relevant color. Currently they only use `TargetFilter.Spell()` which only targets spells on the stack. The effects (`PyroblastEffect` and `BlueElementalBlastEffect`) already handle both modes — they check if the target is on the stack (counter) or battlefield (destroy). The only issue is the TargetFilter prevents targeting permanents.

**Files:**
- Test: `tests/MtgDecker.Engine.Tests/OracleFixesTests.cs` (create)
- Modify: `src/MtgDecker.Engine/TargetFilter.cs`
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`

**Step 1: Write the failing tests**

Create `tests/MtgDecker.Engine.Tests/OracleFixesTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class OracleFixesTests
{
    private static (GameState state, Player p1, Player p2) CreateGameState()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Alice", h1);
        var p2 = new Player(Guid.NewGuid(), "Bob", h2);
        var state = new GameState(p1, p2);
        return (state, p1, p2);
    }

    private static (GameState state, GameEngine engine, TestDecisionHandler h1, TestDecisionHandler h2)
        CreateEngineState()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (state, engine, h1, h2);
    }

    // ═══════════════════════════════════════════════════════════════════
    // FIX 1: REB / BEB / Hydroblast — permanent-destruction mode
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void RedElementalBlast_CanDestroyBluePermanent()
    {
        var (state, p1, p2) = CreateGameState();

        var blueCreature = new GameCard
        {
            Name = "Delver of Secrets",
            CardTypes = CardType.Creature,
            ManaCost = ManaCost.Parse("{U}"),
        };
        p2.Battlefield.Add(blueCreature);

        var rebCard = new GameCard { Name = "Red Elemental Blast", CardTypes = CardType.Instant };
        var spell = new StackObject(rebCard, p1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(blueCreature.Id, p2.Id, ZoneType.Battlefield) }, 0);

        new PyroblastEffect().Resolve(state, spell);

        p2.Battlefield.Cards.Should().NotContain(c => c.Name == "Delver of Secrets");
        p2.Graveyard.Cards.Should().Contain(c => c.Name == "Delver of Secrets");
    }

    [Fact]
    public void BlueElementalBlast_CanDestroyRedPermanent()
    {
        var (state, p1, p2) = CreateGameState();

        var redCreature = new GameCard
        {
            Name = "Goblin Lackey",
            CardTypes = CardType.Creature,
            ManaCost = ManaCost.Parse("{R}"),
        };
        p2.Battlefield.Add(redCreature);

        var bebCard = new GameCard { Name = "Blue Elemental Blast", CardTypes = CardType.Instant };
        var spell = new StackObject(bebCard, p1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(redCreature.Id, p2.Id, ZoneType.Battlefield) }, 0);

        new BlueElementalBlastEffect().Resolve(state, spell);

        p2.Battlefield.Cards.Should().NotContain(c => c.Name == "Goblin Lackey");
        p2.Graveyard.Cards.Should().Contain(c => c.Name == "Goblin Lackey");
    }

    [Fact]
    public void Hydroblast_CanDestroyRedPermanent()
    {
        var (state, p1, p2) = CreateGameState();

        var redCreature = new GameCard
        {
            Name = "Goblin Piledriver",
            CardTypes = CardType.Creature,
            ManaCost = ManaCost.Parse("{1}{R}"),
        };
        p2.Battlefield.Add(redCreature);

        var hydroblastCard = new GameCard { Name = "Hydroblast", CardTypes = CardType.Instant };
        var spell = new StackObject(hydroblastCard, p1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(redCreature.Id, p2.Id, ZoneType.Battlefield) }, 0);

        new BlueElementalBlastEffect().Resolve(state, spell);

        p2.Battlefield.Cards.Should().NotContain(c => c.Name == "Goblin Piledriver");
        p2.Graveyard.Cards.Should().Contain(c => c.Name == "Goblin Piledriver");
    }

    [Fact]
    public void SpellOrPermanent_TargetFilter_AllowsBothStackAndBattlefield()
    {
        var filter = TargetFilter.SpellOrPermanent();
        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature };
        var spell = new GameCard { Name = "Bolt", CardTypes = CardType.Instant };

        filter.IsLegal(creature, ZoneType.Battlefield).Should().BeTrue();
        filter.IsLegal(spell, ZoneType.Stack).Should().BeTrue();
        filter.IsLegal(creature, ZoneType.Hand).Should().BeFalse();
    }

    [Fact]
    public void RedElementalBlast_CardDefinition_UsesSpellOrPermanent()
    {
        CardDefinitions.TryGet("Red Elemental Blast", out var def).Should().BeTrue();
        var filter = def!.TargetFilter!;
        // Must accept both battlefield and stack targets
        filter.IsLegal(new GameCard { Name = "X" }, ZoneType.Battlefield).Should().BeTrue();
        filter.IsLegal(new GameCard { Name = "X" }, ZoneType.Stack).Should().BeTrue();
    }
}
```

**Step 2: Run the tests — they should fail**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~OracleFixesTests" --no-build
```

Expected: compilation error because `TargetFilter.SpellOrPermanent()` doesn't exist.

**Step 3: Implement `TargetFilter.SpellOrPermanent()`**

In `src/MtgDecker.Engine/TargetFilter.cs`, add a new static factory method after the existing `Spell()` method:

```csharp
public static TargetFilter SpellOrPermanent() => new((card, zone) =>
    zone == ZoneType.Stack || zone == ZoneType.Battlefield);
```

**Step 4: Update CardDefinitions for REB, BEB, Hydroblast**

In `src/MtgDecker.Engine/CardDefinitions.cs`, change:

```csharp
// Old:
["Red Elemental Blast"] = new(ManaCost.Parse("{R}"), null, null, null, CardType.Instant,
    TargetFilter.Spell(), new PyroblastEffect()),
["Blue Elemental Blast"] = new(ManaCost.Parse("{U}"), null, null, null, CardType.Instant,
    TargetFilter.Spell(), new BlueElementalBlastEffect()),
["Hydroblast"] = new(ManaCost.Parse("{U}"), null, null, null, CardType.Instant,
    TargetFilter.Spell(), new BlueElementalBlastEffect()),

// New:
["Red Elemental Blast"] = new(ManaCost.Parse("{R}"), null, null, null, CardType.Instant,
    TargetFilter.SpellOrPermanent(), new PyroblastEffect()),
["Blue Elemental Blast"] = new(ManaCost.Parse("{U}"), null, null, null, CardType.Instant,
    TargetFilter.SpellOrPermanent(), new BlueElementalBlastEffect()),
["Hydroblast"] = new(ManaCost.Parse("{U}"), null, null, null, CardType.Instant,
    TargetFilter.SpellOrPermanent(), new BlueElementalBlastEffect()),
```

Also update Pyroblast (which already exists with the same issue):
```csharp
// Old:
["Pyroblast"] = new(ManaCost.Parse("{R}"), null, null, null, CardType.Instant,
    TargetFilter.Spell(), new PyroblastEffect()),

// New:
["Pyroblast"] = new(ManaCost.Parse("{R}"), null, null, null, CardType.Instant,
    TargetFilter.SpellOrPermanent(), new PyroblastEffect()),
```

**Step 5: Run the tests — they should pass**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~OracleFixesTests"
```

**Step 6: Also run existing Pyroblast tests to ensure no regression**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~Pyroblast"
```

---

## Task 2: Stifle — Proper targeting for triggered abilities

Currently `StifleEffect` auto-picks the first `TriggeredAbilityStackObject` without player choice. When multiple triggered abilities are on the stack, the player should choose which to counter.

**Files:**
- Test: `tests/MtgDecker.Engine.Tests/OracleFixesTests.cs` (append)
- Modify: `src/MtgDecker.Engine/Effects/StifleEffect.cs`

**Step 1: Write the failing tests**

Append to `OracleFixesTests.cs`:

```csharp
// ═══════════════════════════════════════════════════════════════════
// FIX 2: Stifle — proper targeting
// ═══════════════════════════════════════════════════════════════════

[Fact]
public async Task Stifle_WithMultipleTriggeredAbilities_PlayerChoosesWhichToCounter()
{
    var (state, engine, h1, h2) = CreateEngineState();

    var source1 = GameCard.Create("Siege-Gang Commander");
    var source2 = GameCard.Create("Goblin Matron");
    var trigger1 = new TriggeredAbilityStackObject(source1, state.Player2.Id, new DrawCardEffect());
    var trigger2 = new TriggeredAbilityStackObject(source2, state.Player2.Id, new DrawCardEffect());
    state.StackPush(trigger1);
    state.StackPush(trigger2);

    // Player chooses to counter the second trigger (Goblin Matron)
    h1.EnqueueCardChoice(source2.Id);

    var stifleCard = new GameCard { Name = "Stifle", CardTypes = CardType.Instant };
    var spell = new StackObject(stifleCard, state.Player1.Id,
        new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);

    var effect = new StifleEffect();
    await effect.ResolveAsync(state, spell, state.Player1.DecisionHandler);

    // Matron trigger removed, Commander trigger remains
    state.Stack.OfType<TriggeredAbilityStackObject>().Should().HaveCount(1);
    state.Stack.OfType<TriggeredAbilityStackObject>().Single().Source.Name
        .Should().Be("Siege-Gang Commander");
}

[Fact]
public async Task Stifle_WithSingleTriggeredAbility_AutoCounters()
{
    var (state, engine, h1, h2) = CreateEngineState();

    var source = GameCard.Create("Siege-Gang Commander");
    var trigger = new TriggeredAbilityStackObject(source, state.Player2.Id, new DrawCardEffect());
    state.StackPush(trigger);

    var stifleCard = new GameCard { Name = "Stifle", CardTypes = CardType.Instant };
    var spell = new StackObject(stifleCard, state.Player1.Id,
        new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);

    var effect = new StifleEffect();
    await effect.ResolveAsync(state, spell, state.Player1.DecisionHandler);

    state.Stack.OfType<TriggeredAbilityStackObject>().Should().BeEmpty();
    state.GameLog.Should().Contain(l => l.Contains("counters"));
}

[Fact]
public async Task Stifle_WithNoTriggeredAbility_Fizzles()
{
    var (state, engine, h1, h2) = CreateEngineState();

    var stifleCard = new GameCard { Name = "Stifle", CardTypes = CardType.Instant };
    var spell = new StackObject(stifleCard, state.Player1.Id,
        new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);

    var effect = new StifleEffect();
    await effect.ResolveAsync(state, spell, state.Player1.DecisionHandler);

    state.GameLog.Should().Contain(l => l.Contains("fizzles"));
}
```

**Step 2: Run the tests — they should fail**

The existing sync `Resolve()` method doesn't support player choice, so the multi-trigger test will fail.

**Step 3: Implement Stifle targeting in `StifleEffect.cs`**

Replace the content of `src/MtgDecker.Engine/Effects/StifleEffect.cs`:

```csharp
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Effects;

/// <summary>
/// Stifle: Counter target activated or triggered ability.
/// NOTE: The engine does not put activated abilities on the stack as separate objects —
/// they resolve immediately via ActivateAbilityHandler. So for now, Stifle can only
/// counter triggered abilities (TriggeredAbilityStackObject).
/// </summary>
public class StifleEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var triggeredAbilities = state.Stack
            .OfType<TriggeredAbilityStackObject>()
            .ToList();

        if (triggeredAbilities.Count == 0)
        {
            state.Log($"{spell.Card.Name} fizzles (no triggered ability on stack).");
            return;
        }

        TriggeredAbilityStackObject target;
        if (triggeredAbilities.Count == 1)
        {
            target = triggeredAbilities[0];
        }
        else
        {
            // Multiple triggered abilities — player chooses which to counter
            var sourceCards = triggeredAbilities.Select(t => t.Source).ToList();
            var chosenId = await handler.ChooseCard(
                sourceCards,
                "Stifle: Choose a triggered ability to counter.",
                optional: false, ct);

            target = chosenId.HasValue
                ? triggeredAbilities.FirstOrDefault(t => t.Source.Id == chosenId.Value)
                  ?? triggeredAbilities[0]
                : triggeredAbilities[0];
        }

        state.StackRemove(target);
        state.Log($"{spell.Card.Name} counters {target.Source.Name}'s triggered ability.");
    }

    // Keep sync Resolve for backward compatibility — delegates to async version with simple behavior
    public override void Resolve(GameState state, StackObject spell)
    {
        var triggeredAbility = state.Stack
            .OfType<TriggeredAbilityStackObject>()
            .FirstOrDefault();

        if (triggeredAbility == null)
        {
            state.Log($"{spell.Card.Name} fizzles (no triggered ability on stack).");
            return;
        }

        state.StackRemove(triggeredAbility);
        state.Log($"{spell.Card.Name} counters {triggeredAbility.Source.Name}'s triggered ability.");
    }
}
```

**Step 4: Run the tests — they should pass**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~OracleFixesTests.Stifle"
```

**Step 5: Run existing Stifle tests for no regression**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~Stifle"
```

---

## Task 3: Tsabo's Web — Add "lands don't untap" static ability

Oracle: "Each land with an activated ability that isn't a mana ability doesn't untap during its controller's untap step."

The `DoesNotUntap` keyword already exists and is enforced in the untap step. Tsabo's Web needs a `ContinuousEffect` with `GrantKeyword: Keyword.DoesNotUntap` that applies to lands with non-mana activated abilities (FetchAbility or ActivatedAbilities on the CardDefinition).

**Files:**
- Test: `tests/MtgDecker.Engine.Tests/OracleFixesTests.cs` (append)
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`

**Step 1: Write the failing tests**

Append to `OracleFixesTests.cs`:

```csharp
// ═══════════════════════════════════════════════════════════════════
// FIX 3: Tsabo's Web — lands with non-mana activated abilities don't untap
// ═══════════════════════════════════════════════════════════════════

[Fact]
public void TsabosWeb_PreventsUntapOfLandWithActivatedAbility()
{
    var (state, engine, h1, h2) = CreateEngineState();

    // Put Tsabo's Web on p1's battlefield
    var web = GameCard.Create("Tsabo's Web");
    state.Player1.Battlefield.Add(web);
    engine.RecalculateState();

    // Rishadan Port has an activated ability (tap target land) — not a mana ability
    var port = GameCard.Create("Rishadan Port");
    port.IsTapped = true;
    state.Player1.Battlefield.Add(port);
    engine.RecalculateState();

    // The port should have DoesNotUntap keyword
    port.ActiveKeywords.Should().Contain(Keyword.DoesNotUntap);
}

[Fact]
public void TsabosWeb_BasicLandStillUntaps()
{
    var (state, engine, h1, h2) = CreateEngineState();

    var web = GameCard.Create("Tsabo's Web");
    state.Player1.Battlefield.Add(web);

    var mountain = GameCard.Create("Mountain");
    mountain.IsTapped = true;
    state.Player1.Battlefield.Add(mountain);
    engine.RecalculateState();

    // Basic land with only a mana ability should NOT have DoesNotUntap
    mountain.ActiveKeywords.Should().NotContain(Keyword.DoesNotUntap);
}

[Fact]
public void TsabosWeb_FetchlandDoesNotUntap()
{
    var (state, engine, h1, h2) = CreateEngineState();

    var web = GameCard.Create("Tsabo's Web");
    state.Player1.Battlefield.Add(web);

    var fetch = GameCard.Create("Wooded Foothills");
    fetch.IsTapped = true;
    state.Player1.Battlefield.Add(fetch);
    engine.RecalculateState();

    // Fetchland has a non-mana activated ability — should be locked down
    fetch.ActiveKeywords.Should().Contain(Keyword.DoesNotUntap);
}

[Fact]
public void TsabosWeb_WastelandDoesNotUntap()
{
    var (state, engine, h1, h2) = CreateEngineState();

    var web = GameCard.Create("Tsabo's Web");
    state.Player1.Battlefield.Add(web);

    // Wasteland has mana ability AND activated ability
    var wasteland = GameCard.Create("Wasteland");
    wasteland.IsTapped = true;
    state.Player1.Battlefield.Add(wasteland);
    engine.RecalculateState();

    // Wasteland has a non-mana activated ability (destroy target non-basic land)
    wasteland.ActiveKeywords.Should().Contain(Keyword.DoesNotUntap);
}
```

**Step 2: Run the tests — they should fail**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~OracleFixesTests.TsabosWeb"
```

**Step 3: Implement Tsabo's Web ContinuousEffect**

In `src/MtgDecker.Engine/CardDefinitions.cs`, update the Tsabo's Web entry. The `Applies` function needs to check if the card is a land AND has a non-mana activated ability (either ActivatedAbilities or FetchAbility in CardDefinitions).

```csharp
["Tsabo's Web"] = new(ManaCost.Parse("{2}"), null, null, null, CardType.Artifact)
{
    Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new DrawCardEffect())],
    ContinuousEffects =
    [
        // "Each land with an activated ability that isn't a mana ability
        // doesn't untap during its controller's untap step."
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
            (card, _) =>
            {
                if (!card.IsLand) return false;
                // Check if the land has non-mana activated abilities in CardDefinitions
                if (!CardDefinitions.TryGet(card.Name, out var def)) return false;
                // FetchAbility is a non-mana activated ability
                if (def.FetchAbility != null) return true;
                // ActivatedAbilities are non-mana activated abilities
                if (def.ActivatedAbilities.Count > 0) return true;
                return false;
            },
            GrantedKeyword: Keyword.DoesNotUntap,
            Layer: EffectLayer.Layer6_AbilityAddRemove),
    ],
},
```

Remove the old deferred comment.

**Step 4: Run the tests — they should pass**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~OracleFixesTests.TsabosWeb"
```

---

## Task 4: Flash of Insight — Dynamic flashback X

The FlashbackHandler already supports `ExileBlueCardsFromGraveyard` and prompts the player to choose blue cards to exile. It already sets `XValue` on the StackObject. The issue description says it hardcodes 1, but reading `FlashbackHandler.cs` lines 88-110: it already allows the player to choose UP TO `blueCards.Count` cards (via `ChooseCardsToExile`). The `ExileBlueCardsFromGraveyard: 1` in the definition only requires at least 1 blue card to be available, but the handler lets the player exile as many as they want.

However, the definition value of `1` is used only as a "minimum required" check (`if (fbCost.ExileBlueCardsFromGraveyard > 0)`). The actual count exiled is determined by `ChooseCardsToExile` which can return up to `blueCards.Count`.

The test needs to verify: flashback with exiling 3 blue cards sets X=3 and looks at top 3.

**Files:**
- Test: `tests/MtgDecker.Engine.Tests/OracleFixesTests.cs` (append)

**Step 1: Write the test**

Append to `OracleFixesTests.cs`:

```csharp
// ═══════════════════════════════════════════════════════════════════
// FIX 4: Flash of Insight — dynamic flashback X
// ═══════════════════════════════════════════════════════════════════

[Fact]
public async Task FlashOfInsight_FlashbackExiling3BlueCards_LooksAtTop3()
{
    var (state, engine, h1, h2) = CreateEngineState();

    // Put Flash of Insight in graveyard
    var foi = GameCard.Create("Flash of Insight");
    state.Player1.Graveyard.Add(foi);

    // Put 3 blue cards in graveyard (not the FOI itself)
    var blue1 = new GameCard { Name = "Brainstorm", ManaCost = ManaCost.Parse("{U}"), CardTypes = CardType.Instant };
    var blue2 = new GameCard { Name = "Counterspell", ManaCost = ManaCost.Parse("{U}{U}"), CardTypes = CardType.Instant };
    var blue3 = new GameCard { Name = "Force of Will", ManaCost = ManaCost.Parse("{3}{U}{U}"), CardTypes = CardType.Instant };
    state.Player1.Graveyard.Add(blue1);
    state.Player1.Graveyard.Add(blue2);
    state.Player1.Graveyard.Add(blue3);

    // Library has 5 cards
    var lib1 = new GameCard { Name = "Card A" };
    var lib2 = new GameCard { Name = "Card B" };
    var lib3 = new GameCard { Name = "Card C" };
    var lib4 = new GameCard { Name = "Card D" };
    var lib5 = new GameCard { Name = "Card E" };
    state.Player1.Library.Add(lib5);
    state.Player1.Library.Add(lib4);
    state.Player1.Library.Add(lib3);
    state.Player1.Library.Add(lib2);
    state.Player1.Library.Add(lib1);

    // Add {1}{U} mana for flashback cost
    state.Player1.ManaPool.Add(ManaColor.Colorless, 1);
    state.Player1.ManaPool.Add(ManaColor.Blue, 1);

    // Exile choice: exile all 3 blue cards
    h1.EnqueueExileChoice((cards, max) => cards.Take(3).ToList());

    // Card choice for Flash of Insight: pick Card B
    h1.EnqueueCardChoice(lib2.Id);

    // Cast flashback
    state.CurrentPhase = Phase.MainPhase1;
    state.ActivePlayer = state.Player1;
    state.PriorityPlayer = state.Player1;
    h1.EnqueueAction(GameAction.Flashback(state.Player1.Id, foi.Id));
    h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
    h2.EnqueueAction(GameAction.Pass(state.Player2.Id));

    await engine.RunPriorityAsync(CancellationToken.None);

    // Verify: FOI exiled (flashback), 3 blue cards exiled, X=3
    state.Player1.Exile.Cards.Should().Contain(c => c.Name == "Brainstorm");
    state.Player1.Exile.Cards.Should().Contain(c => c.Name == "Counterspell");
    state.Player1.Exile.Cards.Should().Contain(c => c.Name == "Force of Will");
    state.Player1.Exile.Cards.Should().Contain(c => c.Name == "Flash of Insight");

    // Card B should be in hand (chosen from top 3)
    state.Player1.Hand.Cards.Should().Contain(c => c.Name == "Card B");

    // The log should mention X=3
    state.GameLog.Should().Contain(l => l.Contains("X=3"));
}
```

**Step 2: Run the test**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~OracleFixesTests.FlashOfInsight"
```

This test should already pass since the FlashbackHandler already handles dynamic blue card exile. If it doesn't, debug the handler's flow. The key path:
1. `FlashbackHandler` detects `ExileBlueCardsFromGraveyard > 0`
2. Finds blue cards in graveyard (excluding FOI itself)
3. Calls `ChooseCardsToExile` with max = `blueCards.Count`
4. Sets `XValue = fbExiledBlueCards.Count` (which is 3)
5. `FlashOfInsightEffect.ResolveAsync` reads `spell.XValue` = 3

If the test passes, no code changes needed — the implementation was already correct. If it fails, investigate the specific failure point and fix.

---

## Task 5: Gloom — Activated ability cost increase for white enchantments

Oracle: "Activated abilities of white enchantments cost {3} more to activate."

Need to add a cost modification check in `ActivateAbilityHandler` for effects that modify activated ability costs, and add a new `ContinuousEffectType` for this.

**Files:**
- Test: `tests/MtgDecker.Engine.Tests/OracleFixesTests.cs` (append)
- Modify: `src/MtgDecker.Engine/ContinuousEffect.cs`
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Modify: `src/MtgDecker.Engine/Actions/ActivateAbilityHandler.cs`

**Step 1: Write the failing test**

Append to `OracleFixesTests.cs`:

```csharp
// ═══════════════════════════════════════════════════════════════════
// FIX 5: Gloom — activated ability cost increase for white enchantments
// ═══════════════════════════════════════════════════════════════════

[Fact]
public async Task Gloom_IncreasesActivatedAbilityCostOfWhiteEnchantment()
{
    var (state, engine, h1, h2) = CreateEngineState();

    // Put Gloom on the battlefield
    var gloom = GameCard.Create("Gloom", "Enchantment");
    state.Player2.Battlefield.Add(gloom);
    engine.RecalculateState();

    // Circle of Protection: Red is a {1}{W} white enchantment
    // with activated ability cost {1}
    var cop = GameCard.Create("Circle of Protection: Red");
    state.Player1.Battlefield.Add(cop);

    // With Gloom, the {1} activation cost should become {4} ({1} + {3})
    // Player has only {1} mana — not enough with Gloom's tax
    state.Player1.ManaPool.Add(ManaColor.Colorless, 1);

    h1.EnqueueAction(GameAction.ActivateAbility(state.Player1.Id, cop.Id));
    h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
    h2.EnqueueAction(GameAction.Pass(state.Player2.Id));

    state.CurrentPhase = Phase.MainPhase1;
    state.ActivePlayer = state.Player1;
    state.PriorityPlayer = state.Player1;
    await engine.RunPriorityAsync(CancellationToken.None);

    // Should fail due to insufficient mana (needs 4, has 1)
    state.GameLog.Should().Contain(l => l.Contains("not enough mana"));
}

[Fact]
public async Task Gloom_DoesNotAffectNonWhiteEnchantmentAbilities()
{
    var (state, engine, h1, h2) = CreateEngineState();

    // Put Gloom on the battlefield
    var gloom = GameCard.Create("Gloom", "Enchantment");
    state.Player2.Battlefield.Add(gloom);
    engine.RecalculateState();

    // River Boa is a green creature with {G} activated ability (not a white enchantment)
    var boa = GameCard.Create("River Boa");
    state.Player1.Battlefield.Add(boa);
    state.Player1.ManaPool.Add(ManaColor.Green, 1);

    h1.EnqueueAction(GameAction.ActivateAbility(state.Player1.Id, boa.Id));
    h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
    h2.EnqueueAction(GameAction.Pass(state.Player2.Id));

    state.CurrentPhase = Phase.MainPhase1;
    state.ActivePlayer = state.Player1;
    state.PriorityPlayer = state.Player1;
    await engine.RunPriorityAsync(CancellationToken.None);

    // Should succeed — Gloom only affects white enchantments
    state.GameLog.Should().Contain(l => l.Contains("ability is put on the stack"));
}

[Fact]
public async Task Gloom_WhiteEnchantmentCanActivateWithEnoughMana()
{
    var (state, engine, h1, h2) = CreateEngineState();

    var gloom = GameCard.Create("Gloom", "Enchantment");
    state.Player2.Battlefield.Add(gloom);
    engine.RecalculateState();

    var cop = GameCard.Create("Circle of Protection: Red");
    state.Player1.Battlefield.Add(cop);

    // With Gloom, {1} activation becomes {4}, so provide 4 colorless mana
    state.Player1.ManaPool.Add(ManaColor.Colorless, 4);

    // CoP: Red targets damage — we just need it to go on the stack
    h1.EnqueueAction(GameAction.ActivateAbility(state.Player1.Id, cop.Id));
    h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
    h2.EnqueueAction(GameAction.Pass(state.Player2.Id));

    state.CurrentPhase = Phase.MainPhase1;
    state.ActivePlayer = state.Player1;
    state.PriorityPlayer = state.Player1;
    await engine.RunPriorityAsync(CancellationToken.None);

    // Should succeed with enough mana
    state.GameLog.Should().Contain(l => l.Contains("ability is put on the stack"));
}
```

**Step 2: Run the tests — they should fail**

The cost increase for activated abilities isn't implemented yet.

**Step 3: Add `ModifyActivatedAbilityCost` to ContinuousEffectType**

In `src/MtgDecker.Engine/ContinuousEffect.cs`, add to the enum:

```csharp
ModifyActivatedAbilityCost,
```

Also add a new property to the `ContinuousEffect` record for filtering which permanents are affected:

```csharp
Func<GameCard, bool>? ActivatedAbilityCostApplies = null
```

**Step 4: Update CardDefinitions for Gloom**

In `src/MtgDecker.Engine/CardDefinitions.cs`, update Gloom:

```csharp
["Gloom"] = new(ManaCost.Parse("{2}{B}"), null, null, null, CardType.Enchantment)
{
    ContinuousEffects =
    [
        // White spells cost {3} more to cast
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyCost,
            (_, _) => true, CostMod: 3,
            CostApplies: c => c.ManaCost?.ColorRequirements.ContainsKey(ManaColor.White) == true),
        // Activated abilities of white enchantments cost {3} more to activate
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyActivatedAbilityCost,
            (_, _) => true, CostMod: 3,
            ActivatedAbilityCostApplies: c =>
                c.CardTypes.HasFlag(CardType.Enchantment)
                && c.ManaCost?.ColorRequirements.ContainsKey(ManaColor.White) == true),
    ],
},
```

**Step 5: Update ActivateAbilityHandler to check for ability cost modifications**

In `src/MtgDecker.Engine/Actions/ActivateAbilityHandler.cs`, after determining the ability cost and before the `CanPay` check (around line 80), add cost modification logic:

After `var cost = ability.Cost;` (line 54) and before `if (cost.ManaCost != null && !player.ManaPool.CanPay(cost.ManaCost))` (line 80):

```csharp
// Check for activated ability cost modifications (e.g., Gloom)
var effectiveCost = cost.ManaCost;
if (effectiveCost != null)
{
    var extraCost = state.ActiveEffects
        .Where(e => e.Type == ContinuousEffectType.ModifyActivatedAbilityCost
               && e.ActivatedAbilityCostApplies != null
               && e.ActivatedAbilityCostApplies(abilitySource))
        .Sum(e => e.CostMod);

    if (extraCost > 0)
    {
        // Add extra generic mana to the cost
        var colorReqs = new Dictionary<ManaColor, int>(effectiveCost.ColorRequirements);
        var phyrexianReqs = new Dictionary<ManaColor, int>(effectiveCost.PhyrexianRequirements);
        effectiveCost = ManaCost.Parse(effectiveCost.ToString());
        // Need to create a new ManaCost with increased generic
        // Simplest: use WithGenericReduction with negative value (adds cost)
        effectiveCost = effectiveCost.WithGenericReduction(-extraCost);
    }
}
```

Then update the CanPay check to use `effectiveCost` instead of `cost.ManaCost`.

And update the `PayManaCostAsync` call to use `effectiveCost`.

The exact line changes:

```csharp
// Line ~80: Change from:
if (cost.ManaCost != null && !player.ManaPool.CanPay(cost.ManaCost))
// To:
if (effectiveCost != null && !player.ManaPool.CanPay(effectiveCost))
```

```csharp
// Line ~254: Change from:
if (cost.ManaCost != null)
{
    await engine.PayManaCostAsync(cost.ManaCost, player, ct);
// To:
if (effectiveCost != null)
{
    await engine.PayManaCostAsync(effectiveCost, player, ct);
```

**Step 6: Run the tests — they should pass**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~OracleFixesTests.Gloom"
```

---

## Task 6: Run full test suite and commit

**Step 1: Run all engine tests**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/
```

All tests should pass (existing + new).

**Step 2: Commit**

```bash
git add src/MtgDecker.Engine/TargetFilter.cs \
        src/MtgDecker.Engine/Effects/StifleEffect.cs \
        src/MtgDecker.Engine/Effects/PyroblastEffect.cs \
        src/MtgDecker.Engine/Effects/BlueElementalBlastEffect.cs \
        src/MtgDecker.Engine/CardDefinitions.cs \
        src/MtgDecker.Engine/ContinuousEffect.cs \
        src/MtgDecker.Engine/Actions/ActivateAbilityHandler.cs \
        tests/MtgDecker.Engine.Tests/OracleFixesTests.cs

git commit -m "fix(engine): fix REB/BEB/Hydroblast targeting, Stifle, Tsabo's Web, Flash of Insight, Gloom"
```
