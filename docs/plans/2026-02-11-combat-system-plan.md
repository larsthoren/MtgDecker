# Combat System & Auto-Parse Card Data — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add full MTG combat mechanics (attackers, blockers, multi-block ordering, damage, summoning sickness, creature death) and auto-parse Scryfall card data so P/T, mana costs, and card types work for ALL cards — not just the 38 in CardDefinitions.

**Architecture:** Three workstreams executed sequentially. (A) Extend the data pipeline to store P/T on Card entity and pass it to the engine. (B) Build the combat engine with sub-phases inside the existing Combat phase, using the TaskCompletionSource decision handler pattern. (C) Build the combat UI following the existing PlayerZone/ActionMenu/inline-picker patterns.

**Tech Stack:** .NET 10, C# 14, xUnit + FluentAssertions, MtgDecker.Engine (no EF dependency), Blazor Server + MudBlazor

---

## Workstream A: Auto-Parse Card Data from Scryfall

### Task 1: Add Power/Toughness to ScryfallCard DTO

**Files:**
- Modify: `src/MtgDecker.Infrastructure/Scryfall/ScryfallCard.cs`

**Step 1: Add P/T properties to ScryfallCard**

Add after the `Layout` property (line 43):

```csharp
[JsonPropertyName("power")]
public string? Power { get; set; }

[JsonPropertyName("toughness")]
public string? Toughness { get; set; }
```

Scryfall sends `power` and `toughness` at the top level for single-faced creatures. Multi-faced cards have them per face (already mapped in `ScryfallCardFace`).

**Step 2: Commit**

```bash
git add src/MtgDecker.Infrastructure/Scryfall/ScryfallCard.cs
git commit -m "feat(infrastructure): add power/toughness to ScryfallCard DTO"
```

---

### Task 2: Add Power/Toughness to Card Domain Entity

**Files:**
- Modify: `src/MtgDecker.Domain/Entities/Card.cs`

**Step 1: Add P/T properties to Card entity**

Add after `Layout` property (after line 26):

```csharp
public string? Power { get; set; }
public string? Toughness { get; set; }
```

These are `string?` because MTG has non-numeric P/T like `*`, `1+*`, `X`.

**Step 2: Commit**

```bash
git add src/MtgDecker.Domain/Entities/Card.cs
git commit -m "feat(domain): add power/toughness to Card entity"
```

---

### Task 3: Update ScryfallCardMapper to Map P/T

**Files:**
- Modify: `src/MtgDecker.Infrastructure/Scryfall/ScryfallCardMapper.cs`

**Step 1: Map P/T in MapToCard**

In the `MapToCard` method, add P/T mapping to the Card object initializer (after `Layout = source.Layout` around line 27):

```csharp
Power = source.Power ?? source.CardFaces?.FirstOrDefault()?.Power,
Toughness = source.Toughness ?? source.CardFaces?.FirstOrDefault()?.Toughness,
```

This uses top-level P/T (single-faced cards) with fallback to first face (multi-faced cards).

**Step 2: Commit**

```bash
git add src/MtgDecker.Infrastructure/Scryfall/ScryfallCardMapper.cs
git commit -m "feat(infrastructure): map power/toughness in ScryfallCardMapper"
```

---

### Task 4: Add EF Core Migration for Power/Toughness

**Files:**
- Modify: `src/MtgDecker.Infrastructure/Data/Configurations/` (if explicit config exists for Card)
- Create: New migration files via `dotnet ef`

**Step 1: Check if Card has explicit EF configuration**

Look in `src/MtgDecker.Infrastructure/Data/Configurations/` for a CardConfiguration class. If P/T are simple `string?` properties, EF Core convention should handle them without explicit config.

**Step 2: Generate migration**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet ef migrations add AddCardPowerToughness --project src/MtgDecker.Infrastructure/ --startup-project src/MtgDecker.Web/
```

**Step 3: Verify migration SQL looks correct**

Read the generated migration file and confirm it adds two nullable `nvarchar(max)` columns.

**Step 4: Commit**

```bash
git add src/MtgDecker.Infrastructure/Data/Migrations/
git commit -m "feat(infrastructure): add migration for card power/toughness columns"
```

---

### Task 5: Add CardTypeParser to Engine

**Files:**
- Create: `src/MtgDecker.Engine/CardTypeParser.cs`
- Create: `tests/MtgDecker.Engine.Tests/CardTypeParserTests.cs`

**Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class CardTypeParserTests
{
    [Theory]
    [InlineData("Creature — Goblin", CardType.Creature)]
    [InlineData("Basic Land — Mountain", CardType.Land)]
    [InlineData("Enchantment Creature — Human", CardType.Creature | CardType.Enchantment)]
    [InlineData("Artifact Creature — Golem", CardType.Creature | CardType.Artifact)]
    [InlineData("Legendary Enchantment", CardType.Enchantment)]
    [InlineData("Instant", CardType.Instant)]
    [InlineData("Sorcery", CardType.Sorcery)]
    [InlineData("Artifact", CardType.Artifact)]
    [InlineData("Land", CardType.Land)]
    [InlineData("Legendary Creature — Dragon", CardType.Creature)]
    [InlineData("Legendary Artifact — Equipment", CardType.Artifact)]
    [InlineData("Enchantment — Aura", CardType.Enchantment)]
    [InlineData("", CardType.None)]
    public void Parse_TypeLine_ReturnsCorrectCardType(string typeLine, CardType expected)
    {
        CardTypeParser.Parse(typeLine).Should().Be(expected);
    }
}
```

