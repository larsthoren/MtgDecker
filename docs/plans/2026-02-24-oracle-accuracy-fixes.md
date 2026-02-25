# Oracle Accuracy Fixes Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fix 5 card implementation issues found during a strict Oracle accuracy review: Dauthi Slayer (missing), Ensnaring Bridge (wrong hand check), Perish/Crumble (missing "can't be regenerated"), Spinning Darkness (should auto-exile top 3), Gaea's Blessing (targeting + mill-only trigger).

**Architecture:** All changes are in the `MtgDecker.Engine` project (standalone game engine, no EF Core). Tests are in `tests/MtgDecker.Engine.Tests/`. Uses TDD — write failing test first, implement, verify.

**Tech Stack:** C# 14, .NET 10, xUnit + FluentAssertions, MtgDecker.Engine

---

### Task 1: Add MustAttack property to CardDefinition and Dauthi Slayer

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinition.cs`
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (RunCombatAsync, around line 975-980)
- Create: `tests/MtgDecker.Engine.Tests/DauthiSlayerTests.cs`

**Step 1: Write the failing tests**

Create `tests/MtgDecker.Engine.Tests/DauthiSlayerTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class DauthiSlayerTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler p1Handler, TestDecisionHandler p2Handler) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);

        for (int i = 0; i < 40; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}", TypeLine = "Creature" });
            p2.Library.Add(new GameCard { Name = $"Card{i}", TypeLine = "Creature" });
        }

        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, h1, h2);
    }

    [Fact]
    public void DauthiSlayer_IsRegistered_WithCorrectStats()
    {
        CardDefinitions.TryGet("Dauthi Slayer", out var def).Should().BeTrue();
        def!.ManaCost!.ConvertedManaCost.Should().Be(2);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Black).WhoseValue.Should().Be(2);
        def.Power.Should().Be(2);
        def.Toughness.Should().Be(2);
        def.CardTypes.Should().Be(CardType.Creature);
        def.Subtypes.Should().BeEquivalentTo(new[] { "Dauthi", "Soldier" });
    }

    [Fact]
    public void DauthiSlayer_HasShadow()
    {
        var card = GameCard.Create("Dauthi Slayer", "Creature — Dauthi Soldier");
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", new TestDecisionHandler()),
            new Player(Guid.NewGuid(), "P2", new TestDecisionHandler()));
        state.Player1.Battlefield.Add(card);
        state.RecalculateState();

        card.ActiveKeywords.Should().Contain(Keyword.Shadow);
    }

    [Fact]
    public void DauthiSlayer_HasMustAttack()
    {
        CardDefinitions.TryGet("Dauthi Slayer", out var def).Should().BeTrue();
        def!.MustAttack.Should().BeTrue();
    }

    [Fact]
    public async Task MustAttack_ForcesCreatureIntoAttack_EvenWhenPlayerChoosesNone()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var slayer = GameCard.Create("Dauthi Slayer", "Creature — Dauthi Soldier");
        slayer.TurnEnteredBattlefield = 0; // No summoning sickness
        state.Player1.Battlefield.Add(slayer);
        state.RecalculateState();

        // Player tries to declare no attackers
        p1Handler.EnqueueAttackers(Array.Empty<Guid>());
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid>());

        await engine.RunCombatAsync(CancellationToken.None);

        // Dauthi Slayer should have been forced to attack
        slayer.IsTapped.Should().BeTrue("Dauthi Slayer must attack each combat if able");
        state.Player2.Life.Should().Be(18, "Dauthi Slayer has 2 power and shadow (unblockable by non-shadow)");
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "DauthiSlayerTests" --verbosity minimal`
Expected: FAIL — card not registered, MustAttack property doesn't exist

**Step 3: Add MustAttack to CardDefinition**

In `src/MtgDecker.Engine/CardDefinition.cs`, add after `public bool CyclingReplaceDraw { get; init; }` (line 53):

```csharp
    public bool MustAttack { get; init; }
```

**Step 4: Add Dauthi Slayer to CardDefinitions.cs**

In `src/MtgDecker.Engine/CardDefinitions.cs`, find the `// Shadow creatures` section (around line 1528) and add before `["Soltari Foot Soldier"]`:

