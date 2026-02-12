# Trigger System & ETB Effects Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a generic trigger/effect system for the game engine, starting with ETB triggers for Siege-Gang Commander, Goblin Matron, and Goblin Ringleader.

**Architecture:** Event-driven. Cards declare `Trigger` instances in `CardDefinitions`. When game events occur (ETB), the engine scans permanents for matching triggers and resolves their `IEffect` implementations. Decision handlers extended with card-choice and reveal-cards methods for interactive effects.

**Tech Stack:** MtgDecker.Engine (C# 14, xUnit, FluentAssertions), MudBlazor dialogs for UI.

**Worktree:** `.worktrees/trigger-system` on branch `feature/trigger-system`

**Design doc:** `docs/plans/2026-02-11-trigger-system-design.md`

---

### Task 1: Subtype Parsing

Extend `CardTypeParser` to return subtypes (the part after the em dash in a type line).

**Files:**
- Modify: `src/MtgDecker.Engine/CardTypeParser.cs`
- Test: `tests/MtgDecker.Engine.Tests/CardTypeParserTests.cs`

**Step 1: Write failing tests**

Add these test cases to the existing `CardTypeParserTests.cs` file. The current tests use `[Theory] [InlineData]` testing `CardTypeParser.Parse()` which returns `CardType`. We need a new method `ParseFull()` that returns `ParsedTypeLine`.

```csharp
// Add to existing test file:
public record ParsedTypeLineTestData(string TypeLine, CardType ExpectedType, string[] ExpectedSubtypes);

[Theory]
[MemberData(nameof(ParseFullTestData))]
public void ParseFull_ReturnsTypesAndSubtypes(string typeLine, CardType expectedType, string[] expectedSubtypes)
{
    var result = CardTypeParser.ParseFull(typeLine);

    result.Types.Should().Be(expectedType);
    result.Subtypes.Should().BeEquivalentTo(expectedSubtypes);
}

public static IEnumerable<object[]> ParseFullTestData()
{
    yield return new object[] { "Creature — Goblin", CardType.Creature, new[] { "Goblin" } };
    yield return new object[] { "Legendary Creature — Goblin Warrior", CardType.Creature, new[] { "Goblin", "Warrior" } };
    yield return new object[] { "Enchantment — Aura", CardType.Enchantment, new[] { "Aura" } };
    yield return new object[] { "Basic Land — Mountain", CardType.Land, new[] { "Mountain" } };
    yield return new object[] { "Artifact Creature — Golem", CardType.Creature | CardType.Artifact, new[] { "Golem" } };
    yield return new object[] { "Creature", CardType.Creature, Array.Empty<string>() };
    yield return new object[] { "Instant", CardType.Instant, Array.Empty<string>() };
    yield return new object[] { "", CardType.None, Array.Empty<string>() };
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "ParseFull" --verbosity quiet`
Expected: FAIL — `ParseFull` method doesn't exist.

**Step 3: Implement ParseFull**

In `src/MtgDecker.Engine/CardTypeParser.cs`, add:

```csharp
public record ParsedTypeLine(CardType Types, IReadOnlyList<string> Subtypes);

// Keep existing Parse() method unchanged.

public static ParsedTypeLine ParseFull(string typeLine)
{
    var types = Parse(typeLine);

    if (string.IsNullOrWhiteSpace(typeLine) || !typeLine.Contains('—'))
        return new ParsedTypeLine(types, []);

    var subtypePart = typeLine[(typeLine.IndexOf('—') + 1)..].Trim();
    var subtypes = subtypePart.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    return new ParsedTypeLine(types, subtypes);
}
```

**Step 4: Run tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "CardTypeParser" --verbosity quiet`
Expected: ALL PASS

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/CardTypeParser.cs tests/MtgDecker.Engine.Tests/CardTypeParserTests.cs
git commit -m "feat(engine): add ParseFull to CardTypeParser for subtype extraction"
```

---

### Task 2: GameCard Subtypes and IsToken

Add `Subtypes` and `IsToken` properties to `GameCard`. Wire `Subtypes` into the auto-parse `Create` overload.

**Files:**
- Modify: `src/MtgDecker.Engine/GameCard.cs`
- Test: `tests/MtgDecker.Engine.Tests/GameCardAutoParseTests.cs`

**Step 1: Write failing tests**

Add to existing `GameCardAutoParseTests.cs`:

```csharp
[Fact]
public void Create_AutoParse_ExtractsSubtypes()
{
    var card = GameCard.Create("Test Goblin", "Creature — Goblin Warrior", null, "{R}", "2", "2");
    card.Subtypes.Should().BeEquivalentTo("Goblin", "Warrior");
}

[Fact]
public void Create_AutoParse_NoSubtypes_ReturnsEmpty()
{
    var card = GameCard.Create("Test Spell", "Instant", null, "{R}", null, null);
    card.Subtypes.Should().BeEmpty();
}

[Fact]
public void Create_Registry_GetsSubtypes_WhenDefined()
{
    // After Task 6 updates CardDefinitions, this would get subtypes from registry.
    // For now, verify the property exists and defaults to empty.
    var card = GameCard.Create("Mountain");
    card.Subtypes.Should().BeEmpty();
}

[Fact]
public void IsToken_DefaultsFalse()
{
    var card = GameCard.Create("Test", "Creature", null);
    card.IsToken.Should().BeFalse();
}

[Fact]
public void IsToken_CanBeSetTrue()
{
    var card = new GameCard { Name = "Goblin Token", IsToken = true };
    card.IsToken.Should().BeTrue();
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "Subtypes|IsToken" --verbosity quiet`
Expected: FAIL — properties don't exist.

**Step 3: Implement**

In `src/MtgDecker.Engine/GameCard.cs`:

1. Add properties:
```csharp
public IReadOnlyList<string> Subtypes { get; init; } = [];
public bool IsToken { get; init; }
```

2. In the 6-param `Create` overload, after `card.CardTypes = CardTypeParser.Parse(typeLine);`, replace with:
```csharp
var parsed = CardTypeParser.ParseFull(typeLine);
card.CardTypes = parsed.Types;
card = card with { Subtypes = parsed.Subtypes };
```

Wait — `GameCard` is a `class`, not a `record`, so `with` won't work. Instead, use object initializer pattern. Since `Subtypes` is `init`, we need to set it at construction. Restructure the auto-parse branch:

```csharp
// Auto-parse from raw data
var parsed = CardTypeParser.ParseFull(typeLine);
card = new GameCard
{
    Name = name,
    TypeLine = typeLine,
    ImageUrl = imageUrl,
    CardTypes = parsed.Types,
    Subtypes = parsed.Subtypes
};

if (!string.IsNullOrWhiteSpace(manaCost))
    card.ManaCost = ManaCost.Parse(manaCost);

if (int.TryParse(power, out var p))
    card.Power = p;
if (int.TryParse(toughness, out var t))
    card.Toughness = t;

card.ManaAbility = DetectBasicLandManaAbility(typeLine);

return card;
```

**Step 4: Run all engine tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --verbosity quiet`
Expected: ALL PASS (319 existing + 5 new)

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/GameCard.cs tests/MtgDecker.Engine.Tests/GameCardAutoParseTests.cs
git commit -m "feat(engine): add Subtypes and IsToken properties to GameCard"
```

---

### Task 3: Zone.PeekTop

Add a `PeekTop(int count)` method to `Zone` for looking at the top N cards without removing them.

**Files:**
- Modify: `src/MtgDecker.Engine/Zone.cs`
- Create: `tests/MtgDecker.Engine.Tests/ZoneTests.cs`

**Step 1: Write failing test**

Create `tests/MtgDecker.Engine.Tests/ZoneTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class ZoneTests
{
    [Fact]
    public void PeekTop_ReturnsTopNCards_WithoutRemoving()
    {
        var zone = new Zone(ZoneType.Library);
        var card1 = new GameCard { Name = "Bottom" };
        var card2 = new GameCard { Name = "Middle" };
        var card3 = new GameCard { Name = "Top" };
        zone.Add(card1);
        zone.Add(card2);
        zone.Add(card3);

        var peeked = zone.PeekTop(2);

        peeked.Should().HaveCount(2);
        peeked[0].Name.Should().Be("Top");
        peeked[1].Name.Should().Be("Middle");
        zone.Count.Should().Be(3); // Not removed
    }

    [Fact]
    public void PeekTop_ReturnsAllCards_WhenCountExceedsSize()
    {
        var zone = new Zone(ZoneType.Library);
        zone.Add(new GameCard { Name = "Only" });

        var peeked = zone.PeekTop(5);

        peeked.Should().HaveCount(1);
        peeked[0].Name.Should().Be("Only");
    }

    [Fact]
    public void PeekTop_EmptyZone_ReturnsEmpty()
    {
        var zone = new Zone(ZoneType.Library);

        var peeked = zone.PeekTop(3);

        peeked.Should().BeEmpty();
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "ZoneTests" --verbosity quiet`
Expected: FAIL — `PeekTop` doesn't exist.

**Step 3: Implement**

In `src/MtgDecker.Engine/Zone.cs`, add after `DrawFromTop()`:

```csharp
public IReadOnlyList<GameCard> PeekTop(int count)
{
    if (_cards.Count == 0 || count <= 0) return [];
    var take = Math.Min(count, _cards.Count);
    // Cards are stored bottom-first. Top is at end.
    return _cards.Skip(_cards.Count - take).Reverse().ToList();
}
```

**Step 4: Run tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "ZoneTests" --verbosity quiet`
Expected: ALL PASS

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/Zone.cs tests/MtgDecker.Engine.Tests/ZoneTests.cs
git commit -m "feat(engine): add PeekTop method to Zone"
```

---

### Task 4: Trigger System Core Types

Create `GameEvent`, `TriggerCondition`, `Trigger`, `IEffect`, `EffectContext`, and `TriggeredAbility`.

**Files:**
- Create: `src/MtgDecker.Engine/Enums/GameEvent.cs`
- Create: `src/MtgDecker.Engine/Triggers/TriggerCondition.cs`
- Create: `src/MtgDecker.Engine/Triggers/Trigger.cs`
- Create: `src/MtgDecker.Engine/Triggers/IEffect.cs`
- Create: `src/MtgDecker.Engine/Triggers/EffectContext.cs`
- Create: `src/MtgDecker.Engine/Triggers/TriggeredAbility.cs`

**Step 1: Create all types**

`src/MtgDecker.Engine/Enums/GameEvent.cs`:
```csharp
namespace MtgDecker.Engine.Enums;

public enum GameEvent
{
    EnterBattlefield,
    LeavesBattlefield,
    Dies,
    SpellCast,
    CombatDamageDealt,
    DrawCard,
    Upkeep,
}
```

`src/MtgDecker.Engine/Triggers/TriggerCondition.cs`:
```csharp
namespace MtgDecker.Engine.Triggers;

public enum TriggerCondition
{
    Self,
    AnyCreatureDies,
    ControllerCasts,
}
```

`src/MtgDecker.Engine/Triggers/Trigger.cs`:
```csharp
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers;

public record Trigger(GameEvent Event, TriggerCondition Condition, IEffect Effect);
```

`src/MtgDecker.Engine/Triggers/IEffect.cs`:
```csharp
namespace MtgDecker.Engine.Triggers;

public interface IEffect
{
    Task Execute(EffectContext context, CancellationToken ct = default);
}
```

`src/MtgDecker.Engine/Triggers/EffectContext.cs`:
```csharp
namespace MtgDecker.Engine.Triggers;

public record EffectContext(GameState State, Player Controller, GameCard Source, IPlayerDecisionHandler DecisionHandler);
```

`src/MtgDecker.Engine/Triggers/TriggeredAbility.cs`:
```csharp
namespace MtgDecker.Engine.Triggers;

public class TriggeredAbility(GameCard source, Player controller, Trigger trigger)
{
    public GameCard Source { get; } = source;
    public Player Controller { get; } = controller;
    public Trigger Trigger { get; } = trigger;

    public async Task ResolveAsync(GameState state, CancellationToken ct = default)
    {
        var context = new EffectContext(state, Controller, Source, Controller.DecisionHandler);
        await Trigger.Effect.Execute(context, ct);
    }
}
```

**Step 2: Verify it builds**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet build src/MtgDecker.Engine/ --verbosity quiet`
Expected: Build succeeded.

**Step 3: Commit**

```bash
git add src/MtgDecker.Engine/Enums/GameEvent.cs src/MtgDecker.Engine/Triggers/
git commit -m "feat(engine): add trigger system core types (GameEvent, Trigger, IEffect, TriggeredAbility)"
```

---

### Task 5: Extend CardDefinition with Subtypes and Triggers

Add `Subtypes` and `Triggers` to the `CardDefinition` record, and wire them into `GameCard.Create`.

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinition.cs`
- Modify: `src/MtgDecker.Engine/GameCard.cs`
- Test: `tests/MtgDecker.Engine.Tests/GameCardAutoParseTests.cs`

**Step 1: Write failing test**

Add to `GameCardAutoParseTests.cs`:

```csharp
[Fact]
public void Create_Registry_GetsSubtypesFromDefinition()
{
    // Goblin Matron should have Subtypes = ["Goblin"] after CardDefinitions is updated (Task 6).
    // For now, just verify the mechanism works: if CardDefinition has Subtypes, they flow to GameCard.
    // This test will pass once Task 6 updates CardDefinitions.
    var card = GameCard.Create("Goblin Lackey");
    // Goblin Lackey will get Subtypes from CardDefinition after Task 6.
    // For now just verify it doesn't crash and returns something.
    card.Subtypes.Should().NotBeNull();
}
```

**Step 2: Implement**

In `src/MtgDecker.Engine/CardDefinition.cs`, convert from positional record to nominal record with optional properties:

```csharp
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Triggers;

namespace MtgDecker.Engine;

public record CardDefinition(
    ManaCost? ManaCost,
    ManaAbility? ManaAbility,
    int? Power,
    int? Toughness,
    CardType CardTypes
)
{
    public IReadOnlyList<string> Subtypes { get; init; } = [];
    public IReadOnlyList<Trigger> Triggers { get; init; } = [];
}
```

In `src/MtgDecker.Engine/GameCard.cs`, add `Triggers` property:
```csharp
public IReadOnlyList<Trigger> Triggers { get; init; } = [];
```

In the registry branch of both `Create` overloads (where `CardDefinitions.TryGet` succeeds), add after setting `card.CardTypes`:
```csharp
card = new GameCard
{
    Id = card.Id,
    Name = name,
    TypeLine = typeLine,
    ImageUrl = imageUrl,
    ManaCost = def.ManaCost,
    ManaAbility = def.ManaAbility,
    Power = def.Power,
    Toughness = def.Toughness,
    CardTypes = def.CardTypes,
    Subtypes = def.Subtypes,
    Triggers = def.Triggers,
};
```

Wait — the 3-param Create doesn't pass typeLine or imageUrl. Let me be more careful. For the 3-param Create:

```csharp
public static GameCard Create(string name, string typeLine = "", string? imageUrl = null)
{
    if (CardDefinitions.TryGet(name, out var def))
    {
        return new GameCard
        {
            Name = name,
            TypeLine = typeLine,
            ImageUrl = imageUrl,
            ManaCost = def.ManaCost,
            ManaAbility = def.ManaAbility,
            Power = def.Power,
            Toughness = def.Toughness,
            CardTypes = def.CardTypes,
            Subtypes = def.Subtypes,
            Triggers = def.Triggers,
        };
    }
    return new GameCard { Name = name, TypeLine = typeLine, ImageUrl = imageUrl };
}
```

For the 6-param Create, the registry branch similarly:

```csharp
if (CardDefinitions.TryGet(name, out var def))
{
    return new GameCard
    {
        Name = name,
        TypeLine = typeLine,
        ImageUrl = imageUrl,
        ManaCost = def.ManaCost,
        ManaAbility = def.ManaAbility,
        Power = def.Power,
        Toughness = def.Toughness,
        CardTypes = def.CardTypes,
        Subtypes = def.Subtypes,
        Triggers = def.Triggers,
    };
}
```

Add `using MtgDecker.Engine.Triggers;` to `GameCard.cs`.

**Step 3: Run all engine tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --verbosity quiet`
Expected: ALL PASS

**Step 4: Commit**

```bash
git add src/MtgDecker.Engine/CardDefinition.cs src/MtgDecker.Engine/GameCard.cs tests/MtgDecker.Engine.Tests/GameCardAutoParseTests.cs
git commit -m "feat(engine): add Subtypes and Triggers to CardDefinition, wire to GameCard.Create"
```

---

### Task 6: CreateTokensEffect

Implement the first effect: creating token creatures on the battlefield.

**Files:**
- Create: `src/MtgDecker.Engine/Triggers/Effects/CreateTokensEffect.cs`
- Create: `tests/MtgDecker.Engine.Tests/Triggers/CreateTokensEffectTests.cs`

**Step 1: Write failing tests**

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests.Triggers;

public class CreateTokensEffectTests
{
    private (GameState state, Player player, TestDecisionHandler handler) CreateSetup()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Player 1", handler);
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        return (state, p1, handler);
    }

    [Fact]
    public async Task Execute_CreatesTokensOnBattlefield()
    {
        var (state, player, handler) = CreateSetup();
        var source = new GameCard { Name = "Siege-Gang Commander" };
        var effect = new CreateTokensEffect("Goblin", 1, 1, CardType.Creature, ["Goblin"], count: 3);
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        player.Battlefield.Count.Should().Be(3);
        player.Battlefield.Cards.Should().AllSatisfy(c =>
        {
            c.Name.Should().Be("Goblin");
            c.Power.Should().Be(1);
            c.Toughness.Should().Be(1);
            c.IsToken.Should().BeTrue();
            c.IsCreature.Should().BeTrue();
            c.Subtypes.Should().Contain("Goblin");
        });
    }

    [Fact]
    public async Task Execute_TokensHaveSummoningSickness()
    {
        var (state, player, handler) = CreateSetup();
        state.TurnNumber = 3;
        var source = new GameCard { Name = "Commander" };
        var effect = new CreateTokensEffect("Goblin", 1, 1, CardType.Creature, ["Goblin"], count: 1);
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        var token = player.Battlefield.Cards[0];
        token.TurnEnteredBattlefield.Should().Be(3);
        token.HasSummoningSickness(3).Should().BeTrue();
    }

    [Fact]
    public async Task Execute_TokensHaveUniqueIds()
    {
        var (state, player, handler) = CreateSetup();
        var source = new GameCard { Name = "Commander" };
        var effect = new CreateTokensEffect("Goblin", 1, 1, CardType.Creature, ["Goblin"], count: 3);
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        var ids = player.Battlefield.Cards.Select(c => c.Id).ToList();
        ids.Should().OnlyHaveUniqueItems();
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "CreateTokensEffect" --verbosity quiet`
Expected: FAIL — class doesn't exist.

**Step 3: Implement**

Create `src/MtgDecker.Engine/Triggers/Effects/CreateTokensEffect.cs`:

```csharp
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class CreateTokensEffect(
    string name, int power, int toughness, CardType cardTypes,
    IReadOnlyList<string> subtypes, int count = 1) : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        for (int i = 0; i < count; i++)
        {
            var token = new GameCard
            {
                Name = name,
                Power = power,
                Toughness = toughness,
                CardTypes = cardTypes,
                Subtypes = subtypes,
                IsToken = true,
                TurnEnteredBattlefield = context.State.TurnNumber,
            };
            context.Controller.Battlefield.Add(token);
            context.State.Log($"{context.Controller.Name} creates a {name} token ({power}/{toughness}).");
        }
        await Task.CompletedTask;
    }
}
```

**Step 4: Run tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "CreateTokensEffect" --verbosity quiet`
Expected: ALL PASS

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/Triggers/Effects/CreateTokensEffect.cs tests/MtgDecker.Engine.Tests/Triggers/CreateTokensEffectTests.cs
git commit -m "feat(engine): add CreateTokensEffect for token creature generation"
```

---

### Task 7: Decision Handler Extensions (ChooseCard + RevealCards)

Add `ChooseCard` and `RevealCards` to the decision handler interface and both implementations.

**Files:**
- Modify: `src/MtgDecker.Engine/IPlayerDecisionHandler.cs`
- Modify: `src/MtgDecker.Engine/InteractiveDecisionHandler.cs`
- Modify: `tests/MtgDecker.Engine.Tests/Helpers/TestDecisionHandler.cs`

**Step 1: Add interface methods**

In `src/MtgDecker.Engine/IPlayerDecisionHandler.cs`, add:

```csharp
Task<Guid?> ChooseCard(IReadOnlyList<GameCard> options, string prompt,
    bool optional = false, CancellationToken ct = default);

Task RevealCards(IReadOnlyList<GameCard> cards, IReadOnlyList<GameCard> kept,
    string prompt, CancellationToken ct = default);
```

**Step 2: Implement in TestDecisionHandler**

In `tests/MtgDecker.Engine.Tests/Helpers/TestDecisionHandler.cs`, add queue + methods:

```csharp
private readonly Queue<Guid?> _cardChoiceQueue = new();
public void EnqueueCardChoice(Guid? cardId) => _cardChoiceQueue.Enqueue(cardId);

public Task<Guid?> ChooseCard(IReadOnlyList<GameCard> options, string prompt,
    bool optional = false, CancellationToken ct = default)
{
    if (_cardChoiceQueue.Count > 0)
        return Task.FromResult(_cardChoiceQueue.Dequeue());
    // Default: choose first if available, null if optional
    return Task.FromResult(options.Count > 0 ? options[0].Id : (Guid?)null);
}

public Task RevealCards(IReadOnlyList<GameCard> cards, IReadOnlyList<GameCard> kept,
    string prompt, CancellationToken ct = default)
{
    // Test handler auto-acknowledges reveals
    return Task.CompletedTask;
}
```

**Step 3: Implement in InteractiveDecisionHandler**

Add TCS fields, waiting properties, context properties, and submit methods following the existing pattern:

```csharp
private TaskCompletionSource<Guid?>? _cardChoiceTcs;
private TaskCompletionSource<bool>? _revealAckTcs;

public bool IsWaitingForCardChoice => _cardChoiceTcs is { Task.IsCompleted: false };
public bool IsWaitingForRevealAck => _revealAckTcs is { Task.IsCompleted: false };
public IReadOnlyList<GameCard>? CardChoiceOptions { get; private set; }
public string? CardChoicePrompt { get; private set; }
public bool CardChoiceOptional { get; private set; }
public IReadOnlyList<GameCard>? RevealedCards { get; private set; }
public IReadOnlyList<GameCard>? KeptCards { get; private set; }
public string? RevealPrompt { get; private set; }

public Task<Guid?> ChooseCard(IReadOnlyList<GameCard> options, string prompt,
    bool optional = false, CancellationToken ct = default)
{
    CardChoiceOptions = options;
    CardChoicePrompt = prompt;
    CardChoiceOptional = optional;
    _cardChoiceTcs = new TaskCompletionSource<Guid?>(TaskCreationOptions.RunContinuationsAsynchronously);
    var registration = ct.Register(() => { CardChoiceOptions = null; CardChoicePrompt = null; _cardChoiceTcs.TrySetCanceled(); });
    _cardChoiceTcs.Task.ContinueWith(_ => registration.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
    OnWaitingForInput?.Invoke();
    return _cardChoiceTcs.Task;
}

public Task RevealCards(IReadOnlyList<GameCard> cards, IReadOnlyList<GameCard> kept,
    string prompt, CancellationToken ct = default)
{
    RevealedCards = cards;
    KeptCards = kept;
    RevealPrompt = prompt;
    _revealAckTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var registration = ct.Register(() => { RevealedCards = null; KeptCards = null; RevealPrompt = null; _revealAckTcs.TrySetCanceled(); });
    _revealAckTcs.Task.ContinueWith(_ => registration.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
    OnWaitingForInput?.Invoke();
    return _revealAckTcs.Task;
}

public void SubmitCardChoice(Guid? cardId)
{
    CardChoiceOptions = null;
    CardChoicePrompt = null;
    _cardChoiceTcs?.TrySetResult(cardId);
}

public void AcknowledgeReveal()
{
    RevealedCards = null;
    KeptCards = null;
    RevealPrompt = null;
    _revealAckTcs?.TrySetResult(true);
}
```

**Step 4: Build and run all tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --verbosity quiet`
Expected: ALL PASS (all existing tests still pass — new methods have defaults)

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/IPlayerDecisionHandler.cs src/MtgDecker.Engine/InteractiveDecisionHandler.cs tests/MtgDecker.Engine.Tests/Helpers/TestDecisionHandler.cs
git commit -m "feat(engine): add ChooseCard and RevealCards to decision handlers"
```

---

### Task 8: SearchLibraryEffect

Implement the tutor effect for Goblin Matron.

**Files:**
- Create: `src/MtgDecker.Engine/Triggers/Effects/SearchLibraryEffect.cs`
- Create: `tests/MtgDecker.Engine.Tests/Triggers/SearchLibraryEffectTests.cs`

**Step 1: Write failing tests**

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests.Triggers;

public class SearchLibraryEffectTests
{
    private (GameState state, Player player, TestDecisionHandler handler) CreateSetup()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Player 1", handler);
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        return (state, p1, handler);
    }

    [Fact]
    public async Task Execute_FindsMatchingCard_AddsToHand()
    {
        var (state, player, handler) = CreateSetup();
        var goblin = new GameCard { Name = "Goblin Piledriver", Subtypes = ["Goblin", "Warrior"] };
        var nonGoblin = new GameCard { Name = "Mountain", CardTypes = CardType.Land };
        player.Library.Add(nonGoblin);
        player.Library.Add(goblin);
        handler.EnqueueCardChoice(goblin.Id);

        var effect = new SearchLibraryEffect("Goblin");
        var source = new GameCard { Name = "Goblin Matron" };
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        player.Hand.Cards.Should().Contain(c => c.Id == goblin.Id);
        player.Library.Cards.Should().NotContain(c => c.Id == goblin.Id);
    }

    [Fact]
    public async Task Execute_ShufflesLibraryAfterSearch()
    {
        var (state, player, handler) = CreateSetup();
        // Add 20 cards so shuffle is detectable
        for (int i = 0; i < 20; i++)
            player.Library.Add(new GameCard { Name = $"Card {i}", Subtypes = i < 5 ? ["Goblin"] : [] });
        handler.EnqueueCardChoice(player.Library.Cards[19].Id); // pick last goblin

        var effect = new SearchLibraryEffect("Goblin");
        var context = new EffectContext(state, player, new GameCard { Name = "Matron" }, handler);

        await effect.Execute(context);

        // Library was shuffled — count should be 19 (one moved to hand)
        player.Library.Count.Should().Be(19);
        player.Hand.Count.Should().Be(1);
    }

    [Fact]
    public async Task Execute_NoMatchingCards_SkipsSearch()
    {
        var (state, player, handler) = CreateSetup();
        player.Library.Add(new GameCard { Name = "Mountain", CardTypes = CardType.Land, Subtypes = ["Mountain"] });

        var effect = new SearchLibraryEffect("Goblin");
        var context = new EffectContext(state, player, new GameCard { Name = "Matron" }, handler);

        await effect.Execute(context);

        player.Hand.Count.Should().Be(0);
    }

    [Fact]
    public async Task Execute_PlayerDeclinesOptional_NoCardAdded()
    {
        var (state, player, handler) = CreateSetup();
        player.Library.Add(new GameCard { Name = "Goblin Piledriver", Subtypes = ["Goblin"] });
        handler.EnqueueCardChoice(null); // Player declines

        var effect = new SearchLibraryEffect("Goblin", optional: true);
        var context = new EffectContext(state, player, new GameCard { Name = "Matron" }, handler);

        await effect.Execute(context);

        player.Hand.Count.Should().Be(0);
        player.Library.Count.Should().Be(1);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "SearchLibraryEffect" --verbosity quiet`
Expected: FAIL — class doesn't exist.

**Step 3: Implement**

Create `src/MtgDecker.Engine/Triggers/Effects/SearchLibraryEffect.cs`:

```csharp
namespace MtgDecker.Engine.Triggers.Effects;

public class SearchLibraryEffect(string subtype, bool optional = true) : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var matches = context.Controller.Library.Cards
            .Where(c => c.Subtypes.Contains(subtype, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            context.State.Log($"{context.Controller.Name} searches library but finds no {subtype}.");
            context.Controller.Library.Shuffle();
            return;
        }

        var chosenId = await context.DecisionHandler.ChooseCard(
            matches, $"Search for a {subtype}", optional, ct);

        if (chosenId.HasValue)
        {
            var chosen = context.Controller.Library.RemoveById(chosenId.Value);
            if (chosen != null)
            {
                context.Controller.Hand.Add(chosen);
                context.State.Log($"{context.Controller.Name} searches library and adds {chosen.Name} to hand.");
            }
        }
        else
        {
            context.State.Log($"{context.Controller.Name} declines to search.");
        }

        context.Controller.Library.Shuffle();
    }
}
```

**Step 4: Run tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "SearchLibraryEffect" --verbosity quiet`
Expected: ALL PASS

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/Triggers/Effects/SearchLibraryEffect.cs tests/MtgDecker.Engine.Tests/Triggers/SearchLibraryEffectTests.cs
git commit -m "feat(engine): add SearchLibraryEffect for tutor abilities"
```

---

### Task 9: RevealAndFilterEffect

Implement the reveal + filter effect for Goblin Ringleader.

**Files:**
- Create: `src/MtgDecker.Engine/Triggers/Effects/RevealAndFilterEffect.cs`
- Create: `tests/MtgDecker.Engine.Tests/Triggers/RevealAndFilterEffectTests.cs`

**Step 1: Write failing tests**

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests.Triggers;

public class RevealAndFilterEffectTests
{
    private (GameState state, Player player, TestDecisionHandler handler) CreateSetup()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Player 1", handler);
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        return (state, p1, handler);
    }

    [Fact]
    public async Task Execute_MatchingCardsGoToHand()
    {
        var (state, player, handler) = CreateSetup();
        // Library is bottom-first. Add bottom first, then top.
        player.Library.Add(new GameCard { Name = "Extra", Subtypes = [] });
        player.Library.Add(new GameCard { Name = "Mountain", CardTypes = CardType.Land, Subtypes = ["Mountain"] });
        player.Library.Add(new GameCard { Name = "Goblin Matron", Subtypes = ["Goblin"] });
        player.Library.Add(new GameCard { Name = "Lightning Bolt", Subtypes = [] });
        player.Library.Add(new GameCard { Name = "Goblin Piledriver", Subtypes = ["Goblin", "Warrior"] });

        var effect = new RevealAndFilterEffect(4, "Goblin");
        var context = new EffectContext(state, player, new GameCard { Name = "Ringleader" }, handler);

        await effect.Execute(context);

        player.Hand.Cards.Should().HaveCount(2);
        player.Hand.Cards.Select(c => c.Name).Should().Contain("Goblin Matron", "Goblin Piledriver");
        player.Library.Count.Should().Be(3); // 1 extra + 2 non-goblin to bottom
    }

    [Fact]
    public async Task Execute_NonMatchingGoToBottomOfLibrary()
    {
        var (state, player, handler) = CreateSetup();
        player.Library.Add(new GameCard { Name = "Bottom Card", Subtypes = [] });
        player.Library.Add(new GameCard { Name = "Non Goblin 1", Subtypes = [] });
        player.Library.Add(new GameCard { Name = "Non Goblin 2", Subtypes = [] });

        var effect = new RevealAndFilterEffect(2, "Goblin");
        var context = new EffectContext(state, player, new GameCard { Name = "Ringleader" }, handler);

        await effect.Execute(context);

        player.Hand.Count.Should().Be(0); // No goblins found
        player.Library.Count.Should().Be(3); // All returned to library
    }

    [Fact]
    public async Task Execute_LessThanNCardsInLibrary_RevealsWhatExists()
    {
        var (state, player, handler) = CreateSetup();
        player.Library.Add(new GameCard { Name = "Goblin Lackey", Subtypes = ["Goblin"] });

        var effect = new RevealAndFilterEffect(4, "Goblin");
        var context = new EffectContext(state, player, new GameCard { Name = "Ringleader" }, handler);

        await effect.Execute(context);

        player.Hand.Cards.Should().HaveCount(1);
        player.Hand.Cards[0].Name.Should().Be("Goblin Lackey");
        player.Library.Count.Should().Be(0);
    }

    [Fact]
    public async Task Execute_EmptyLibrary_DoesNothing()
    {
        var (state, player, handler) = CreateSetup();

        var effect = new RevealAndFilterEffect(4, "Goblin");
        var context = new EffectContext(state, player, new GameCard { Name = "Ringleader" }, handler);

        await effect.Execute(context);

        player.Hand.Count.Should().Be(0);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "RevealAndFilterEffect" --verbosity quiet`
Expected: FAIL — class doesn't exist.

**Step 3: Implement**

Create `src/MtgDecker.Engine/Triggers/Effects/RevealAndFilterEffect.cs`:

```csharp
namespace MtgDecker.Engine.Triggers.Effects;

public class RevealAndFilterEffect(int count, string subtype) : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var revealed = context.Controller.Library.PeekTop(count);
        if (revealed.Count == 0)
        {
            context.State.Log($"{context.Controller.Name} reveals top {count} — library is empty.");
            return;
        }

        // Remove all revealed from library
        foreach (var card in revealed)
            context.Controller.Library.RemoveById(card.Id);

        var matching = revealed.Where(c =>
            c.Subtypes.Contains(subtype, StringComparer.OrdinalIgnoreCase)).ToList();
        var nonMatching = revealed.Where(c =>
            !c.Subtypes.Contains(subtype, StringComparer.OrdinalIgnoreCase)).ToList();

        // Show revealed cards to player
        await context.DecisionHandler.RevealCards(
            revealed.ToList(), matching,
            $"Revealed {revealed.Count} cards — {matching.Count} {subtype}(s) found", ct);

        // Matching → hand
        foreach (var card in matching)
        {
            context.Controller.Hand.Add(card);
            context.State.Log($"{context.Controller.Name} puts {card.Name} into hand.");
        }

        // Non-matching → bottom of library
        foreach (var card in nonMatching)
            context.Controller.Library.AddToBottom(card);

        if (nonMatching.Count > 0)
            context.State.Log($"{context.Controller.Name} puts {nonMatching.Count} card(s) on the bottom of library.");
    }
}
```

**Step 4: Run tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "RevealAndFilterEffect" --verbosity quiet`
Expected: ALL PASS

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/Triggers/Effects/RevealAndFilterEffect.cs tests/MtgDecker.Engine.Tests/Triggers/RevealAndFilterEffectTests.cs
git commit -m "feat(engine): add RevealAndFilterEffect for reveal-and-filter abilities"
```

---

### Task 10: ProcessTriggersAsync in GameEngine

Wire trigger processing into the game engine. When a card enters the battlefield, check for ETB triggers and resolve them.

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs`
- Create: `tests/MtgDecker.Engine.Tests/Triggers/TriggerIntegrationTests.cs`

**Step 1: Write failing tests**

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests.Triggers;

public class TriggerIntegrationTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler p1Handler, TestDecisionHandler p2Handler) CreateSetup()
    {
        var p1Handler = new TestDecisionHandler();
        var p2Handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "Player 1", p1Handler),
            new Player(Guid.NewGuid(), "Player 2", p2Handler));
        var engine = new GameEngine(state);
        return (engine, state, p1Handler, p2Handler);
    }

    [Fact]
    public async Task CastCreatureWithETB_TriggersEffect()
    {
        var (engine, state, p1Handler, _) = CreateSetup();
        await engine.StartGameAsync();

        var commander = new GameCard
        {
            Name = "Siege-Gang Commander",
            CardTypes = CardType.Creature,
            ManaCost = MtgDecker.Engine.Mana.ManaCost.Parse("{3}{R}{R}"),
            Power = 2,
            Toughness = 2,
            Subtypes = ["Goblin"],
            Triggers = [
                new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self,
                    new CreateTokensEffect("Goblin", 1, 1, CardType.Creature, ["Goblin"], count: 3))
            ]
        };
        state.Player1.Hand.Add(commander);

        // Give enough mana
        state.Player1.ManaPool.Add(MtgDecker.Engine.Enums.ManaColor.Red, 5);

        // Cast it
        p1Handler.EnqueueAction(GameAction.PlayCard(state.Player1.Id, commander.Id));
        p1Handler.EnqueueAction(GameAction.Pass(state.Player1.Id));
        p2Handler.EnqueueAction(GameAction.Pass(state.Player2.Id));

        await engine.RunPriorityAsync();

        // Commander + 3 tokens on battlefield
        state.Player1.Battlefield.Cards.Where(c => c.Name == "Siege-Gang Commander").Should().HaveCount(1);
        state.Player1.Battlefield.Cards.Where(c => c.Name == "Goblin" && c.IsToken).Should().HaveCount(3);
    }

    [Fact]
    public async Task CardWithNoTriggers_NoEffectFired()
    {
        var (engine, state, p1Handler, _) = CreateSetup();
        await engine.StartGameAsync();

        var bear = new GameCard
        {
            Name = "Grizzly Bears",
            CardTypes = CardType.Creature,
            ManaCost = MtgDecker.Engine.Mana.ManaCost.Parse("{1}{G}"),
            Power = 2,
            Toughness = 2,
        };
        state.Player1.Hand.Add(bear);
        state.Player1.ManaPool.Add(MtgDecker.Engine.Enums.ManaColor.Green, 2);

        p1Handler.EnqueueAction(GameAction.PlayCard(state.Player1.Id, bear.Id));
        p1Handler.EnqueueAction(GameAction.Pass(state.Player1.Id));
        p2Handler.EnqueueAction(GameAction.Pass(state.Player2.Id));

        await engine.RunPriorityAsync();

        state.Player1.Battlefield.Count.Should().Be(1); // Just the bear, no tokens
    }
}
```

Note: `p2Handler` needs to be stored as a field reference. The test helper `CreateSetup` already returns it. The `RunPriorityAsync` method is the existing public method used in other engine tests.

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "TriggerIntegration" --verbosity quiet`
Expected: FAIL — first test: Commander is on battlefield but no tokens (triggers not wired).

**Step 3: Implement**

In `src/MtgDecker.Engine/GameEngine.cs`:

Add using at top:
```csharp
using MtgDecker.Engine.Triggers;
```

Add the `ProcessTriggersAsync` method:

```csharp
private async Task ProcessTriggersAsync(GameEvent evt, GameCard source, Player controller, CancellationToken ct)
{
    if (source.Triggers.Count == 0) return;

    foreach (var trigger in source.Triggers)
    {
        if (trigger.Event != evt) continue;
        if (trigger.Condition == TriggerCondition.Self)
        {
            var ability = new TriggeredAbility(source, controller, trigger);
            _state.Log($"{source.Name} triggers: {trigger.Effect.GetType().Name.Replace("Effect", "")}");
            await ability.ResolveAsync(_state, ct);
        }
    }
}
```

Call `ProcessTriggersAsync` after each ETB. In the `ExecuteAction` method, there are 3 places a card enters the battlefield:

1. **Land drop** (around line 117): After `_state.Log($"... plays {playCard.Name} (land drop).");`
   Add: `await ProcessTriggersAsync(GameEvent.EnterBattlefield, playCard, player, ct);`

2. **Spell cast to battlefield** (around line 204): After `_state.Log($"... casts {playCard.Name}.");`
   Add: `await ProcessTriggersAsync(GameEvent.EnterBattlefield, playCard, player, ct);`

3. **Sandbox play** (around line 216): After `_state.Log($"... plays {playCard.Name}.");`
   Add: `await ProcessTriggersAsync(GameEvent.EnterBattlefield, playCard, player, ct);`

**Step 4: Run all engine tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --verbosity quiet`
Expected: ALL PASS

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/Triggers/TriggerIntegrationTests.cs
git commit -m "feat(engine): wire ProcessTriggersAsync into GameEngine for ETB triggers"
```

---

### Task 11: Update CardDefinitions with Subtypes and Triggers

Add subtypes to all starter deck cards and triggers to Siege-Gang Commander, Goblin Matron, and Goblin Ringleader.

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Create: `tests/MtgDecker.Engine.Tests/Triggers/CardDefinitionTriggerTests.cs`

**Step 1: Write failing tests**

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests.Triggers;

public class CardDefinitionTriggerTests
{
    [Fact]
    public void SiegeGangCommander_HasCreateTokensTrigger()
    {
        CardDefinitions.TryGet("Siege-Gang Commander", out var def).Should().BeTrue();
        def!.Triggers.Should().HaveCount(1);
        def.Triggers[0].Event.Should().Be(GameEvent.EnterBattlefield);
        def.Triggers[0].Effect.Should().BeOfType<CreateTokensEffect>();
        def.Subtypes.Should().Contain("Goblin");
    }

    [Fact]
    public void GoblinMatron_HasSearchLibraryTrigger()
    {
        CardDefinitions.TryGet("Goblin Matron", out var def).Should().BeTrue();
        def!.Triggers.Should().HaveCount(1);
        def.Triggers[0].Event.Should().Be(GameEvent.EnterBattlefield);
        def.Triggers[0].Effect.Should().BeOfType<SearchLibraryEffect>();
        def.Subtypes.Should().Contain("Goblin");
    }

    [Fact]
    public void GoblinRingleader_HasRevealAndFilterTrigger()
    {
        CardDefinitions.TryGet("Goblin Ringleader", out var def).Should().BeTrue();
        def!.Triggers.Should().HaveCount(1);
        def.Triggers[0].Event.Should().Be(GameEvent.EnterBattlefield);
        def.Triggers[0].Effect.Should().BeOfType<RevealAndFilterEffect>();
        def.Subtypes.Should().Contain("Goblin");
    }

    [Theory]
    [InlineData("Goblin Lackey", new[] { "Goblin" })]
    [InlineData("Goblin Piledriver", new[] { "Goblin" })]
    [InlineData("Goblin Warchief", new[] { "Goblin" })]
    [InlineData("Mogg Fanatic", new[] { "Goblin" })]
    [InlineData("Gempalm Incinerator", new[] { "Goblin" })]
    [InlineData("Goblin King", new[] { "Goblin" })]
    [InlineData("Goblin Pyromancer", new[] { "Goblin" })]
    [InlineData("Goblin Sharpshooter", new[] { "Goblin" })]
    [InlineData("Goblin Tinkerer", new[] { "Goblin" })]
    [InlineData("Skirk Prospector", new[] { "Goblin" })]
    [InlineData("Argothian Enchantress", new[] { "Human", "Druid" })]
    public void StarterDeckCreatures_HaveCorrectSubtypes(string cardName, string[] expectedSubtypes)
    {
        CardDefinitions.TryGet(cardName, out var def).Should().BeTrue();
        def!.Subtypes.Should().BeEquivalentTo(expectedSubtypes);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "CardDefinitionTrigger" --verbosity quiet`
Expected: FAIL — subtypes are empty, triggers don't exist.

**Step 3: Update CardDefinitions**

In `src/MtgDecker.Engine/CardDefinitions.cs`, add usings:
```csharp
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;
```

Update each card entry with `Subtypes` and `Triggers` where applicable. Replace the entire dictionary content with:

```csharp
// === Goblins deck ===
["Goblin Lackey"] = new(ManaCost.Parse("{R}"), null, 1, 1, CardType.Creature) { Subtypes = ["Goblin"] },
["Goblin Matron"] = new(ManaCost.Parse("{2}{R}"), null, 1, 1, CardType.Creature)
{
    Subtypes = ["Goblin"],
    Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new SearchLibraryEffect("Goblin", optional: true))]
},
["Goblin Piledriver"] = new(ManaCost.Parse("{1}{R}"), null, 1, 2, CardType.Creature) { Subtypes = ["Goblin"] },
["Goblin Ringleader"] = new(ManaCost.Parse("{3}{R}"), null, 2, 2, CardType.Creature)
{
    Subtypes = ["Goblin"],
    Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new RevealAndFilterEffect(4, "Goblin"))]
},
["Goblin Warchief"] = new(ManaCost.Parse("{1}{R}{R}"), null, 2, 2, CardType.Creature) { Subtypes = ["Goblin"] },
["Mogg Fanatic"] = new(ManaCost.Parse("{R}"), null, 1, 1, CardType.Creature) { Subtypes = ["Goblin"] },
["Gempalm Incinerator"] = new(ManaCost.Parse("{1}{R}"), null, 2, 1, CardType.Creature) { Subtypes = ["Goblin"] },
["Siege-Gang Commander"] = new(ManaCost.Parse("{3}{R}{R}"), null, 2, 2, CardType.Creature)
{
    Subtypes = ["Goblin"],
    Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new CreateTokensEffect("Goblin", 1, 1, CardType.Creature, ["Goblin"], count: 3))]
},
["Goblin King"] = new(ManaCost.Parse("{1}{R}{R}"), null, 2, 2, CardType.Creature) { Subtypes = ["Goblin"] },
["Goblin Pyromancer"] = new(ManaCost.Parse("{3}{R}"), null, 2, 2, CardType.Creature) { Subtypes = ["Goblin"] },
["Goblin Sharpshooter"] = new(ManaCost.Parse("{2}{R}"), null, 1, 1, CardType.Creature) { Subtypes = ["Goblin"] },
["Goblin Tinkerer"] = new(ManaCost.Parse("{1}{R}"), null, 1, 1, CardType.Creature) { Subtypes = ["Goblin"] },
["Skirk Prospector"] = new(ManaCost.Parse("{R}"), null, 1, 1, CardType.Creature) { Subtypes = ["Goblin"] },
["Naturalize"] = new(ManaCost.Parse("{1}{G}"), null, null, null, CardType.Instant),

// === Goblins lands ===
["Mountain"] = new(null, ManaAbility.Fixed(ManaColor.Red), null, null, CardType.Land),
["Forest"] = new(null, ManaAbility.Fixed(ManaColor.Green), null, null, CardType.Land),
["Karplusan Forest"] = new(null, ManaAbility.Choice(ManaColor.Colorless, ManaColor.Red, ManaColor.Green), null, null, CardType.Land),
["Wooded Foothills"] = new(null, null, null, null, CardType.Land),
["Rishadan Port"] = new(null, null, null, null, CardType.Land),
["Wasteland"] = new(null, null, null, null, CardType.Land),

// === Enchantress deck ===
["Argothian Enchantress"] = new(ManaCost.Parse("{1}{G}"), null, 0, 1, CardType.Creature | CardType.Enchantment) { Subtypes = ["Human", "Druid"] },
["Swords to Plowshares"] = new(ManaCost.Parse("{W}"), null, null, null, CardType.Instant),
["Replenish"] = new(ManaCost.Parse("{3}{W}"), null, null, null, CardType.Sorcery),
["Enchantress's Presence"] = new(ManaCost.Parse("{2}{G}"), null, null, null, CardType.Enchantment),
["Wild Growth"] = new(ManaCost.Parse("{G}"), null, null, null, CardType.Enchantment) { Subtypes = ["Aura"] },
["Exploration"] = new(ManaCost.Parse("{G}"), null, null, null, CardType.Enchantment),
["Mirri's Guile"] = new(ManaCost.Parse("{G}"), null, null, null, CardType.Enchantment),
["Opalescence"] = new(ManaCost.Parse("{2}{W}{W}"), null, null, null, CardType.Enchantment),
["Parallax Wave"] = new(ManaCost.Parse("{2}{W}{W}"), null, null, null, CardType.Enchantment),
["Sterling Grove"] = new(ManaCost.Parse("{G}{W}"), null, null, null, CardType.Enchantment),
["Aura of Silence"] = new(ManaCost.Parse("{1}{W}{W}"), null, null, null, CardType.Enchantment),
["Seal of Cleansing"] = new(ManaCost.Parse("{1}{W}"), null, null, null, CardType.Enchantment),
["Solitary Confinement"] = new(ManaCost.Parse("{2}{W}"), null, null, null, CardType.Enchantment),
["Sylvan Library"] = new(ManaCost.Parse("{1}{G}"), null, null, null, CardType.Enchantment),

// === Enchantress lands ===
["Plains"] = new(null, ManaAbility.Fixed(ManaColor.White), null, null, CardType.Land),
["Brushland"] = new(null, ManaAbility.Choice(ManaColor.Colorless, ManaColor.Green, ManaColor.White), null, null, CardType.Land),
["Windswept Heath"] = new(null, null, null, null, CardType.Land),
["Serra's Sanctum"] = new(null, null, null, null, CardType.Land),
```

**Step 4: Run all engine tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --verbosity quiet`
Expected: ALL PASS

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/CardDefinitions.cs tests/MtgDecker.Engine.Tests/Triggers/CardDefinitionTriggerTests.cs
git commit -m "feat(engine): add subtypes and ETB triggers to all starter deck CardDefinitions"
```

---

### Task 12: Token Cleanup in Combat Deaths

When a token creature dies in combat, it should be removed from the game (not stay in graveyard).

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs`
- Add test to: `tests/MtgDecker.Engine.Tests/CombatEngineTests.cs`

**Step 1: Write failing test**

Add to `CombatEngineTests.cs`:

```csharp
[Fact]
public async Task TokenDeath_RemovedFromGame()
{
    var (engine, state, p1Handler, p2Handler) = CreateSetup();
    await engine.StartGameAsync();

    var token = new GameCard { Name = "Goblin", Power = 1, Toughness = 1, CardTypes = CardType.Creature, IsToken = true, TurnEnteredBattlefield = 0 };
    state.Player1.Battlefield.Add(token);

    var blocker = new GameCard { Name = "Wall", Power = 0, Toughness = 4, CardTypes = CardType.Creature, TurnEnteredBattlefield = 0 };
    state.Player2.Battlefield.Add(blocker);

    // Token attacks, gets blocked
    p1Handler.EnqueueAttackers([token.Id]);
    p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid> { { blocker.Id, token.Id } });

    // Run combat
    p1Handler.EnqueueAction(GameAction.Pass(state.Player1.Id)); // begin combat
    p2Handler.EnqueueAction(GameAction.Pass(state.Player2.Id));
    // ... (combat runs automatically)

    // The token takes lethal damage and should be removed entirely
    state.Player1.Battlefield.Cards.Should().NotContain(c => c.Id == token.Id);
    state.Player1.Graveyard.Cards.Should().NotContain(c => c.Id == token.Id);
}
```

Note: This test may need adjustment based on how combat flow works exactly — the token needs to attack, get blocked by the wall, take 0 damage but have 1 toughness... wait, the wall has 0 power, so the token would survive. Let me fix:

```csharp
[Fact]
public async Task TokenDeath_RemovedFromGame()
{
    var (engine, state, p1Handler, p2Handler) = CreateSetup();
    await engine.StartGameAsync();

    var token = new GameCard { Name = "Goblin", Power = 1, Toughness = 1, CardTypes = CardType.Creature, IsToken = true, TurnEnteredBattlefield = 0 };
    state.Player1.Battlefield.Add(token);

    var blocker = new GameCard { Name = "Bear", Power = 2, Toughness = 2, CardTypes = CardType.Creature, TurnEnteredBattlefield = 0 };
    state.Player2.Battlefield.Add(blocker);

    p1Handler.EnqueueAttackers([token.Id]);
    p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid> { { blocker.Id, token.Id } });

    state.CurrentPhase = Phase.Combat;
    await engine.RunCombatAsync();

    // Token should not be in graveyard — tokens cease to exist
    state.Player1.Graveyard.Cards.Should().NotContain(c => c.Id == token.Id);
    state.Player1.Battlefield.Cards.Should().NotContain(c => c.Id == token.Id);
}
```

**Step 2: Run test to verify it fails**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "TokenDeath" --verbosity quiet`
Expected: FAIL — token is in graveyard (currently `ProcessCombatDeaths` moves dead creatures to graveyard without token cleanup).

