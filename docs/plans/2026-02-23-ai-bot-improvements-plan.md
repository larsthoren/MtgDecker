# AI Bot Improvements Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Transform the AI bot from "random legal moves" to a decent casual player that doesn't make obvious blunders.

**Architecture:** Add SpellRole classification to CardDefinition, then rewrite AiBotDecisionHandler with planned action queues, smart land/mana selection, reactive play (counterspells/removal), and smarter combat. All changes in Engine project — no interface or engine loop changes.

**Tech Stack:** C#, xUnit, FluentAssertions

---

### Task 1: Add SpellRole enum and property to CardDefinition

**Files:**
- Create: `src/MtgDecker.Engine/Enums/SpellRole.cs`
- Modify: `src/MtgDecker.Engine/CardDefinition.cs`
- Create: `tests/MtgDecker.Engine.Tests/AI/SpellRoleTests.cs`

**Step 1: Write the failing test**

Create `tests/MtgDecker.Engine.Tests/AI/SpellRoleTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests.AI;

public class SpellRoleTests
{
    [Fact]
    public void CardDefinition_HasSpellRole_DefaultsToProactive()
    {
        CardDefinitions.TryGet("Goblin Lackey", out var def).Should().BeTrue();
        def!.SpellRole.Should().Be(SpellRole.Proactive);
    }

    [Theory]
    [InlineData("Counterspell", SpellRole.Counterspell)]
    [InlineData("Daze", SpellRole.Counterspell)]
    [InlineData("Force of Will", SpellRole.Counterspell)]
    [InlineData("Mana Leak", SpellRole.Counterspell)]
    [InlineData("Spell Pierce", SpellRole.Counterspell)]
    [InlineData("Flusterstorm", SpellRole.Counterspell)]
    [InlineData("Absorb", SpellRole.Counterspell)]
    [InlineData("Prohibit", SpellRole.Counterspell)]
    [InlineData("Pyroblast", SpellRole.Counterspell)]
    public void Counterspells_HaveCorrectRole(string cardName, SpellRole expected)
    {
        CardDefinitions.TryGet(cardName, out var def).Should().BeTrue();
        def!.SpellRole.Should().Be(expected);
    }

    [Theory]
    [InlineData("Lightning Bolt", SpellRole.InstantRemoval)]
    [InlineData("Swords to Plowshares", SpellRole.InstantRemoval)]
    [InlineData("Dismember", SpellRole.InstantRemoval)]
    [InlineData("Fatal Push", SpellRole.InstantRemoval)]
    [InlineData("Smother", SpellRole.InstantRemoval)]
    [InlineData("Snuff Out", SpellRole.InstantRemoval)]
    [InlineData("Incinerate", SpellRole.InstantRemoval)]
    [InlineData("Shock", SpellRole.InstantRemoval)]
    [InlineData("Searing Blood", SpellRole.InstantRemoval)]
    public void InstantRemoval_HasCorrectRole(string cardName, SpellRole expected)
    {
        CardDefinitions.TryGet(cardName, out var def).Should().BeTrue();
        def!.SpellRole.Should().Be(expected);
    }

    [Theory]
    [InlineData("Brainstorm", SpellRole.InstantUtility)]
    [InlineData("Fact or Fiction", SpellRole.InstantUtility)]
    [InlineData("Impulse", SpellRole.InstantUtility)]
    [InlineData("Dark Ritual", SpellRole.InstantUtility)]
    public void InstantUtility_HasCorrectRole(string cardName, SpellRole expected)
    {
        CardDefinitions.TryGet(cardName, out var def).Should().BeTrue();
        def!.SpellRole.Should().Be(expected);
    }

    [Theory]
    [InlineData("Goblin Lackey", SpellRole.Proactive)]
    [InlineData("Siege-Gang Commander", SpellRole.Proactive)]
    [InlineData("Naturalize", SpellRole.Proactive)]
    [InlineData("Replenish", SpellRole.Proactive)]
    public void ProactiveCards_HaveDefaultRole(string cardName, SpellRole expected)
    {
        CardDefinitions.TryGet(cardName, out var def).Should().BeTrue();
        def!.SpellRole.Should().Be(expected);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "SpellRoleTests"`
Expected: FAIL — `SpellRole` type does not exist.

**Step 3: Write minimal implementation**

Create `src/MtgDecker.Engine/Enums/SpellRole.cs`:

```csharp
namespace MtgDecker.Engine.Enums;

/// <summary>
/// Classifies spells by when/how the AI should play them.
/// </summary>
public enum SpellRole
{
    /// <summary>Creatures, sorceries, enchantments — play in main phase with empty stack.</summary>
    Proactive,
    /// <summary>Only cast in response to opponent's spell on the stack.</summary>
    Counterspell,
    /// <summary>Instant-speed removal — cast reactively during combat or end of turn.</summary>
    InstantRemoval,
    /// <summary>Instant-speed utility — card draw, mana, etc. Cast at end of opponent's turn.</summary>
    InstantUtility,
}
```

Add to `src/MtgDecker.Engine/CardDefinition.cs` — add a new property after `Adventure`:

```csharp
public SpellRole SpellRole { get; init; } = SpellRole.Proactive;
```

Add `using MtgDecker.Engine.Enums;` to the top of `CardDefinition.cs` if not already present.

Now tag all instants in `CardDefinitions.cs`. Add `SpellRole = SpellRole.Counterspell` to these card entries:
- `Counterspell`, `Daze`, `Force of Will`, `Mana Leak`, `Absorb`, `Spell Pierce`, `Flusterstorm`, `Prohibit`, `Pyroblast`