```csharp
            ["Dauthi Slayer"] = new(ManaCost.Parse("{B}{B}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Dauthi", "Soldier"],
                MustAttack = true,
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Dauthi Slayer",
                        GrantedKeyword: Keyword.Shadow,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
            },
```

**Step 5: Enforce MustAttack in GameEngine.RunCombatAsync**

In `src/MtgDecker.Engine/GameEngine.cs`, after line ~980 where `validAttackerIds` is computed, add logic to force MustAttack creatures:

```csharp
        // Force MustAttack creatures into the attack
        var mustAttackIds = eligibleAttackers
            .Where(c => CardDefinitions.TryGet(c.Name, out var d) && d.MustAttack)
            .Select(c => c.Id)
            .ToList();
        foreach (var id in mustAttackIds)
        {
            if (!validAttackerIds.Contains(id))
                validAttackerIds.Add(id);
        }
```

This goes between the `validAttackerIds` filtering (line ~980) and the `if (validAttackerIds.Count == 0)` check (line ~982).

**Step 6: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "DauthiSlayerTests" --verbosity minimal`
Expected: PASS

**Step 7: Run full test suite**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --verbosity minimal`
Expected: All pass

---

### Task 2: Fix Ensnaring Bridge controller hand check

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (lines 931-942)
- Create: `tests/MtgDecker.Engine.Tests/EnsnaringBridgeControllerTests.cs`

**Step 1: Write the failing tests**

Create `tests/MtgDecker.Engine.Tests/EnsnaringBridgeControllerTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class EnsnaringBridgeControllerTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler p1Handler, TestDecisionHandler p2Handler) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);

        for (int i = 0; i < 40; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}", TypeLine = "Creature" });
            p2.Library.Add(new GameCard { Name = $"Card{i}", TypeLine = "Creature" });
        }

        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, h1, h2);
    }

    [Fact]
    public async Task DefenderControlsBridge_With1Card_AttackerPower2_CannotAttack()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        // Attacker (P1) has a 3/3 creature
        var creature = new GameCard { Name = "Bear", TypeLine = "Creature", Power = 3, Toughness = 3, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(creature);

        // Defender (P2) controls Ensnaring Bridge with 1 card in hand
        var bridge = GameCard.Create("Ensnaring Bridge", "Artifact");
        state.Player2.Battlefield.Add(bridge);
        // P2 has 1 card in hand (from start game draw minus some)
        while (state.Player2.Hand.Cards.Count > 1)
            state.Player2.Hand.Cards.RemoveAt(0);

        // Attacker (P1) has 5 cards in hand — should NOT matter since P1 doesn't control Bridge
        // P1 hand is already full from start game draw

        p1Handler.EnqueueAttackers(new List<Guid> { creature.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid>());

        await engine.RunCombatAsync(CancellationToken.None);

        // 3/3 creature should NOT be able to attack because defender's bridge controller hand = 1
        state.Player2.Life.Should().Be(20, "creature power (3) > bridge controller hand size (1), attack should be blocked");
    }

    [Fact]
    public async Task AttackerControlsBridge_AttackerHas1Card_DefenderHas5_CreatureCanAttack()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        // Attacker (P1) controls Bridge with 1 card in hand
        var bridge = GameCard.Create("Ensnaring Bridge", "Artifact");
        state.Player1.Battlefield.Add(bridge);
        while (state.Player1.Hand.Cards.Count > 1)
            state.Player1.Hand.Cards.RemoveAt(0);

        // Attacker has a 1/1 creature (power <= P1's hand of 1)
        var smallCreature = new GameCard { Name = "Elf", TypeLine = "Creature", Power = 1, Toughness = 1, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(smallCreature);

        // Defender (P2) has 5 cards — doesn't matter for P1's bridge
        while (state.Player2.Hand.Cards.Count > 5)
            state.Player2.Hand.Cards.RemoveAt(0);

        p1Handler.EnqueueAttackers(new List<Guid> { smallCreature.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid>());

        await engine.RunCombatAsync(CancellationToken.None);

        // 1/1 can attack since power (1) <= P1's hand size (1)
        state.Player2.Life.Should().Be(19, "1/1 creature can attack — power <= bridge controller hand size");
    }

    [Fact]
    public async Task AttackerControlsBridge_AttackerHas1Card_Power2Creature_CannotAttack()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        // Attacker (P1) controls Bridge with 1 card in hand
        var bridge = GameCard.Create("Ensnaring Bridge", "Artifact");
        state.Player1.Battlefield.Add(bridge);
        while (state.Player1.Hand.Cards.Count > 1)
            state.Player1.Hand.Cards.RemoveAt(0);

        // Attacker has a 2/2 creature (power > P1's hand of 1)
        var creature = new GameCard { Name = "Bear", TypeLine = "Creature", Power = 2, Toughness = 2, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(creature);

        p1Handler.EnqueueAttackers(new List<Guid> { creature.Id });

        await engine.RunCombatAsync(CancellationToken.None);

        // 2/2 should NOT attack because P1 controls bridge with hand=1
        state.Player2.Life.Should().Be(20);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "EnsnaringBridgeControllerTests" --verbosity minimal`