**Step 3: Implement**

In `src/MtgDecker.Engine/GameEngine.cs`, in the `ProcessCombatDeaths` method, after moving a creature to graveyard, check if it's a token and remove it:

Find the line where dead creatures are moved to graveyard (inside `ProcessCombatDeaths`). After `player.Graveyard.Add(dead);`, add:

```csharp
if (dead.IsToken)
    player.Graveyard.RemoveById(dead.Id);
```

**Step 4: Run tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --verbosity quiet`
Expected: ALL PASS

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/CombatEngineTests.cs
git commit -m "feat(engine): remove tokens from game when they die in combat"
```

---

### Task 13: Card Selection Dialog UI

Create a MudBlazor dialog for card selection (tutor) and card reveal (Ringleader) interactions.

**Files:**
- Create: `src/MtgDecker.Web/Components/Pages/Game/CardSelectionDialog.razor`
- Create: `src/MtgDecker.Web/Components/Pages/Game/CardSelectionDialog.razor.css`

**Step 1: Create the dialog component**

`src/MtgDecker.Web/Components/Pages/Game/CardSelectionDialog.razor`:

```razor
@using MtgDecker.Engine
@namespace MtgDecker.Web.Components.Pages.Game

<MudDialog>
    <TitleContent>
        <MudText Typo="Typo.h6">@Prompt</MudText>
    </TitleContent>
    <DialogContent>
        <div class="card-selection-grid">
            @foreach (var card in Cards)
            {
                var isKept = KeptCards?.Any(k => k.Id == card.Id) == true;
                var isSelected = _selectedCardId == card.Id;
                <div class="card-selection-item @(isKept ? "kept" : "") @(!isKept && IsRevealMode ? "not-kept" : "") @(isSelected ? "selected" : "")"
                     @onclick="() => HandleCardClick(card)">
                    <CardDisplay ImageUrl="@card.ImageUrl"
                                 Name="@card.Name"
                                 Clickable="@(!IsRevealMode)" />
                    @if (isKept)
                    {
                        <MudChip T="string" Size="Size.Small" Color="Color.Success" Class="kept-badge">Kept</MudChip>
                    }
                </div>
            }
        </div>
    </DialogContent>
    <DialogActions>
        @if (IsRevealMode)
        {
            <MudButton Variant="Variant.Filled" Color="Color.Primary"
                       OnClick="() => MudDialog.Close(DialogResult.Ok(default(Guid?)))">OK</MudButton>
        }
        else
        {
            @if (Optional)
            {
                <MudButton Variant="Variant.Outlined"
                           OnClick="() => MudDialog.Close(DialogResult.Ok(default(Guid?)))">Skip</MudButton>
            }
            <MudButton Variant="Variant.Filled" Color="Color.Primary"
                       Disabled="@(_selectedCardId == null)"
                       OnClick="() => MudDialog.Close(DialogResult.Ok(_selectedCardId))">Choose</MudButton>
        }
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;
    [Parameter] public IReadOnlyList<GameCard> Cards { get; set; } = [];
    [Parameter] public IReadOnlyList<GameCard>? KeptCards { get; set; }
    [Parameter] public string Prompt { get; set; } = "";
    [Parameter] public bool Optional { get; set; }
    [Parameter] public bool IsRevealMode { get; set; }

    private Guid? _selectedCardId;

    private void HandleCardClick(GameCard card)
    {
        if (IsRevealMode) return;
        _selectedCardId = _selectedCardId == card.Id ? null : card.Id;
    }
}
```