Add `SpellRole = SpellRole.InstantRemoval` to:
- `Lightning Bolt`, `Swords to Plowshares`, `Dismember`, `Fatal Push`, `Smother`, `Snuff Out`, `Incinerate`, `Shock`, `Searing Blood`, `Diabolic Edict`, `Wipe Away`, `Disenchant`, `Naturalize` (change from Proactive — it's an Instant), `Ray of Revelation`

Wait — `Naturalize` is `CardType.Instant` but it destroys enchantment/artifact, so it should be `InstantRemoval`. `Disenchant` same.

Add `SpellRole = SpellRole.InstantUtility` to:
- `Brainstorm`, `Fact or Fiction`, `Impulse`, `Dark Ritual`, `Skeletal Scrying`, `Funeral Charm`, `Funeral Pyre`, `Surgical Extraction`

All other cards (creatures, sorceries, enchantments, lands) keep the default `Proactive`.

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "SpellRoleTests"`
Expected: PASS (all tests)

**Step 5: Run all engine tests to verify no regressions**

Run: `dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All pass (1730+)

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/Enums/SpellRole.cs src/MtgDecker.Engine/CardDefinition.cs src/MtgDecker.Engine/CardDefinitions.cs tests/MtgDecker.Engine.Tests/AI/SpellRoleTests.cs
git commit -m "feat(engine): add SpellRole classification to CardDefinition"
```

---

### Task 2: Smart land selection

**Files:**
- Modify: `src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs`
- Create: `tests/MtgDecker.Engine.Tests/AI/AiBotLandSelectionTests.cs`

**Step 1: Write the failing tests**

Create `tests/MtgDecker.Engine.Tests/AI/AiBotLandSelectionTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.AI;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests.AI;

public class AiBotLandSelectionTests
{
    [Fact]
    public void ChooseLandToPlay_PrefersBasicOverCityOfTraitors()
    {
        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain", "", null, null, null);
        var city = GameCard.Create("City of Traitors", "Land", "", null, null, null);
        var bolt = GameCard.Create("Lightning Bolt", "Instant", "", "{R}", null, null);

        var hand = new List<GameCard> { city, mountain, bolt };

        var result = AiBotDecisionHandler.ChooseLandToPlay(hand, neededColors: new HashSet<ManaColor> { ManaColor.Red });

        result.Should().NotBeNull();
        result!.Name.Should().Be("Mountain");
    }

    [Fact]
    public void ChooseLandToPlay_PrefersColorMatchingBasic()
    {
        var forest = GameCard.Create("Forest", "Basic Land — Forest", "", null, null, null);
        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain", "", null, null, null);
        var bolt = GameCard.Create("Lightning Bolt", "Instant", "", "{R}", null, null);

        var hand = new List<GameCard> { forest, mountain, bolt };

        var result = AiBotDecisionHandler.ChooseLandToPlay(hand, neededColors: new HashSet<ManaColor> { ManaColor.Red });

        result!.Name.Should().Be("Mountain");
    }

    [Fact]
    public void ChooseLandToPlay_PrefersDualOverNonMatchingBasic()
    {
        var forest = GameCard.Create("Forest", "Basic Land — Forest", "", null, null, null);
        var karplusan = GameCard.Create("Karplusan Forest", "Land", "", null, null, null);
        var bolt = GameCard.Create("Lightning Bolt", "Instant", "", "{R}", null, null);

        var hand = new List<GameCard> { forest, karplusan, bolt };

        // Karplusan Forest produces Red (needed) — Forest doesn't
        var result = AiBotDecisionHandler.ChooseLandToPlay(hand, neededColors: new HashSet<ManaColor> { ManaColor.Red });

        result!.Name.Should().Be("Karplusan Forest");
    }

    [Fact]
    public void ChooseLandToPlay_CityOfTraitors_OnlyWhenNoOtherLands()
    {
        var city = GameCard.Create("City of Traitors", "Land", "", null, null, null);
        var bolt = GameCard.Create("Lightning Bolt", "Instant", "", "{R}", null, null);

        var hand = new List<GameCard> { city, bolt };

        var result = AiBotDecisionHandler.ChooseLandToPlay(hand, neededColors: new HashSet<ManaColor> { ManaColor.Red });

        result!.Name.Should().Be("City of Traitors");
    }

    [Fact]
    public void ChooseLandToPlay_PrefersBasicOverAncientTomb()
    {
        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain", "", null, null, null);
        var tomb = GameCard.Create("Ancient Tomb", "Land", "", null, null, null);
        var bolt = GameCard.Create("Lightning Bolt", "Instant", "", "{R}", null, null);

        var hand = new List<GameCard> { tomb, mountain, bolt };

        var result = AiBotDecisionHandler.ChooseLandToPlay(hand, neededColors: new HashSet<ManaColor> { ManaColor.Red });

        result!.Name.Should().Be("Mountain");
    }

    [Fact]
    public void ChooseLandToPlay_ReturnsNullIfNoLands()
    {
        var bolt = GameCard.Create("Lightning Bolt", "Instant", "", "{R}", null, null);

        var hand = new List<GameCard> { bolt };

        var result = AiBotDecisionHandler.ChooseLandToPlay(hand, neededColors: new HashSet<ManaColor> { ManaColor.Red });

        result.Should().BeNull();
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "AiBotLandSelectionTests"`
Expected: FAIL — `ChooseLandToPlay` method does not exist.

**Step 3: Write minimal implementation**

Add this static method to `AiBotDecisionHandler.cs`:

```csharp
/// <summary>
/// Ranks lands in hand and returns the best one to play.
/// Priority: color-matching basic > color-matching dual > non-matching basic > utility > City of Traitors/Ancient Tomb.
/// </summary>
internal static GameCard? ChooseLandToPlay(IReadOnlyList<GameCard> hand, HashSet<ManaColor> neededColors)
{
    var lands = hand.Where(c => c.IsLand).ToList();
    if (lands.Count == 0) return null;

    return lands
        .OrderByDescending(land => ScoreLand(land, neededColors))
        .First();
}

private static int ScoreLand(GameCard land, HashSet<ManaColor> neededColors)
{
    var score = 0;
    var producesNeededColor = false;

    if (CardDefinitions.TryGet(land.Name, out var def) && def.ManaAbility != null)
    {
        var ability = def.ManaAbility;
        if (ability.FixedColor.HasValue && neededColors.Contains(ability.FixedColor.Value))
            producesNeededColor = true;
        if (ability.ChoiceColors != null && ability.ChoiceColors.Any(c => neededColors.Contains(c)))
            producesNeededColor = true;
        if (ability.DynamicColor.HasValue && neededColors.Contains(ability.DynamicColor.Value))
            producesNeededColor = true;
    }

    // Basic lands that produce needed colors are best
    if (land.IsBasicLand && producesNeededColor) score += 100;
    // Non-basic that produces needed colors (duals, pain lands)
    else if (producesNeededColor) score += 80;
    // Basic land not matching needed color (still fine for generic mana)
    else if (land.IsBasicLand) score += 60;

    // Penalize self-damage lands (Ancient Tomb)
    if (CardDefinitions.TryGet(land.Name, out var dmgDef) && dmgDef.ManaAbility?.SelfDamage > 0)
        score -= 30;

    // Heavily penalize City of Traitors (sacrifices when you play another land)
    if (land.Name == "City of Traitors") score -= 50;

    // Utility lands with activated abilities but no mana or only colorless
    if (CardDefinitions.TryGet(land.Name, out var utilDef) && utilDef.ActivatedAbility != null)
    {
        if (utilDef.ManaAbility?.FixedColor == ManaColor.Colorless && !producesNeededColor)
            score -= 10; // Wasteland, Rishadan Port — deprioritize as mana sources
    }

    return score;
}
```

Now update `GetAction` to use this method. Replace the land-play block (lines 48-57 in the original):

```csharp
// Priority 1: Play a land
if (hand.Count > 0 && player.LandsPlayedThisTurn == 0)
{
    // Compute needed colors from spells in hand
    var neededColorsForLand = new HashSet<ManaColor>();
    foreach (var spell in hand.Where(c => !c.IsLand && c.ManaCost != null))
        foreach (var color in spell.ManaCost!.ColorRequirements.Keys)
            neededColorsForLand.Add(color);

    var bestLand = ChooseLandToPlay(hand, neededColorsForLand);
    if (bestLand != null)
    {
        await DelayAsync(ct);
        return GameAction.PlayLand(playerId, bestLand.Id);
    }
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "AiBotLandSelectionTests"`
Expected: PASS (all 6 tests)

**Step 5: Run all engine tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs tests/MtgDecker.Engine.Tests/AI/AiBotLandSelectionTests.cs
git commit -m "feat(engine): add smart land selection to AI bot"
```

---

### Task 3: Planned mana tapping — tap exactly what you need

**Files:**
- Modify: `src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs`
- Create: `tests/MtgDecker.Engine.Tests/AI/AiBotManaTappingTests.cs`

**Step 1: Write the failing tests**

Create `tests/MtgDecker.Engine.Tests/AI/AiBotManaTappingTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.AI;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests.AI;

public class AiBotManaTappingTests
{
    [Fact]
    public void PlanTapSequence_OneManaSpell_TapsOneLand()
    {
        var mountain1 = CreateLand("Mountain", "m1");
        var mountain2 = CreateLand("Mountain", "m2");
        var mountain3 = CreateLand("Mountain", "m3");
        var untappedLands = new List<GameCard> { mountain1, mountain2, mountain3 };
        var cost = ManaCost.Parse("{R}");

        var tapIds = AiBotDecisionHandler.PlanTapSequence(untappedLands, cost);

        tapIds.Should().HaveCount(1);
    }

    [Fact]
    public void PlanTapSequence_TwoColorSpell_TapsCorrectLands()
    {
        var mountain = CreateLand("Mountain", "mtn");
        var forest = CreateLand("Forest", "fst");
        var mountain2 = CreateLand("Mountain", "mtn2");
        var untappedLands = new List<GameCard> { mountain, forest, mountain2 };
        // {1}{R}{G} — needs red, green, and 1 generic
        var cost = ManaCost.Parse("{1}{R}{G}");

        var tapIds = AiBotDecisionHandler.PlanTapSequence(untappedLands, cost);

        tapIds.Should().HaveCount(3);
    }

    [Fact]
    public void PlanTapSequence_PrefersFixedColorForColoredCost()
    {
        // Karplusan Forest can produce R or G (choice), Mountain is fixed R
        var karplusan = CreateLand("Karplusan Forest", "kf");
        var mountain = CreateLand("Mountain", "mtn");
        var untappedLands = new List<GameCard> { karplusan, mountain };
        // {R} — should tap Mountain (fixed) to save Karplusan's flexibility
        var cost = ManaCost.Parse("{R}");

        var tapIds = AiBotDecisionHandler.PlanTapSequence(untappedLands, cost);

        tapIds.Should().HaveCount(1);
        tapIds[0].Should().Be(mountain.Id);
    }

    [Fact]
    public void PlanTapSequence_ReturnsEmpty_WhenCantAfford()
    {
        var forest = CreateLand("Forest", "fst");
        var untappedLands = new List<GameCard> { forest };
        var cost = ManaCost.Parse("{R}{R}"); // Need 2 red, only have green

        var tapIds = AiBotDecisionHandler.PlanTapSequence(untappedLands, cost);

        tapIds.Should().BeEmpty();
    }

    [Fact]
    public void PlanTapSequence_GenericCost_UsesLeastFlexibleLands()
    {
        var mountain = CreateLand("Mountain", "mtn"); // fixed Red
        var karplusan = CreateLand("Karplusan Forest", "kf"); // choice R/G
        var untappedLands = new List<GameCard> { karplusan, mountain };
        // {1} — only generic, should tap Mountain (less flexible) over Karplusan
        var cost = ManaCost.Parse("{1}");

        var tapIds = AiBotDecisionHandler.PlanTapSequence(untappedLands, cost);

        tapIds.Should().HaveCount(1);
        tapIds[0].Should().Be(mountain.Id);
    }

    private static GameCard CreateLand(string name, string suffix)
    {
        var card = GameCard.Create(name, name == "Karplusan Forest" ? "Land" : $"Basic Land — {name}",
            "", null, null, null);
        return card;
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "AiBotManaTappingTests"`
Expected: FAIL — `PlanTapSequence` does not exist.

**Step 3: Write minimal implementation**

Add to `AiBotDecisionHandler.cs`:

```csharp
/// <summary>
/// Plans which lands to tap for a given mana cost. Returns the list of land IDs
/// to tap in order. Returns empty list if cost cannot be paid.
/// Prefers fixed-color lands for colored costs and least-flexible lands for generic.
/// </summary>
internal static List<Guid> PlanTapSequence(IReadOnlyList<GameCard> untappedLands, ManaCost cost)
{
    var result = new List<Guid>();
    var remaining = new List<GameCard>(untappedLands);
    var colorNeeded = new Dictionary<ManaColor, int>(cost.ColorRequirements);
    var genericNeeded = cost.GenericCost;

    // Step 1: Satisfy colored requirements using best-fit lands
    foreach (var (color, count) in colorNeeded)
    {
        for (int i = 0; i < count; i++)
        {
            // Prefer fixed-color lands that produce exactly this color
            var fixedLand = remaining.FirstOrDefault(l =>
                GetLandDef(l)?.FixedColor == color);

            if (fixedLand != null)
            {
                result.Add(fixedLand.Id);
                remaining.Remove(fixedLand);
                continue;
            }

            // Fall back to choice lands that can produce this color
            var choiceLand = remaining
                .Where(l => GetLandDef(l)?.ChoiceColors?.Contains(color) == true)
                .OrderBy(l => GetLandDef(l)?.ChoiceColors?.Count ?? 0) // prefer fewer choices (less flexible)
                .FirstOrDefault();

            if (choiceLand != null)
            {
                result.Add(choiceLand.Id);
                remaining.Remove(choiceLand);
                continue;
            }

            // Can't satisfy this color — abort
            return [];
        }
    }

    // Step 2: Satisfy generic cost with least flexible remaining lands
    for (int i = 0; i < genericNeeded; i++)
    {
        // Prefer fixed-color lands (least flexible), then lands with fewer choice colors
        var genericLand = remaining
            .OrderBy(l =>
            {
                var def = GetLandDef(l);
                if (def == null) return 0;
                if (def.FixedColor.HasValue) return 1; // fixed = least flexible
                return (def.ChoiceColors?.Count ?? 0) + 10; // more choices = more flexible = tap last
            })
            .FirstOrDefault();

        if (genericLand != null)
        {
            result.Add(genericLand.Id);
            remaining.Remove(genericLand);
        }
        else
        {
            return []; // Not enough lands
        }
    }

    return result;
}

private static ManaAbility? GetLandDef(GameCard land)
{
    return CardDefinitions.TryGet(land.Name, out var def) ? def.ManaAbility : land.ManaAbility;
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "AiBotManaTappingTests"`
Expected: PASS (all 5 tests)

**Step 5: Run all engine tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs tests/MtgDecker.Engine.Tests/AI/AiBotManaTappingTests.cs
git commit -m "feat(engine): add planned mana tap sequence to AI bot"
```

---

### Task 4: Restructure GetAction with planned action queue

**Files:**
- Modify: `src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs`
- Create: `tests/MtgDecker.Engine.Tests/AI/AiBotActionPlanningTests.cs`

This is the core refactor. Instead of tapping one land per GetAction call, the bot now decides what spell to cast, plans the full tap sequence, queues everything, and returns actions one at a time.

**Step 1: Write the failing tests**

Create `tests/MtgDecker.Engine.Tests/AI/AiBotActionPlanningTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.AI;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests.AI;

public class AiBotActionPlanningTests
{
    [Fact]
    public async Task GetAction_DoesNotCastCounterspellProactively()
    {
        // Setup: bot has Daze in hand, empty stack, main phase
        var state = CreateMinimalGameState();
        var player = state.Player1;
        var daze = GameCard.Create("Daze", "Instant", "", "{1}{U}", null, null);
        player.Hand.Add(daze);

        // Add an Island so Daze could theoretically be cast
        var island = GameCard.Create("Island", "Basic Land — Island", "", null, null, null);
        island.ManaAbility = ManaAbility.Fixed(ManaColor.Blue);
        player.Battlefield.Add(island);
        player.ManaPool.Add(ManaColor.Blue);
        player.ManaPool.Add(ManaColor.Colorless);

        var bot = new AiBotDecisionHandler { ActionDelayMs = 0 };
        var action = await bot.GetAction(state, player.Id);

        // Should NOT cast Daze — it's a counterspell (SpellRole.Counterspell)
        action.Type.Should().NotBe(ActionType.CastSpell);
    }

    [Fact]
    public async Task GetAction_DoesNotCastInstantRemovalProactively()
    {
        var state = CreateMinimalGameState();
        var player = state.Player1;
        var bolt = GameCard.Create("Lightning Bolt", "Instant", "", "{R}", null, null);
        player.Hand.Add(bolt);

        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain", "", null, null, null);
        mountain.ManaAbility = ManaAbility.Fixed(ManaColor.Red);
        player.Battlefield.Add(mountain);
        player.ManaPool.Add(ManaColor.Red);

        var bot = new AiBotDecisionHandler { ActionDelayMs = 0 };
        var action = await bot.GetAction(state, player.Id);

        // Should NOT cast Lightning Bolt proactively in main phase
        action.Type.Should().NotBe(ActionType.CastSpell);
    }

    [Fact]
    public async Task GetAction_CastsProactiveSpells()
    {
        var state = CreateMinimalGameState();
        var player = state.Player1;
        var lackey = GameCard.Create("Goblin Lackey", "Creature — Goblin", "", "{R}", null, null);
        player.Hand.Add(lackey);
        player.ManaPool.Add(ManaColor.Red);

        var bot = new AiBotDecisionHandler { ActionDelayMs = 0 };
        var action = await bot.GetAction(state, player.Id);

        action.Type.Should().Be(ActionType.CastSpell);
        action.CardId.Should().Be(lackey.Id);
    }

    [Fact]
    public async Task GetAction_TapsExactlyNeededLands_ThenCasts()
    {
        // Bot has a 1R creature and 3 mountains — should tap 2, not 3
        var state = CreateMinimalGameState();
        var player = state.Player1;
        player.LandsPlayedThisTurn = 1; // Already played a land

        var creature = GameCard.Create("Mogg Fanatic", "Creature — Goblin", "", "{R}", null, null);
        player.Hand.Add(creature);

        for (int i = 0; i < 3; i++)
        {
            var mtn = GameCard.Create("Mountain", "Basic Land — Mountain", "", null, null, null);
            mtn.ManaAbility = ManaAbility.Fixed(ManaColor.Red);
            player.Battlefield.Add(mtn);
        }

        var bot = new AiBotDecisionHandler { ActionDelayMs = 0 };

        // First call should return TapCard
        var action1 = await bot.GetAction(state, player.Id);
        action1.Type.Should().Be(ActionType.TapCard);

        // Simulate the tap producing mana
        var tappedCard = player.Battlefield.Cards.First(c => c.Id == action1.CardId);
        tappedCard.IsTapped = true;
        player.ManaPool.Add(ManaColor.Red);

        // Second call should return CastSpell (only 1 red needed, already have it)
        var action2 = await bot.GetAction(state, player.Id);
        action2.Type.Should().Be(ActionType.CastSpell);
        action2.CardId.Should().Be(creature.Id);
    }

    private static GameState CreateMinimalGameState()
    {
        var p1 = new Player(Guid.NewGuid(), "Bot");
        var p2 = new Player(Guid.NewGuid(), "Opponent");
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = p1;
        state.PriorityPlayer = p1;
        return state;
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "AiBotActionPlanningTests"`
Expected: FAIL — bot still casts counterspells proactively.

**Step 3: Write minimal implementation**

Refactor the core of `GetAction` in `AiBotDecisionHandler.cs`. Add a planned action queue and use SpellRole filtering:

Add field:

```csharp
private readonly Queue<GameAction> _plannedActions = new();
```

Rewrite `GetAction` (replace the entire method body):

```csharp
public async Task<GameAction> GetAction(GameState gameState, Guid playerId, CancellationToken ct = default)
{
    // Drain planned action queue first
    if (_plannedActions.Count > 0)
    {
        await DelayAsync(ct);
        return _plannedActions.Dequeue();
    }

    // Non-active player: evaluate reactive plays (Task 5 will add logic here)
    if (gameState.ActivePlayer.Id != playerId)
        return GameAction.Pass(playerId);

    // Only act during main phases
    if (gameState.CurrentPhase != Phase.MainPhase1 && gameState.CurrentPhase != Phase.MainPhase2)
        return GameAction.Pass(playerId);

    var player = gameState.Player1.Id == playerId ? gameState.Player1 : gameState.Player2;
    var opponent = gameState.Player1.Id == playerId ? gameState.Player2 : gameState.Player1;
    var hand = player.Hand.Cards;

    // Priority 1: Play a land
    if (hand.Count > 0 && player.LandsPlayedThisTurn == 0)
    {
        var neededColorsForLand = new HashSet<ManaColor>();
        foreach (var spell in hand.Where(c => !c.IsLand && c.ManaCost != null))
            foreach (var color in spell.ManaCost!.ColorRequirements.Keys)
                neededColorsForLand.Add(color);

        var bestLand = ChooseLandToPlay(hand, neededColorsForLand);
        if (bestLand != null)
        {
            await DelayAsync(ct);
            return GameAction.PlayLand(playerId, bestLand.Id);
        }
    }

    // Priority 2: Activate a fetch land if we have spells to cast
    var fetchLand = player.Battlefield.Cards
        .FirstOrDefault(c => !c.IsTapped && c.FetchAbility != null);
    if (fetchLand != null)
    {
        var hasSpellInHand = hand.Any(c => !c.IsLand && c.ManaCost != null);
        if (hasSpellInHand)
        {
            await DelayAsync(ct);
            return GameAction.ActivateFetch(playerId, fetchLand.Id);
        }
    }

    // Priority 2.5: Activated abilities on permanents
    var abilityAction = EvaluateActivatedAbilities(player, opponent, gameState);
    if (abilityAction != null)
    {
        await DelayAsync(ct);
        return abilityAction;
    }

    if (hand.Count == 0)
        return GameAction.Pass(playerId);

    // Priority 3+4: Find best proactive spell and plan tap sequence + cast
    // Only cast sorcery-speed spells when stack is empty
    if (gameState.StackCount == 0)
    {
        var bestSpell = ChooseBestProactiveSpell(hand, player, gameState);
        if (bestSpell != null)
        {
            var untappedLands = player.Battlefield.Cards
                .Where(c => c.IsLand && !c.IsTapped && c.ManaAbility != null)
                .ToList();

            var effectiveCost = GetEffectiveCost(bestSpell, gameState, player);

            // Check if we can already pay from pool
            if (player.ManaPool.CanPay(effectiveCost))
            {
                await DelayAsync(ct);
                return GameAction.CastSpell(playerId, bestSpell.Id);
            }

            // Plan tap sequence for remaining cost
            var tapIds = PlanTapSequence(untappedLands, effectiveCost);
            if (tapIds.Count > 0)
            {
                // Cache needed colors for ChooseManaColor callback
                _neededColors.Clear();
                foreach (var color in effectiveCost.ColorRequirements.Keys)
                    _neededColors.Add(color);

                // Queue: tap lands, then cast
                for (int i = 1; i < tapIds.Count; i++)
                    _plannedActions.Enqueue(GameAction.TapCard(playerId, tapIds[i]));
                _plannedActions.Enqueue(GameAction.CastSpell(playerId, bestSpell.Id));

                await DelayAsync(ct);
                return GameAction.TapCard(playerId, tapIds[0]);
            }
        }
    }

    // Priority 5: Cycling
    foreach (var card in hand)
    {
        if (CardDefinitions.TryGet(card.Name, out var cycleDef) && cycleDef.CyclingCost != null)
        {
            if (player.ManaPool.CanPay(cycleDef.CyclingCost))
            {
                if (card.ManaCost == null || !player.ManaPool.CanPay(card.ManaCost))
                {
                    await DelayAsync(ct);
                    return GameAction.Cycle(playerId, card.Id);
                }
            }
        }
    }

    return GameAction.Pass(playerId);
}

/// <summary>
/// Selects the best proactive spell to cast. Filters out counterspells, instant removal,
/// and instant utility. Returns highest CMC affordable proactive spell.
/// </summary>
private static GameCard? ChooseBestProactiveSpell(IReadOnlyList<GameCard> hand, Player player, GameState gameState)
{
    return hand
        .Where(c => !c.IsLand && c.ManaCost != null)
        .Where(c =>
        {
            // Only cast Proactive spells in main phase
            if (!CardDefinitions.TryGet(c.Name, out var def))
                return true; // Unknown cards default to proactive
            return def.SpellRole == SpellRole.Proactive;
        })
        .Select(c => (Card: c, EffectiveCost: GetEffectiveCost(c, gameState, player)))
        .Where(x =>
        {
            // Check if affordable with pool + untapped lands
            var untappedLands = player.Battlefield.Cards
                .Where(l => l.IsLand && !l.IsTapped && l.ManaAbility != null)
                .ToList();
            var potentialMana = player.ManaPool.Total + untappedLands.Count;
            if (x.EffectiveCost.ConvertedManaCost > potentialMana) return false;

            // Check color requirements can be produced
            var producibleColors = GetProducibleColors(player, untappedLands);
            return x.EffectiveCost.ColorRequirements.All(kvp => producibleColors.Contains(kvp.Key));
        })
        .OrderByDescending(x => x.Card.ManaCost!.ConvertedManaCost)
        .Select(x => x.Card)
        .FirstOrDefault();
}

private static ManaCost GetEffectiveCost(GameCard card, GameState gameState, Player player)
{
    var cost = card.ManaCost!;
    var reduction = ComputeCostModification(gameState, card, player);
    if (reduction != 0)
        cost = cost.WithGenericReduction(-reduction);
    return cost;
}

private static HashSet<ManaColor> GetProducibleColors(Player player, List<GameCard> untappedLands)
{
    var colors = new HashSet<ManaColor>();
    foreach (var (color, _) in player.ManaPool.Available)
        colors.Add(color);
    foreach (var land in untappedLands)
    {
        var ability = GetLandDef(land);
        if (ability == null) continue;
        if (ability.FixedColor.HasValue) colors.Add(ability.FixedColor.Value);
        if (ability.ChoiceColors != null)
            foreach (var c in ability.ChoiceColors) colors.Add(c);
        if (ability.DynamicColor.HasValue) colors.Add(ability.DynamicColor.Value);
    }
    return colors;
}
```

Remove the old `CanAffordAnySpell` method (no longer needed — its logic is inlined in `ChooseBestProactiveSpell`).

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "AiBotActionPlanningTests"`
Expected: PASS (all 4 tests)

**Step 5: Run all engine tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All pass. Some existing AI simulation tests may need minor adjustments if they relied on the old tap-everything behavior.

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs tests/MtgDecker.Engine.Tests/AI/AiBotActionPlanningTests.cs
git commit -m "feat(engine): restructure AI bot with planned action queue and spell role filtering"
```

---

### Task 5: Reactive play — counterspells

**Files:**
- Modify: `src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs`
- Create: `tests/MtgDecker.Engine.Tests/AI/AiBotReactivePlayTests.cs`

**Step 1: Write the failing tests**

Create `tests/MtgDecker.Engine.Tests/AI/AiBotReactivePlayTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.AI;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests.AI;

public class AiBotReactivePlayTests
{
    [Fact]
    public async Task GetAction_CountersSpellOnStack_WhenHasCounterspell()
    {
        var state = CreateMinimalGameState();
        var bot = state.Player2; // Bot is non-active player
        var opponent = state.Player1;
        state.ActivePlayer = opponent;
        state.PriorityPlayer = bot;

        // Opponent has a spell on the stack
        var opponentSpell = GameCard.Create("Siege-Gang Commander", "Creature — Goblin", "", "{3}{R}{R}", null, null);
        state.StackPush(new StackObject(opponentSpell, opponent.Id, new Dictionary<ManaColor, int>(), null, 0));

        // Bot has Counterspell and UU available
        var counterspell = GameCard.Create("Counterspell", "Instant", "", "{U}{U}", null, null);
        bot.Hand.Add(counterspell);
        bot.ManaPool.Add(ManaColor.Blue, 2);

        var handler = new AiBotDecisionHandler { ActionDelayMs = 0 };
        var action = await handler.GetAction(state, bot.Id);

        action.Type.Should().Be(ActionType.CastSpell);
        action.CardId.Should().Be(counterspell.Id);
    }

    [Fact]
    public async Task GetAction_DazeCounters_WhenOpponentTappedOut()
    {
        var state = CreateMinimalGameState();
        var bot = state.Player2;
        var opponent = state.Player1;
        state.ActivePlayer = opponent;
        state.PriorityPlayer = bot;

        // Opponent's spell on stack, opponent has NO untapped lands
        var opponentSpell = GameCard.Create("Tarmogoyf", "Creature", "", "{1}{G}", null, null);
        state.StackPush(new StackObject(opponentSpell, opponent.Id, new Dictionary<ManaColor, int>(), null, 0));

        // Bot has Daze and an Island on battlefield
        var daze = GameCard.Create("Daze", "Instant", "", "{1}{U}", null, null);
        bot.Hand.Add(daze);
        var island = GameCard.Create("Island", "Basic Land — Island", "", null, null, null);
        island.ManaAbility = ManaAbility.Fixed(ManaColor.Blue);
        island.Subtypes = ["Island"];
        bot.Battlefield.Add(island);

        var handler = new AiBotDecisionHandler { ActionDelayMs = 0 };
        var action = await handler.GetAction(state, bot.Id);

        action.Type.Should().Be(ActionType.CastSpell);
        action.CardId.Should().Be(daze.Id);
        action.UseAlternateCost.Should().BeTrue();
    }

    [Fact]
    public async Task GetAction_DazeDoesNotCounter_WhenOpponentHasUntappedLands()
    {
        var state = CreateMinimalGameState();
        var bot = state.Player2;
        var opponent = state.Player1;
        state.ActivePlayer = opponent;
        state.PriorityPlayer = bot;

        // Opponent's spell on stack, opponent HAS untapped lands
        var opponentSpell = GameCard.Create("Tarmogoyf", "Creature", "", "{1}{G}", null, null);
        state.StackPush(new StackObject(opponentSpell, opponent.Id, new Dictionary<ManaColor, int>(), null, 0));
        var oppLand = GameCard.Create("Forest", "Basic Land — Forest", "", null, null, null);
        oppLand.ManaAbility = ManaAbility.Fixed(ManaColor.Green);
        opponent.Battlefield.Add(oppLand);

        // Bot has Daze
        var daze = GameCard.Create("Daze", "Instant", "", "{1}{U}", null, null);
        bot.Hand.Add(daze);
        var island = GameCard.Create("Island", "Basic Land — Island", "", null, null, null);
        island.ManaAbility = ManaAbility.Fixed(ManaColor.Blue);
        island.Subtypes = ["Island"];
        bot.Battlefield.Add(island);

        var handler = new AiBotDecisionHandler { ActionDelayMs = 0 };
        var action = await handler.GetAction(state, bot.Id);

        // Should pass — opponent can pay {1}
        action.Type.Should().Be(ActionType.PassPriority);
    }

    [Fact]
    public async Task GetAction_ForceOfWill_CountersExpensiveSpell()
    {
        var state = CreateMinimalGameState();
        var bot = state.Player2;
        var opponent = state.Player1;
        state.ActivePlayer = opponent;
        state.PriorityPlayer = bot;

        // Opponent casts expensive spell (CMC >= 4)
        var opponentSpell = GameCard.Create("Siege-Gang Commander", "Creature — Goblin", "", "{3}{R}{R}", null, null);
        state.StackPush(new StackObject(opponentSpell, opponent.Id, new Dictionary<ManaColor, int>(), null, 0));

        // Bot has Force of Will and a blue card to exile
        var fow = GameCard.Create("Force of Will", "Instant", "", "{3}{U}{U}", null, null);
        var blueCard = GameCard.Create("Brainstorm", "Instant", "", "{U}", null, null);
        bot.Hand.Add(fow);
        bot.Hand.Add(blueCard);

        var handler = new AiBotDecisionHandler { ActionDelayMs = 0 };
        var action = await handler.GetAction(state, bot.Id);

        action.Type.Should().Be(ActionType.CastSpell);
        action.CardId.Should().Be(fow.Id);
        action.UseAlternateCost.Should().BeTrue();
    }

    [Fact]
    public async Task GetAction_DoesNotCounter_CheapSpells_WithForceOfWill()
    {
        var state = CreateMinimalGameState();
        var bot = state.Player2;
        var opponent = state.Player1;
        state.ActivePlayer = opponent;
        state.PriorityPlayer = bot;

        // Opponent casts cheap spell (CMC 1)
        var opponentSpell = GameCard.Create("Llanowar Elves", "Creature", "", "{G}", null, null);
        state.StackPush(new StackObject(opponentSpell, opponent.Id, new Dictionary<ManaColor, int>(), null, 0));

        // Bot has Force of Will and blue card — shouldn't waste FoW on a 1-drop
        var fow = GameCard.Create("Force of Will", "Instant", "", "{3}{U}{U}", null, null);
        var blueCard = GameCard.Create("Brainstorm", "Instant", "", "{U}", null, null);
        bot.Hand.Add(fow);
        bot.Hand.Add(blueCard);

        var handler = new AiBotDecisionHandler { ActionDelayMs = 0 };
        var action = await handler.GetAction(state, bot.Id);

        action.Type.Should().Be(ActionType.PassPriority);
    }

    [Fact]
    public async Task GetAction_PassesPriority_WhenNoReactiveSpells()
    {
        var state = CreateMinimalGameState();
        var bot = state.Player2;
        state.ActivePlayer = state.Player1;
        state.PriorityPlayer = bot;

        // Spell on stack but bot has no counters
        var opponentSpell = GameCard.Create("Tarmogoyf", "Creature", "", "{1}{G}", null, null);
        state.StackPush(new StackObject(opponentSpell, state.Player1.Id, new Dictionary<ManaColor, int>(), null, 0));

        var creature = GameCard.Create("Goblin Lackey", "Creature — Goblin", "", "{R}", null, null);
        bot.Hand.Add(creature);

        var handler = new AiBotDecisionHandler { ActionDelayMs = 0 };
        var action = await handler.GetAction(state, bot.Id);

        action.Type.Should().Be(ActionType.PassPriority);
    }

    private static GameState CreateMinimalGameState()
    {
        var p1 = new Player(Guid.NewGuid(), "Opponent");
        var p2 = new Player(Guid.NewGuid(), "Bot");
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = p1;
        state.PriorityPlayer = p2;
        return state;
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "AiBotReactivePlayTests"`
Expected: FAIL — bot always passes as non-active player.

**Step 3: Write minimal implementation**

In `GetAction`, replace the non-active player block with:

```csharp
// Non-active player: evaluate reactive plays
if (gameState.ActivePlayer.Id != playerId)
{
    var reaction = EvaluateReaction(player, opponent, gameState, playerId);
    if (reaction != null)
    {
        await DelayAsync(ct);
        return reaction;
    }
    return GameAction.Pass(playerId);
}
```

Note: need to move `var opponent =` line before this block so it's available. The `opponent` variable declaration should be moved to right after `var player =`:

```csharp
var player = gameState.Player1.Id == playerId ? gameState.Player1 : gameState.Player2;
var opponent = gameState.Player1.Id == playerId ? gameState.Player2 : gameState.Player1;
var hand = player.Hand.Cards;
```

Add the `EvaluateReaction` method:

```csharp
/// <summary>
/// Evaluates whether to play a reactive spell (counterspell, removal).
/// Returns null if no reactive play is warranted.
/// </summary>
private static GameAction? EvaluateReaction(Player player, Player opponent, GameState gameState, Guid playerId)
{
    // Check for counterspells when opponent has spell on stack
    if (gameState.StackCount > 0)
    {
        var topOfStack = gameState.StackPeekTop();
        if (topOfStack != null && topOfStack.ControllerId != playerId)
        {
            var counterAction = EvaluateCounterspell(player, opponent, gameState, playerId, topOfStack);
            if (counterAction != null) return counterAction;
        }
    }

    // TODO Task 6: instant removal during combat

    return null;
}

private static GameAction? EvaluateCounterspell(Player player, Player opponent, GameState gameState, Guid playerId, IStackObject targetSpell)
{
    var hand = player.Hand.Cards;
    var opponentUntappedLands = opponent.Battlefield.Cards.Count(c => c.IsLand && !c.IsTapped);

    // Get the spell's CMC from the stack object
    var spellCmc = 0;
    if (targetSpell is StackObject so)
        spellCmc = so.Card.ManaCost?.ConvertedManaCost ?? 0;

    foreach (var card in hand)
    {
        if (!CardDefinitions.TryGet(card.Name, out var def)) continue;
        if (def.SpellRole != SpellRole.Counterspell) continue;

        // --- Hard counters (Counterspell, Absorb) ---
        if (def.Effect is CounterSpellEffect or CounterAndGainLifeEffect)
        {
            if (def.AlternateCost == null && def.ManaCost != null && player.ManaPool.CanPay(def.ManaCost))
            {
                // Counter anything CMC >= 3, or counter if we have plenty of mana
                if (spellCmc >= 3)
                    return GameAction.CastSpell(playerId, card.Id);
            }
        }

        // --- Daze (soft counter — return Island to hand) ---
        if (card.Name == "Daze")
        {
            // Only Daze when opponent is tapped out (can't pay {1})
            if (opponentUntappedLands == 0)
            {
                // Check if we have an Island to return
                var hasIsland = player.Battlefield.Cards.Any(c => c.Subtypes.Contains("Island"));
                if (hasIsland)
                    return GameAction.CastSpell(playerId, card.Id, useAlternateCost: true);
            }
            continue;
        }

        // --- Conditional counters (Mana Leak {3}, Spell Pierce {2}, Flusterstorm {1}, Prohibit CMC<=2) ---
        if (def.Effect is ConditionalCounterEffect conditional)
        {
            if (def.ManaCost != null && player.ManaPool.CanPay(def.ManaCost))
            {
                // Only use if opponent likely can't pay the extra
                if (opponentUntappedLands < conditional.PayAmount)
                    return GameAction.CastSpell(playerId, card.Id);
            }
            continue;
        }

        // --- Force of Will (exile blue card, pay 1 life) ---
        if (card.Name == "Force of Will")
        {
            // Only FoW expensive/impactful spells (CMC >= 4)
            if (spellCmc >= 4)
            {
                var hasBlueCardToExile = hand.Any(c => c.Id != card.Id
                    && c.ManaCost != null && c.ManaCost.ColorRequirements.ContainsKey(ManaColor.Blue));
                if (hasBlueCardToExile && player.Life > 1)
                    return GameAction.CastSpell(playerId, card.Id, useAlternateCost: true);
            }
            continue;
        }

        // --- Pyroblast (counter blue spells) ---
        if (card.Name == "Pyroblast")
        {
            if (targetSpell is StackObject pyrTarget && pyrTarget.Card.ManaCost?.ColorRequirements.ContainsKey(ManaColor.Blue) == true)
            {
                if (def.ManaCost != null && player.ManaPool.CanPay(def.ManaCost))
                    return GameAction.CastSpell(playerId, card.Id);
            }
            continue;
        }
    }

    return null;
}
```

Note: `ConditionalCounterEffect` has a `PayAmount` property. Check the actual property name — search for it:

If the property name is different (e.g., `Amount`), adjust accordingly. The key is accessing the pay-or-counter threshold.

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "AiBotReactivePlayTests"`
Expected: PASS (all 6 tests)

**Step 5: Run all engine tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs tests/MtgDecker.Engine.Tests/AI/AiBotReactivePlayTests.cs
git commit -m "feat(engine): add reactive counterspell play to AI bot"
```

---

### Task 6: Reactive play — instant removal

**Files:**
- Modify: `src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs`
- Modify: `tests/MtgDecker.Engine.Tests/AI/AiBotReactivePlayTests.cs`

**Step 1: Write the failing tests**

Add to `AiBotReactivePlayTests.cs`:

```csharp
[Fact]
public async Task GetAction_CastsInstantRemoval_DuringOpponentCombat()
{
    var state = CreateMinimalGameState();
    var bot = state.Player2;
    var opponent = state.Player1;
    state.ActivePlayer = opponent;
    state.PriorityPlayer = bot;
    state.CurrentPhase = Phase.Combat;
    state.CombatStep = CombatStep.DeclareAttackers;

    // Opponent has a big attacking creature
    var attacker = GameCard.Create("Tarmogoyf", "Creature", "", "{1}{G}", "4", "5");
    attacker.Zone = ZoneType.Battlefield;
    opponent.Battlefield.Add(attacker);

    // Bot has Lightning Bolt and R available
    var bolt = GameCard.Create("Lightning Bolt", "Instant", "", "{R}", null, null);
    bot.Hand.Add(bolt);
    bot.ManaPool.Add(ManaColor.Red);

    var handler = new AiBotDecisionHandler { ActionDelayMs = 0 };
    var action = await handler.GetAction(state, bot.Id);

    action.Type.Should().Be(ActionType.CastSpell);
    action.CardId.Should().Be(bolt.Id);
}

[Fact]
public async Task GetAction_DoesNotCastRemoval_InMainPhase_AsNonActivePlayer()
{
    var state = CreateMinimalGameState();
    var bot = state.Player2;
    state.ActivePlayer = state.Player1;
    state.PriorityPlayer = bot;
    state.CurrentPhase = Phase.MainPhase1;

    // Empty stack, opponent has a creature
    var creature = GameCard.Create("Tarmogoyf", "Creature", "", "{1}{G}", "4", "5");
    state.Player1.Battlefield.Add(creature);

    var bolt = GameCard.Create("Lightning Bolt", "Instant", "", "{R}", null, null);
    bot.Hand.Add(bolt);
    bot.ManaPool.Add(ManaColor.Red);

    var handler = new AiBotDecisionHandler { ActionDelayMs = 0 };
    var action = await handler.GetAction(state, bot.Id);

    // Don't cast removal during opponent's main phase with empty stack — wait for combat or end step
    action.Type.Should().Be(ActionType.PassPriority);
}

[Fact]
public async Task GetAction_CastsRemoval_DuringEndStep()
{
    var state = CreateMinimalGameState();
    var bot = state.Player2;
    var opponent = state.Player1;
    state.ActivePlayer = opponent;
    state.PriorityPlayer = bot;
    state.CurrentPhase = Phase.EndStep;

    var creature = GameCard.Create("Goblin Lackey", "Creature — Goblin", "", "{R}", "1", "1");
    opponent.Battlefield.Add(creature);

    var swords = GameCard.Create("Swords to Plowshares", "Instant", "", "{W}", null, null);
    bot.Hand.Add(swords);
    bot.ManaPool.Add(ManaColor.White);

    var handler = new AiBotDecisionHandler { ActionDelayMs = 0 };
    var action = await handler.GetAction(state, bot.Id);

    action.Type.Should().Be(ActionType.CastSpell);
    action.CardId.Should().Be(swords.Id);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "AiBotReactivePlayTests"`
Expected: FAIL on the new tests

**Step 3: Write minimal implementation**

Add instant removal evaluation to `EvaluateReaction`:

```csharp
private static GameAction? EvaluateReaction(Player player, Player opponent, GameState gameState, Guid playerId)
{
    // Check for counterspells when opponent has spell on stack
    if (gameState.StackCount > 0)
    {
        var topOfStack = gameState.StackPeekTop();
        if (topOfStack != null && topOfStack.ControllerId != playerId)
        {
            var counterAction = EvaluateCounterspell(player, opponent, gameState, playerId, topOfStack);
            if (counterAction != null) return counterAction;
        }
    }

    // Instant removal: during combat (declare attackers) or end step
    if (gameState.StackCount == 0
        && (gameState.CurrentPhase == Phase.Combat || gameState.CurrentPhase == Phase.EndStep))
    {
        var removalAction = EvaluateInstantRemoval(player, opponent, playerId);
        if (removalAction != null) return removalAction;
    }

    return null;
}

private static GameAction? EvaluateInstantRemoval(Player player, Player opponent, Guid playerId)
{
    var opponentCreatures = opponent.Battlefield.Cards
        .Where(c => c.IsCreature)
        .OrderByDescending(c => c.Power ?? 0)
        .ToList();

    if (opponentCreatures.Count == 0) return null;

    foreach (var card in player.Hand.Cards)
    {
        if (!CardDefinitions.TryGet(card.Name, out var def)) continue;
        if (def.SpellRole != SpellRole.InstantRemoval) continue;
        if (def.ManaCost == null) continue;

        // Check if we can pay the mana cost
        if (!player.ManaPool.CanPay(def.ManaCost))
        {
            // Check alternate cost (Snuff Out: 4 life)
            if (def.AlternateCost != null && def.AlternateCost.LifeCost > 0
                && player.Life > def.AlternateCost.LifeCost + 5) // keep some life buffer
            {
                return GameAction.CastSpell(playerId, card.Id, useAlternateCost: true);
            }
            continue;
        }

        // Target the biggest creature
        return GameAction.CastSpell(playerId, card.Id);
    }

    return null;
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "AiBotReactivePlayTests"`
Expected: PASS (all 9 tests)

**Step 5: Run all engine tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs tests/MtgDecker.Engine.Tests/AI/AiBotReactivePlayTests.cs
git commit -m "feat(engine): add instant removal reactive play to AI bot"
```

---

### Task 7: Smart attack evaluation

**Files:**
- Modify: `src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs`
- Create: `tests/MtgDecker.Engine.Tests/AI/AiBotCombatTests.cs`

**Step 1: Write the failing tests**

Create `tests/MtgDecker.Engine.Tests/AI/AiBotCombatTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.AI;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests.AI;

public class AiBotCombatTests
{
    [Fact]
    public async Task ChooseAttackers_AttacksAll_WhenOpponentHasNoCreatures()
    {
        var attacker1 = CreateCreature("Goblin Lackey", 1, 1);
        var attacker2 = CreateCreature("Mogg Fanatic", 1, 1);
        var eligible = new List<GameCard> { attacker1, attacker2 };

        var bot = new AiBotDecisionHandler { ActionDelayMs = 0 };
        var result = await bot.ChooseAttackers(eligible, opponentCreatures: [], opponentLife: 20);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ChooseAttackers_DoesNotAttack_WhenWouldDieToBlocker()
    {
        // 1/1 goblin vs opponent's 4/5 Tarmogoyf — don't attack into certain death
        var smallCreature = CreateCreature("Goblin Lackey", 1, 1);
        var bigBlocker = CreateCreature("Tarmogoyf", 4, 5);

        var bot = new AiBotDecisionHandler { ActionDelayMs = 0 };
        var result = await bot.ChooseAttackers(
            [smallCreature],
            opponentCreatures: [bigBlocker],
            opponentLife: 20);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ChooseAttackers_AttacksWithEvasion_EvenWithBlockers()
    {
        var flyer = CreateCreature("Insectile Aberration", 3, 2);
        flyer.ActiveKeywords.Add(Keyword.Flying);
        var groundBlocker = CreateCreature("Tarmogoyf", 4, 5);

        var bot = new AiBotDecisionHandler { ActionDelayMs = 0 };
        var result = await bot.ChooseAttackers(
            [flyer],
            opponentCreatures: [groundBlocker],
            opponentLife: 20);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task ChooseAttackers_AttacksAll_WhenLethal()
    {
        // Total power = 6, opponent at 5 life — attack with everything
        var creature1 = CreateCreature("Siege-Gang Commander", 2, 2);
        var creature2 = CreateCreature("Goblin Piledriver", 4, 2); // pretend pumped
        var bigBlocker = CreateCreature("Tarmogoyf", 4, 5);

        var bot = new AiBotDecisionHandler { ActionDelayMs = 0 };
        var result = await bot.ChooseAttackers(
            [creature1, creature2],
            opponentCreatures: [bigBlocker],
            opponentLife: 5);

        // Should attack with all — total 6 power > 5 life even if one gets blocked
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ChooseAttackers_AttacksWithFavorableTrade()
    {
        // 3/3 vs opponent's 2/2 — attack because we survive the block
        var ourCreature = CreateCreature("Big Goblin", 3, 3);
        var theirCreature = CreateCreature("Small Goblin", 2, 2);

        var bot = new AiBotDecisionHandler { ActionDelayMs = 0 };
        var result = await bot.ChooseAttackers(
            [ourCreature],
            opponentCreatures: [theirCreature],
            opponentLife: 20);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task ChooseBlockers_BlocksWhenLethal()
    {
        // Opponent attacks with 5/5, we're at 5 life — MUST block even with 1/1
        var attacker = CreateCreature("Big Creature", 5, 5);
        var blocker = CreateCreature("Goblin Lackey", 1, 1);

        var bot = new AiBotDecisionHandler { ActionDelayMs = 0 };
        var result = await bot.ChooseBlockers([blocker], [attacker], playerLife: 5);

        result.Should().ContainKey(blocker.Id);
    }

    [Fact]
    public async Task ChooseBlockers_DoesNotChumpBlock_WhenNotLethal()
    {
        // Opponent attacks with 5/5, we're at 20 life — don't chump with 1/1
        var attacker = CreateCreature("Big Creature", 5, 5);
        var blocker = CreateCreature("Goblin Lackey", 1, 1);

        var bot = new AiBotDecisionHandler { ActionDelayMs = 0 };
        var result = await bot.ChooseBlockers([blocker], [attacker], playerLife: 20);

        result.Should().BeEmpty();
    }

    private static GameCard CreateCreature(string name, int power, int toughness)
    {
        return GameCard.Create(name, "Creature", "", "{1}", power.ToString(), toughness.ToString());
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "AiBotCombatTests"`
Expected: FAIL — `ChooseAttackers` signature doesn't match (needs opponent info).

**Step 3: Write minimal implementation**

The existing `ChooseAttackers` and `ChooseBlockers` interface methods have fixed signatures defined by `IPlayerDecisionHandler`. We can't change the interface. Instead, we need to store game context for the bot to use.

Add a field to cache game state for combat decisions:

```csharp
private Player? _cachedOpponent;
private int _cachedOpponentLife;
```

Add a method to set context before combat (called from GetAction or a hook):

Actually, the problem is the `ChooseAttackers` interface only receives `eligibleAttackers`. The bot needs to see the opponent's board. Let's check if we can add overloaded versions or if we need a different approach.

**Better approach:** Add a `SetGameContext` method that the engine calls, or cache the last GameState from `GetAction`. Since `GetAction` is always called before combat (in the main phase / beginning of combat priority), we can cache the opponent state there.

Add fields:

```csharp
private Player? _lastOpponent;
private int _lastOpponentLife;
```

In `GetAction`, cache opponent info:

```csharp
// Cache opponent info for combat decisions
_lastOpponent = opponent;
_lastOpponentLife = opponent.Life;
```

Then override `ChooseAttackers` to use the cached info:

```csharp
public async Task<IReadOnlyList<Guid>> ChooseAttackers(IReadOnlyList<GameCard> eligibleAttackers,
    CancellationToken ct = default)
{
    await DelayAsync(ct);
    return ChooseAttackers(eligibleAttackers,
        opponentCreatures: _lastOpponent?.Battlefield.Cards.Where(c => c.IsCreature).ToList() ?? [],
        opponentLife: _lastOpponentLife);
}

internal static IReadOnlyList<Guid> ChooseAttackers(IReadOnlyList<GameCard> eligibleAttackers,
    IReadOnlyList<GameCard> opponentCreatures, int opponentLife)
{
    if (eligibleAttackers.Count == 0) return [];

    var totalPower = eligibleAttackers.Sum(c => c.Power ?? 0);

    // Always attack with everything if lethal
    if (totalPower >= opponentLife)
        return eligibleAttackers.Select(c => c.Id).ToList();

    // No blockers — attack with everything
    if (opponentCreatures.Count == 0)
        return eligibleAttackers.Select(c => c.Id).ToList();

    var selected = new List<Guid>();
    foreach (var attacker in eligibleAttackers)
    {
        var power = attacker.Power ?? 0;
        var toughness = attacker.Toughness ?? 0;

        // Flying creatures attack if opponent has no flyers
        if (attacker.ActiveKeywords.Contains(Keyword.Flying))
        {
            var opponentHasFlyer = opponentCreatures.Any(c =>
                c.ActiveKeywords.Contains(Keyword.Flying) || c.ActiveKeywords.Contains(Keyword.Reach));
            if (!opponentHasFlyer)
            {
                selected.Add(attacker.Id);
                continue;
            }
        }

        // Check if any opponent creature can profitably block us
        var wouldDie = opponentCreatures.Any(blocker =>
        {
            var blockerPower = blocker.Power ?? 0;
            var blockerToughness = blocker.Toughness ?? 0;
            // Blocker can block and would kill us
            return blockerPower >= toughness && CanBlock(blocker, attacker);
        });

        var wouldKillBlocker = opponentCreatures.Any(blocker =>
        {
            var blockerToughness = blocker.Toughness ?? 0;
            return power >= blockerToughness && CanBlock(blocker, attacker);
        });

        // Attack if we wouldn't die, or if it's a favorable trade
        if (!wouldDie || (wouldDie && wouldKillBlocker))
            selected.Add(attacker.Id);
    }

    return selected;
}

private static bool CanBlock(GameCard blocker, GameCard attacker)
{
    // Flying can only be blocked by flying/reach
    if (attacker.ActiveKeywords.Contains(Keyword.Flying))
        return blocker.ActiveKeywords.Contains(Keyword.Flying)
            || blocker.ActiveKeywords.Contains(Keyword.Reach);
    return true;
}
```

Similarly, update `ChooseBlockers` to consider lethal damage:

```csharp
public async Task<Dictionary<Guid, Guid>> ChooseBlockers(IReadOnlyList<GameCard> eligibleBlockers,
    IReadOnlyList<GameCard> attackers, CancellationToken ct = default)
{
    await DelayAsync(ct);
    return ChooseBlockers(eligibleBlockers, attackers, _lastOpponentLife > 0 ? GetSelfLife() : 20);
}

internal static Dictionary<Guid, Guid> ChooseBlockers(IReadOnlyList<GameCard> eligibleBlockers,
    IReadOnlyList<GameCard> attackers, int playerLife)
{
    var assignments = new Dictionary<Guid, Guid>();
    var usedBlockers = new HashSet<Guid>();

    // Calculate unblocked damage to see if we need to block for survival
    var totalAttackDamage = attackers.Sum(a => a.Power ?? 0);
    var mustBlockForSurvival = totalAttackDamage >= playerLife;

    foreach (var attacker in attackers.OrderByDescending(a => a.Power ?? 0))
    {
        // Look for favorable block (can kill attacker)
        var killBlocker = eligibleBlockers
            .Where(b => !usedBlockers.Contains(b.Id))
            .Where(b => (b.Power ?? 0) >= (attacker.Toughness ?? 0))
            .OrderBy(b => b.Power ?? 0)
            .FirstOrDefault();

        if (killBlocker != null)
        {
            assignments[killBlocker.Id] = attacker.Id;
            usedBlockers.Add(killBlocker.Id);
            continue;
        }

        // Chump-block only if damage would be lethal
        if (mustBlockForSurvival)
        {
            var chumpBlocker = eligibleBlockers
                .Where(b => !usedBlockers.Contains(b.Id))
                .OrderBy(b => b.Power ?? 0) // sacrifice smallest
                .FirstOrDefault();

            if (chumpBlocker != null)
            {
                assignments[chumpBlocker.Id] = attacker.Id;
                usedBlockers.Add(chumpBlocker.Id);
                totalAttackDamage -= (attacker.Power ?? 0); // Attacker's damage absorbed
                mustBlockForSurvival = totalAttackDamage >= playerLife;
            }
        }
    }

    return assignments;
}
```

The test methods use `internal static` overloads that accept opponent info directly, while the `IPlayerDecisionHandler` interface methods use cached state. Need a `GetSelfLife()` helper or cache player life too.

Add cache for self life:

```csharp
private int _lastSelfLife = 20;
```

In `GetAction`, cache: `_lastSelfLife = player.Life;`

Update the `ChooseBlockers(interface)` call:

```csharp
return ChooseBlockers(eligibleBlockers, attackers, playerLife: _lastSelfLife);
```

The test-only overloads with `opponentCreatures`/`opponentLife`/`playerLife` parameters let us test the logic directly without needing a full GameState.

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "AiBotCombatTests"`
Expected: PASS (all 7 tests)

**Step 5: Run all engine tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs tests/MtgDecker.Engine.Tests/AI/AiBotCombatTests.cs
git commit -m "feat(engine): add smart attack and block evaluation to AI bot"
```

---

### Task 8: Spell sequencing — curve and ramp priority

**Files:**
- Modify: `src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs`
- Create: `tests/MtgDecker.Engine.Tests/AI/AiBotSpellSequencingTests.cs`

**Step 1: Write the failing tests**

Create `tests/MtgDecker.Engine.Tests/AI/AiBotSpellSequencingTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.AI;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests.AI;

public class AiBotSpellSequencingTests
{
    [Fact]
    public void ChooseBestProactiveSpell_PrefersCheaperOnCurve()
    {
        // With 3 mana available, prefer 3-drop over 2-drop (uses mana efficiently)
        var twoDrop = GameCard.Create("Goblin Piledriver", "Creature — Goblin", "", "{1}{R}", null, null);
        var threeDrop = GameCard.Create("Goblin Warchief", "Creature — Goblin", "", "{1}{R}{R}", null, null);
        var hand = new List<GameCard> { twoDrop, threeDrop };

        // 3 red mana available
        var player = new Player(Guid.NewGuid(), "Bot");
        player.ManaPool.Add(ManaColor.Red, 3);
        var state = new GameState(player, new Player(Guid.NewGuid(), "Opp"));

        var result = AiBotDecisionHandler.ChooseBestProactiveSpell(hand, player, state);

        // Should pick the 3-drop — uses all available mana
        result!.Name.Should().Be("Goblin Warchief");
    }

    [Fact]
    public void ChooseBestProactiveSpell_ExcludesCounterspells()
    {
        var daze = GameCard.Create("Daze", "Instant", "", "{1}{U}", null, null);
        var creature = GameCard.Create("Delver of Secrets", "Creature — Human Wizard", "", "{U}", null, null);
        var hand = new List<GameCard> { daze, creature };

        var player = new Player(Guid.NewGuid(), "Bot");
        player.ManaPool.Add(ManaColor.Blue, 2);
        var state = new GameState(player, new Player(Guid.NewGuid(), "Opp"));

        var result = AiBotDecisionHandler.ChooseBestProactiveSpell(hand, player, state);

        result!.Name.Should().Be("Delver of Secrets");
    }

    [Fact]
    public void ChooseBestProactiveSpell_ExcludesInstantRemoval()
    {
        var bolt = GameCard.Create("Lightning Bolt", "Instant", "", "{R}", null, null);
        var creature = GameCard.Create("Goblin Lackey", "Creature — Goblin", "", "{R}", null, null);
        var hand = new List<GameCard> { bolt, creature };

        var player = new Player(Guid.NewGuid(), "Bot");
        player.ManaPool.Add(ManaColor.Red, 1);
        var state = new GameState(player, new Player(Guid.NewGuid(), "Opp"));

        var result = AiBotDecisionHandler.ChooseBestProactiveSpell(hand, player, state);

        result!.Name.Should().Be("Goblin Lackey");
    }

    [Fact]
    public void ChooseBestProactiveSpell_ReturnsNull_WhenOnlyReactiveSpells()
    {
        var bolt = GameCard.Create("Lightning Bolt", "Instant", "", "{R}", null, null);
        var daze = GameCard.Create("Daze", "Instant", "", "{1}{U}", null, null);
        var hand = new List<GameCard> { bolt, daze };

        var player = new Player(Guid.NewGuid(), "Bot");
        player.ManaPool.Add(ManaColor.Red, 1);
        player.ManaPool.Add(ManaColor.Blue, 2);
        var state = new GameState(player, new Player(Guid.NewGuid(), "Opp"));

        var result = AiBotDecisionHandler.ChooseBestProactiveSpell(hand, player, state);

        result.Should().BeNull();
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "AiBotSpellSequencingTests"`
Expected: Some may already pass (from Task 4's implementation). The key test is that the method is accessible and works correctly.

**Step 3: Verify/adjust implementation**

The `ChooseBestProactiveSpell` method from Task 4 already filters by SpellRole and sorts by CMC descending. The tests validate this behavior. If tests pass, the implementation is correct. If not, fix accordingly.

The "prefers cheaper on curve" test validates that highest CMC is chosen when affordable — this is the current `OrderByDescending(CMC)` behavior, which is correct for mana efficiency (use all your mana each turn).

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "AiBotSpellSequencingTests"`
Expected: PASS (all 4 tests)

**Step 5: Run all engine tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All pass

**Step 6: Commit**

```bash
git add tests/MtgDecker.Engine.Tests/AI/AiBotSpellSequencingTests.cs
git commit -m "test(engine): add spell sequencing tests for AI bot"
```

---

### Task 9: Final integration test and cleanup

**Files:**
- Modify: `src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs` (if cleanup needed)
- Run all tests

**Step 1: Run all engine tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All pass (1730 + ~30 new = ~1760)

**Step 2: Build the full web project**

Run: `dotnet build src/MtgDecker.Web/`
Expected: 0 errors

**Step 3: Run the other test projects to check for regressions**

Run: `dotnet test tests/MtgDecker.Domain.Tests/ && dotnet test tests/MtgDecker.Application.Tests/ && dotnet test tests/MtgDecker.Infrastructure.Tests/`
Expected: All pass

**Step 4: Fix any issues found**

If any existing simulation tests or AI-related tests fail due to behavioral changes (the AI now plays differently), update them to match the new improved behavior. The bot will now:
- Not cast counterspells proactively
- Tap fewer lands (only what's needed)
- Not attack blindly into blockers
- Choose better lands to play

Some simulation results may change, which is expected and correct.

**Step 5: Commit if cleanup was needed**

Only commit if changes were required.