**Step 2: Run test to verify it fails**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "CardTypeParserTests" -v minimal
```

Expected: FAIL — `CardTypeParser` does not exist.

**Step 3: Write minimal implementation**

```csharp
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public static class CardTypeParser
{
    public static CardType Parse(string typeLine)
    {
        if (string.IsNullOrWhiteSpace(typeLine))
            return CardType.None;

        // Only check the part before the em dash (supertypes + types, not subtypes)
        var mainPart = typeLine.Contains('—')
            ? typeLine[..typeLine.IndexOf('—')]
            : typeLine;

        var result = CardType.None;

        if (mainPart.Contains("Creature", StringComparison.OrdinalIgnoreCase))
            result |= CardType.Creature;
        if (mainPart.Contains("Land", StringComparison.OrdinalIgnoreCase))
            result |= CardType.Land;
        if (mainPart.Contains("Enchantment", StringComparison.OrdinalIgnoreCase))
            result |= CardType.Enchantment;
        if (mainPart.Contains("Instant", StringComparison.OrdinalIgnoreCase))
            result |= CardType.Instant;
        if (mainPart.Contains("Sorcery", StringComparison.OrdinalIgnoreCase))
            result |= CardType.Sorcery;
        if (mainPart.Contains("Artifact", StringComparison.OrdinalIgnoreCase))
            result |= CardType.Artifact;

        return result;
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "CardTypeParserTests" -v minimal
```

Expected: All pass.

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/CardTypeParser.cs tests/MtgDecker.Engine.Tests/CardTypeParserTests.cs
git commit -m "feat(engine): add CardTypeParser for parsing TypeLine into CardType flags"
```

---

### Task 6: Enhance GameCard.Create to Accept Raw Card Data

**Files:**
- Modify: `src/MtgDecker.Engine/GameCard.cs`
- Create: `tests/MtgDecker.Engine.Tests/GameCardAutoParseTests.cs`

**Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests;

public class GameCardAutoParseTests
{
    [Fact]
    public void Create_WithManaCostString_ParsesManaCost()
    {
        var card = GameCard.Create("Grizzly Bears", "Creature — Bear",
            imageUrl: null, manaCost: "{1}{G}", power: "2", toughness: "2");

        card.ManaCost.Should().NotBeNull();
        card.ManaCost!.ConvertedManaCost.Should().Be(2);
        card.ManaCost.ColorRequirements[ManaColor.Green].Should().Be(1);
        card.ManaCost.GenericCost.Should().Be(1);
    }

    [Fact]
    public void Create_WithPowerToughness_ParsesIntegers()
    {
        var card = GameCard.Create("Grizzly Bears", "Creature — Bear",
            imageUrl: null, manaCost: "{1}{G}", power: "2", toughness: "2");

        card.Power.Should().Be(2);
        card.Toughness.Should().Be(2);
    }

    [Fact]
    public void Create_WithNonNumericPower_SetsNull()
    {
        var card = GameCard.Create("Tarmogoyf", "Creature — Lhurgoyf",
            imageUrl: null, manaCost: "{1}{G}", power: "*", toughness: "1+*");

        card.Power.Should().BeNull();
        card.Toughness.Should().BeNull();
    }

    [Fact]
    public void Create_WithTypeLine_ParsesCardType()
    {
        var card = GameCard.Create("Grizzly Bears", "Creature — Bear",
            imageUrl: null, manaCost: "{1}{G}", power: "2", toughness: "2");

        card.CardTypes.Should().Be(CardType.Creature);
    }

    [Fact]
    public void Create_BasicLand_AutoDetectsManaAbility()
    {
        var card = GameCard.Create("Mountain", "Basic Land — Mountain",
            imageUrl: null, manaCost: null, power: null, toughness: null);

        card.ManaAbility.Should().NotBeNull();
        card.ManaAbility!.Type.Should().Be(Mana.ManaAbilityType.Fixed);
        card.ManaAbility.FixedColor.Should().Be(ManaColor.Red);
        card.CardTypes.Should().Be(CardType.Land);
    }

    [Fact]
    public void Create_RegistryCardOverridesAutoParse()
    {
        // Goblin Lackey is in CardDefinitions — its registry data should take precedence
        var card = GameCard.Create("Goblin Lackey", "Creature — Goblin",
            imageUrl: null, manaCost: "{R}", power: "1", toughness: "1");

        card.ManaCost.Should().NotBeNull();
        card.Power.Should().Be(1);
        card.Toughness.Should().Be(1);
    }

    [Fact]
    public void Create_InstantSorcery_HasNoManaAbility()
    {
        var card = GameCard.Create("Lightning Bolt", "Instant",
            imageUrl: null, manaCost: "{R}", power: null, toughness: null);

        card.ManaAbility.Should().BeNull();
        card.CardTypes.Should().Be(CardType.Instant);
    }

    [Fact]
    public void Create_LegacyOverload_StillWorks()
    {
        // The old 3-parameter overload should still work
        var card = GameCard.Create("Mountain", "Basic Land — Mountain");

        card.Name.Should().Be("Mountain");
        card.TypeLine.Should().Be("Basic Land — Mountain");
    }
}
```

**Step 2: Run test to verify it fails**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "GameCardAutoParseTests" -v minimal
```

Expected: FAIL — no matching overload.

**Step 3: Write the implementation**

Update `GameCard.cs`:

```csharp
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine;

public class GameCard
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
    public string TypeLine { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public bool IsTapped { get; set; }

    // Resolved from CardDefinitions registry or auto-parsed
    public ManaCost? ManaCost { get; set; }
    public ManaAbility? ManaAbility { get; set; }
    public int? Power { get; set; }
    public int? Toughness { get; set; }
    public CardType CardTypes { get; set; } = CardType.None;

    // Backward-compatible: check both CardTypes flags and TypeLine
    public bool IsLand =>
        CardTypes.HasFlag(CardType.Land) ||
        TypeLine.Contains("Land", StringComparison.OrdinalIgnoreCase);

    public bool IsCreature =>
        CardTypes.HasFlag(CardType.Creature) ||
        TypeLine.Contains("Creature", StringComparison.OrdinalIgnoreCase);

    /// <summary>Original factory: uses CardDefinitions registry only.</summary>
    public static GameCard Create(string name, string typeLine = "", string? imageUrl = null)
    {
        var card = new GameCard { Name = name, TypeLine = typeLine, ImageUrl = imageUrl };
        if (CardDefinitions.TryGet(name, out var def))
        {
            card.ManaCost = def.ManaCost;
            card.ManaAbility = def.ManaAbility;
            card.Power = def.Power;
            card.Toughness = def.Toughness;
            card.CardTypes = def.CardTypes;
        }
        return card;
    }

    /// <summary>
    /// Enhanced factory: auto-parses card data from raw strings (Scryfall DB fields).
    /// Falls back to CardDefinitions for ManaAbility on non-basic lands.
    /// </summary>
    public static GameCard Create(string name, string typeLine, string? imageUrl,
        string? manaCost, string? power, string? toughness)
    {
        var card = new GameCard { Name = name, TypeLine = typeLine, ImageUrl = imageUrl };

        // CardDefinitions registry takes full precedence if the card is registered
        if (CardDefinitions.TryGet(name, out var def))
        {
            card.ManaCost = def.ManaCost;
            card.ManaAbility = def.ManaAbility;
            card.Power = def.Power;
            card.Toughness = def.Toughness;
            card.CardTypes = def.CardTypes;
            return card;
        }

        // Auto-parse from raw data
        card.CardTypes = CardTypeParser.Parse(typeLine);

        if (!string.IsNullOrWhiteSpace(manaCost))
            card.ManaCost = ManaCost.Parse(manaCost);

        if (int.TryParse(power, out var p))
            card.Power = p;
        if (int.TryParse(toughness, out var t))
            card.Toughness = t;

        // Auto-detect mana ability for basic lands
        card.ManaAbility = DetectBasicLandManaAbility(typeLine);

        return card;
    }

    private static ManaAbility? DetectBasicLandManaAbility(string typeLine)
    {
        if (!typeLine.Contains("Basic Land", StringComparison.OrdinalIgnoreCase))
            return null;

        if (typeLine.Contains("Plains", StringComparison.OrdinalIgnoreCase))
            return ManaAbility.Fixed(ManaColor.White);
        if (typeLine.Contains("Island", StringComparison.OrdinalIgnoreCase))
            return ManaAbility.Fixed(ManaColor.Blue);
        if (typeLine.Contains("Swamp", StringComparison.OrdinalIgnoreCase))
            return ManaAbility.Fixed(ManaColor.Black);
        if (typeLine.Contains("Mountain", StringComparison.OrdinalIgnoreCase))
            return ManaAbility.Fixed(ManaColor.Red);
        if (typeLine.Contains("Forest", StringComparison.OrdinalIgnoreCase))
            return ManaAbility.Fixed(ManaColor.Green);

        return null;
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "GameCardAutoParseTests" -v minimal
```

Expected: All pass.

**Step 5: Run all engine tests to check for regressions**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ -v minimal
```

Expected: All 261+ tests pass.

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/GameCard.cs tests/MtgDecker.Engine.Tests/GameCardAutoParseTests.cs
git commit -m "feat(engine): auto-parse card data from raw Scryfall fields in GameCard.Create"
```

---

### Task 7: Update GameLobby to Pass Card Data to Engine

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/GameLobby.razor`

**Step 1: Update LoadGameDeck method**

Change line 176 from:
```csharp
gameCards.Add(GameCard.Create(card.Name, card.TypeLine, card.ImageUriSmall ?? card.ImageUri));
```

To:
```csharp
gameCards.Add(GameCard.Create(card.Name, card.TypeLine,
    card.ImageUriSmall ?? card.ImageUri,
    card.ManaCost, card.Power, card.Toughness));
```

**Step 2: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/GameLobby.razor
git commit -m "feat(web): pass card ManaCost/Power/Toughness to engine GameCard.Create"
```

---

## Workstream B: Combat Engine

### Task 8: Add CombatStep Enum

**Files:**
- Create: `src/MtgDecker.Engine/Enums/CombatStep.cs`

**Step 1: Create the enum**

```csharp
namespace MtgDecker.Engine.Enums;

public enum CombatStep
{
    None,
    BeginCombat,
    DeclareAttackers,
    DeclareBlockers,
    CombatDamage,
    EndCombat
}
```

**Step 2: Commit**

```bash
git add src/MtgDecker.Engine/Enums/CombatStep.cs
git commit -m "feat(engine): add CombatStep enum for combat sub-phases"
```

---

### Task 9: Add CombatState Class

**Files:**
- Create: `src/MtgDecker.Engine/CombatState.cs`
- Create: `tests/MtgDecker.Engine.Tests/CombatStateTests.cs`

**Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class CombatStateTests
{
    [Fact]
    public void DeclareAttacker_AddsToAttackersList()
    {
        var state = new CombatState(Guid.NewGuid(), Guid.NewGuid());
        var attackerId = Guid.NewGuid();

        state.DeclareAttacker(attackerId);

        state.Attackers.Should().Contain(attackerId);
    }

    [Fact]
    public void DeclareBlocker_AssignsBlockerToAttacker()
    {
        var state = new CombatState(Guid.NewGuid(), Guid.NewGuid());
        var attackerId = Guid.NewGuid();
        var blockerId = Guid.NewGuid();
        state.DeclareAttacker(attackerId);

        state.DeclareBlocker(blockerId, attackerId);

        state.GetBlockers(attackerId).Should().Contain(blockerId);
    }

    [Fact]
    public void DeclareBlocker_MultipleBlockersOnOneAttacker()
    {
        var state = new CombatState(Guid.NewGuid(), Guid.NewGuid());
        var attackerId = Guid.NewGuid();
        var blocker1 = Guid.NewGuid();
        var blocker2 = Guid.NewGuid();
        state.DeclareAttacker(attackerId);

        state.DeclareBlocker(blocker1, attackerId);
        state.DeclareBlocker(blocker2, attackerId);

        state.GetBlockers(attackerId).Should().HaveCount(2);
        state.IsBlocked(attackerId).Should().BeTrue();
    }

    [Fact]
    public void IsBlocked_UnblockedAttacker_ReturnsFalse()
    {
        var state = new CombatState(Guid.NewGuid(), Guid.NewGuid());
        var attackerId = Guid.NewGuid();
        state.DeclareAttacker(attackerId);

        state.IsBlocked(attackerId).Should().BeFalse();
    }

    [Fact]
    public void SetBlockerOrder_SetsOrderForMultiBlock()
    {
        var state = new CombatState(Guid.NewGuid(), Guid.NewGuid());
        var attackerId = Guid.NewGuid();
        var blocker1 = Guid.NewGuid();
        var blocker2 = Guid.NewGuid();
        state.DeclareAttacker(attackerId);
        state.DeclareBlocker(blocker1, attackerId);
        state.DeclareBlocker(blocker2, attackerId);

        state.SetBlockerOrder(attackerId, new List<Guid> { blocker2, blocker1 });

        state.GetBlockerOrder(attackerId).Should().ContainInOrder(blocker2, blocker1);
    }

    [Fact]
    public void GetBlockerOrder_SingleBlocker_ReturnsThatBlocker()
    {
        var state = new CombatState(Guid.NewGuid(), Guid.NewGuid());
        var attackerId = Guid.NewGuid();
        var blockerId = Guid.NewGuid();
        state.DeclareAttacker(attackerId);
        state.DeclareBlocker(blockerId, attackerId);

        state.GetBlockerOrder(attackerId).Should().ContainSingle()
            .Which.Should().Be(blockerId);
    }
}
```

**Step 2: Run test to verify it fails**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "CombatStateTests" -v minimal
```

Expected: FAIL — `CombatState` does not exist.

**Step 3: Write the implementation**

```csharp
namespace MtgDecker.Engine;

public class CombatState
{
    public Guid AttackingPlayerId { get; }
    public Guid DefendingPlayerId { get; }
    public List<Guid> Attackers { get; } = new();

    private readonly Dictionary<Guid, List<Guid>> _blockerAssignments = new(); // attackerId -> blockerIds
    private readonly Dictionary<Guid, List<Guid>> _blockerOrder = new(); // attackerId -> ordered blockerIds

    public CombatState(Guid attackingPlayerId, Guid defendingPlayerId)
    {
        AttackingPlayerId = attackingPlayerId;
        DefendingPlayerId = defendingPlayerId;
    }

    public void DeclareAttacker(Guid cardId)
    {
        if (!Attackers.Contains(cardId))
            Attackers.Add(cardId);
    }

    public void DeclareBlocker(Guid blockerId, Guid attackerId)
    {
        if (!_blockerAssignments.ContainsKey(attackerId))
            _blockerAssignments[attackerId] = new List<Guid>();
        _blockerAssignments[attackerId].Add(blockerId);
    }

    public IReadOnlyList<Guid> GetBlockers(Guid attackerId) =>
        _blockerAssignments.TryGetValue(attackerId, out var blockers) ? blockers : [];

    public bool IsBlocked(Guid attackerId) =>
        _blockerAssignments.TryGetValue(attackerId, out var blockers) && blockers.Count > 0;

    public void SetBlockerOrder(Guid attackerId, List<Guid> orderedBlockerIds)
    {
        _blockerOrder[attackerId] = orderedBlockerIds;
    }

    public IReadOnlyList<Guid> GetBlockerOrder(Guid attackerId)
    {
        if (_blockerOrder.TryGetValue(attackerId, out var order))
            return order;
        // Default: return blockers in declaration order
        return GetBlockers(attackerId);
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "CombatStateTests" -v minimal
```

Expected: All pass.

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/CombatState.cs tests/MtgDecker.Engine.Tests/CombatStateTests.cs
git commit -m "feat(engine): add CombatState class for tracking attackers and blockers"
```

---

### Task 10: Add Combat Properties to GameState and GameCard

**Files:**
- Modify: `src/MtgDecker.Engine/GameState.cs`
- Modify: `src/MtgDecker.Engine/GameCard.cs`

**Step 1: Add CombatStep and CombatState to GameState**

Add after `IsFirstTurn` property:

```csharp
public CombatStep CombatStep { get; set; } = CombatStep.None;
public CombatState? Combat { get; set; }
```

Add `using MtgDecker.Engine.Enums;` if not already present (it is — `Phase` is from that namespace).

**Step 2: Add combat tracking to GameCard**

Add after `CardTypes` property:

```csharp
// Combat tracking
public int? TurnEnteredBattlefield { get; set; }
public int DamageMarked { get; set; }

public bool HasSummoningSickness(int currentTurn) =>
    IsCreature && TurnEnteredBattlefield.HasValue && TurnEnteredBattlefield.Value >= currentTurn;
```

**Step 3: Set TurnEnteredBattlefield in GameEngine.ExecuteAction**

In `GameEngine.ExecuteAction`, for `ActionType.PlayCard`:
- After `player.Battlefield.Add(playCard)` in the land drop path (line 102), add:
  ```csharp
  playCard.TurnEnteredBattlefield = _state.TurnNumber;
  ```
- After `player.Battlefield.Add(playCard)` in the spell path (line 191), add:
  ```csharp
  playCard.TurnEnteredBattlefield = _state.TurnNumber;
  ```
- After `player.Battlefield.Add(playCard)` in the sandbox path (line 202), add:
  ```csharp
  playCard.TurnEnteredBattlefield = _state.TurnNumber;
  ```

**Step 4: Commit**

```bash
git add src/MtgDecker.Engine/GameState.cs src/MtgDecker.Engine/GameCard.cs src/MtgDecker.Engine/GameEngine.cs
git commit -m "feat(engine): add combat state to GameState, summoning sickness and damage tracking to GameCard"
```

---

### Task 11: Add Combat Decision Handler Methods

**Files:**
- Modify: `src/MtgDecker.Engine/IPlayerDecisionHandler.cs`
- Modify: `src/MtgDecker.Engine/InteractiveDecisionHandler.cs`
- Modify: `tests/MtgDecker.Engine.Tests/Helpers/TestDecisionHandler.cs`

**Step 1: Add methods to IPlayerDecisionHandler**

```csharp
Task<IReadOnlyList<Guid>> ChooseAttackers(IReadOnlyList<GameCard> eligibleAttackers, CancellationToken ct = default);
Task<Dictionary<Guid, Guid>> ChooseBlockers(IReadOnlyList<GameCard> eligibleBlockers, IReadOnlyList<GameCard> attackers, CancellationToken ct = default);
Task<IReadOnlyList<Guid>> OrderBlockers(Guid attackerId, IReadOnlyList<GameCard> blockers, CancellationToken ct = default);
```

- `ChooseAttackers`: Returns list of card IDs the player wants to attack with (may be empty = skip combat).
- `ChooseBlockers`: Returns `blockerId -> attackerId` mapping. Only includes creatures that ARE blocking (creatures not in the dict don't block).
- `OrderBlockers`: For multi-block situations, the attacker orders the blockers on one of their attackers. Returns ordered list of blocker card IDs.

**Step 2: Add to InteractiveDecisionHandler**

Add new TCS fields:

```csharp
private TaskCompletionSource<IReadOnlyList<Guid>>? _attackersTcs;
private TaskCompletionSource<Dictionary<Guid, Guid>>? _blockersTcs;
private TaskCompletionSource<IReadOnlyList<Guid>>? _blockerOrderTcs;
```

Add waiting properties:

```csharp
public bool IsWaitingForAttackers => _attackersTcs is { Task.IsCompleted: false };
public bool IsWaitingForBlockers => _blockersTcs is { Task.IsCompleted: false };
public bool IsWaitingForBlockerOrder => _blockerOrderTcs is { Task.IsCompleted: false };
public IReadOnlyList<GameCard>? EligibleAttackers { get; private set; }
public IReadOnlyList<GameCard>? EligibleBlockers { get; private set; }
public IReadOnlyList<GameCard>? CurrentAttackers { get; private set; }
public Guid? OrderingAttackerId { get; private set; }
public IReadOnlyList<GameCard>? BlockersToOrder { get; private set; }
```

Add interface implementations:

```csharp
public Task<IReadOnlyList<Guid>> ChooseAttackers(IReadOnlyList<GameCard> eligibleAttackers, CancellationToken ct = default)
{
    EligibleAttackers = eligibleAttackers;
    _attackersTcs = new TaskCompletionSource<IReadOnlyList<Guid>>(TaskCreationOptions.RunContinuationsAsynchronously);
    var registration = ct.Register(() => { EligibleAttackers = null; _attackersTcs.TrySetCanceled(); });
    _attackersTcs.Task.ContinueWith(_ => registration.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
    OnWaitingForInput?.Invoke();
    return _attackersTcs.Task;
}

public Task<Dictionary<Guid, Guid>> ChooseBlockers(IReadOnlyList<GameCard> eligibleBlockers, IReadOnlyList<GameCard> attackers, CancellationToken ct = default)
{
    EligibleBlockers = eligibleBlockers;
    CurrentAttackers = attackers;
    _blockersTcs = new TaskCompletionSource<Dictionary<Guid, Guid>>(TaskCreationOptions.RunContinuationsAsynchronously);
    var registration = ct.Register(() => { EligibleBlockers = null; CurrentAttackers = null; _blockersTcs.TrySetCanceled(); });
    _blockersTcs.Task.ContinueWith(_ => registration.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
    OnWaitingForInput?.Invoke();
    return _blockersTcs.Task;
}

public Task<IReadOnlyList<Guid>> OrderBlockers(Guid attackerId, IReadOnlyList<GameCard> blockers, CancellationToken ct = default)
{
    OrderingAttackerId = attackerId;
    BlockersToOrder = blockers;
    _blockerOrderTcs = new TaskCompletionSource<IReadOnlyList<Guid>>(TaskCreationOptions.RunContinuationsAsynchronously);
    var registration = ct.Register(() => { OrderingAttackerId = null; BlockersToOrder = null; _blockerOrderTcs.TrySetCanceled(); });
    _blockerOrderTcs.Task.ContinueWith(_ => registration.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
    OnWaitingForInput?.Invoke();
    return _blockerOrderTcs.Task;
}
```

Add submit methods:

```csharp
public void SubmitAttackers(IReadOnlyList<Guid> attackerIds)
{
    EligibleAttackers = null;
    _attackersTcs?.TrySetResult(attackerIds);
}

public void SubmitBlockers(Dictionary<Guid, Guid> assignments)
{
    EligibleBlockers = null;
    CurrentAttackers = null;
    _blockersTcs?.TrySetResult(assignments);
}

public void SubmitBlockerOrder(IReadOnlyList<Guid> orderedBlockerIds)
{
    OrderingAttackerId = null;
    BlockersToOrder = null;
    _blockerOrderTcs?.TrySetResult(orderedBlockerIds);
}
```

**Step 3: Add to TestDecisionHandler**

Add queues:

```csharp
private readonly Queue<IReadOnlyList<Guid>> _attackerQueue = new();
private readonly Queue<Dictionary<Guid, Guid>> _blockerQueue = new();
private readonly Queue<IReadOnlyList<Guid>> _blockerOrderQueue = new();
```

Add enqueue methods:

```csharp
public void EnqueueAttackers(IReadOnlyList<Guid> attackerIds) => _attackerQueue.Enqueue(attackerIds);
public void EnqueueBlockers(Dictionary<Guid, Guid> assignments) => _blockerQueue.Enqueue(assignments);
public void EnqueueBlockerOrder(IReadOnlyList<Guid> order) => _blockerOrderQueue.Enqueue(order);
```

Add interface implementations with defaults:

```csharp
public Task<IReadOnlyList<Guid>> ChooseAttackers(IReadOnlyList<GameCard> eligibleAttackers, CancellationToken ct = default)
    => Task.FromResult(_attackerQueue.Count > 0 ? _attackerQueue.Dequeue() : (IReadOnlyList<Guid>)Array.Empty<Guid>());

public Task<Dictionary<Guid, Guid>> ChooseBlockers(IReadOnlyList<GameCard> eligibleBlockers, IReadOnlyList<GameCard> attackers, CancellationToken ct = default)
    => Task.FromResult(_blockerQueue.Count > 0 ? _blockerQueue.Dequeue() : new Dictionary<Guid, Guid>());

public Task<IReadOnlyList<Guid>> OrderBlockers(Guid attackerId, IReadOnlyList<GameCard> blockers, CancellationToken ct = default)
    => Task.FromResult(_blockerOrderQueue.Count > 0 ? _blockerOrderQueue.Dequeue() : (IReadOnlyList<Guid>)blockers.Select(b => b.Id).ToList());
```

Default behavior: no attackers, no blockers, keep declaration order.

**Step 4: Commit**

```bash
git add src/MtgDecker.Engine/IPlayerDecisionHandler.cs src/MtgDecker.Engine/InteractiveDecisionHandler.cs tests/MtgDecker.Engine.Tests/Helpers/TestDecisionHandler.cs
git commit -m "feat(engine): add combat decision handler methods (attackers, blockers, ordering)"
```

---

### Task 12: Implement RunCombatAsync in GameEngine

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs`
- Create: `tests/MtgDecker.Engine.Tests/CombatEngineTests.cs`

**Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class CombatEngineTests
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
    public async Task NoAttackers_SkipsCombat()
    {
        var (engine, state, p1Handler, _) = CreateSetup();
        await engine.StartGameAsync();

        // Place a creature (no summoning sickness — set turn before current)
        var creature = new GameCard { Name = "Bear", TypeLine = "Creature — Bear", Power = 2, Toughness = 2, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(creature);

        // Enqueue: no attackers (empty list)
        p1Handler.EnqueueAttackers(Array.Empty<Guid>());

        await engine.RunCombatAsync(CancellationToken.None);

        state.Player2.Life.Should().Be(20, "no attackers means no damage");
    }

    [Fact]
    public async Task UnblockedAttacker_DealsDamageToDefender()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var creature = new GameCard { Name = "Bear", TypeLine = "Creature — Bear", Power = 2, Toughness = 2, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(creature);

        p1Handler.EnqueueAttackers(new List<Guid> { creature.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid>()); // no blockers

        await engine.RunCombatAsync(CancellationToken.None);

        state.Player2.Life.Should().Be(18, "2/2 creature should deal 2 damage");
        creature.IsTapped.Should().BeTrue("attacker should be tapped");
    }

    [Fact]
    public async Task BlockedAttacker_DealsNoDamageToDefender()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var attacker = new GameCard { Name = "Bear", TypeLine = "Creature — Bear", Power = 2, Toughness = 2, TurnEnteredBattlefield = 0 };
        var blocker = new GameCard { Name = "Wall", TypeLine = "Creature — Wall", Power = 0, Toughness = 4, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(attacker);
        state.Player2.Battlefield.Add(blocker);

        p1Handler.EnqueueAttackers(new List<Guid> { attacker.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid> { { blocker.Id, attacker.Id } });

        await engine.RunCombatAsync(CancellationToken.None);

        state.Player2.Life.Should().Be(20, "blocked attacker deals no damage to player");
    }

    [Fact]
    public async Task CombatDamage_KillsCreatureWithLethalDamage()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var attacker = new GameCard { Name = "Big Bear", TypeLine = "Creature", Power = 5, Toughness = 5, TurnEnteredBattlefield = 0 };
        var blocker = new GameCard { Name = "Small Bear", TypeLine = "Creature", Power = 2, Toughness = 2, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(attacker);
        state.Player2.Battlefield.Add(blocker);

        p1Handler.EnqueueAttackers(new List<Guid> { attacker.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid> { { blocker.Id, attacker.Id } });

        await engine.RunCombatAsync(CancellationToken.None);

        state.Player2.Battlefield.Cards.Should().NotContain(c => c.Id == blocker.Id, "blocker should die to 5 damage");
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Id == blocker.Id, "dead blocker goes to graveyard");
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == attacker.Id, "5/5 survives 2 damage");
        attacker.DamageMarked.Should().Be(2, "attacker took 2 damage from blocker");
    }

    [Fact]
    public async Task CombatDamage_BothCreaturesDie()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var attacker = new GameCard { Name = "Bear", TypeLine = "Creature", Power = 2, Toughness = 2, TurnEnteredBattlefield = 0 };
        var blocker = new GameCard { Name = "Bear2", TypeLine = "Creature", Power = 2, Toughness = 2, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(attacker);
        state.Player2.Battlefield.Add(blocker);

        p1Handler.EnqueueAttackers(new List<Guid> { attacker.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid> { { blocker.Id, attacker.Id } });

        await engine.RunCombatAsync(CancellationToken.None);

        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Id == attacker.Id);
        state.Player2.Battlefield.Cards.Should().NotContain(c => c.Id == blocker.Id);
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Id == attacker.Id);
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Id == blocker.Id);
    }

    [Fact]
    public async Task SummoningSickness_PreventsAttacking()
    {
        var (engine, state, p1Handler, _) = CreateSetup();
        await engine.StartGameAsync();

        // Creature entered this turn (has summoning sickness)
        var creature = new GameCard { Name = "Bear", TypeLine = "Creature", Power = 2, Toughness = 2, TurnEnteredBattlefield = state.TurnNumber };
        state.Player1.Battlefield.Add(creature);

        // Try to declare it as attacker — engine should filter it out
        p1Handler.EnqueueAttackers(new List<Guid> { creature.Id });

        await engine.RunCombatAsync(CancellationToken.None);

        state.Player2.Life.Should().Be(20, "creature with summoning sickness cannot attack");
    }

    [Fact]
    public async Task TappedCreature_CannotAttack()
    {
        var (engine, state, p1Handler, _) = CreateSetup();
        await engine.StartGameAsync();

        var creature = new GameCard { Name = "Bear", TypeLine = "Creature", Power = 2, Toughness = 2, TurnEnteredBattlefield = 0 };
        creature.IsTapped = true;
        state.Player1.Battlefield.Add(creature);

        p1Handler.EnqueueAttackers(new List<Guid> { creature.Id });

        await engine.RunCombatAsync(CancellationToken.None);

        state.Player2.Life.Should().Be(20, "tapped creature cannot attack");
    }

    [Fact]
    public async Task MultipleAttackers_DealCombinedDamage()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var bear1 = new GameCard { Name = "Bear1", TypeLine = "Creature", Power = 2, Toughness = 2, TurnEnteredBattlefield = 0 };
        var bear2 = new GameCard { Name = "Bear2", TypeLine = "Creature", Power = 3, Toughness = 3, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(bear1);
        state.Player1.Battlefield.Add(bear2);

        p1Handler.EnqueueAttackers(new List<Guid> { bear1.Id, bear2.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid>());

        await engine.RunCombatAsync(CancellationToken.None);

        state.Player2.Life.Should().Be(15, "2 + 3 = 5 damage");
    }

    [Fact]
    public async Task MultiBlock_AttackerDamageAssignedInOrder()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var attacker = new GameCard { Name = "Dragon", TypeLine = "Creature", Power = 5, Toughness = 5, TurnEnteredBattlefield = 0 };
        var blocker1 = new GameCard { Name = "Bear1", TypeLine = "Creature", Power = 2, Toughness = 2, TurnEnteredBattlefield = 0 };
        var blocker2 = new GameCard { Name = "Bear2", TypeLine = "Creature", Power = 2, Toughness = 3, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(attacker);
        state.Player2.Battlefield.Add(blocker1);
        state.Player2.Battlefield.Add(blocker2);

        p1Handler.EnqueueAttackers(new List<Guid> { attacker.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid>
        {
            { blocker1.Id, attacker.Id },
            { blocker2.Id, attacker.Id }
        });
        // Attacker orders: blocker1 first, blocker2 second
        p1Handler.EnqueueBlockerOrder(new List<Guid> { blocker1.Id, blocker2.Id });

        await engine.RunCombatAsync(CancellationToken.None);

        // 5 power: 2 lethal to blocker1, 3 lethal to blocker2 — both die
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Id == blocker1.Id);
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Id == blocker2.Id);
        // Attacker takes 2+2 = 4 damage, survives (5 toughness)
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == attacker.Id);
        attacker.DamageMarked.Should().Be(4);
    }

    [Fact]
    public async Task EndOfTurn_ClearsDamage()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var attacker = new GameCard { Name = "Bear", TypeLine = "Creature", Power = 2, Toughness = 5, TurnEnteredBattlefield = 0 };
        var blocker = new GameCard { Name = "Wall", TypeLine = "Creature", Power = 1, Toughness = 4, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(attacker);
        state.Player2.Battlefield.Add(blocker);

        p1Handler.EnqueueAttackers(new List<Guid> { attacker.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid> { { blocker.Id, attacker.Id } });

        await engine.RunCombatAsync(CancellationToken.None);

        attacker.DamageMarked.Should().Be(1, "took 1 damage from wall");
        blocker.DamageMarked.Should().Be(2, "took 2 damage from bear");

        // Simulate end of turn cleanup
        engine.ClearDamage();

        attacker.DamageMarked.Should().Be(0);
        blocker.DamageMarked.Should().Be(0);
    }

    [Fact]
    public async Task CombatStep_ProgresessCorrectly()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var creature = new GameCard { Name = "Bear", TypeLine = "Creature", Power = 2, Toughness = 2, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(creature);

        p1Handler.EnqueueAttackers(new List<Guid> { creature.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid>());

        await engine.RunCombatAsync(CancellationToken.None);

        state.CombatStep.Should().Be(CombatStep.None, "combat should be cleaned up after resolution");
        state.Combat.Should().BeNull("combat state should be cleared after resolution");
    }

    [Fact]
    public async Task NonCreatureCard_CannotAttack()
    {
        var (engine, state, p1Handler, _) = CreateSetup();
        await engine.StartGameAsync();

        var enchantment = new GameCard { Name = "Pacifism", TypeLine = "Enchantment — Aura", CardTypes = CardType.Enchantment, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(enchantment);

        p1Handler.EnqueueAttackers(new List<Guid> { enchantment.Id });

        await engine.RunCombatAsync(CancellationToken.None);

        state.Player2.Life.Should().Be(20, "non-creature cannot attack");
    }
}
```

**Step 2: Run test to verify it fails**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "CombatEngineTests" -v minimal
```

Expected: FAIL — `RunCombatAsync` does not exist.

**Step 3: Write the implementation**

Add to `GameEngine.cs`:

```csharp
public async Task RunCombatAsync(CancellationToken ct)
{
    var attacker = _state.ActivePlayer;
    var defender = _state.GetOpponent(attacker);

    // Begin Combat
    _state.CombatStep = CombatStep.BeginCombat;
    _state.Combat = new CombatState(attacker.Id, defender.Id);
    _state.Log("Beginning of combat.");

    // Declare Attackers
    _state.CombatStep = CombatStep.DeclareAttackers;

    var eligibleAttackers = attacker.Battlefield.Cards
        .Where(c => c.IsCreature && !c.IsTapped && !c.HasSummoningSickness(_state.TurnNumber))
        .ToList();

    if (eligibleAttackers.Count == 0)
    {
        _state.Log("No eligible attackers.");
        _state.CombatStep = CombatStep.None;
        _state.Combat = null;
        return;
    }

    var chosenAttackerIds = await attacker.DecisionHandler.ChooseAttackers(eligibleAttackers, ct);

    // Filter to only valid attackers
    var validAttackerIds = chosenAttackerIds
        .Where(id => eligibleAttackers.Any(c => c.Id == id))
        .ToList();

    if (validAttackerIds.Count == 0)
    {
        _state.Log("No attackers declared.");
        _state.CombatStep = CombatStep.None;
        _state.Combat = null;
        return;
    }

    // Tap attackers and register them
    foreach (var attackerId in validAttackerIds)
    {
        var card = attacker.Battlefield.Cards.First(c => c.Id == attackerId);
        card.IsTapped = true;
        _state.Combat.DeclareAttacker(attackerId);
        _state.Log($"{attacker.Name} attacks with {card.Name} ({card.Power}/{card.Toughness}).");
    }

    // Declare Blockers
    _state.CombatStep = CombatStep.DeclareBlockers;

    var attackerCards = validAttackerIds
        .Select(id => attacker.Battlefield.Cards.First(c => c.Id == id))
        .ToList();

    var eligibleBlockers = defender.Battlefield.Cards
        .Where(c => c.IsCreature && !c.IsTapped)
        .ToList();

    if (eligibleBlockers.Count > 0)
    {
        var blockerAssignments = await defender.DecisionHandler.ChooseBlockers(eligibleBlockers, attackerCards, ct);

        // Validate and register blocker assignments
        foreach (var (blockerId, attackerCardId) in blockerAssignments)
        {
            if (eligibleBlockers.Any(c => c.Id == blockerId) && validAttackerIds.Contains(attackerCardId))
            {
                _state.Combat.DeclareBlocker(blockerId, attackerCardId);
                var blockerCard = defender.Battlefield.Cards.First(c => c.Id == blockerId);
                var attackerCard = attacker.Battlefield.Cards.First(c => c.Id == attackerCardId);
                _state.Log($"{defender.Name} blocks {attackerCard.Name} with {blockerCard.Name}.");
            }
        }
    }

    // Order blockers for multi-block scenarios
    foreach (var attackerId in validAttackerIds)
    {
        var blockers = _state.Combat.GetBlockers(attackerId);
        if (blockers.Count > 1)
        {
            var blockerCards = blockers
                .Select(id => defender.Battlefield.Cards.First(c => c.Id == id))
                .ToList();

            var orderedIds = await attacker.DecisionHandler.OrderBlockers(attackerId, blockerCards, ct);
            _state.Combat.SetBlockerOrder(attackerId, orderedIds.ToList());
        }
    }

    // Combat Damage
    _state.CombatStep = CombatStep.CombatDamage;
    ResolveCombatDamage(attacker, defender);

    // Process deaths (state-based actions)
    ProcessCombatDeaths(attacker);
    ProcessCombatDeaths(defender);

    // End Combat
    _state.CombatStep = CombatStep.EndCombat;
    _state.Log("End of combat.");

    _state.CombatStep = CombatStep.None;
    _state.Combat = null;
}

private void ResolveCombatDamage(Player attacker, Player defender)
{
    foreach (var attackerId in _state.Combat!.Attackers)
    {
        var attackerCard = attacker.Battlefield.Cards.FirstOrDefault(c => c.Id == attackerId);
        if (attackerCard == null) continue;

        if (!_state.Combat.IsBlocked(attackerId))
        {
            // Unblocked: deal damage to defending player
            var damage = attackerCard.Power ?? 0;
            if (damage > 0)
            {
                defender.AdjustLife(-damage);
                _state.Log($"{attackerCard.Name} deals {damage} damage to {defender.Name}. ({defender.Life} life)");
            }
        }
        else
        {
            // Blocked: deal damage to blockers in order, receive damage from all blockers
            var blockerOrder = _state.Combat.GetBlockerOrder(attackerId);
            var remainingDamage = attackerCard.Power ?? 0;

            foreach (var blockerId in blockerOrder)
            {
                var blockerCard = defender.Battlefield.Cards.FirstOrDefault(c => c.Id == blockerId);
                if (blockerCard == null || remainingDamage <= 0) continue;

                // Assign lethal damage to this blocker, then move on
                var lethal = (blockerCard.Toughness ?? 0) - blockerCard.DamageMarked;
                var assigned = Math.Min(remainingDamage, Math.Max(lethal, 0));
                if (assigned == 0 && remainingDamage > 0)
                    assigned = Math.Min(remainingDamage, 1); // Assign at least 1 if we have remaining damage
                blockerCard.DamageMarked += assigned;
                remainingDamage -= assigned;
                _state.Log($"{attackerCard.Name} deals {assigned} damage to {blockerCard.Name}.");
            }

            // All blockers deal damage to attacker simultaneously
            foreach (var blockerId in blockerOrder)
            {
                var blockerCard = defender.Battlefield.Cards.FirstOrDefault(c => c.Id == blockerId);
                if (blockerCard == null) continue;

                var blockerDamage = blockerCard.Power ?? 0;
                if (blockerDamage > 0)
                {
                    attackerCard.DamageMarked += blockerDamage;
                    _state.Log($"{blockerCard.Name} deals {blockerDamage} damage to {attackerCard.Name}.");
                }
            }
        }
    }
}

private void ProcessCombatDeaths(Player player)
{
    var dead = player.Battlefield.Cards
        .Where(c => c.IsCreature && c.Toughness.HasValue && c.DamageMarked >= c.Toughness.Value)
        .ToList();

    foreach (var card in dead)
    {
        player.Battlefield.RemoveById(card.Id);
        player.Graveyard.Add(card);
        card.DamageMarked = 0;
        _state.Log($"{card.Name} dies.");
    }
}

public void ClearDamage()
{
    foreach (var card in _state.Player1.Battlefield.Cards)
        card.DamageMarked = 0;
    foreach (var card in _state.Player2.Battlefield.Cards)
        card.DamageMarked = 0;
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "CombatEngineTests" -v minimal
```

Expected: All pass.

**Step 5: Run all engine tests for regressions**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ -v minimal
```

Expected: All pass.

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/CombatEngineTests.cs
git commit -m "feat(engine): implement full combat system with attackers, blockers, multi-block ordering, damage"
```

---

### Task 13: Integrate Combat into Turn Loop

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs`
- Create: `tests/MtgDecker.Engine.Tests/CombatIntegrationTests.cs`

**Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class CombatIntegrationTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler p1Handler, TestDecisionHandler p2Handler) CreateGame()
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
    public async Task FullTurn_CombatPhaseRunsDuringTurn()
    {
        var (engine, state, p1Handler, p2Handler) = CreateGame();
        await engine.StartGameAsync();
        state.IsFirstTurn = true;

        // Place a creature that was already there (no summoning sickness)
        var creature = new GameCard { Name = "Bear", TypeLine = "Creature", Power = 3, Toughness = 3, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(creature);

        // Program: attack with creature, no blockers
        p1Handler.EnqueueAttackers(new List<Guid> { creature.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid>());

        await engine.RunTurnAsync();

        state.Player2.Life.Should().Be(17, "3/3 creature should have dealt 3 damage during combat");
    }

    [Fact]
    public async Task DamageClears_AtEndOfTurn()
    {
        var (engine, state, p1Handler, p2Handler) = CreateGame();
        await engine.StartGameAsync();
        state.IsFirstTurn = true;

        var attacker = new GameCard { Name = "Bear", TypeLine = "Creature", Power = 2, Toughness = 5, TurnEnteredBattlefield = 0 };
        var blocker = new GameCard { Name = "Wall", TypeLine = "Creature", Power = 1, Toughness = 5, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(attacker);
        state.Player2.Battlefield.Add(blocker);

        p1Handler.EnqueueAttackers(new List<Guid> { attacker.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid> { { blocker.Id, attacker.Id } });

        await engine.RunTurnAsync();

        attacker.DamageMarked.Should().Be(0, "damage clears at end of turn");
        blocker.DamageMarked.Should().Be(0, "damage clears at end of turn");
    }

    [Fact]
    public async Task CombatKill_ReducesLifeToZero_EndsGame()
    {
        var (engine, state, p1Handler, p2Handler) = CreateGame();
        await engine.StartGameAsync();
        state.IsFirstTurn = true;

        state.Player2.AdjustLife(-18); // Reduce to 2 life

        var creature = new GameCard { Name = "Bear", TypeLine = "Creature", Power = 3, Toughness = 3, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(creature);

        p1Handler.EnqueueAttackers(new List<Guid> { creature.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid>());

        await engine.RunTurnAsync();

        state.Player2.Life.Should().BeLessThanOrEqualTo(0);
    }
}
```

**Step 2: Run test to verify it fails**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "CombatIntegrationTests" -v minimal
```

Expected: FAIL — combat is not called from `RunTurnAsync`.

**Step 3: Modify RunTurnAsync to call RunCombatAsync**

In `GameEngine.RunTurnAsync`, the Combat phase currently just grants priority. Replace the generic phase handling for Combat with a call to `RunCombatAsync`:

```csharp
public async Task RunTurnAsync(CancellationToken ct = default)
{
    _turnStateMachine.Reset();
    _state.ActivePlayer.LandsPlayedThisTurn = 0;
    _state.Log($"Turn {_state.TurnNumber}: {_state.ActivePlayer.Name}'s turn.");

    do
    {
        var phase = _turnStateMachine.CurrentPhase;
        _state.CurrentPhase = phase.Phase;
        _state.Log($"Phase: {phase.Phase}");

        if (phase.HasTurnBasedAction)
        {
            bool skipDraw = phase.Phase == Phase.Draw && _state.IsFirstTurn;
            if (!skipDraw)
                ExecuteTurnBasedAction(phase.Phase);
        }

        if (phase.Phase == Phase.Combat)
        {
            await RunCombatAsync(ct);
        }
        else if (phase.GrantsPriority)
        {
            await RunPriorityAsync(ct);
        }

        _state.Player1.ManaPool.Clear();
        _state.Player2.ManaPool.Clear();

    } while (_turnStateMachine.AdvancePhase() != null);

    // Clear damage at end of turn
    ClearDamage();

    _state.IsFirstTurn = false;
    _state.TurnNumber++;
    _state.ActivePlayer = _state.GetOpponent(_state.ActivePlayer);
}
```

Note: The Combat phase no longer calls `RunPriorityAsync` separately — the `RunCombatAsync` method handles the full combat sequence. If you want priority windows within combat (between steps), that can be added later.

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "CombatIntegrationTests" -v minimal
```

Expected: All pass.

**Step 5: Run ALL engine tests**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ -v minimal
```

Expected: All pass. Watch for any existing tests that relied on the Combat phase granting priority.

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/CombatIntegrationTests.cs
git commit -m "feat(engine): integrate combat system into turn loop with end-of-turn damage cleanup"
```

---

## Workstream C: Combat UI

### Task 14: Update GameBoard Turn Bar for Combat Steps

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor`

**Step 1: Add CombatStep display**

In the turn bar section where the current phase is displayed, add combat step information:

```razor
@if (State.CurrentPhase == Phase.Combat && State.CombatStep != CombatStep.None)
{
    <MudChip T="string" Color="Color.Error" Size="Size.Small">@FormatCombatStep(State.CombatStep)</MudChip>
}
```

Add the helper method:
```csharp
private string FormatCombatStep(CombatStep step) => step switch
{
    CombatStep.BeginCombat => "Begin Combat",
    CombatStep.DeclareAttackers => "Declare Attackers",
    CombatStep.DeclareBlockers => "Declare Blockers",
    CombatStep.CombatDamage => "Combat Damage",
    CombatStep.EndCombat => "End Combat",
    _ => ""
};
```

**Step 2: Pass combat-related handler properties down to PlayerZone**

Add new parameters to GameBoard's PlayerZone invocations:

```razor
<PlayerZone ...existing params...
    IsWaitingForAttackers="@(isLocal && Handler?.IsWaitingForAttackers == true)"
    EligibleAttackers="@(isLocal ? Handler?.EligibleAttackers : null)"
    IsWaitingForBlockers="@(isLocal && Handler?.IsWaitingForBlockers == true)"
    EligibleBlockers="@(isLocal ? Handler?.EligibleBlockers : null)"
    CurrentAttackers="@(isLocal ? Handler?.CurrentAttackers : null)"
    IsWaitingForBlockerOrder="@(isLocal && Handler?.IsWaitingForBlockerOrder == true)"
    OrderingAttackerId="@(Handler?.OrderingAttackerId)"
    BlockersToOrder="@(isLocal ? Handler?.BlockersToOrder : null)"
    OnAttackersChosen="@OnAttackersChosen"
    OnBlockersChosen="@OnBlockersChosen"
    OnBlockerOrderChosen="@OnBlockerOrderChosen"
    CombatState="@State.Combat" />
```

Add the event callbacks:
```csharp
[Parameter] public EventCallback<IReadOnlyList<Guid>> OnAttackersChosen { get; set; }
[Parameter] public EventCallback<Dictionary<Guid, Guid>> OnBlockersChosen { get; set; }
[Parameter] public EventCallback<IReadOnlyList<Guid>> OnBlockerOrderChosen { get; set; }
```

**Step 3: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor
git commit -m "feat(web): display combat step in turn bar and pass combat params to PlayerZone"
```

---

### Task 15: Add Attacker Declaration UI to PlayerZone

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor`
- Modify: `src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor.css`

**Step 1: Add parameters**

```csharp
[Parameter] public bool IsWaitingForAttackers { get; set; }
[Parameter] public IReadOnlyList<GameCard>? EligibleAttackers { get; set; }
[Parameter] public EventCallback<IReadOnlyList<Guid>> OnAttackersChosen { get; set; }
[Parameter] public CombatState? CombatState { get; set; }
```

**Step 2: Add attacker selection state**

```csharp
private HashSet<Guid> _selectedAttackers = new();

private void ToggleAttacker(Guid cardId)
{
    if (_selectedAttackers.Contains(cardId))
        _selectedAttackers.Remove(cardId);
    else
        _selectedAttackers.Add(cardId);
}

private async Task ConfirmAttackers()
{
    await OnAttackersChosen.InvokeAsync(_selectedAttackers.ToList());
    _selectedAttackers.Clear();
}

private async Task SkipAttack()
{
    await OnAttackersChosen.InvokeAsync(Array.Empty<Guid>());
    _selectedAttackers.Clear();
}
```

**Step 3: Add attacker selection UI**

In the battlefield section, when `IsWaitingForAttackers` is true, render eligible creatures with click-to-toggle attack status:

```razor
@if (IsWaitingForAttackers && !IsOpponent)
{
    <div class="combat-prompt">
        <MudText Typo="Typo.subtitle1">Declare Attackers</MudText>
        <MudText Typo="Typo.body2" Class="mb-2">Click creatures to attack with, then confirm.</MudText>
        <MudButtonGroup>
            <MudButton Variant="Variant.Filled" Color="Color.Error" OnClick="ConfirmAttackers"
                       Disabled="@(_selectedAttackers.Count == 0)">
                Attack (@_selectedAttackers.Count)
            </MudButton>
            <MudButton Variant="Variant.Outlined" OnClick="SkipAttack">Skip</MudButton>
        </MudButtonGroup>
    </div>
}
```

In the CardDisplay rendering for battlefield cards, add conditional CSS class:

```razor
<CardDisplay ...
    class="@GetCardCssClass(card)"
    Clickable="@(IsWaitingForAttackers && EligibleAttackers?.Any(c => c.Id == card.Id) == true)"
    OnClick="@(() => ToggleAttacker(card.Id))" />
```

Where `GetCardCssClass` returns `"attacking"` if the card ID is in `_selectedAttackers` or if it's in `CombatState?.Attackers`.

**Step 4: Add CSS classes**

```css
.attacking {
    border: 3px solid #ff4444;
    box-shadow: 0 0 10px rgba(255, 68, 68, 0.5);
}

.combat-prompt {
    padding: 8px 12px;
    background: rgba(255, 68, 68, 0.1);
    border: 1px solid rgba(255, 68, 68, 0.3);
    border-radius: 8px;
    margin-bottom: 8px;
}
```

**Step 5: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor.css
git commit -m "feat(web): add attacker declaration UI with multi-select and confirm/skip"
```

---

### Task 16: Add Blocker Assignment UI to PlayerZone

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor`
- Modify: `src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor.css`

**Step 1: Add parameters**

```csharp
[Parameter] public bool IsWaitingForBlockers { get; set; }
[Parameter] public IReadOnlyList<GameCard>? EligibleBlockers { get; set; }
[Parameter] public IReadOnlyList<GameCard>? CurrentAttackers { get; set; }
[Parameter] public EventCallback<Dictionary<Guid, Guid>> OnBlockersChosen { get; set; }
```

**Step 2: Add blocker selection state**

Two-phase selection: click a blocker, then click an attacker to assign it to.

```csharp
private Dictionary<Guid, Guid> _blockerAssignments = new(); // blockerId -> attackerId
private Guid? _selectedBlocker;

private void SelectBlocker(Guid cardId)
{
    if (_blockerAssignments.ContainsKey(cardId))
    {
        // Unassign this blocker
        _blockerAssignments.Remove(cardId);
        _selectedBlocker = null;
    }
    else
    {
        _selectedBlocker = cardId;
    }
}

private void AssignBlockerToAttacker(Guid attackerId)
{
    if (_selectedBlocker.HasValue)
    {
        _blockerAssignments[_selectedBlocker.Value] = attackerId;
        _selectedBlocker = null;
    }
}

private async Task ConfirmBlockers()
{
    await OnBlockersChosen.InvokeAsync(new Dictionary<Guid, Guid>(_blockerAssignments));
    _blockerAssignments.Clear();
    _selectedBlocker = null;
}

private async Task SkipBlocking()
{
    await OnBlockersChosen.InvokeAsync(new Dictionary<Guid, Guid>());
    _blockerAssignments.Clear();
    _selectedBlocker = null;
}
```

**Step 3: Add blocker selection UI**

```razor
@if (IsWaitingForBlockers && !IsOpponent)
{
    <div class="combat-prompt blocker-prompt">
        <MudText Typo="Typo.subtitle1">Declare Blockers</MudText>
        <MudText Typo="Typo.body2" Class="mb-2">
            @if (_selectedBlocker == null)
            {
                @("Click a creature to block with, then click an attacker to assign it.")
            }
            else
            {
                @("Now click an attacking creature to block.")
            }
        </MudText>

        @if (CurrentAttackers != null)
        {
            <div class="attacker-targets">
                <MudText Typo="Typo.caption">Attackers:</MudText>
                @foreach (var attacker in CurrentAttackers)
                {
                    <MudChip T="string" Color="Color.Error"
                             OnClick="@(() => AssignBlockerToAttacker(attacker.Id))"
                             Class="@(_selectedBlocker != null ? "target-highlight" : "")">
                        @attacker.Name (@attacker.Power/@attacker.Toughness)
                    </MudChip>
                }
            </div>
        }

        <MudButtonGroup Class="mt-2">
            <MudButton Variant="Variant.Filled" Color="Color.Primary" OnClick="ConfirmBlockers">
                Confirm Blocks (@_blockerAssignments.Count)
            </MudButton>
            <MudButton Variant="Variant.Outlined" OnClick="SkipBlocking">No Blocks</MudButton>
        </MudButtonGroup>
    </div>
}
```

**Step 4: Add CSS**

```css
.blocking {
    border: 3px solid #4488ff;
    box-shadow: 0 0 10px rgba(68, 136, 255, 0.5);
}

.blocker-selected {
    border: 3px solid #ffcc00;
    box-shadow: 0 0 10px rgba(255, 204, 0, 0.5);
}

.blocker-prompt .attacker-targets {
    display: flex;
    flex-wrap: wrap;
    gap: 4px;
    margin: 8px 0;
}

.target-highlight {
    cursor: pointer;
    animation: pulse 1s infinite;
}

@keyframes pulse {
    0%, 100% { opacity: 1; }
    50% { opacity: 0.7; }
}
```

**Step 5: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor.css
git commit -m "feat(web): add blocker assignment UI with two-phase click selection"
```

---

### Task 17: Add Blocker Ordering UI

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor`

**Step 1: Add parameters**

```csharp
[Parameter] public bool IsWaitingForBlockerOrder { get; set; }
[Parameter] public Guid? OrderingAttackerId { get; set; }
[Parameter] public IReadOnlyList<GameCard>? BlockersToOrder { get; set; }
[Parameter] public EventCallback<IReadOnlyList<Guid>> OnBlockerOrderChosen { get; set; }
```

**Step 2: Add ordering state**

Simple click-to-order approach: click blockers in the order you want them.

```csharp
private List<Guid> _blockerOrder = new();

private void AddToBlockerOrder(Guid blockerId)
{
    if (!_blockerOrder.Contains(blockerId))
        _blockerOrder.Add(blockerId);
}

private async Task ConfirmBlockerOrder()
{
    // Add any unclicked blockers at the end
    if (BlockersToOrder != null)
    {
        foreach (var b in BlockersToOrder)
        {
            if (!_blockerOrder.Contains(b.Id))
                _blockerOrder.Add(b.Id);
        }
    }
    await OnBlockerOrderChosen.InvokeAsync(_blockerOrder.ToList());
    _blockerOrder.Clear();
}
```

**Step 3: Add ordering UI**

```razor
@if (IsWaitingForBlockerOrder && !IsOpponent && BlockersToOrder != null)
{
    <div class="combat-prompt">
        <MudText Typo="Typo.subtitle1">Order Blockers</MudText>
        <MudText Typo="Typo.body2" Class="mb-2">Click blockers in damage order (first to receive damage first).</MudText>

        <div class="blocker-order-list">
            @foreach (var blocker in BlockersToOrder)
            {
                var orderIndex = _blockerOrder.IndexOf(blocker.Id);
                <MudChip T="string" Color="@(orderIndex >= 0 ? Color.Primary : Color.Default)"
                         OnClick="@(() => AddToBlockerOrder(blocker.Id))">
                    @if (orderIndex >= 0) { @($"#{orderIndex + 1} ") }
                    @blocker.Name (@blocker.Power/@blocker.Toughness)
                </MudChip>
            }
        </div>

        <MudButton Variant="Variant.Filled" Color="Color.Primary" OnClick="ConfirmBlockerOrder" Class="mt-2">
            Confirm Order
        </MudButton>
    </div>
}
```

**Step 4: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor
git commit -m "feat(web): add blocker ordering UI for multi-block scenarios"
```

---

### Task 18: Wire Combat Events in GamePage

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GamePage.razor` (or wherever the root game component is)

**Step 1: Add combat event handlers**

```csharp
private async Task HandleAttackersChosen(IReadOnlyList<Guid> attackerIds)
{
    var handler = _session?.GetHandler(_mySeat);
    if (handler is InteractiveDecisionHandler h)
        h.SubmitAttackers(attackerIds);
}

private async Task HandleBlockersChosen(Dictionary<Guid, Guid> assignments)
{
    var handler = _session?.GetHandler(_mySeat);
    if (handler is InteractiveDecisionHandler h)
        h.SubmitBlockers(assignments);
}

private async Task HandleBlockerOrderChosen(IReadOnlyList<Guid> orderedBlockerIds)
{
    var handler = _session?.GetHandler(_mySeat);
    if (handler is InteractiveDecisionHandler h)
        h.SubmitBlockerOrder(orderedBlockerIds);
}
```

**Step 2: Pass these to GameBoard**

```razor
<GameBoard ...existing params...
    OnAttackersChosen="HandleAttackersChosen"
    OnBlockersChosen="HandleBlockersChosen"
    OnBlockerOrderChosen="HandleBlockerOrderChosen" />
```

**Step 3: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/Game/GamePage.razor
git commit -m "feat(web): wire combat event handlers in GamePage"
```

---

### Task 19: Add P/T Display to Cards & Visual Combat Indicators

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/Game/CardDisplay.razor`
- Modify: `src/MtgDecker.Web/Components/Pages/Game/CardDisplay.razor.css`

**Step 1: Add P/T overlay to card display**

Add parameters:

```csharp
[Parameter] public int? Power { get; set; }
[Parameter] public int? Toughness { get; set; }
[Parameter] public int DamageMarked { get; set; }
[Parameter] public bool IsAttacking { get; set; }
[Parameter] public bool IsBlocking { get; set; }
```

Add P/T badge overlay at bottom-right of card (similar to real MTG cards):

```razor
@if (Power.HasValue && Toughness.HasValue)
{
    <div class="pt-badge @(DamageMarked > 0 ? "damaged" : "")">
        @Power/@(Toughness.Value - DamageMarked)
    </div>
}

@if (IsAttacking)
{
    <div class="combat-indicator attacking-indicator">ATK</div>
}
@if (IsBlocking)
{
    <div class="combat-indicator blocking-indicator">BLK</div>
}
```

**Step 2: Add CSS**

```css
.pt-badge {
    position: absolute;
    bottom: 4px;
    right: 4px;
    background: rgba(0, 0, 0, 0.8);
    color: white;
    padding: 2px 6px;
    border-radius: 4px;
    font-size: 12px;
    font-weight: bold;
}

.pt-badge.damaged {
    color: #ff4444;
}

.combat-indicator {
    position: absolute;
    top: 4px;
    right: 4px;
    padding: 2px 4px;
    border-radius: 4px;
    font-size: 10px;
    font-weight: bold;
}

.attacking-indicator {
    background: rgba(255, 68, 68, 0.9);
    color: white;
}

.blocking-indicator {
    background: rgba(68, 136, 255, 0.9);
    color: white;
}
```

**Step 3: Update PlayerZone to pass P/T and combat state to CardDisplay**

In PlayerZone, when rendering cards, pass the new parameters:

```razor
<CardDisplay ...
    Power="@card.Power"
    Toughness="@card.Toughness"
    DamageMarked="@card.DamageMarked"
    IsAttacking="@(CombatState?.Attackers.Contains(card.Id) == true)"
    IsBlocking="@(IsCardBlocking(card.Id))" />
```

Add helper:
```csharp
private bool IsCardBlocking(Guid cardId) =>
    CombatState?.Attackers.Any(a => CombatState.GetBlockers(a).Contains(cardId)) == true;
```

**Step 4: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/Game/CardDisplay.razor src/MtgDecker.Web/Components/Pages/Game/CardDisplay.razor.css src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor
git commit -m "feat(web): add P/T display, damage markers, and combat indicators to cards"
```

---

### Task 20: Build, Test, and Verify End-to-End

**Step 1: Build the entire solution**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet build src/MtgDecker.Web/
```

Expected: Build succeeds.

**Step 2: Run all engine tests**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ -v minimal
```

Expected: All tests pass.

**Step 3: Run all application tests**

```bash
dotnet test tests/MtgDecker.Application.Tests/ -v minimal
dotnet test tests/MtgDecker.Domain.Tests/ -v minimal
dotnet test tests/MtgDecker.Infrastructure.Tests/ -v minimal
```

Expected: All tests pass (these shouldn't be affected by engine changes).

**Step 4: Manual smoke test**

```bash
dotnet run --project src/MtgDecker.Web/
```

Open browser, create a game, play creatures, advance to combat phase, declare attackers, assign blockers, verify damage resolution.

**Step 5: Final commit**

If any fixes were needed, commit them. Otherwise, the feature is complete.

```bash
git add -A
git commit -m "feat: complete combat system with auto-parse card data, full combat mechanics, and combat UI"
```

---

## Summary

| Task | Description | Files Changed |
|------|-------------|---------------|
| 1 | Add P/T to ScryfallCard DTO | 1 |
| 2 | Add P/T to Card domain entity | 1 |
| 3 | Update ScryfallCardMapper | 1 |
| 4 | EF Migration | 1+ |
| 5 | CardTypeParser | 2 (new) |
| 6 | Enhanced GameCard.Create | 2 |
| 7 | Update GameLobby | 1 |
| 8 | CombatStep enum | 1 (new) |
| 9 | CombatState class | 2 (new) |
| 10 | Combat props on GameState/GameCard | 3 |
| 11 | Decision handler combat methods | 3 |
| 12 | RunCombatAsync implementation | 2 |
| 13 | Turn loop integration | 2 |
| 14 | Turn bar combat step display | 1 |
| 15 | Attacker declaration UI | 2 |
| 16 | Blocker assignment UI | 2 |
| 17 | Blocker ordering UI | 1 |
| 18 | GamePage combat wiring | 1 |
| 19 | P/T display + combat indicators | 3 |
| 20 | Build, test, verify | 0 |

**Total: ~20 tasks, ~30 files touched/created**