`src/MtgDecker.Web/Components/Pages/Game/CardSelectionDialog.razor.css`:

```css
.card-selection-grid {
    display: flex;
    flex-wrap: wrap;
    gap: 12px;
    justify-content: center;
    padding: 16px;
    max-height: 400px;
    overflow-y: auto;
}

.card-selection-item {
    position: relative;
    cursor: pointer;
    border: 2px solid transparent;
    border-radius: 8px;
    padding: 4px;
    transition: border-color 0.2s;
}

.card-selection-item.selected {
    border-color: var(--mud-palette-primary);
    box-shadow: 0 0 8px var(--mud-palette-primary);
}

.card-selection-item.kept {
    border-color: var(--mud-palette-success);
}

.card-selection-item.not-kept {
    opacity: 0.5;
}

.kept-badge {
    position: absolute;
    bottom: 4px;
    left: 50%;
    transform: translateX(-50%);
}
```

**Step 2: Verify it builds**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet build src/MtgDecker.Web/ --verbosity quiet`
Expected: Build succeeded.

**Step 3: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/Game/CardSelectionDialog.razor src/MtgDecker.Web/Components/Pages/Game/CardSelectionDialog.razor.css
git commit -m "feat(web): add CardSelectionDialog for tutor and reveal interactions"
```

