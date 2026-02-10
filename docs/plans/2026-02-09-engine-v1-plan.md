# MTG Rules Engine v1 Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build the core MTG game engine with turn structure, priority passing, London mulligan, and manual card play — a rules-aware shared tabletop for two players.

**Architecture:** Independent `MtgDecker.Engine` class library with zero dependencies on existing MtgDecker layers. Mutable game state with light logging, all mutations through the GameEngine mediator. Async game loop with `IPlayerDecisionHandler` abstraction for pluggable player input (test harness, UI, AI).

**Tech Stack:** .NET 10, C# 14, xUnit 2.9.3, FluentAssertions 8.8.0, NSubstitute 5.3.0

**Brainstorming Q&A:** `docs/plans/2026-02-09-engine-brainstorm-qa.md`

---

## File Map

```
src/MtgDecker.Engine/
  MtgDecker.Engine.csproj
  Enums/
    Phase.cs
    ActionType.cs
    ZoneType.cs
    MulliganDecision.cs
  GameCard.cs
  Zone.cs
  Player.cs
  GameState.cs
  GameAction.cs
  IPlayerDecisionHandler.cs
  PhaseDefinition.cs
  TurnStateMachine.cs
  GameEngine.cs

tests/MtgDecker.Engine.Tests/
  MtgDecker.Engine.Tests.csproj
  Helpers/
    TestDecisionHandler.cs
    DeckBuilder.cs
  GameCardTests.cs
  ZoneTests.cs
  PlayerTests.cs
  GameStateTests.cs
  GameActionTests.cs
  TestDecisionHandlerTests.cs
  DeckBuilderTests.cs
  TurnStateMachineTests.cs
  GameEngineTurnBasedActionTests.cs
  GameEngineActionExecutionTests.cs
  GameEnginePriorityTests.cs
  GameEngineMulliganTests.cs
  GameEngineGameLoopTests.cs
```

---

## Task 1: Project Scaffolding

**Files:**
- Create: `src/MtgDecker.Engine/MtgDecker.Engine.csproj`
- Create: `tests/MtgDecker.Engine.Tests/MtgDecker.Engine.Tests.csproj`
- Modify: `MtgDecker.slnx`

**Step 1: Create Engine class library**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet new classlib -n MtgDecker.Engine -o src/MtgDecker.Engine --framework net10.0
```

**Step 2: Create Engine test project**

```bash
dotnet new xunit -n MtgDecker.Engine.Tests -o tests/MtgDecker.Engine.Tests --framework net10.0
```

**Step 3: Add projects to solution**

```bash
dotnet sln MtgDecker.slnx add src/MtgDecker.Engine/MtgDecker.Engine.csproj
dotnet sln MtgDecker.slnx add tests/MtgDecker.Engine.Tests/MtgDecker.Engine.Tests.csproj
```

**Step 4: Add project reference and test dependencies**

```bash
dotnet add tests/MtgDecker.Engine.Tests/MtgDecker.Engine.Tests.csproj reference src/MtgDecker.Engine/MtgDecker.Engine.csproj
dotnet add tests/MtgDecker.Engine.Tests/MtgDecker.Engine.Tests.csproj package FluentAssertions --version 8.8.0
dotnet add tests/MtgDecker.Engine.Tests/MtgDecker.Engine.Tests.csproj package NSubstitute --version 5.3.0
dotnet add tests/MtgDecker.Engine.Tests/MtgDecker.Engine.Tests.csproj package coverlet.collector --version 6.0.4
```

**Step 5: Configure InternalsVisibleTo**

Add to `src/MtgDecker.Engine/MtgDecker.Engine.csproj`:
```xml
<ItemGroup>
  <InternalsVisibleTo Include="MtgDecker.Engine.Tests" />
</ItemGroup>
```

**Step 6: Delete template files**

Delete `src/MtgDecker.Engine/Class1.cs` and `tests/MtgDecker.Engine.Tests/UnitTest1.cs`.

**Step 7: Add global using to test project**

Ensure `tests/MtgDecker.Engine.Tests/MtgDecker.Engine.Tests.csproj` has:
```xml
<ItemGroup>
  <Using Include="Xunit" />
</ItemGroup>
```

**Step 8: Verify build**

```bash
dotnet build src/MtgDecker.Engine/ && dotnet build tests/MtgDecker.Engine.Tests/
```
Expected: Build succeeded.

**Step 9: Commit**

```bash
git add src/MtgDecker.Engine/ tests/MtgDecker.Engine.Tests/ MtgDecker.slnx
git commit -m "feat(engine): scaffold MtgDecker.Engine and test projects"
```

---

## Task 2: Enums

**Files:**
- Create: `src/MtgDecker.Engine/Enums/Phase.cs`
- Create: `src/MtgDecker.Engine/Enums/ActionType.cs`
- Create: `src/MtgDecker.Engine/Enums/ZoneType.cs`
- Create: `src/MtgDecker.Engine/Enums/MulliganDecision.cs`

No tests — enums are type definitions.

**Step 1: Create Phase enum**

```csharp
namespace MtgDecker.Engine.Enums;

public enum Phase
{
    Untap,
    Upkeep,
    Draw,
    MainPhase1,
    Combat,
    MainPhase2,
    End
}
```

**Step 2: Create ActionType enum**

```csharp
namespace MtgDecker.Engine.Enums;

public enum ActionType
{
    PassPriority,
    PlayCard,
    TapCard,
    UntapCard,
    MoveCard
}
```

**Step 3: Create ZoneType enum**

```csharp
namespace MtgDecker.Engine.Enums;

public enum ZoneType
{
    Library,
    Hand,
    Battlefield,
    Graveyard
}
```

**Step 4: Create MulliganDecision enum**

```csharp
namespace MtgDecker.Engine.Enums;

public enum MulliganDecision
{
    Keep,
    Mulligan
}
```

**Step 5: Verify build**

```bash
dotnet build src/MtgDecker.Engine/
```

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/Enums/
git commit -m "feat(engine): add Phase, ActionType, ZoneType, MulliganDecision enums"
```

---

## Task 3: GameCard

**Files:**
- Create: `src/MtgDecker.Engine/GameCard.cs`
- Create: `tests/MtgDecker.Engine.Tests/GameCardTests.cs`

**Step 1: Write failing tests**

```csharp
using FluentAssertions;
using MtgDecker.Engine;

namespace MtgDecker.Engine.Tests;

public class GameCardTests
{
    [Fact]
    public void Constructor_GeneratesUniqueId()
    {
        var card1 = new GameCard { Name = "Forest" };
        var card2 = new GameCard { Name = "Forest" };

        card1.Id.Should().NotBe(Guid.Empty);
        card1.Id.Should().NotBe(card2.Id);
    }

    [Fact]
    public void Properties_SetViaInitializer()
    {
        var card = new GameCard
        {
            Name = "Lightning Bolt",
            TypeLine = "Instant",
            ImageUrl = "https://example.com/bolt.jpg"
        };

        card.Name.Should().Be("Lightning Bolt");
        card.TypeLine.Should().Be("Instant");
        card.ImageUrl.Should().Be("https://example.com/bolt.jpg");
    }

    [Fact]
    public void IsTapped_DefaultsFalse()
    {
        var card = new GameCard { Name = "Forest" };
        card.IsTapped.Should().BeFalse();
    }

    [Fact]
    public void IsTapped_CanBeSet()
    {
        var card = new GameCard { Name = "Forest" };
        card.IsTapped = true;
        card.IsTapped.Should().BeTrue();
    }

    [Theory]
    [InlineData("Basic Land — Forest", true)]
    [InlineData("Land — Urza's Tower", true)]
    [InlineData("Creature — Elf Warrior", false)]
    [InlineData("Instant", false)]
    public void IsLand_DetectsLandTypeLine(string typeLine, bool expected)
    {
        var card = new GameCard { Name = "Test", TypeLine = typeLine };
        card.IsLand.Should().Be(expected);
    }

    [Theory]
    [InlineData("Creature — Human Wizard", true)]
    [InlineData("Legendary Creature — Elf", true)]
    [InlineData("Artifact Creature — Golem", true)]
    [InlineData("Instant", false)]
    [InlineData("Basic Land — Forest", false)]
    public void IsCreature_DetectsCreatureTypeLine(string typeLine, bool expected)
    {
        var card = new GameCard { Name = "Test", TypeLine = typeLine };
        card.IsCreature.Should().Be(expected);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~GameCardTests"
```
Expected: FAIL — `GameCard` type doesn't exist.