Expected: First test FAIL (currently uses attacker's hand, not bridge controller's)

**Step 3: Fix the Ensnaring Bridge check in GameEngine.cs**

Replace lines 931-942 in `src/MtgDecker.Engine/GameEngine.cs`:

OLD:
```csharp
        // Ensnaring Bridge: creatures with power > cards in controller's hand can't attack
        var ensnaringBridges = new[] { _state.Player1, _state.Player2 }
            .SelectMany(p => p.Battlefield.Cards)
            .Where(c => string.Equals(c.Name, "Ensnaring Bridge", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (ensnaringBridges.Count > 0)
        {
            var handSize = attacker.Hand.Cards.Count;
            eligibleAttackers = eligibleAttackers
                .Where(c => (c.Power ?? 0) <= handSize)
                .ToList();
        }
```

NEW:
```csharp
        // Ensnaring Bridge: creatures with power > bridge controller's hand size can't attack
        var bridgeControllers = _state.Player1.Battlefield.Cards
            .Where(c => string.Equals(c.Name, "Ensnaring Bridge", StringComparison.OrdinalIgnoreCase))
            .Select(_ => _state.Player1)
            .Concat(_state.Player2.Battlefield.Cards
                .Where(c => string.Equals(c.Name, "Ensnaring Bridge", StringComparison.OrdinalIgnoreCase))
                .Select(_ => _state.Player2));

        foreach (var bridgeController in bridgeControllers)
        {
            var handSize = bridgeController.Hand.Cards.Count;
            eligibleAttackers = eligibleAttackers
                .Where(c => (c.Power ?? 0) <= handSize)
                .ToList();
        }
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "EnsnaringBridgeControllerTests" --verbosity minimal`
Expected: PASS