---

### Task 14: Wire Trigger UI into GamePage and GameBoard

Connect the card choice and reveal interactions from InteractiveDecisionHandler through to the CardSelectionDialog.

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/GamePage.razor`
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor`

**Step 1: Update GamePage.razor**

Add handler methods and event wiring. In the `HandleWaitingForInput` method, add cases for `IsWaitingForCardChoice` and `IsWaitingForRevealAck`:

```csharp
else if (handler.IsWaitingForCardChoice)
{
    InvokeAsync(StateHasChanged);
}
else if (handler.IsWaitingForRevealAck)
{
    InvokeAsync(StateHasChanged);
}
```

Add event handler methods:

```csharp
private void HandleCardChosen(Guid? cardId)
{
    var handler = _session?.GetHandler(_playerSeat);
    handler?.SubmitCardChoice(cardId);
}

private void HandleRevealAcknowledged()
{
    var handler = _session?.GetHandler(_playerSeat);
    handler?.AcknowledgeReveal();
}
```

Pass new callbacks to GameBoard:
```razor
OnCardChosen="HandleCardChosen"
OnRevealAcknowledged="HandleRevealAcknowledged"
```

**Step 2: Update GameBoard.razor**

Add parameters:
```csharp
[Parameter] public EventCallback<Guid?> OnCardChosen { get; set; }
[Parameter] public EventCallback OnRevealAcknowledged { get; set; }
```