**Step 3: Implement GameCard**

```csharp
namespace MtgDecker.Engine;

public class GameCard
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
    public string TypeLine { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public bool IsTapped { get; set; }

    public bool IsLand => TypeLine.Contains("Land", StringComparison.OrdinalIgnoreCase);
    public bool IsCreature => TypeLine.Contains("Creature", StringComparison.OrdinalIgnoreCase);
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~GameCardTests"
```
Expected: All pass.

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/GameCard.cs tests/MtgDecker.Engine.Tests/GameCardTests.cs
git commit -m "feat(engine): add GameCard with type detection"
```

---

## Task 4: Zone

**Files:**
- Create: `src/MtgDecker.Engine/Zone.cs`
- Create: `tests/MtgDecker.Engine.Tests/ZoneTests.cs`

**Step 1: Write failing tests**

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class ZoneTests
{
    [Fact]
    public void Constructor_SetsType()
    {
        var zone = new Zone(ZoneType.Hand);
        zone.Type.Should().Be(ZoneType.Hand);
    }

    [Fact]
    public void Constructor_StartsEmpty()
    {
        var zone = new Zone(ZoneType.Hand);
        zone.Count.Should().Be(0);
        zone.Cards.Should().BeEmpty();
    }

    [Fact]
    public void Add_AddsCardToEnd()
    {
        var zone = new Zone(ZoneType.Hand);
        var card = new GameCard { Name = "Forest" };

        zone.Add(card);

        zone.Count.Should().Be(1);
        zone.Cards[0].Should().BeSameAs(card);
    }

    [Fact]
    public void AddToBottom_InsertsAtBeginning()
    {
        var zone = new Zone(ZoneType.Library);
        var first = new GameCard { Name = "Forest" };
        var bottom = new GameCard { Name = "Mountain" };

        zone.Add(first);
        zone.AddToBottom(bottom);

        zone.Cards[0].Should().BeSameAs(bottom);
        zone.Cards[1].Should().BeSameAs(first);
    }

    [Fact]
    public void AddRange_AddsMultipleCards()
    {
        var zone = new Zone(ZoneType.Library);
        var cards = new[] { new GameCard { Name = "A" }, new GameCard { Name = "B" } };

        zone.AddRange(cards);

        zone.Count.Should().Be(2);
    }

    [Fact]
    public void RemoveById_RemovesAndReturnsCard()
    {
        var zone = new Zone(ZoneType.Hand);
        var card = new GameCard { Name = "Forest" };
        zone.Add(card);

        var removed = zone.RemoveById(card.Id);

        removed.Should().BeSameAs(card);
        zone.Count.Should().Be(0);
    }

    [Fact]
    public void RemoveById_ReturnsNull_WhenNotFound()
    {
        var zone = new Zone(ZoneType.Hand);

        var removed = zone.RemoveById(Guid.NewGuid());

        removed.Should().BeNull();
    }

    [Fact]
    public void DrawFromTop_RemovesAndReturnsLastCard()
    {
        var zone = new Zone(ZoneType.Library);
        var bottom = new GameCard { Name = "Bottom" };
        var top = new GameCard { Name = "Top" };
        zone.Add(bottom);
        zone.Add(top);

        var drawn = zone.DrawFromTop();

        drawn.Should().BeSameAs(top);
        zone.Count.Should().Be(1);
        zone.Cards[0].Should().BeSameAs(bottom);
    }

    [Fact]
    public void DrawFromTop_ReturnsNull_WhenEmpty()
    {
        var zone = new Zone(ZoneType.Library);

        var drawn = zone.DrawFromTop();

        drawn.Should().BeNull();
    }

    [Fact]
    public void Contains_ReturnsTrueForExistingCard()
    {
        var zone = new Zone(ZoneType.Hand);
        var card = new GameCard { Name = "Forest" };
        zone.Add(card);

        zone.Contains(card.Id).Should().BeTrue();
    }

    [Fact]
    public void Contains_ReturnsFalseForMissingCard()
    {
        var zone = new Zone(ZoneType.Hand);

        zone.Contains(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void Clear_RemovesAllCards()
    {
        var zone = new Zone(ZoneType.Hand);
        zone.Add(new GameCard { Name = "A" });
        zone.Add(new GameCard { Name = "B" });

        zone.Clear();

        zone.Count.Should().Be(0);
    }

    [Fact]
    public void Shuffle_PreservesAllCards()
    {
        var zone = new Zone(ZoneType.Library);
        var cards = Enumerable.Range(0, 20)
            .Select(i => new GameCard { Name = $"Card{i}" })
            .ToList();
        zone.AddRange(cards);

        zone.Shuffle();

        zone.Count.Should().Be(20);
        foreach (var card in cards)
            zone.Contains(card.Id).Should().BeTrue();
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~ZoneTests"
```
Expected: FAIL — `Zone` type doesn't exist.

**Step 3: Implement Zone**

```csharp
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public class Zone
{
    private readonly List<GameCard> _cards = new();

    public ZoneType Type { get; }
    public IReadOnlyList<GameCard> Cards => _cards.AsReadOnly();
    public int Count => _cards.Count;

    public Zone(ZoneType type) => Type = type;

    public void Add(GameCard card) => _cards.Add(card);

    public void AddToBottom(GameCard card) => _cards.Insert(0, card);

    public void AddRange(IEnumerable<GameCard> cards) => _cards.AddRange(cards);

    public GameCard? RemoveById(Guid cardId)
    {
        var card = _cards.FirstOrDefault(c => c.Id == cardId);
        if (card != null) _cards.Remove(card);
        return card;
    }

    public GameCard? DrawFromTop()
    {
        if (_cards.Count == 0) return null;
        var card = _cards[^1];
        _cards.RemoveAt(_cards.Count - 1);
        return card;
    }

    public void Shuffle()
    {
        var rng = Random.Shared;
        for (int i = _cards.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
        }
    }

    public void Clear() => _cards.Clear();

    public bool Contains(Guid cardId) => _cards.Any(c => c.Id == cardId);
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~ZoneTests"
```
Expected: All pass.

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/Zone.cs tests/MtgDecker.Engine.Tests/ZoneTests.cs
git commit -m "feat(engine): add Zone with add/remove/draw/shuffle operations"
```

---

## Task 5: GameAction + IPlayerDecisionHandler

**Files:**
- Create: `src/MtgDecker.Engine/GameAction.cs`
- Create: `src/MtgDecker.Engine/IPlayerDecisionHandler.cs`
- Create: `tests/MtgDecker.Engine.Tests/GameActionTests.cs`

IPlayerDecisionHandler references GameState (Task 7), so we forward-declare the interface here with a temporary parameter type, then update it in Task 7. **Alternative:** create the interface with the correct signature now since C# compiles all files together — we just can't test it until GameState exists.

**Step 1: Create IPlayerDecisionHandler interface**

```csharp
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public interface IPlayerDecisionHandler
{
    Task<GameAction> GetAction(GameState gameState, Guid playerId, CancellationToken ct = default);
    Task<MulliganDecision> GetMulliganDecision(IReadOnlyList<GameCard> hand, int mulliganCount, CancellationToken ct = default);
    Task<IReadOnlyList<GameCard>> ChooseCardsToBottom(IReadOnlyList<GameCard> hand, int count, CancellationToken ct = default);
}
```

Note: This won't compile until GameState exists (Task 7). That's fine — we create GameAction and GameState together before building.

**Step 2: Write failing tests for GameAction**

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class GameActionTests
{
    private readonly Guid _playerId = Guid.NewGuid();
    private readonly Guid _cardId = Guid.NewGuid();

    [Fact]
    public void Pass_CreatesPassAction()
    {
        var action = GameAction.Pass(_playerId);

        action.Type.Should().Be(ActionType.PassPriority);
        action.PlayerId.Should().Be(_playerId);
        action.CardId.Should().BeNull();
    }

    [Fact]
    public void PlayCard_CreatesPlayAction()
    {
        var action = GameAction.PlayCard(_playerId, _cardId);

        action.Type.Should().Be(ActionType.PlayCard);
        action.PlayerId.Should().Be(_playerId);
        action.CardId.Should().Be(_cardId);
        action.SourceZone.Should().Be(ZoneType.Hand);
        action.DestinationZone.Should().Be(ZoneType.Battlefield);
    }

    [Fact]
    public void TapCard_CreatesTapAction()
    {
        var action = GameAction.TapCard(_playerId, _cardId);

        action.Type.Should().Be(ActionType.TapCard);
        action.PlayerId.Should().Be(_playerId);
        action.CardId.Should().Be(_cardId);
    }

    [Fact]
    public void UntapCard_CreatesUntapAction()
    {
        var action = GameAction.UntapCard(_playerId, _cardId);

        action.Type.Should().Be(ActionType.UntapCard);
        action.PlayerId.Should().Be(_playerId);
        action.CardId.Should().Be(_cardId);
    }

    [Fact]
    public void MoveCard_CreatesMoveAction()
    {
        var action = GameAction.MoveCard(_playerId, _cardId, ZoneType.Battlefield, ZoneType.Graveyard);

        action.Type.Should().Be(ActionType.MoveCard);
        action.PlayerId.Should().Be(_playerId);
        action.CardId.Should().Be(_cardId);
        action.SourceZone.Should().Be(ZoneType.Battlefield);
        action.DestinationZone.Should().Be(ZoneType.Graveyard);
    }
}
```