**Step 5: Run full test suite**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --verbosity minimal`
Expected: All pass

---

### Task 3: Add "can't be regenerated" to Perish and Crumble

**Files:**
- Modify: `src/MtgDecker.Engine/Effects/DestroyAllByColorEffect.cs`
- Modify: `src/MtgDecker.Engine/Effects/CrumbleEffect.cs`
- Create: `tests/MtgDecker.Engine.Tests/CantBeRegeneratedTests.cs`

**Step 1: Write the failing tests**

Create `tests/MtgDecker.Engine.Tests/CantBeRegeneratedTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class CantBeRegeneratedTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler p1Handler, TestDecisionHandler p2Handler) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);

        for (int i = 0; i < 40; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}", TypeLine = "Creature" });
            p2.Library.Add(new GameCard { Name = $"Card{i}", TypeLine = "Creature" });
        }

        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, h1, h2);
    }

    [Fact]
    public void Perish_ClearsRegenerationShields_BeforeDestroying()
    {
        // River Boa is {1}{G}, green creature with regeneration
        var riverBoa = GameCard.Create("River Boa", "Creature — Snake");
        riverBoa.TurnEnteredBattlefield = 0;
        riverBoa.RegenerationShields = 1; // Has a regen shield

        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", new TestDecisionHandler()),
            new Player(Guid.NewGuid(), "P2", new TestDecisionHandler()));
        state.Player2.Battlefield.Add(riverBoa);

        // Cast Perish — destroys all green creatures, can't be regenerated
        var perishCard = GameCard.Create("Perish", "Sorcery");
        var spell = new StackObject(
            perishCard,
            state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(),
            0);

        var effect = new Effects.DestroyAllByColorEffect(ManaColor.Green, CardType.Creature);
        effect.Resolve(state, spell);

        // River Boa should be in graveyard despite having a regen shield
        state.Player2.Battlefield.Cards.Should().NotContain(c => c.Name == "River Boa");
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Name == "River Boa");
    }

    [Fact]
    public void Crumble_ClearsRegenerationShields_BeforeDestroying()
    {
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", new TestDecisionHandler()),
            new Player(Guid.NewGuid(), "P2", new TestDecisionHandler()));

        // Create an artifact creature with regen shield
        var artifact = new GameCard
        {
            Name = "TestArtifact",
            TypeLine = "Artifact",
            CardTypes = CardType.Artifact,
            ManaCost = ManaCost.Parse("{3}"),
            RegenerationShields = 1,
        };
        state.Player2.Battlefield.Add(artifact);

        var crumbleCard = GameCard.Create("Crumble", "Instant");
        var spell = new StackObject(
            crumbleCard,
            state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(artifact.Id, state.Player2.Id, ZoneType.Battlefield) },
            0);

        var effect = new Effects.CrumbleEffect();
        effect.Resolve(state, spell);

        // Artifact should be destroyed despite regen shield
        state.Player2.Battlefield.Cards.Should().NotContain(c => c.Name == "TestArtifact");
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Name == "TestArtifact");
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "CantBeRegeneratedTests" --verbosity minimal`
Expected: FAIL — Perish doesn't clear regen shields. Note: the test checks the effect directly (not through the full engine lethal damage flow), so the regen shield check in `CheckLethalDamage` is NOT relevant here. But we still need to clear them so that if CheckLethalDamage processes them later, the shields don't save them.

Actually, looking at the Perish effect code again — it directly removes from battlefield and adds to graveyard, it does NOT go through CheckLethalDamage. So clearing regen shields is just safety. The test should pass already for destruction since the effect directly moves to graveyard.

Wait — the test AS WRITTEN should already pass because DestroyAllByColorEffect directly moves to graveyard (bypasses regeneration entirely). So let me revise the test approach. The real issue is: if someone gives River Boa a regeneration shield, and then Perish fires, the card should NOT regenerate. Since the current code does `Battlefield.Remove` + `Graveyard.Add` (no regen check), it already bypasses regen. But for consistency and future-proofing, we should still clear shields. Let me adjust the tests to verify the shields are cleared.

Revised test focus: Verify `card.RegenerationShields` is 0 after effect resolves.

```csharp
    [Fact]
    public void Perish_ClearsRegenerationShields()
    {
        var riverBoa = GameCard.Create("River Boa", "Creature — Snake");
        riverBoa.TurnEnteredBattlefield = 0;
        riverBoa.RegenerationShields = 2;

        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", new TestDecisionHandler()),
            new Player(Guid.NewGuid(), "P2", new TestDecisionHandler()));
        state.Player2.Battlefield.Add(riverBoa);

        var perishCard = GameCard.Create("Perish", "Sorcery");
        var spell = new StackObject(perishCard, state.Player1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);

        new Effects.DestroyAllByColorEffect(ManaColor.Green, CardType.Creature).Resolve(state, spell);

        state.Player2.Graveyard.Cards.Should().Contain(c => c.Name == "River Boa");
        riverBoa.RegenerationShields.Should().Be(0, "Perish says 'can't be regenerated'");
    }
```

**Step 3: Implement — clear regen shields in DestroyAllByColorEffect**

In `src/MtgDecker.Engine/Effects/DestroyAllByColorEffect.cs`, add `card.RegenerationShields = 0;` before removal:

```csharp
            foreach (var card in targets)
            {
                card.RegenerationShields = 0; // Can't be regenerated
                player.Battlefield.Remove(card);
                player.Graveyard.Add(card);
            }