Add computed properties:
```csharp
private bool IsWaitingForCardChoice => Handler?.IsWaitingForCardChoice == true;
private bool IsWaitingForRevealAck => Handler?.IsWaitingForRevealAck == true;
```

Pass to local player's PlayerZone:
```razor
IsWaitingForCardChoice="@IsWaitingForCardChoice"
CardChoiceOptions="@(Handler?.CardChoiceOptions)"
CardChoicePrompt="@(Handler?.CardChoicePrompt)"
CardChoiceOptional="@(Handler?.CardChoiceOptional ?? false)"
OnCardChosen="OnCardChosen"
IsWaitingForRevealAck="@IsWaitingForRevealAck"
RevealedCards="@(Handler?.RevealedCards)"
KeptCards="@(Handler?.KeptCards)"
RevealPrompt="@(Handler?.RevealPrompt)"
OnRevealAcknowledged="OnRevealAcknowledged"
```

**Step 3: Update PlayerZone.razor**

Add parameters:
```csharp
[Parameter] public bool IsWaitingForCardChoice { get; set; }
[Parameter] public IReadOnlyList<GameCard>? CardChoiceOptions { get; set; }
[Parameter] public string? CardChoicePrompt { get; set; }
[Parameter] public bool CardChoiceOptional { get; set; }
[Parameter] public EventCallback<Guid?> OnCardChosen { get; set; }
[Parameter] public bool IsWaitingForRevealAck { get; set; }
[Parameter] public IReadOnlyList<GameCard>? RevealedCards { get; set; }
[Parameter] public IReadOnlyList<GameCard>? KeptCards { get; set; }
[Parameter] public string? RevealPrompt { get; set; }
[Parameter] public EventCallback OnRevealAcknowledged { get; set; }
```