**Step 3: Implement GameAction**

```csharp
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public class GameAction
{
    public ActionType Type { get; init; }
    public Guid PlayerId { get; init; }
    public Guid? CardId { get; init; }
    public ZoneType? SourceZone { get; init; }
    public ZoneType? DestinationZone { get; init; }

    public static GameAction Pass(Guid playerId) => new()
    {
        Type = ActionType.PassPriority,
        PlayerId = playerId
    };

    public static GameAction PlayCard(Guid playerId, Guid cardId) => new()
    {
        Type = ActionType.PlayCard,
        PlayerId = playerId,
        CardId = cardId,
        SourceZone = ZoneType.Hand,
        DestinationZone = ZoneType.Battlefield
    };

    public static GameAction TapCard(Guid playerId, Guid cardId) => new()
    {
        Type = ActionType.TapCard,
        PlayerId = playerId,
        CardId = cardId
    };

    public static GameAction UntapCard(Guid playerId, Guid cardId) => new()
    {
        Type = ActionType.UntapCard,
        PlayerId = playerId,
        CardId = cardId
    };

    public static GameAction MoveCard(Guid playerId, Guid cardId, ZoneType from, ZoneType to) => new()
    {
        Type = ActionType.MoveCard,
        PlayerId = playerId,
        CardId = cardId,
        SourceZone = from,
        DestinationZone = to
    };
}
```

**Note:** Don't build yet — IPlayerDecisionHandler needs GameState which comes in Task 7. Continue to Tasks 6-7 and build after.

---

## Task 6: Player

**Files:**
- Create: `src/MtgDecker.Engine/Player.cs`
- Create: `tests/MtgDecker.Engine.Tests/PlayerTests.cs`

**Step 1: Write failing tests**

```csharp
using FluentAssertions;
using NSubstitute;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class PlayerTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var id = Guid.NewGuid();
        var handler = Substitute.For<IPlayerDecisionHandler>();

        var player = new Player(id, "Alice", handler);

        player.Id.Should().Be(id);
        player.Name.Should().Be("Alice");
        player.DecisionHandler.Should().BeSameAs(handler);
    }

    [Fact]
    public void Constructor_InitializesEmptyZones()
    {
        var player = new Player(Guid.NewGuid(), "Alice", Substitute.For<IPlayerDecisionHandler>());

        player.Library.Type.Should().Be(ZoneType.Library);
        player.Library.Count.Should().Be(0);
        player.Hand.Type.Should().Be(ZoneType.Hand);
        player.Hand.Count.Should().Be(0);
        player.Battlefield.Type.Should().Be(ZoneType.Battlefield);
        player.Battlefield.Count.Should().Be(0);
        player.Graveyard.Type.Should().Be(ZoneType.Graveyard);
        player.Graveyard.Count.Should().Be(0);
    }

    [Theory]
    [InlineData(ZoneType.Library)]
    [InlineData(ZoneType.Hand)]
    [InlineData(ZoneType.Battlefield)]
    [InlineData(ZoneType.Graveyard)]
    public void GetZone_ReturnsCorrectZone(ZoneType type)
    {
        var player = new Player(Guid.NewGuid(), "Alice", Substitute.For<IPlayerDecisionHandler>());

        var zone = player.GetZone(type);

        zone.Type.Should().Be(type);
    }
}
```

**Step 2: Implement Player**

```csharp
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public class Player
{
    public Guid Id { get; }
    public string Name { get; }
    public IPlayerDecisionHandler DecisionHandler { get; }

    public Zone Library { get; }
    public Zone Hand { get; }
    public Zone Battlefield { get; }
    public Zone Graveyard { get; }

    public Player(Guid id, string name, IPlayerDecisionHandler decisionHandler)
    {
        Id = id;
        Name = name;
        DecisionHandler = decisionHandler;
        Library = new Zone(ZoneType.Library);
        Hand = new Zone(ZoneType.Hand);
        Battlefield = new Zone(ZoneType.Battlefield);
        Graveyard = new Zone(ZoneType.Graveyard);
    }

    public Zone GetZone(ZoneType type) => type switch
    {
        ZoneType.Library => Library,
        ZoneType.Hand => Hand,
        ZoneType.Battlefield => Battlefield,
        ZoneType.Graveyard => Graveyard,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
```

---

## Task 7: GameState

**Files:**
- Create: `src/MtgDecker.Engine/GameState.cs`
- Create: `tests/MtgDecker.Engine.Tests/GameStateTests.cs`

**Step 1: Write failing tests**

```csharp
using FluentAssertions;
using NSubstitute;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class GameStateTests
{
    private readonly Player _player1;
    private readonly Player _player2;

    public GameStateTests()
    {
        _player1 = new Player(Guid.NewGuid(), "Alice", Substitute.For<IPlayerDecisionHandler>());
        _player2 = new Player(Guid.NewGuid(), "Bob", Substitute.For<IPlayerDecisionHandler>());
    }

    [Fact]
    public void Constructor_SetsInitialState()
    {
        var state = new GameState(_player1, _player2);

        state.Player1.Should().BeSameAs(_player1);
        state.Player2.Should().BeSameAs(_player2);
        state.ActivePlayer.Should().BeSameAs(_player1);
        state.PriorityPlayer.Should().BeSameAs(_player1);
        state.CurrentPhase.Should().Be(Phase.Untap);
        state.TurnNumber.Should().Be(1);
        state.IsGameOver.Should().BeFalse();
        state.GameLog.Should().BeEmpty();
    }

    [Fact]
    public void GetOpponent_ReturnsOtherPlayer()
    {
        var state = new GameState(_player1, _player2);

        state.GetOpponent(_player1).Should().BeSameAs(_player2);
        state.GetOpponent(_player2).Should().BeSameAs(_player1);
    }

    [Fact]
    public void Log_AddsMessage()
    {
        var state = new GameState(_player1, _player2);

        state.Log("Test message");

        state.GameLog.Should().ContainSingle().Which.Should().Be("Test message");
    }
}
```

**Step 2: Implement GameState**

```csharp
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public class GameState
{
    public Player Player1 { get; }
    public Player Player2 { get; }
    public Player ActivePlayer { get; set; }
    public Player PriorityPlayer { get; set; }
    public Phase CurrentPhase { get; set; }
    public int TurnNumber { get; set; }
    public bool IsGameOver { get; set; }
    public List<string> GameLog { get; } = new();

    public GameState(Player player1, Player player2)
    {
        Player1 = player1;
        Player2 = player2;
        ActivePlayer = player1;
        PriorityPlayer = player1;
        CurrentPhase = Phase.Untap;
        TurnNumber = 1;
    }

    public Player GetOpponent(Player player) =>
        player == Player1 ? Player2 : Player1;

    public void Log(string message) => GameLog.Add(message);
}
```

**Step 3: Now build and run ALL tests from Tasks 3-7**

```bash
dotnet test tests/MtgDecker.Engine.Tests/
```
Expected: All pass (GameCard, Zone, GameAction, Player, GameState tests).

**Step 4: Commit**

```bash
git add src/MtgDecker.Engine/ tests/MtgDecker.Engine.Tests/
git commit -m "feat(engine): add GameAction, IPlayerDecisionHandler, Player, GameState"
```

---

## Task 8: TestDecisionHandler + DeckBuilder