```

**Step 4: Implement — clear regen shields in CrumbleEffect**

In `src/MtgDecker.Engine/Effects/CrumbleEffect.cs`, add `artifact.RegenerationShields = 0;` before destruction:

After `if (artifact == null) return;`:
```csharp
        artifact.RegenerationShields = 0; // Can't be regenerated
```

**Step 5: Run tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "CantBeRegeneratedTests" --verbosity minimal`
Expected: PASS

---

### Task 4: Fix Spinning Darkness auto-exile top 3 black cards

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (PayAlternateCostAsync, around line 623-647)
- Create: `tests/MtgDecker.Engine.Tests/SpinningDarknessAutoExileTests.cs`

**Step 1: Write the failing test**

Create `tests/MtgDecker.Engine.Tests/SpinningDarknessAutoExileTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class SpinningDarknessAutoExileTests
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
    public async Task SpinningDarkness_ExilesTop3BlackCards_NotPlayerChoice()
    {
        var (engine, state, h1, _) = CreateSetup();

        // Put 5 cards in graveyard:
        // Bottom: NonBlack1, Black1, Black2, NonBlack2, Black3 (top)
        // "Top" = last added = end of list
        var nonBlack1 = new GameCard { Name = "NonBlack1", ManaCost = ManaCost.Parse("{1}{G}") };
        var black1 = new GameCard { Name = "Black1", ManaCost = ManaCost.Parse("{B}") };
        var black2 = new GameCard { Name = "Black2", ManaCost = ManaCost.Parse("{1}{B}") };
        var nonBlack2 = new GameCard { Name = "NonBlack2", ManaCost = ManaCost.Parse("{2}{R}") };
        var black3 = new GameCard { Name = "Black3", ManaCost = ManaCost.Parse("{2}{B}{B}") };

        state.Player1.Graveyard.Add(nonBlack1);
        state.Player1.Graveyard.Add(black1);
        state.Player1.Graveyard.Add(black2);
        state.Player1.Graveyard.Add(nonBlack2);
        state.Player1.Graveyard.Add(black3);

        var spinningDarkness = GameCard.Create("Spinning Darkness", "Instant");
        state.Player1.Hand.Add(spinningDarkness);

        CardDefinitions.TryGet("Spinning Darkness", out var def).Should().BeTrue();
        await engine.PayAlternateCostAsync(def!.AlternateCost!, state.Player1, spinningDarkness, CancellationToken.None);

        // Top 3 black cards from graveyard should be exiled: Black3, Black2, Black1
        // (from top = end of list, working downward)
        state.Player1.Exile.Cards.Select(c => c.Name).Should().BeEquivalentTo(
            new[] { "Black3", "Black2", "Black1" });

        // NonBlack cards should remain in graveyard
        state.Player1.Graveyard.Cards.Select(c => c.Name).Should().BeEquivalentTo(
            new[] { "NonBlack1", "NonBlack2" });
    }
}
```

**Step 2: Run test to verify it fails**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "SpinningDarknessAutoExileTests" --verbosity minimal`
Expected: FAIL — currently uses ChooseCard (player picks), not automatic top-3

**Step 3: Fix PayAlternateCostAsync**

In `src/MtgDecker.Engine/GameEngine.cs`, replace the `ExileFromGraveyardCount` handling block (lines ~623-647):

OLD:
```csharp
        // Exile cards from graveyard
        if (alt.ExileFromGraveyardCount > 0 && alt.ExileFromGraveyardColor.HasValue)
        {
            for (int i = 0; i < alt.ExileFromGraveyardCount; i++)
            {
                var eligible = player.Graveyard.Cards.Where(c =>
                    c.ManaCost != null && c.ManaCost.ColorRequirements.ContainsKey(alt.ExileFromGraveyardColor.Value)).ToList();

                if (eligible.Count == 0) break;

                var chosenId = await player.DecisionHandler.ChooseCard(
                    eligible, $"Choose a {alt.ExileFromGraveyardColor} card to exile from graveyard ({i + 1}/{alt.ExileFromGraveyardCount})", optional: false, ct);

                if (chosenId.HasValue)
                {
                    var exiled = player.Graveyard.Cards.FirstOrDefault(c => c.Id == chosenId.Value);
                    if (exiled != null)
                    {
                        player.Graveyard.RemoveById(exiled.Id);
                        player.Exile.Add(exiled);
                        _state.Log($"{player.Name} exiles {exiled.Name} from graveyard.");
                    }
                }
            }
        }