Add dialog handling methods and show the CardSelectionDialog when appropriate. In the razor markup, after the existing combat prompts (before the battlefield), add:

```razor
@if (IsWaitingForCardChoice && CardChoiceOptions != null)
{
    <div class="card-choice-prompt">
        <MudText Typo="Typo.body2" Class="mb-2">@CardChoicePrompt</MudText>
        <div class="card-choice-grid">
            @foreach (var card in CardChoiceOptions)
            {
                var isSelected = _selectedChoiceId == card.Id;
                <div class="card-choice-item @(isSelected ? "selected" : "")"
                     @onclick="() => ToggleCardChoice(card.Id)">
                    <CardDisplay ImageUrl="@card.ImageUrl" Name="@card.Name" Clickable="true" />
                </div>
            }
        </div>
        <div class="card-choice-actions">
            @if (CardChoiceOptional)
            {
                <MudButton Size="Size.Small" Variant="Variant.Outlined" OnClick="() => SubmitCardChoice(null)">Skip</MudButton>
            }
            <MudButton Size="Size.Small" Variant="Variant.Filled" Color="Color.Primary"
                       Disabled="@(_selectedChoiceId == null)" OnClick="() => SubmitCardChoice(_selectedChoiceId)">Choose</MudButton>
        </div>
    </div>
}

@if (IsWaitingForRevealAck && RevealedCards != null)
{
    <div class="reveal-prompt">
        <MudText Typo="Typo.body2" Class="mb-2">@RevealPrompt</MudText>
        <div class="card-choice-grid">
            @foreach (var card in RevealedCards)
            {
                var isKept = KeptCards?.Any(k => k.Id == card.Id) == true;
                <div class="card-choice-item @(isKept ? "kept" : "not-kept")">
                    <CardDisplay ImageUrl="@card.ImageUrl" Name="@card.Name" Clickable="false" />
                    @if (isKept)
                    {
                        <MudChip T="string" Size="Size.Small" Color="Color.Success">To Hand</MudChip>
                    }
                </div>
            }
        </div>
        <MudButton Size="Size.Small" Variant="Variant.Filled" Color="Color.Primary" Class="mt-2"
                   OnClick="AcknowledgeReveal">OK</MudButton>
    </div>
}
```

