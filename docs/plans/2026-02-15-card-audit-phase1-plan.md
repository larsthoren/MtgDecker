# Card Audit Phase 1: Registry Quick Fixes

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fix ~19 card definitions with trivial registry-only changes plus two small engine hooks (Lifelink, Defender keywords).

**Architecture:** All changes are in `CardDefinitions.cs` (registry entries), `Keyword.cs` (two new enum values), and `GameEngine.cs` (two keyword enforcement hooks). No new effect classes needed.

**Tech Stack:** C# 14, xUnit, FluentAssertions

---

### Task 1: Fix Pain Lands (4 cards)

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs` (lines 285-291)
- Test: `tests/MtgDecker.Engine.Tests/CardDefinitionsTests.cs`

**Step 1: Write failing tests**

Add to `CardDefinitionsTests.cs`:

```csharp
[Theory]
[InlineData("Caves of Koilos")]
[InlineData("Llanowar Wastes")]
[InlineData("Battlefield Forge")]
[InlineData("Adarkar Wastes")]
public void PainLand_HasPainColors(string cardName)
{
    CardDefinitions.TryGet(cardName, out var def);

    def!.ManaAbility.Should().NotBeNull();
    def.ManaAbility!.PainColors.Should().NotBeNull(
        because: $"{cardName} should deal damage when tapping for colored mana");
    def.ManaAbility.PainColors!.Count.Should().BeGreaterThan(0);
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "PainLand_HasPainColors" -v n`
Expected: FAIL — PainColors is null because these lands use `Choice` not `PainChoice`

**Step 3: Fix the registry entries**

In `CardDefinitions.cs`, change these four entries:

```csharp
// Caves of Koilos — was Choice, should be PainChoice
["Caves of Koilos"] = new(null, ManaAbility.PainChoice(
    [ManaColor.Colorless, ManaColor.White, ManaColor.Black],
    [ManaColor.White, ManaColor.Black]), null, null, CardType.Land),

// Llanowar Wastes — was Choice, should be PainChoice
["Llanowar Wastes"] = new(null, ManaAbility.PainChoice(
    [ManaColor.Colorless, ManaColor.Black, ManaColor.Green],
    [ManaColor.Black, ManaColor.Green]), null, null, CardType.Land),

// Battlefield Forge — was Choice, should be PainChoice
["Battlefield Forge"] = new(null, ManaAbility.PainChoice(
    [ManaColor.Colorless, ManaColor.Red, ManaColor.White],
    [ManaColor.Red, ManaColor.White]), null, null, CardType.Land),

// Adarkar Wastes — was Choice, should be PainChoice
["Adarkar Wastes"] = new(null, ManaAbility.PainChoice(
    [ManaColor.Colorless, ManaColor.White, ManaColor.Blue],
    [ManaColor.White, ManaColor.Blue]), null, null, CardType.Land),
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "PainLand_HasPainColors" -v n`
Expected: PASS

**Step 5: Run all existing tests to check for regressions**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v n`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/CardDefinitions.cs tests/MtgDecker.Engine.Tests/CardDefinitionsTests.cs
git commit -m "fix(engine): pain lands deal damage for colored mana

Change Caves of Koilos, Llanowar Wastes, Battlefield Forge, Adarkar
Wastes from ManaAbility.Choice to ManaAbility.PainChoice so they
correctly deal 1 damage when tapping for colored mana."
```

---

### Task 2: Fix Missing Land Abilities (3 cards)

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs` (lines 113-118, 276)
- Test: `tests/MtgDecker.Engine.Tests/CardDefinitionsTests.cs`

**Step 1: Write failing tests**

Add to `CardDefinitionsTests.cs`:

```csharp
[Fact]
public void RishadanPort_HasColorlessManaAbility()
{
    CardDefinitions.TryGet("Rishadan Port", out var def);

    def!.ManaAbility.Should().NotBeNull(
        because: "Rishadan Port taps for {C}");
    def.ManaAbility!.FixedColor.Should().Be(ManaColor.Colorless);
}

[Fact]
public void Wasteland_HasColorlessManaAbility()
{
    CardDefinitions.TryGet("Wasteland", out var def);

    def!.ManaAbility.Should().NotBeNull(
        because: "Wasteland taps for {C}");
    def.ManaAbility!.FixedColor.Should().Be(ManaColor.Colorless);
}

[Fact]
public void ScaldingTarn_HasFetchAbility()
{
    CardDefinitions.TryGet("Scalding Tarn", out var def);

    def!.FetchAbility.Should().NotBeNull(
        because: "Scalding Tarn fetches Island or Mountain");
    def.FetchAbility!.SearchTypes.Should().BeEquivalentTo(
        new[] { "Island", "Mountain" });
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "RishadanPort_HasColorless|Wasteland_HasColorless|ScaldingTarn_HasFetch" -v n`
Expected: FAIL — ManaAbility is null for Port/Wasteland, FetchAbility is null for Scalding Tarn

**Step 3: Fix the registry entries**

In `CardDefinitions.cs`:

```csharp
// Rishadan Port — add ManaAbility.Fixed(Colorless)
["Rishadan Port"] = new(null, ManaAbility.Fixed(ManaColor.Colorless), null, null, CardType.Land)
{
    ActivatedAbility = new(new ActivatedAbilityCost(TapSelf: true, ManaCost: ManaCost.Parse("{1}")), new TapTargetEffect(), c => c.IsLand),
},

// Wasteland — add ManaAbility.Fixed(Colorless)
["Wasteland"] = new(null, ManaAbility.Fixed(ManaColor.Colorless), null, null, CardType.Land)
{
    ActivatedAbility = new(new ActivatedAbilityCost(TapSelf: true, SacrificeSelf: true), new DestroyTargetEffect(), c => c.IsLand && !c.IsBasicLand),
},

// Scalding Tarn — add FetchAbility
["Scalding Tarn"] = new(null, null, null, null, CardType.Land) { FetchAbility = new FetchAbility(["Island", "Mountain"]) },
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "RishadanPort_HasColorless|Wasteland_HasColorless|ScaldingTarn_HasFetch" -v n`
Expected: PASS

**Step 5: Run all tests for regressions**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v n`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/CardDefinitions.cs tests/MtgDecker.Engine.Tests/CardDefinitionsTests.cs
git commit -m "fix(engine): add missing mana abilities to Rishadan Port, Wasteland, Scalding Tarn

Rishadan Port and Wasteland can tap for colorless mana.
Scalding Tarn fetches Island or Mountain."
```

---

### Task 3: Fix Missing Land Subtypes (2 cards)

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs` (lines 274, 275)
- Test: `tests/MtgDecker.Engine.Tests/CardDefinitionsTests.cs`

**Step 1: Write failing tests**

Add to `CardDefinitionsTests.cs`:

```csharp
[Fact]
public void Island_HasIslandSubtype()
{
    CardDefinitions.TryGet("Island", out var def);

    def!.Subtypes.Should().Contain("Island",
        because: "Island has the Island land subtype for fetchland interactions");
}

[Fact]
public void VolcanicIsland_HasDualSubtypes()
{
    CardDefinitions.TryGet("Volcanic Island", out var def);

    def!.Subtypes.Should().Contain("Island");
    def.Subtypes.Should().Contain("Mountain");
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "Island_HasIslandSubtype|VolcanicIsland_HasDualSubtypes" -v n`
Expected: FAIL — Subtypes is empty

**Step 3: Fix the registry entries**

In `CardDefinitions.cs`:

```csharp
["Island"] = new(null, ManaAbility.Fixed(ManaColor.Blue), null, null, CardType.Land) { Subtypes = ["Island"] },
["Volcanic Island"] = new(null, ManaAbility.Choice(ManaColor.Blue, ManaColor.Red), null, null, CardType.Land) { Subtypes = ["Island", "Mountain"] },
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "Island_HasIslandSubtype|VolcanicIsland_HasDualSubtypes" -v n`
Expected: PASS

**Step 5: Run all tests for regressions**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v n`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/CardDefinitions.cs tests/MtgDecker.Engine.Tests/CardDefinitionsTests.cs
git commit -m "fix(engine): add missing land subtypes to Island and Volcanic Island

Island gets ['Island'] subtype, Volcanic Island gets ['Island', 'Mountain'].
These subtypes matter for fetchland searches and landwalk abilities."
```

---

### Task 4: Add Lifelink and Defender to Keyword Enum + Engine Enforcement

**Files:**
- Modify: `src/MtgDecker.Engine/Enums/Keyword.cs`
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (attack eligibility + combat damage)
- Test: `tests/MtgDecker.Engine.Tests/CombatIntegrationTests.cs`

**Step 1: Write failing tests**

Add to `CombatIntegrationTests.cs`:

```csharp
[Fact]
public async Task Creature_WithDefender_CannotAttack()
{
    var handler1 = new TestDecisionHandler();
    var handler2 = new TestDecisionHandler();
    var p1 = new Player(Guid.NewGuid(), "P1", handler1);
    var p2 = new Player(Guid.NewGuid(), "P2", handler2);
    var state = new GameState(p1, p2);
    var engine = new GameEngine(state);

    var wall = new GameCard
    {
        Name = "Test Wall", BasePower = 0, BaseToughness = 4,
        CardTypes = CardType.Creature, TurnEnteredBattlefield = 0
    };
    wall.ActiveKeywords.Add(Keyword.Defender);
    p1.Battlefield.Add(wall);

    // The wall is the only creature — it should not appear as eligible
    handler1.EnqueueAttackers(new List<Guid> { wall.Id });
    handler2.EnqueueBlockers(new Dictionary<Guid, Guid>());

    state.TurnNumber = 2;
    state.ActivePlayer = p1;
    state.Phase = Phase.Combat;

    await engine.RunCombatAsync(default);

    // Wall should NOT have dealt any damage (it can't attack)
    p2.Life.Should().Be(20, "creature with Defender cannot attack");
}

[Fact]
public void Lifelink_Creature_Heals_Controller_OnCombatDamage()
{
    var handler1 = new TestDecisionHandler();
    var handler2 = new TestDecisionHandler();
    var p1 = new Player(Guid.NewGuid(), "P1", handler1);
    var p2 = new Player(Guid.NewGuid(), "P2", handler2);
    var state = new GameState(p1, p2);
    var engine = new GameEngine(state);

    var angel = new GameCard
    {
        Name = "Test Angel", BasePower = 4, BaseToughness = 5,
        CardTypes = CardType.Creature, TurnEnteredBattlefield = 0
    };
    angel.ActiveKeywords.Add(Keyword.Lifelink);
    p1.Battlefield.Add(angel);

    // Set up combat — angel attacks unblocked
    state.Combat = new CombatState();
    state.Combat.AddAttacker(angel.Id);

    // Start P1 at 15 life to verify gain
    p1.AdjustLife(-5); // now at 15

    // Resolve combat damage directly
    var method = typeof(GameEngine).GetMethod("ResolveCombatDamage",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    method!.Invoke(engine, new object[] { p1, p2 });

    p2.Life.Should().Be(16, "angel deals 4 damage");
    p1.Life.Should().Be(19, "lifelink heals controller for 4 (15 + 4 = 19)");
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "Creature_WithDefender_CannotAttack|Lifelink_Creature_Heals" -v n`
Expected: FAIL — `Keyword.Defender` and `Keyword.Lifelink` don't exist yet

**Step 3: Add enum values**

In `src/MtgDecker.Engine/Enums/Keyword.cs`, add:

```csharp
public enum Keyword
{
    Haste,
    Shroud,
    Mountainwalk,
    Flying,
    Trample,
    FirstStrike,
    Protection,
    Swampwalk,
    Forestwalk,
    Islandwalk,
    Plainswalk,
    Lifelink,
    Defender,
}
```

**Step 4: Run tests again — they should compile but may still fail on logic**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "Creature_WithDefender_CannotAttack|Lifelink_Creature_Heals" -v n`
Expected: Defender test may pass or fail depending on how attack validation works. Lifelink test FAIL — no life gain logic yet.

**Step 5: Add Defender enforcement in GameEngine.cs**

Find the eligible attackers line (around line 905-906):

```csharp
// BEFORE:
var eligibleAttackers = attacker.Battlefield.Cards
    .Where(c => c.IsCreature && !c.IsTapped && !c.HasSummoningSickness(_state.TurnNumber))
    .ToList();

// AFTER:
var eligibleAttackers = attacker.Battlefield.Cards
    .Where(c => c.IsCreature && !c.IsTapped && !c.HasSummoningSickness(_state.TurnNumber)
        && !c.ActiveKeywords.Contains(Keyword.Defender))
    .ToList();
```

**Step 6: Add Lifelink enforcement in ResolveCombatDamage**

In `ResolveCombatDamage`, after the unblocked damage line `defender.AdjustLife(-damage)` (around line 1085), add lifelink gain:

```csharp
defender.AdjustLife(-damage);
_state.Log($"{attackerCard.Name} deals {damage} damage to {defender.Name}. ({defender.Life} life)");
unblockedAttackers.Add(attackerCard);

// Lifelink: controller gains life equal to damage dealt
if (attackerCard.ActiveKeywords.Contains(Keyword.Lifelink))
{
    attacker.AdjustLife(damage);
    _state.Log($"{attackerCard.Name} has lifelink — {attacker.Name} gains {damage} life. ({attacker.Life} life)");
}
```

Also add lifelink for blocked combat (attacker dealing damage to blockers). After the blocker damage assignment line `blockerCard.DamageMarked += assigned` (around line 1107):

```csharp
blockerCard.DamageMarked += assigned;
remainingDamage -= assigned;
_state.Log($"{attackerCard.Name} deals {assigned} damage to {blockerCard.Name}.");

// Lifelink on attacker dealing damage to blockers
if (assigned > 0 && attackerCard.ActiveKeywords.Contains(Keyword.Lifelink))
{
    attacker.AdjustLife(assigned);
    _state.Log($"{attackerCard.Name} has lifelink — {attacker.Name} gains {assigned} life. ({attacker.Life} life)");
}
```

And for blockers with lifelink dealing damage to attackers. After `attackerCard.DamageMarked += blockerDamage` (around line 1121):

```csharp
attackerCard.DamageMarked += blockerDamage;
_state.Log($"{blockerCard.Name} deals {blockerDamage} damage to {attackerCard.Name}.");

// Lifelink on blocker dealing damage to attacker
if (blockerDamage > 0 && blockerCard.ActiveKeywords.Contains(Keyword.Lifelink))
{
    defender.AdjustLife(blockerDamage);
    _state.Log($"{blockerCard.Name} has lifelink — {defender.Name} gains {blockerDamage} life. ({defender.Life} life)");
}
```

**Step 7: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "Creature_WithDefender_CannotAttack|Lifelink_Creature_Heals" -v n`
Expected: PASS

**Step 8: Run all tests for regressions**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v n`
Expected: All pass

**Step 9: Commit**

```bash
git add src/MtgDecker.Engine/Enums/Keyword.cs src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/CombatIntegrationTests.cs
git commit -m "feat(engine): add Lifelink and Defender keyword enforcement

Lifelink: controller gains life when creature deals combat damage.
Defender: creature cannot be declared as an attacker."
```

---

### Task 5: Add Missing Haste Keywords (4 cards)

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Test: `tests/MtgDecker.Engine.Tests/CardDefinitionsTests.cs`

Cards: Goblin Guide, Goblin Ringleader, Monastery Swiftspear, Anger

**Step 1: Write failing tests**

Add to `CardDefinitionsTests.cs`:

```csharp
[Theory]
[InlineData("Goblin Guide")]
[InlineData("Goblin Ringleader")]
[InlineData("Monastery Swiftspear")]
[InlineData("Anger")]
public void Card_HasHaste(string cardName)
{
    CardDefinitions.TryGet(cardName, out var def);

    def!.ContinuousEffects.Should().Contain(e =>
        e.Type == ContinuousEffectType.GrantKeyword
        && e.GrantedKeyword == Keyword.Haste,
        because: $"{cardName} should have haste");
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "Card_HasHaste" -v n`
Expected: FAIL — these cards have no ContinuousEffects with Haste

**Step 3: Fix the registry entries**

In `CardDefinitions.cs`, update these four entries:

```csharp
// Goblin Guide — add Haste
["Goblin Guide"] = new(ManaCost.Parse("{R}"), null, 2, 2, CardType.Creature)
{
    Subtypes = ["Goblin"],
    ContinuousEffects =
    [
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
            (card, _) => card.Name == "Goblin Guide",
            GrantedKeyword: Keyword.Haste),
    ],
},

// Goblin Ringleader — add Haste (keep existing triggers)
["Goblin Ringleader"] = new(ManaCost.Parse("{3}{R}"), null, 2, 2, CardType.Creature)
{
    Subtypes = ["Goblin"],
    Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new RevealAndFilterEffect(4, "Goblin"))],
    ContinuousEffects =
    [
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
            (card, _) => card.Name == "Goblin Ringleader",
            GrantedKeyword: Keyword.Haste),
    ],
},

// Monastery Swiftspear — add Haste
["Monastery Swiftspear"] = new(ManaCost.Parse("{R}"), null, 1, 2, CardType.Creature)
{
    Subtypes = ["Human", "Monk"],
    ContinuousEffects =
    [
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
            (card, _) => card.Name == "Monastery Swiftspear",
            GrantedKeyword: Keyword.Haste),
    ],
},

// Anger — add Haste
["Anger"] = new(ManaCost.Parse("{3}{R}"), null, 2, 2, CardType.Creature)
{
    Subtypes = ["Incarnation"],
    ContinuousEffects =
    [
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
            (card, _) => card.Name == "Anger",
            GrantedKeyword: Keyword.Haste),
    ],
},
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "Card_HasHaste" -v n`
Expected: PASS

**Step 5: Run all tests for regressions**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v n`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/CardDefinitions.cs tests/MtgDecker.Engine.Tests/CardDefinitionsTests.cs
git commit -m "fix(engine): add missing Haste to Goblin Guide, Ringleader, Swiftspear, Anger"
```

---

### Task 6: Add Exalted Angel Lifelink + Wall of Blossoms Defender

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Test: `tests/MtgDecker.Engine.Tests/CardDefinitionsTests.cs`

**Step 1: Write failing tests**

Add to `CardDefinitionsTests.cs`:

```csharp
[Fact]
public void ExaltedAngel_HasLifelink()
{
    CardDefinitions.TryGet("Exalted Angel", out var def);

    def!.ContinuousEffects.Should().Contain(e =>
        e.Type == ContinuousEffectType.GrantKeyword
        && e.GrantedKeyword == Keyword.Lifelink,
        because: "Exalted Angel has lifelink");
}

[Fact]
public void WallOfBlossoms_HasDefender()
{
    CardDefinitions.TryGet("Wall of Blossoms", out var def);

    def!.ContinuousEffects.Should().Contain(e =>
        e.Type == ContinuousEffectType.GrantKeyword
        && e.GrantedKeyword == Keyword.Defender,
        because: "Wall of Blossoms has defender");
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "ExaltedAngel_HasLifelink|WallOfBlossoms_HasDefender" -v n`
Expected: FAIL

**Step 3: Fix the registry entries**

In `CardDefinitions.cs`:

```csharp
// Exalted Angel — add Lifelink (keep existing Flying)
["Exalted Angel"] = new(ManaCost.Parse("{4}{W}{W}"), null, 4, 5, CardType.Creature)
{
    Subtypes = ["Angel"],
    ContinuousEffects =
    [
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
            (card, _) => card.Name == "Exalted Angel",
            GrantedKeyword: Keyword.Flying),
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
            (card, _) => card.Name == "Exalted Angel",
            GrantedKeyword: Keyword.Lifelink),
    ],
},

// Wall of Blossoms — add Defender (keep existing ETB trigger)
["Wall of Blossoms"] = new(ManaCost.Parse("{1}{G}"), null, 0, 4, CardType.Creature)
{
    Subtypes = ["Plant", "Wall"],
    Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new DrawCardEffect())],
    ContinuousEffects =
    [
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
            (card, _) => card.Name == "Wall of Blossoms",
            GrantedKeyword: Keyword.Defender),
    ],
},
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "ExaltedAngel_HasLifelink|WallOfBlossoms_HasDefender" -v n`
Expected: PASS

**Step 5: Run all tests for regressions**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v n`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/CardDefinitions.cs tests/MtgDecker.Engine.Tests/CardDefinitionsTests.cs
git commit -m "fix(engine): add Lifelink to Exalted Angel, Defender to Wall of Blossoms"
```

---

### Task 7: Fix Goblin King (add Mountainwalk grant)

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Test: `tests/MtgDecker.Engine.Tests/ContinuousEffectWiringTests.cs`

Note: Goblin King's P/T buff already correctly excludes self (via SourceId skip in `ApplyPowerToughnessEffect`). We just need to add the Mountainwalk keyword grant.

**Step 1: Write failing test**

Add to `ContinuousEffectWiringTests.cs`:

```csharp
[Fact]
public void Goblin_King_Grants_Mountainwalk_To_Other_Goblins()
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

    grunt.ActiveKeywords.Should().Contain(Keyword.Mountainwalk,
        "other Goblins should get mountainwalk from Goblin King");
    king.ActiveKeywords.Should().NotContain(Keyword.Mountainwalk,
        "Goblin King doesn't give mountainwalk to itself");
}
```

**Step 2: Run test to verify it fails**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "Goblin_King_Grants_Mountainwalk" -v n`
Expected: FAIL — no Mountainwalk keyword granted

**Step 3: Fix Goblin King registry entry**

In `CardDefinitions.cs`, update Goblin King to add a second ContinuousEffect for Mountainwalk:

```csharp
["Goblin King"] = new(ManaCost.Parse("{1}{R}{R}"), null, 2, 2, CardType.Creature)
{
    Subtypes = ["Goblin"],
    ContinuousEffects =
    [
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyPowerToughness,
            (card, _) => card.IsCreature && card.Subtypes.Contains("Goblin"),
            PowerMod: 1, ToughnessMod: 1),
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
            (card, _) => card.IsCreature && card.Subtypes.Contains("Goblin"),
            GrantedKeyword: Keyword.Mountainwalk,
            ExcludeSelf: true),
    ],
},
```

**Step 4: Run test to verify it passes**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "Goblin_King_Grants_Mountainwalk" -v n`
Expected: PASS

**Step 5: Run all tests for regressions**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v n`
Expected: All pass. The existing `Goblin_King_Auto_Buffs_Other_Goblins_From_CardDefinitions` test should still pass since P/T logic is unchanged.

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/CardDefinitions.cs tests/MtgDecker.Engine.Tests/ContinuousEffectWiringTests.cs
git commit -m "fix(engine): Goblin King grants mountainwalk to other Goblins"
```

---

### Task 8: Fix Grim Lavamancer (damage 1→2, add {R} cost)

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Test: `tests/MtgDecker.Engine.Tests/CardDefinitionsTests.cs`

**Step 1: Write failing tests**

Add to `CardDefinitionsTests.cs`:

```csharp
[Fact]
public void GrimLavamancer_Deals2Damage()
{
    CardDefinitions.TryGet("Grim Lavamancer", out var def);

    def!.ActivatedAbility.Should().NotBeNull();
    var effect = def.ActivatedAbility!.Effect as Triggers.Effects.DealDamageEffect;
    effect.Should().NotBeNull();
    effect!.Amount.Should().Be(2,
        because: "Grim Lavamancer deals 2 damage, not 1");
}

[Fact]
public void GrimLavamancer_CostsRedMana()
{
    CardDefinitions.TryGet("Grim Lavamancer", out var def);

    def!.ActivatedAbility.Should().NotBeNull();
    def.ActivatedAbility!.Cost.ManaCost.Should().NotBeNull(
        because: "Grim Lavamancer costs {R} to activate");
    def.ActivatedAbility.Cost.ManaCost!.ColorRequirements.Should()
        .ContainKey(ManaColor.Red);
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "GrimLavamancer_Deals2|GrimLavamancer_CostsRed" -v n`
Expected: FAIL — currently deals 1, no mana cost

**Step 3: Fix the registry entry**

In `CardDefinitions.cs`:

```csharp
["Grim Lavamancer"] = new(ManaCost.Parse("{R}"), null, 1, 1, CardType.Creature)
{
    Subtypes = ["Human", "Wizard"],
    ActivatedAbility = new(new ActivatedAbilityCost(TapSelf: true, ManaCost: ManaCost.Parse("{R}")), new DealDamageEffect(2), c => c.IsCreature, CanTargetPlayer: true),
},
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "GrimLavamancer_Deals2|GrimLavamancer_CostsRed" -v n`
Expected: PASS

**Step 5: Run all tests for regressions**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v n`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/CardDefinitions.cs tests/MtgDecker.Engine.Tests/CardDefinitionsTests.cs
git commit -m "fix(engine): Grim Lavamancer deals 2 damage and costs {R} to activate"
```

---

### Task 9: Fix Goblin Tinkerer (add {R} cost)

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Test: `tests/MtgDecker.Engine.Tests/CardDefinitionsTests.cs`

**Step 1: Write failing test**

Add to `CardDefinitionsTests.cs`:

```csharp
[Fact]
public void GoblinTinkerer_CostsRedMana()
{
    CardDefinitions.TryGet("Goblin Tinkerer", out var def);

    def!.ActivatedAbility.Should().NotBeNull();
    def.ActivatedAbility!.Cost.ManaCost.Should().NotBeNull(
        because: "Goblin Tinkerer costs {R} to activate");
    def.ActivatedAbility.Cost.ManaCost!.ColorRequirements.Should()
        .ContainKey(ManaColor.Red);
}
```

**Step 2: Run test to verify it fails**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "GoblinTinkerer_CostsRed" -v n`
Expected: FAIL — ManaCost is null

**Step 3: Fix the registry entry**

In `CardDefinitions.cs`:

```csharp
["Goblin Tinkerer"] = new(ManaCost.Parse("{1}{R}"), null, 1, 1, CardType.Creature)
{
    Subtypes = ["Goblin"],
    ActivatedAbility = new(new ActivatedAbilityCost(SacrificeSelf: true, ManaCost: ManaCost.Parse("{R}")), new DestroyTargetEffect(), c => c.CardTypes.HasFlag(CardType.Artifact)),
},
```

**Step 4: Run test to verify it passes**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "GoblinTinkerer_CostsRed" -v n`
Expected: PASS

**Step 5: Run all tests for regressions**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v n`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/CardDefinitions.cs tests/MtgDecker.Engine.Tests/CardDefinitionsTests.cs
git commit -m "fix(engine): Goblin Tinkerer costs {R} to activate"
```

---

### Task 10: Final Verification + Squash Commit

**Step 1: Run ALL engine tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v n`
Expected: All pass, zero failures

**Step 2: Run full solution build**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet build src/MtgDecker.Web/`
Expected: Build succeeded

**Step 3: Verify git log looks clean**

Run: `git log --oneline -10`
Expected: 9 clean commits for this phase