```

NEW:
```csharp
        // Exile the top N cards of the required color from graveyard (not player choice — Oracle says "the top three")
        if (alt.ExileFromGraveyardCount > 0 && alt.ExileFromGraveyardColor.HasValue)
        {
            var exiledCount = 0;
            // Iterate from top of graveyard (end of list) to bottom
            for (int i = player.Graveyard.Cards.Count - 1; i >= 0 && exiledCount < alt.ExileFromGraveyardCount; i--)
            {
                var card = player.Graveyard.Cards[i];
                if (card.ManaCost != null && card.ManaCost.ColorRequirements.ContainsKey(alt.ExileFromGraveyardColor.Value))
                {
                    player.Graveyard.RemoveById(card.Id);
                    player.Exile.Add(card);
                    _state.Log($"{player.Name} exiles {card.Name} from graveyard.");
                    exiledCount++;
                }
            }
        }
```

**Step 4: Run tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "SpinningDarknessAutoExileTests" --verbosity minimal`
Expected: PASS

**Step 5: Run full test suite** to check no regressions

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --verbosity minimal`
Expected: All pass (existing alternate cost tests may need minor adjustment if they relied on ChooseCard for graveyard exile)

---

### Task 5: Fix Gaea's Blessing — target any player + mill-only trigger

**Files:**
- Modify: `src/MtgDecker.Engine/Effects/GaeasBlessingEffect.cs`
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs` (add TargetFilter.Player() to Gaea's Blessing)
- Modify: `src/MtgDecker.Engine/CardDefinition.cs` (add ShuffleGraveyardOnMill)
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (change ShuffleGraveyardOnDeath check to ShuffleGraveyardOnMill for Gaea's Blessing; move trigger to mill code)
- Modify: `src/MtgDecker.Engine/Effects/BrainFreezeEffect.cs` (add ShuffleGraveyardOnMill check in mill)
- Create: `tests/MtgDecker.Engine.Tests/GaeasBlessingFixTests.cs`

**Step 1: Write the failing tests**

Create `tests/MtgDecker.Engine.Tests/GaeasBlessingFixTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class GaeasBlessingFixTests
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
    public void GaeasBlessing_HasPlayerTargetFilter()
    {
        CardDefinitions.TryGet("Gaea's Blessing", out var def).Should().BeTrue();
        def!.TargetFilter.Should().NotBeNull("Gaea's Blessing should target a player");
    }

    [Fact]
    public async Task GaeasBlessing_TargetingOpponent_ShufflesOpponentGraveyard()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();

        // Put some cards in opponent's graveyard
        var graveyardCard1 = new GameCard { Name = "OppGY1" };
        var graveyardCard2 = new GameCard { Name = "OppGY2" };
        state.Player2.Graveyard.Add(graveyardCard1);
        state.Player2.Graveyard.Add(graveyardCard2);
        var p2LibraryCountBefore = state.Player2.Library.Cards.Count;

        // Cast Gaea's Blessing targeting opponent
        var blessing = GameCard.Create("Gaea's Blessing", "Sorcery");
        var spell = new StackObject(
            blessing,
            state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(Guid.Empty, state.Player2.Id, ZoneType.None) },
            0);

        // Enqueue card choices for the up to 3 cards from opponent's graveyard
        h1.EnqueueCardChoice(graveyardCard1.Id);
        h1.EnqueueCardChoice(graveyardCard2.Id);
        h1.EnqueueCardChoice(null); // stop choosing after 2

        CardDefinitions.TryGet("Gaea's Blessing", out var def).Should().BeTrue();
        var handler = state.Player1.DecisionHandler;
        await def!.Effect!.ResolveAsync(state, spell, handler, CancellationToken.None);

        // Opponent's graveyard cards should be shuffled into opponent's library
        state.Player2.Graveyard.Cards.Should().BeEmpty("all graveyard cards were chosen");
        state.Player2.Library.Cards.Count.Should().Be(p2LibraryCountBefore + 2);

        // Caster draws a card (not the target player)
        // This is verified by P1's hand growing
    }

    [Fact]
    public void GaeasBlessing_HasShuffleGraveyardOnMill()
    {
        CardDefinitions.TryGet("Gaea's Blessing", out var def).Should().BeTrue();
        def!.ShuffleGraveyardOnMill.Should().BeTrue();
        // ShuffleGraveyardOnDeath should be false (or not used for mill-only trigger)
    }

    [Fact]
    public void GaeasBlessing_DiscardedFromHand_DoesNotTriggerShuffle()
    {
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", new TestDecisionHandler()),
            new Player(Guid.NewGuid(), "P2", new TestDecisionHandler()));

        // Put some cards in P1's graveyard first
        state.Player1.Graveyard.Add(new GameCard { Name = "ExistingGY" });

        // Simulate discarding Gaea's Blessing from hand
        var blessing = GameCard.Create("Gaea's Blessing", "Sorcery");
        var engine = new GameEngine(state);

        // Use MoveToGraveyardWithReplacement — should NOT trigger shuffle for discard
        engine.MoveToGraveyardWithReplacement(blessing, state.Player1);

        // Both the existing card AND the blessing should be in graveyard (no shuffle)
        state.Player1.Graveyard.Cards.Should().HaveCount(2);
        state.Player1.Graveyard.Cards.Select(c => c.Name).Should().Contain("ExistingGY");
        state.Player1.Graveyard.Cards.Select(c => c.Name).Should().Contain("Gaea's Blessing");
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "GaeasBlessingFixTests" --verbosity minimal`
Expected: FAIL

**Step 3a: Add ShuffleGraveyardOnMill to CardDefinition**

In `src/MtgDecker.Engine/CardDefinition.cs`, add after `ShuffleGraveyardOnDeath`:

```csharp
    public bool ShuffleGraveyardOnMill { get; init; }
```

**Step 3b: Update Gaea's Blessing in CardDefinitions.cs**

Change the Gaea's Blessing entry to add TargetFilter.Player() and use ShuffleGraveyardOnMill instead of ShuffleGraveyardOnDeath:

```csharp
            ["Gaea's Blessing"] = new(ManaCost.Parse("{1}{G}"), null, null, null, CardType.Sorcery,
                TargetFilter.Player(), new GaeasBlessingEffect())
            {
                ShuffleGraveyardOnMill = true,
            },
```

**Step 3c: Fix GaeasBlessingEffect to use target player**

In `src/MtgDecker.Engine/Effects/GaeasBlessingEffect.cs`:

```csharp
public class GaeasBlessingEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var controller = state.GetPlayer(spell.ControllerId);

        // Target player (from spell targets)
        Player targetPlayer;
        if (spell.Targets.Count > 0)
            targetPlayer = state.GetPlayer(spell.Targets[0].PlayerId);
        else
            targetPlayer = controller;

        // Choose up to 3 cards from target player's graveyard to shuffle into their library
        var graveyardCards = targetPlayer.Graveyard.Cards.ToList();
        if (graveyardCards.Count > 0)
        {
            var maxChoose = Math.Min(3, graveyardCards.Count);
            var shuffled = 0;

            for (int i = 0; i < maxChoose; i++)
            {
                var eligible = targetPlayer.Graveyard.Cards.ToList();
                if (eligible.Count == 0) break;

                var chosenId = await handler.ChooseCard(
                    eligible, $"Choose a card to shuffle into library ({i + 1}/{maxChoose})", optional: true, ct);

                if (!chosenId.HasValue) break;

                var card = targetPlayer.Graveyard.Cards.FirstOrDefault(c => c.Id == chosenId.Value);
                if (card != null)
                {
                    targetPlayer.Graveyard.RemoveById(card.Id);
                    targetPlayer.Library.Add(card);
                    shuffled++;
                }
            }

            if (shuffled > 0)
            {
                targetPlayer.Library.Shuffle();
                state.Log($"{targetPlayer.Name} shuffles {shuffled} card(s) from graveyard into library (Gaea's Blessing).");
            }
        }

        // Caster draws a card (always the controller, not the target)
        var drawn = controller.Library.DrawFromTop();
        if (drawn != null)
        {
            controller.Hand.Add(drawn);
            state.Log($"{controller.Name} draws a card (Gaea's Blessing).");
        }
    }
}
```

**Step 3d: Fix MoveToGraveyardWithReplacement**

In `src/MtgDecker.Engine/GameEngine.cs`, the `MoveToGraveyardWithReplacement` method currently triggers shuffle for ShuffleGraveyardOnDeath. For Gaea's Blessing, this is wrong — it should only trigger when milled (from library). Change the condition to NOT trigger for ShuffleGraveyardOnMill cards:

Current code checks `def.ShuffleGraveyardOnDeath`. Gaea's Blessing currently has `ShuffleGraveyardOnDeath = true`. We're changing it to `ShuffleGraveyardOnMill = true` and removing `ShuffleGraveyardOnDeath`. So the existing check will naturally stop triggering for Gaea's Blessing since we removed the flag.

The ShuffleGraveyardOnDeath flag stays on Emrakul (where it's correct — Emrakul shuffles from any zone to graveyard). No change needed for Emrakul.

**Step 3e: Add mill trigger for ShuffleGraveyardOnMill**

When a card with `ShuffleGraveyardOnMill = true` is milled (moved from library to graveyard), trigger the shuffle. Add this check in the BrainFreezeEffect.MillCards method and anywhere else cards move from library to graveyard.

The cleanest approach: add a static helper method that the mill code calls. In `GameEngine.cs`, add a public static method or instance method:

Actually, looking at the mill code — BrainFreezeEffect directly adds to `player.Graveyard.Add(card)`. The simplest fix is to check after adding to graveyard whether the card has `ShuffleGraveyardOnMill`, and if so, shuffle everything. Add this in BrainFreezeEffect's MillCards.

But there's a subtlety: the mill trigger for Gaea's Blessing is a replacement effect — "When Gaea's Blessing is put into your graveyard from your library, shuffle your graveyard into your library." This means the card actually goes to graveyard first, then triggers. So:

In `BrainFreezeEffect.MillCards`, after `player.Graveyard.Add(card)`, check:
```csharp
if (CardDefinitions.TryGet(card.Name, out var millDef) && millDef.ShuffleGraveyardOnMill)
{
    // Shuffle entire graveyard into library
    foreach (var gyCard in player.Graveyard.Cards.ToList())
    {
        player.Graveyard.Remove(gyCard);
        player.Library.AddToTop(gyCard);
    }
    player.Library.Shuffle();
    state.Log($"{card.Name} was milled — {player.Name} shuffles their graveyard into their library.");
    return; // Stop milling after shuffle
}
```

**Step 4: Run tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "GaeasBlessingFixTests" --verbosity minimal`
Expected: PASS

**Step 5: Run full test suite**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --verbosity minimal`
Expected: All pass

---

### Task 6: Final verification and commit

**Step 1: Run full test suite one final time**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --verbosity minimal`
Expected: All tests pass

**Step 2: Build the web project to ensure no compilation errors**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet build src/MtgDecker.Web/`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add src/MtgDecker.Engine/CardDefinition.cs src/MtgDecker.Engine/CardDefinitions.cs src/MtgDecker.Engine/GameEngine.cs src/MtgDecker.Engine/Effects/DestroyAllByColorEffect.cs src/MtgDecker.Engine/Effects/CrumbleEffect.cs src/MtgDecker.Engine/Effects/GaeasBlessingEffect.cs src/MtgDecker.Engine/Effects/BrainFreezeEffect.cs tests/MtgDecker.Engine.Tests/DauthiSlayerTests.cs tests/MtgDecker.Engine.Tests/EnsnaringBridgeControllerTests.cs tests/MtgDecker.Engine.Tests/CantBeRegeneratedTests.cs tests/MtgDecker.Engine.Tests/SpinningDarknessAutoExileTests.cs tests/MtgDecker.Engine.Tests/GaeasBlessingFixTests.cs
git commit -m "fix(engine): fix Dauthi Slayer, Ensnaring Bridge, Perish/Crumble regen, Spinning Darkness, Gaea's Blessing"
```