Add code-behind fields and methods:

```csharp
private Guid? _selectedChoiceId;

private void ToggleCardChoice(Guid cardId)
{
    _selectedChoiceId = _selectedChoiceId == cardId ? null : cardId;
}

private async Task SubmitCardChoice(Guid? cardId)
{
    _selectedChoiceId = null;
    await OnCardChosen.InvokeAsync(cardId);
}

private async Task AcknowledgeReveal()
{
    await OnRevealAcknowledged.InvokeAsync();
}
```

Add CSS to `PlayerZone.razor.css`:

```css
.card-choice-prompt, .reveal-prompt {
    padding: 8px 12px;
    background: rgba(100, 200, 100, 0.1);
    border: 1px solid rgba(100, 200, 100, 0.3);
    border-radius: 8px;
    margin-bottom: 8px;
}

.card-choice-grid {
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
    margin: 8px 0;
}

.card-choice-item {
    position: relative;
    cursor: pointer;
    border: 2px solid transparent;
    border-radius: 8px;
    padding: 4px;
}

.card-choice-item.selected {
    border-color: var(--mud-palette-primary);
}

.card-choice-item.kept {
    border-color: var(--mud-palette-success);
}

.card-choice-item.not-kept {
    opacity: 0.5;
}

.card-choice-actions {
    display: flex;
    gap: 8px;
    justify-content: flex-end;
}
```