**Files:**
- Create: `tests/MtgDecker.Engine.Tests/Helpers/TestDecisionHandler.cs`
- Create: `tests/MtgDecker.Engine.Tests/Helpers/DeckBuilder.cs`
- Create: `tests/MtgDecker.Engine.Tests/TestDecisionHandlerTests.cs`
- Create: `tests/MtgDecker.Engine.Tests/DeckBuilderTests.cs`

**Step 1: Write failing tests for TestDecisionHandler**

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class TestDecisionHandlerTests
{
    private readonly TestDecisionHandler _handler = new();
    private readonly Guid _playerId = Guid.NewGuid();

    [Fact]
    public async Task GetAction_ReturnsQueuedAction()
    {
        var expected = GameAction.TapCard(_playerId, Guid.NewGuid());
        _handler.EnqueueAction(expected);

        var result = await _handler.GetAction(null!, _playerId);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetAction_DefaultsToPass_WhenQueueEmpty()
    {
        var result = await _handler.GetAction(null!, _playerId);

        result.Type.Should().Be(ActionType.PassPriority);
        result.PlayerId.Should().Be(_playerId);
    }

    [Fact]
    public async Task GetMulliganDecision_ReturnsQueuedDecision()
    {
        _handler.EnqueueMulligan(MulliganDecision.Mulligan);

        var result = await _handler.GetMulliganDecision(Array.Empty<GameCard>(), 0);

        result.Should().Be(MulliganDecision.Mulligan);
    }

    [Fact]
    public async Task GetMulliganDecision_DefaultsToKeep()
    {
        var result = await _handler.GetMulliganDecision(Array.Empty<GameCard>(), 0);

        result.Should().Be(MulliganDecision.Keep);
    }

    [Fact]
    public async Task ChooseCardsToBottom_ReturnsFirstNCards_ByDefault()
    {
        var hand = new[]
        {
            new GameCard { Name = "A" },
            new GameCard { Name = "B" },
            new GameCard { Name = "C" }
        };

        var result = await _handler.ChooseCardsToBottom(hand, 2);

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("A");
        result[1].Name.Should().Be("B");
    }
}
```

**Step 2: Implement TestDecisionHandler**

```csharp
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests.Helpers;

public class TestDecisionHandler : IPlayerDecisionHandler
{
    private readonly Queue<GameAction> _actions = new();
    private readonly Queue<MulliganDecision> _mulliganDecisions = new();
    private readonly Queue<Func<IReadOnlyList<GameCard>, int, IReadOnlyList<GameCard>>> _bottomChoices = new();

    public void EnqueueAction(GameAction action) => _actions.Enqueue(action);

    public void EnqueueMulligan(MulliganDecision decision) => _mulliganDecisions.Enqueue(decision);

    public void EnqueueBottomChoice(Func<IReadOnlyList<GameCard>, int, IReadOnlyList<GameCard>> chooser) =>
        _bottomChoices.Enqueue(chooser);

    public Task<GameAction> GetAction(GameState gameState, Guid playerId, CancellationToken ct = default)
    {
        if (_actions.Count == 0)
            return Task.FromResult(GameAction.Pass(playerId));
        return Task.FromResult(_actions.Dequeue());
    }

    public Task<MulliganDecision> GetMulliganDecision(IReadOnlyList<GameCard> hand, int mulliganCount, CancellationToken ct = default)
    {
        if (_mulliganDecisions.Count == 0)
            return Task.FromResult(MulliganDecision.Keep);
        return Task.FromResult(_mulliganDecisions.Dequeue());
    }

    public Task<IReadOnlyList<GameCard>> ChooseCardsToBottom(IReadOnlyList<GameCard> hand, int count, CancellationToken ct = default)
    {
        if (_bottomChoices.Count == 0)
            return Task.FromResult<IReadOnlyList<GameCard>>(hand.Take(count).ToList());
        return Task.FromResult(_bottomChoices.Dequeue()(hand, count));
    }
}
```

**Step 3: Write failing tests for DeckBuilder**

```csharp
using FluentAssertions;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class DeckBuilderTests
{
    [Fact]
    public void AddCard_CreatesCardsWithCorrectProperties()
    {
        var deck = new DeckBuilder()
            .AddCard("Grizzly Bears", 4, "Creature — Bear")
            .Build();

        deck.Should().HaveCount(4);
        deck.Should().AllSatisfy(c =>
        {
            c.Name.Should().Be("Grizzly Bears");
            c.TypeLine.Should().Be("Creature — Bear");
        });
    }

    [Fact]
    public void AddCard_EachCardHasUniqueId()
    {
        var deck = new DeckBuilder()
            .AddCard("Forest", 3)
            .Build();

        deck.Select(c => c.Id).Distinct().Should().HaveCount(3);
    }

    [Fact]
    public void AddLand_SetsBasicLandTypeLine()
    {
        var deck = new DeckBuilder()
            .AddLand("Forest", 2)
            .Build();

        deck.Should().HaveCount(2);
        deck.Should().AllSatisfy(c =>
        {
            c.Name.Should().Be("Forest");
            c.IsLand.Should().BeTrue();
        });
    }

    [Fact]
    public void Build_CombinesMultipleAddCalls()
    {
        var deck = new DeckBuilder()
            .AddLand("Forest", 20)
            .AddCard("Grizzly Bears", 40, "Creature — Bear")
            .Build();

        deck.Should().HaveCount(60);
        deck.Count(c => c.IsLand).Should().Be(20);
        deck.Count(c => c.IsCreature).Should().Be(40);
    }
}
```

**Step 4: Implement DeckBuilder**

```csharp
using MtgDecker.Engine;

namespace MtgDecker.Engine.Tests.Helpers;

public class DeckBuilder
{
    private readonly List<GameCard> _cards = new();

    public DeckBuilder AddCard(string name, int count, string typeLine = "Creature")
    {
        for (int i = 0; i < count; i++)
        {
            _cards.Add(new GameCard
            {
                Name = name,
                TypeLine = typeLine
            });
        }
        return this;
    }

    public DeckBuilder AddLand(string name, int count)
    {
        return AddCard(name, count, $"Basic Land — {name}");
    }

    public List<GameCard> Build() => new(_cards);
}
```

**Step 5: Run all tests**

```bash
dotnet test tests/MtgDecker.Engine.Tests/
```
Expected: All pass.

**Step 6: Commit**

```bash
git add tests/MtgDecker.Engine.Tests/
git commit -m "feat(engine): add TestDecisionHandler and DeckBuilder test helpers"
```

---

## Task 9: TurnStateMachine

**Files:**
- Create: `src/MtgDecker.Engine/PhaseDefinition.cs`
- Create: `src/MtgDecker.Engine/TurnStateMachine.cs`
- Create: `tests/MtgDecker.Engine.Tests/TurnStateMachineTests.cs`

**Step 1: Write failing tests**

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class TurnStateMachineTests
{
    [Fact]
    public void PhaseSequence_IsCorrectOrder()
    {
        var expected = new[]
        {
            Phase.Untap, Phase.Upkeep, Phase.Draw,
            Phase.MainPhase1, Phase.Combat,
            Phase.MainPhase2, Phase.End
        };

        var sequence = TurnStateMachine.GetPhaseSequence()
            .Select(p => p.Phase)
            .ToList();

        sequence.Should().Equal(expected);
    }

    [Fact]
    public void CurrentPhase_StartsAtUntap()
    {
        var machine = new TurnStateMachine();
        machine.CurrentPhase.Phase.Should().Be(Phase.Untap);
    }

    [Fact]
    public void AdvancePhase_WalksThroughAllPhases()
    {
        var machine = new TurnStateMachine();
        var phases = new List<Phase> { machine.CurrentPhase.Phase };

        while (machine.AdvancePhase() != null)
            phases.Add(machine.CurrentPhase.Phase);

        phases.Should().Equal(
            Phase.Untap, Phase.Upkeep, Phase.Draw,
            Phase.MainPhase1, Phase.Combat,
            Phase.MainPhase2, Phase.End);
    }

    [Fact]
    public void AdvancePhase_ReturnsNull_WhenTurnEnds()
    {
        var machine = new TurnStateMachine();

        // Advance through all 7 phases (6 advances from index 0)
        for (int i = 0; i < 6; i++)
            machine.AdvancePhase().Should().NotBeNull();

        machine.AdvancePhase().Should().BeNull();
    }

    [Fact]
    public void Reset_GoesBackToUntap()
    {
        var machine = new TurnStateMachine();
        machine.AdvancePhase(); // Upkeep
        machine.AdvancePhase(); // Draw

        machine.Reset();

        machine.CurrentPhase.Phase.Should().Be(Phase.Untap);
    }

    [Fact]
    public void UntapPhase_DoesNotGrantPriority()
    {
        var untap = TurnStateMachine.GetPhaseSequence()
            .First(p => p.Phase == Phase.Untap);

        untap.GrantsPriority.Should().BeFalse();
    }

    [Theory]
    [InlineData(Phase.Upkeep)]
    [InlineData(Phase.Draw)]
    [InlineData(Phase.MainPhase1)]
    [InlineData(Phase.Combat)]
    [InlineData(Phase.MainPhase2)]
    [InlineData(Phase.End)]
    public void NonUntapPhases_GrantPriority(Phase phase)
    {
        var phaseDef = TurnStateMachine.GetPhaseSequence()
            .First(p => p.Phase == phase);

        phaseDef.GrantsPriority.Should().BeTrue();
    }

    [Theory]
    [InlineData(Phase.Untap, true)]
    [InlineData(Phase.Draw, true)]
    [InlineData(Phase.Upkeep, false)]
    [InlineData(Phase.MainPhase1, false)]
    [InlineData(Phase.Combat, false)]
    [InlineData(Phase.MainPhase2, false)]
    [InlineData(Phase.End, false)]
    public void HasTurnBasedAction_CorrectPerPhase(Phase phase, bool expected)
    {
        var phaseDef = TurnStateMachine.GetPhaseSequence()
            .First(p => p.Phase == phase);

        phaseDef.HasTurnBasedAction.Should().Be(expected);
    }
}
```

**Step 2: Implement PhaseDefinition**

```csharp
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public class PhaseDefinition
{
    public Phase Phase { get; init; }
    public bool GrantsPriority { get; init; }
    public bool HasTurnBasedAction { get; init; }
}
```

**Step 3: Implement TurnStateMachine**

```csharp
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public class TurnStateMachine
{
    private static readonly List<PhaseDefinition> _phases = new()
    {
        new() { Phase = Phase.Untap, GrantsPriority = false, HasTurnBasedAction = true },
        new() { Phase = Phase.Upkeep, GrantsPriority = true, HasTurnBasedAction = false },
        new() { Phase = Phase.Draw, GrantsPriority = true, HasTurnBasedAction = true },
        new() { Phase = Phase.MainPhase1, GrantsPriority = true, HasTurnBasedAction = false },
        new() { Phase = Phase.Combat, GrantsPriority = true, HasTurnBasedAction = false },
        new() { Phase = Phase.MainPhase2, GrantsPriority = true, HasTurnBasedAction = false },
        new() { Phase = Phase.End, GrantsPriority = true, HasTurnBasedAction = false },
    };

    private int _currentIndex;

    public PhaseDefinition CurrentPhase => _phases[_currentIndex];

    public PhaseDefinition? AdvancePhase()
    {
        _currentIndex++;
        if (_currentIndex >= _phases.Count)
        {
            _currentIndex = 0;
            return null;
        }
        return _phases[_currentIndex];
    }

    public void Reset() => _currentIndex = 0;

    public static IReadOnlyList<PhaseDefinition> GetPhaseSequence() => _phases.AsReadOnly();
}
```

**Step 4: Run tests**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~TurnStateMachineTests"
```
Expected: All pass.

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/PhaseDefinition.cs src/MtgDecker.Engine/TurnStateMachine.cs tests/MtgDecker.Engine.Tests/TurnStateMachineTests.cs
git commit -m "feat(engine): add TurnStateMachine with phase sequence and metadata"
```

---

## Task 10: GameEngine — Turn-Based Actions

**Files:**
- Create: `src/MtgDecker.Engine/GameEngine.cs`
- Create: `tests/MtgDecker.Engine.Tests/GameEngineTurnBasedActionTests.cs`

The GameEngine is built incrementally across Tasks 10-14. Start with turn-based actions (untap, draw) as `internal` methods.

**Step 1: Write failing tests**

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class GameEngineTurnBasedActionTests
{
    private GameEngine CreateEngine(out GameState state, out TestDecisionHandler p1Handler, out TestDecisionHandler p2Handler)
    {
        p1Handler = new TestDecisionHandler();
        p2Handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Alice", p1Handler);
        var p2 = new Player(Guid.NewGuid(), "Bob", p2Handler);
        state = new GameState(p1, p2);
        return new GameEngine(state);
    }

    [Fact]
    public void ExecuteTurnBasedAction_Untap_UntapsAllPermanents()
    {
        var engine = CreateEngine(out var state, out _, out _);
        var card1 = new GameCard { Name = "Forest", TypeLine = "Basic Land — Forest", IsTapped = true };
        var card2 = new GameCard { Name = "Bear", TypeLine = "Creature — Bear", IsTapped = true };
        var card3 = new GameCard { Name = "Untapped", TypeLine = "Creature", IsTapped = false };
        state.ActivePlayer.Battlefield.Add(card1);
        state.ActivePlayer.Battlefield.Add(card2);
        state.ActivePlayer.Battlefield.Add(card3);

        engine.ExecuteTurnBasedAction(Phase.Untap);

        card1.IsTapped.Should().BeFalse();
        card2.IsTapped.Should().BeFalse();
        card3.IsTapped.Should().BeFalse();
    }

    [Fact]
    public void ExecuteTurnBasedAction_Untap_OnlyAffectsActivePlayer()
    {
        var engine = CreateEngine(out var state, out _, out _);
        var opponentCard = new GameCard { Name = "Forest", TypeLine = "Basic Land", IsTapped = true };
        state.GetOpponent(state.ActivePlayer).Battlefield.Add(opponentCard);

        engine.ExecuteTurnBasedAction(Phase.Untap);

        opponentCard.IsTapped.Should().BeTrue();
    }

    [Fact]
    public void ExecuteTurnBasedAction_Draw_DrawsOneCard()
    {
        var engine = CreateEngine(out var state, out _, out _);
        var deck = new DeckBuilder().AddLand("Forest", 10).Build();
        foreach (var card in deck) state.ActivePlayer.Library.Add(card);
        var topCard = state.ActivePlayer.Library.Cards[^1];

        engine.ExecuteTurnBasedAction(Phase.Draw);

        state.ActivePlayer.Hand.Count.Should().Be(1);
        state.ActivePlayer.Hand.Cards[0].Should().BeSameAs(topCard);
        state.ActivePlayer.Library.Count.Should().Be(9);
    }

    [Fact]
    public void ExecuteTurnBasedAction_Draw_DoesNothing_WhenLibraryEmpty()
    {
        var engine = CreateEngine(out var state, out _, out _);

        engine.ExecuteTurnBasedAction(Phase.Draw);

        state.ActivePlayer.Hand.Count.Should().Be(0);
    }

    [Theory]
    [InlineData(Phase.Upkeep)]
    [InlineData(Phase.MainPhase1)]
    [InlineData(Phase.Combat)]
    [InlineData(Phase.MainPhase2)]
    [InlineData(Phase.End)]
    public void ExecuteTurnBasedAction_OtherPhases_DoesNothing(Phase phase)
    {
        var engine = CreateEngine(out var state, out _, out _);
        var deck = new DeckBuilder().AddLand("Forest", 10).Build();
        foreach (var card in deck) state.ActivePlayer.Library.Add(card);

        engine.ExecuteTurnBasedAction(phase);

        state.ActivePlayer.Hand.Count.Should().Be(0);
        state.ActivePlayer.Library.Count.Should().Be(10);
    }
}
```

**Step 2: Implement GameEngine (partial — turn-based actions only)**

```csharp
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public class GameEngine
{
    private readonly GameState _state;
    private readonly TurnStateMachine _turnStateMachine = new();

    public GameEngine(GameState state)
    {
        _state = state;
    }

    internal void ExecuteTurnBasedAction(Phase phase)
    {
        switch (phase)
        {
            case Phase.Untap:
                foreach (var card in _state.ActivePlayer.Battlefield.Cards)
                    card.IsTapped = false;
                _state.Log($"{_state.ActivePlayer.Name} untaps all permanents.");
                break;

            case Phase.Draw:
                var drawn = _state.ActivePlayer.Library.DrawFromTop();
                if (drawn != null)
                {
                    _state.ActivePlayer.Hand.Add(drawn);
                    _state.Log($"{_state.ActivePlayer.Name} draws a card.");
                }
                break;
        }
    }
}
```

**Step 3: Run tests**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~GameEngineTurnBasedActionTests"
```
Expected: All pass.

**Step 4: Commit**

```bash
git add src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/GameEngineTurnBasedActionTests.cs
git commit -m "feat(engine): add GameEngine with untap and draw turn-based actions"
```

---

## Task 11: GameEngine — Action Execution

**Files:**
- Create: `tests/MtgDecker.Engine.Tests/GameEngineActionExecutionTests.cs`
- Modify: `src/MtgDecker.Engine/GameEngine.cs`

**Step 1: Write failing tests**

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class GameEngineActionExecutionTests
{
    private GameEngine CreateEngine(out GameState state, out Player player1)
    {
        var p1Handler = new TestDecisionHandler();
        var p2Handler = new TestDecisionHandler();
        player1 = new Player(Guid.NewGuid(), "Alice", p1Handler);
        var p2 = new Player(Guid.NewGuid(), "Bob", p2Handler);
        state = new GameState(player1, p2);
        return new GameEngine(state);
    }

    [Fact]
    public void ExecuteAction_PlayCard_MovesFromHandToBattlefield()
    {
        var engine = CreateEngine(out var state, out var p1);
        var card = new GameCard { Name = "Forest", TypeLine = "Basic Land — Forest" };
        p1.Hand.Add(card);

        engine.ExecuteAction(GameAction.PlayCard(p1.Id, card.Id));

        p1.Hand.Count.Should().Be(0);
        p1.Battlefield.Count.Should().Be(1);
        p1.Battlefield.Cards[0].Should().BeSameAs(card);
    }

    [Fact]
    public void ExecuteAction_PlayCard_LogsAction()
    {
        var engine = CreateEngine(out var state, out var p1);
        var card = new GameCard { Name = "Forest", TypeLine = "Basic Land — Forest" };
        p1.Hand.Add(card);

        engine.ExecuteAction(GameAction.PlayCard(p1.Id, card.Id));

        state.GameLog.Should().Contain(msg => msg.Contains("Alice") && msg.Contains("Forest"));
    }

    [Fact]
    public void ExecuteAction_TapCard_TapsUntappedCard()
    {
        var engine = CreateEngine(out _, out var p1);
        var card = new GameCard { Name = "Forest", TypeLine = "Basic Land — Forest" };
        p1.Battlefield.Add(card);

        engine.ExecuteAction(GameAction.TapCard(p1.Id, card.Id));

        card.IsTapped.Should().BeTrue();
    }

    [Fact]
    public void ExecuteAction_TapCard_IgnoresAlreadyTapped()
    {
        var engine = CreateEngine(out var state, out var p1);
        var card = new GameCard { Name = "Forest", TypeLine = "Basic Land", IsTapped = true };
        p1.Battlefield.Add(card);
        state.GameLog.Clear();

        engine.ExecuteAction(GameAction.TapCard(p1.Id, card.Id));

        card.IsTapped.Should().BeTrue();
        state.GameLog.Should().BeEmpty();
    }

    [Fact]
    public void ExecuteAction_UntapCard_UntapsTappedCard()
    {
        var engine = CreateEngine(out _, out var p1);
        var card = new GameCard { Name = "Forest", TypeLine = "Basic Land", IsTapped = true };
        p1.Battlefield.Add(card);

        engine.ExecuteAction(GameAction.UntapCard(p1.Id, card.Id));

        card.IsTapped.Should().BeFalse();
    }

    [Fact]
    public void ExecuteAction_UntapCard_IgnoresAlreadyUntapped()
    {
        var engine = CreateEngine(out var state, out var p1);
        var card = new GameCard { Name = "Forest", TypeLine = "Basic Land" };
        p1.Battlefield.Add(card);
        state.GameLog.Clear();

        engine.ExecuteAction(GameAction.UntapCard(p1.Id, card.Id));

        card.IsTapped.Should().BeFalse();
        state.GameLog.Should().BeEmpty();
    }

    [Fact]
    public void ExecuteAction_MoveCard_MovesBetweenZones()
    {
        var engine = CreateEngine(out _, out var p1);
        var card = new GameCard { Name = "Bear", TypeLine = "Creature — Bear" };
        p1.Battlefield.Add(card);

        engine.ExecuteAction(GameAction.MoveCard(p1.Id, card.Id, ZoneType.Battlefield, ZoneType.Graveyard));

        p1.Battlefield.Count.Should().Be(0);
        p1.Graveyard.Count.Should().Be(1);
        p1.Graveyard.Cards[0].Should().BeSameAs(card);
    }
}
```

**Step 2: Add ExecuteAction to GameEngine**

```csharp
internal void ExecuteAction(GameAction action)
{
    var player = action.PlayerId == _state.Player1.Id ? _state.Player1 : _state.Player2;

    switch (action.Type)
    {
        case ActionType.PlayCard:
            var playCard = player.Hand.RemoveById(action.CardId!.Value);
            if (playCard != null)
            {
                player.Battlefield.Add(playCard);
                _state.Log($"{player.Name} plays {playCard.Name}.");
            }
            break;

        case ActionType.TapCard:
            var tapTarget = player.Battlefield.Cards.FirstOrDefault(c => c.Id == action.CardId);
            if (tapTarget != null && !tapTarget.IsTapped)
            {
                tapTarget.IsTapped = true;
                _state.Log($"{player.Name} taps {tapTarget.Name}.");
            }
            break;

        case ActionType.UntapCard:
            var untapTarget = player.Battlefield.Cards.FirstOrDefault(c => c.Id == action.CardId);
            if (untapTarget != null && untapTarget.IsTapped)
            {
                untapTarget.IsTapped = false;
                _state.Log($"{player.Name} untaps {untapTarget.Name}.");
            }
            break;

        case ActionType.MoveCard:
            var source = player.GetZone(action.SourceZone!.Value);
            var dest = player.GetZone(action.DestinationZone!.Value);
            var movedCard = source.RemoveById(action.CardId!.Value);
            if (movedCard != null)
            {
                dest.Add(movedCard);
                _state.Log($"{player.Name} moves {movedCard.Name} from {action.SourceZone} to {action.DestinationZone}.");
            }
            break;
    }
}
```

**Step 3: Run tests**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~GameEngineActionExecutionTests"
```
Expected: All pass.

**Step 4: Commit**

```bash
git add src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/GameEngineActionExecutionTests.cs
git commit -m "feat(engine): add action execution - play, tap, untap, move cards"
```

---

## Task 12: GameEngine — Priority System

**Files:**
- Create: `tests/MtgDecker.Engine.Tests/GameEnginePriorityTests.cs`
- Modify: `src/MtgDecker.Engine/GameEngine.cs`

**Step 1: Write failing tests**

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class GameEnginePriorityTests
{
    private GameEngine CreateEngine(out GameState state, out TestDecisionHandler p1Handler, out TestDecisionHandler p2Handler)
    {
        p1Handler = new TestDecisionHandler();
        p2Handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Alice", p1Handler);
        var p2 = new Player(Guid.NewGuid(), "Bob", p2Handler);
        state = new GameState(p1, p2);
        return new GameEngine(state);
    }

    [Fact]
    public async Task RunPriorityAsync_BothPass_PhaseEnds()
    {
        var engine = CreateEngine(out var state, out var p1Handler, out var p2Handler);
        // Both handlers default to Pass

        await engine.RunPriorityAsync();

        // Phase ended — priority was granted and both passed
        state.GameLog.Should().BeEmpty(); // No actions logged, just passes
    }

    [Fact]
    public async Task RunPriorityAsync_ActivePlayerActsThenBothPass_PhaseEnds()
    {
        var engine = CreateEngine(out var state, out var p1Handler, out var p2Handler);
        var card = new GameCard { Name = "Forest", TypeLine = "Basic Land — Forest" };
        state.Player1.Hand.Add(card);

        // P1: play card, then pass (default). P2: pass (default).
        p1Handler.EnqueueAction(GameAction.PlayCard(state.Player1.Id, card.Id));

        await engine.RunPriorityAsync();

        state.Player1.Hand.Count.Should().Be(0);
        state.Player1.Battlefield.Count.Should().Be(1);
    }

    [Fact]
    public async Task RunPriorityAsync_ActivePlayerPasses_OpponentGetsPriority()
    {
        var engine = CreateEngine(out var state, out var p1Handler, out var p2Handler);
        var card = new GameCard { Name = "Bear", TypeLine = "Creature — Bear" };
        state.Player2.Hand.Add(card);

        // P1: pass. P2: play card, then pass (P1 gets priority back and passes).
        p2Handler.EnqueueAction(GameAction.PlayCard(state.Player2.Id, card.Id));

        await engine.RunPriorityAsync();

        state.Player2.Battlefield.Count.Should().Be(1);
    }

    [Fact]
    public async Task RunPriorityAsync_OpponentActs_ActivePlayerGetsPriorityAgain()
    {
        var engine = CreateEngine(out var state, out var p1Handler, out var p2Handler);
        var card1 = new GameCard { Name = "Forest", TypeLine = "Basic Land — Forest" };
        var card2 = new GameCard { Name = "Bear", TypeLine = "Creature — Bear" };
        state.Player2.Hand.Add(card1);
        state.Player1.Hand.Add(card2);

        // P1 passes → P2 plays card → P1 gets priority, plays card → P1 passes → P2 passes → end
        p2Handler.EnqueueAction(GameAction.PlayCard(state.Player2.Id, card1.Id));
        p1Handler.EnqueueAction(GameAction.PlayCard(state.Player1.Id, card2.Id));

        await engine.RunPriorityAsync();

        state.Player1.Battlefield.Count.Should().Be(1);
        state.Player2.Battlefield.Count.Should().Be(1);
    }

    [Fact]
    public async Task RunPriorityAsync_ActivePlayerStartsWithPriority()
    {
        var engine = CreateEngine(out var state, out var p1Handler, out _);

        state.PriorityPlayer = state.Player2; // Set to wrong player

        await engine.RunPriorityAsync();

        // After RunPriority, active player should have been given priority first
        // (the method resets PriorityPlayer to ActivePlayer at start)
    }
}
```

**Step 2: Add RunPriorityAsync to GameEngine**

```csharp
internal async Task RunPriorityAsync(CancellationToken ct = default)
{
    _state.PriorityPlayer = _state.ActivePlayer;
    bool activePlayerPassed = false;
    bool nonActivePlayerPassed = false;

    while (true)
    {
        var action = await _state.PriorityPlayer.DecisionHandler
            .GetAction(_state, _state.PriorityPlayer.Id, ct);

        if (action.Type == ActionType.PassPriority)
        {
            if (_state.PriorityPlayer == _state.ActivePlayer)
                activePlayerPassed = true;
            else
                nonActivePlayerPassed = true;

            if (activePlayerPassed && nonActivePlayerPassed)
                return;

            _state.PriorityPlayer = _state.GetOpponent(_state.PriorityPlayer);
        }
        else
        {
            ExecuteAction(action);
            activePlayerPassed = false;
            nonActivePlayerPassed = false;
            _state.PriorityPlayer = _state.ActivePlayer;
        }
    }
}
```

**Step 3: Run tests**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~GameEnginePriorityTests"
```
Expected: All pass.

**Step 4: Commit**

```bash
git add src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/GameEnginePriorityTests.cs
git commit -m "feat(engine): add priority system with active-player-first passing"
```

---

## Task 13: GameEngine — London Mulligan

**Files:**
- Create: `tests/MtgDecker.Engine.Tests/GameEngineMulliganTests.cs`
- Modify: `src/MtgDecker.Engine/GameEngine.cs`

**Step 1: Write failing tests**

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class GameEngineMulliganTests
{
    private GameEngine CreateEngineWithDecks(out GameState state, out TestDecisionHandler p1Handler, out TestDecisionHandler p2Handler, int deckSize = 60)
    {
        p1Handler = new TestDecisionHandler();
        p2Handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Alice", p1Handler);
        var p2 = new Player(Guid.NewGuid(), "Bob", p2Handler);

        var deck1 = new DeckBuilder().AddLand("Forest", deckSize / 3).AddCard("Bear", deckSize - deckSize / 3, "Creature — Bear").Build();
        var deck2 = new DeckBuilder().AddLand("Mountain", deckSize / 3).AddCard("Goblin", deckSize - deckSize / 3, "Creature — Goblin").Build();
        foreach (var card in deck1) p1.Library.Add(card);
        foreach (var card in deck2) p2.Library.Add(card);

        state = new GameState(p1, p2);
        return new GameEngine(state);
    }

    [Fact]
    public async Task RunMulliganAsync_KeepImmediately_HandIs7()
    {
        var engine = CreateEngineWithDecks(out var state, out _, out _);
        // Default: keep immediately

        await engine.RunMulliganAsync(state.Player1);

        state.Player1.Hand.Count.Should().Be(7);
        state.Player1.Library.Count.Should().Be(53);
    }

    [Fact]
    public async Task RunMulliganAsync_MulliganOnce_HandIs6()
    {
        var engine = CreateEngineWithDecks(out var state, out var p1Handler, out _);
        p1Handler.EnqueueMulligan(MulliganDecision.Mulligan);
        // Then default: keep

        await engine.RunMulliganAsync(state.Player1);

        state.Player1.Hand.Count.Should().Be(6);
        state.Player1.Library.Count.Should().Be(54);
    }

    [Fact]
    public async Task RunMulliganAsync_MulliganTwice_HandIs5()
    {
        var engine = CreateEngineWithDecks(out var state, out var p1Handler, out _);
        p1Handler.EnqueueMulligan(MulliganDecision.Mulligan);
        p1Handler.EnqueueMulligan(MulliganDecision.Mulligan);

        await engine.RunMulliganAsync(state.Player1);

        state.Player1.Hand.Count.Should().Be(5);
        state.Player1.Library.Count.Should().Be(55);
    }

    [Fact]
    public async Task RunMulliganAsync_MulliganOnce_CardsReturnedToLibraryBeforeRedraw()
    {
        var engine = CreateEngineWithDecks(out var state, out var p1Handler, out _, deckSize: 60);
        p1Handler.EnqueueMulligan(MulliganDecision.Mulligan);

        await engine.RunMulliganAsync(state.Player1);

        // 60 total cards: 6 in hand, 54 in library
        (state.Player1.Hand.Count + state.Player1.Library.Count).Should().Be(60);
    }

    [Fact]
    public async Task RunMulliganAsync_BottomChoice_PutsSelectedCardsOnBottom()
    {
        var engine = CreateEngineWithDecks(out var state, out var p1Handler, out _);
        p1Handler.EnqueueMulligan(MulliganDecision.Mulligan);
        // Keep after 1 mulligan — need to bottom 1 card
        // Custom bottom choice: put the last card in hand on bottom
        p1Handler.EnqueueBottomChoice((hand, count) => hand.TakeLast(count).ToList());

        await engine.RunMulliganAsync(state.Player1);

        state.Player1.Hand.Count.Should().Be(6);
        state.Player1.Library.Count.Should().Be(54);
    }

    [Fact]
    public async Task RunMulliganAsync_LogsResult()
    {
        var engine = CreateEngineWithDecks(out var state, out _, out _);

        await engine.RunMulliganAsync(state.Player1);

        state.GameLog.Should().Contain(msg => msg.Contains("Alice") && msg.Contains("keeps"));
    }
}
```

**Step 2: Add mulligan methods to GameEngine**

```csharp
internal async Task RunMulliganAsync(Player player, CancellationToken ct = default)
{
    int mulliganCount = 0;

    DrawCards(player, 7);

    while (true)
    {
        var decision = await player.DecisionHandler
            .GetMulliganDecision(player.Hand.Cards, mulliganCount, ct);

        if (decision == MulliganDecision.Keep)
        {
            if (mulliganCount > 0)
            {
                var cardsToBottom = await player.DecisionHandler
                    .ChooseCardsToBottom(player.Hand.Cards, mulliganCount, ct);

                foreach (var card in cardsToBottom)
                {
                    player.Hand.RemoveById(card.Id);
                    player.Library.AddToBottom(card);
                }
            }

            _state.Log($"{player.Name} keeps hand of {player.Hand.Count} cards (mulliganed {mulliganCount} times).");
            break;
        }

        mulliganCount++;
        ReturnHandToLibrary(player);
        player.Library.Shuffle();
        DrawCards(player, 7);
    }
}

private void DrawCards(Player player, int count)
{
    for (int i = 0; i < count; i++)
    {
        var card = player.Library.DrawFromTop();
        if (card != null)
            player.Hand.Add(card);
    }
}

private void ReturnHandToLibrary(Player player)
{
    while (player.Hand.Count > 0)
    {
        var card = player.Hand.Cards[0];
        player.Hand.RemoveById(card.Id);
        player.Library.Add(card);
    }
}
```

**Step 3: Run tests**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~GameEngineMulliganTests"
```
Expected: All pass.

**Step 4: Commit**

```bash
git add src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/GameEngineMulliganTests.cs
git commit -m "feat(engine): add London mulligan with bottom card selection"
```

---

## Task 14: GameEngine — Full Game Loop

**Files:**
- Create: `tests/MtgDecker.Engine.Tests/GameEngineGameLoopTests.cs`
- Modify: `src/MtgDecker.Engine/GameEngine.cs`

**Step 1: Write failing tests**

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class GameEngineGameLoopTests
{
    private GameEngine CreateEngineWithDecks(out GameState state, out TestDecisionHandler p1Handler, out TestDecisionHandler p2Handler)
    {
        p1Handler = new TestDecisionHandler();
        p2Handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Alice", p1Handler);
        var p2 = new Player(Guid.NewGuid(), "Bob", p2Handler);

        var deck1 = new DeckBuilder().AddLand("Forest", 20).AddCard("Bear", 40, "Creature — Bear").Build();
        var deck2 = new DeckBuilder().AddLand("Mountain", 20).AddCard("Goblin", 40, "Creature — Goblin").Build();
        foreach (var card in deck1) p1.Library.Add(card);
        foreach (var card in deck2) p2.Library.Add(card);

        state = new GameState(p1, p2);
        return new GameEngine(state);
    }

    [Fact]
    public async Task StartGameAsync_ShufflesAndRunsMulligan()
    {
        var engine = CreateEngineWithDecks(out var state, out _, out _);

        await engine.StartGameAsync();

        state.Player1.Hand.Count.Should().Be(7);
        state.Player2.Hand.Count.Should().Be(7);
        state.Player1.Library.Count.Should().Be(53);
        state.Player2.Library.Count.Should().Be(53);
    }

    [Fact]
    public async Task RunTurnAsync_WalksThroughAllPhases()
    {
        var engine = CreateEngineWithDecks(out var state, out _, out _);
        await engine.StartGameAsync();
        state.GameLog.Clear();

        await engine.RunTurnAsync();

        var phaseNames = new[] { "Untap", "Upkeep", "Draw", "MainPhase1", "Combat", "MainPhase2", "End" };
        foreach (var name in phaseNames)
            state.GameLog.Should().Contain(msg => msg.Contains(name));
    }

    [Fact]
    public async Task RunTurnAsync_ActivePlayerSwitchesAfterTurn()
    {
        var engine = CreateEngineWithDecks(out var state, out _, out _);
        await engine.StartGameAsync();
        state.ActivePlayer.Should().BeSameAs(state.Player1);

        await engine.RunTurnAsync();

        state.ActivePlayer.Should().BeSameAs(state.Player2);
    }

    [Fact]
    public async Task RunTurnAsync_TurnNumberIncrements()
    {
        var engine = CreateEngineWithDecks(out var state, out _, out _);
        await engine.StartGameAsync();
        state.TurnNumber.Should().Be(1);

        await engine.RunTurnAsync();

        state.TurnNumber.Should().Be(2);
    }

    [Fact]
    public async Task RunTurnAsync_DrawStepDrawsCard()
    {
        var engine = CreateEngineWithDecks(out var state, out _, out _);
        await engine.StartGameAsync();
        var handBefore = state.Player1.Hand.Count;
        var libraryBefore = state.Player1.Library.Count;

        await engine.RunTurnAsync();

        state.Player1.Hand.Count.Should().Be(handBefore + 1);
        state.Player1.Library.Count.Should().Be(libraryBefore - 1);
    }

    [Fact]
    public async Task RunTurnAsync_FirstPlayerSkipsDrawOnTurn1()
    {
        var engine = CreateEngineWithDecks(out var state, out _, out _);
        await engine.StartGameAsync();
        state.IsFirstTurn = true;
        var handBefore = state.Player1.Hand.Count;

        await engine.RunTurnAsync();

        // First player doesn't draw on turn 1
        state.Player1.Hand.Count.Should().Be(handBefore);
    }

    [Fact]
    public async Task RunTurnAsync_SecondTurn_PlayerDraws()
    {
        var engine = CreateEngineWithDecks(out var state, out _, out _);
        await engine.StartGameAsync();
        state.IsFirstTurn = true;

        await engine.RunTurnAsync(); // Turn 1 (P1, no draw)
        var handBefore = state.Player2.Hand.Count;

        await engine.RunTurnAsync(); // Turn 2 (P2, draws)

        state.Player2.Hand.Count.Should().Be(handBefore + 1);
    }

    [Fact]
    public async Task RunTurnAsync_UntapStepUntapsActivePlayerCards()
    {
        var engine = CreateEngineWithDecks(out var state, out _, out _);
        await engine.StartGameAsync();
        var card = new GameCard { Name = "Forest", TypeLine = "Basic Land", IsTapped = true };
        state.Player1.Battlefield.Add(card);

        await engine.RunTurnAsync();

        card.IsTapped.Should().BeFalse();
    }

    [Fact]
    public async Task TwoFullTurns_GameProgresses()
    {
        var engine = CreateEngineWithDecks(out var state, out _, out _);
        await engine.StartGameAsync();

        await engine.RunTurnAsync(); // Turn 1
        await engine.RunTurnAsync(); // Turn 2

        state.TurnNumber.Should().Be(3);
        state.ActivePlayer.Should().BeSameAs(state.Player1); // Back to P1
    }
}
```

**Step 2: Add IsFirstTurn to GameState**

Add to `src/MtgDecker.Engine/GameState.cs`:
```csharp
public bool IsFirstTurn { get; set; }
```

**Step 3: Add StartGameAsync and RunTurnAsync to GameEngine**

```csharp
public async Task StartGameAsync(CancellationToken ct = default)
{
    _state.Player1.Library.Shuffle();
    _state.Player2.Library.Shuffle();

    await RunMulliganAsync(_state.Player1, ct);
    await RunMulliganAsync(_state.Player2, ct);

    _state.Log("Game started.");
}

public async Task RunTurnAsync(CancellationToken ct = default)
{
    _turnStateMachine.Reset();
    _state.Log($"Turn {_state.TurnNumber}: {_state.ActivePlayer.Name}'s turn.");

    do
    {
        var phase = _turnStateMachine.CurrentPhase;
        _state.CurrentPhase = phase.Phase;
        _state.Log($"Phase: {phase.Phase}");

        if (phase.HasTurnBasedAction)
        {
            // First player skips draw on turn 1
            bool skipDraw = phase.Phase == Phase.Draw && _state.IsFirstTurn;
            if (!skipDraw)
                ExecuteTurnBasedAction(phase.Phase);
        }

        if (phase.GrantsPriority)
            await RunPriorityAsync(ct);

    } while (_turnStateMachine.AdvancePhase() != null);

    // End of turn
    _state.IsFirstTurn = false;
    _state.TurnNumber++;
    _state.ActivePlayer = _state.GetOpponent(_state.ActivePlayer);
}
```

**Step 4: Run ALL tests**

```bash
dotnet test tests/MtgDecker.Engine.Tests/
```
Expected: All pass.

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/ tests/MtgDecker.Engine.Tests/
git commit -m "feat(engine): add full game loop with start, turns, and first-turn draw skip"
```

---

## Summary

**Total tasks:** 14
**Estimated tests:** ~60+
**Key design decisions:**
- `GameCard` uses `init` for immutable properties, `set` for `IsTapped`
- `Zone` uses Fisher-Yates shuffle with `Random.Shared`
- `GameEngine` methods are `internal` (testable via `InternalsVisibleTo`)
- `TurnStateMachine` is data-driven — phases defined as a static list of `PhaseDefinition`
- Priority loop: active player first, both must pass in succession, any action resets pass count
- London mulligan: draw 7, choose keep/mulligan, put N on bottom of library
- `IsFirstTurn` flag for first-player draw skip

**What v1 does NOT include (deferred):**
- Stack
- Mana costs / payment
- Life tracking
- Formal declare attackers/blockers
- Combat damage
- State-based actions
- Card abilities
- UI
- AI opponent
- Exile / Command zones
- Save/load / replay