**Step 2: Verify it builds**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet build src/MtgDecker.Web/ --verbosity quiet`
Expected: Build succeeded.

**Step 3: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/GamePage.razor src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor.css
git commit -m "feat(web): wire card choice and reveal UI for trigger effects"
```

---

### Task 15: Build, Test, and Verify End-to-End

Run all tests, verify everything builds, and do a final review.

**Files:** None (verification only)

**Step 1: Run all test projects**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --verbosity quiet
dotnet test tests/MtgDecker.Domain.Tests/ --verbosity quiet
dotnet test tests/MtgDecker.Application.Tests/ --verbosity quiet
dotnet test tests/MtgDecker.Infrastructure.Tests/ --verbosity quiet
```

Expected: ALL PASS across all 4 test projects.

**Step 2: Build web project**

```bash
dotnet build src/MtgDecker.Web/ --verbosity quiet
```

Expected: Build succeeded, 0 errors.

**Step 3: Review test count**

Engine tests should have increased by approximately 20-25 tests:
- 8 subtype/token tests (Tasks 1-2)
- 3 Zone.PeekTop tests (Task 3)
- 3 CreateTokensEffect tests (Task 6)
- 4 SearchLibraryEffect tests (Task 8)
- 4 RevealAndFilterEffect tests (Task 9)
- 2 trigger integration tests (Task 10)
- 4+ CardDefinition trigger tests (Task 11)
- 1 token death test (Task 12)
