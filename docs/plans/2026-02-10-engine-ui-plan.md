# Engine UI Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Wire MtgDecker.Engine into the Blazor web app as a two-player MTGO-style game interface with two browser tabs on localhost.

**Architecture:** Server-side game loop runs on a background task. `InteractiveDecisionHandler` bridges engine ↔ UI via `TaskCompletionSource`. `GameSession` holds engine state and fires change events. `GameSessionManager` (singleton) manages active games. Blazor components react to state changes. Engine classes live in MtgDecker.Engine (testable); Razor components in MtgDecker.Web.

**Tech Stack:** .NET 10, Blazor InteractiveServer, MudBlazor 8.x, MtgDecker.Engine, MediatR, xUnit + FluentAssertions

**Design doc:** `docs/plans/2026-02-10-engine-ui-design.md`

---

## File Map

```
src/MtgDecker.Engine/
  GameState.cs                    (modify: add OnStateChanged event)
  InteractiveDecisionHandler.cs   (create)
  GameSession.cs                  (create)
  GameSessionManager.cs           (create)

src/MtgDecker.Web/
  Program.cs                      (modify: register GameSessionManager, add Engine project ref)
  MtgDecker.Web.csproj            (modify: add project reference to Engine)
  Components/Layout/MainLayout.razor  (modify: add Play Game nav link)
  Components/Pages/Game/
    CardDisplay.razor             (create)
    CardDisplay.razor.css         (create)
    ActionMenu.razor              (create)
    PlayerZone.razor              (create)
    PlayerZone.razor.css          (create)
    GameLogPanel.razor            (create)
    GameLogPanel.razor.css        (create)
    MulliganDialog.razor          (create)
    GameBoard.razor               (create)
    GameBoard.razor.css           (create)
  Components/Pages/GameLobby.razor    (create)
  Components/Pages/GamePage.razor     (create)

tests/MtgDecker.Engine.Tests/
  InteractiveDecisionHandlerTests.cs  (create)
  GameSessionTests.cs                 (create)
  GameSessionManagerTests.cs          (create)
```

---

### Task 1: Add OnStateChanged Event to GameState

**Files:**
- Modify: `src/MtgDecker.Engine/GameState.cs`
- Test: `tests/MtgDecker.Engine.Tests/GameStateTests.cs`

**Context:** The UI needs to know when game state changes. Every significant action calls `GameState.Log()`, so adding an event there catches all mutations.

**Step 1: Write the failing test**

Add to `GameStateTests.cs`:

```csharp
[Fact]
public void Log_FiresOnStateChanged()
{
    var p1 = new Player(Guid.NewGuid(), "Alice", new TestDecisionHandler());
    var p2 = new Player(Guid.NewGuid(), "Bob", new TestDecisionHandler());
    var state = new GameState(p1, p2);
    bool fired = false;
    state.OnStateChanged += () => fired = true;

    state.Log("test message");

    fired.Should().BeTrue();
}
```

**Step 2: Run test to verify it fails**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "Log_FiresOnStateChanged" -v normal
```

Expected: FAIL — `OnStateChanged` does not exist.

**Step 3: Implement**

In `GameState.cs`, change the `Log` method and add the event:

```csharp
public event Action? OnStateChanged;

public void Log(string message)
{
    GameLog.Add(message);
    OnStateChanged?.Invoke();
}
```

**Step 4: Run test to verify it passes**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "Log_FiresOnStateChanged" -v normal
```

**Step 5: Run all tests to verify no regressions**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ -v normal
```

Expected: All 117+ tests pass.

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/GameState.cs tests/MtgDecker.Engine.Tests/GameStateTests.cs
git commit -m "feat(engine): add OnStateChanged event to GameState"
```

---

### Task 2: InteractiveDecisionHandler

**Files:**
- Create: `src/MtgDecker.Engine/InteractiveDecisionHandler.cs`
- Create: `tests/MtgDecker.Engine.Tests/InteractiveDecisionHandlerTests.cs`

**Context:** Bridges the async engine loop with interactive UI. When the engine calls `GetAction()`, it awaits a `TaskCompletionSource`. The UI calls `SubmitAction()` to complete it. Uses `RunContinuationsAsynchronously` to prevent deadlocks.

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/InteractiveDecisionHandlerTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class InteractiveDecisionHandlerTests
{
    private readonly GameState _state;
    private readonly Guid _playerId = Guid.NewGuid();

    public InteractiveDecisionHandlerTests()
    {
        var p1 = new Player(Guid.NewGuid(), "Alice", new InteractiveDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "Bob", new InteractiveDecisionHandler());
        _state = new GameState(p1, p2);
    }

    [Fact]
    public async Task GetAction_WaitsUntilSubmitAction()
    {
        var handler = new InteractiveDecisionHandler();
        var actionTask = handler.GetAction(_state, _playerId);

        actionTask.IsCompleted.Should().BeFalse();

        var pass = GameAction.Pass(_playerId);
        handler.SubmitAction(pass);

        var result = await actionTask;
        result.Type.Should().Be(ActionType.PassPriority);
    }

    [Fact]
    public async Task GetMulliganDecision_WaitsUntilSubmitMulligan()
    {
        var handler = new InteractiveDecisionHandler();
        var hand = new List<GameCard> { new() { Name = "Forest" } };
        var task = handler.GetMulliganDecision(hand, 0);

        task.IsCompleted.Should().BeFalse();

        handler.SubmitMulliganDecision(MulliganDecision.Keep);

        var result = await task;
        result.Should().Be(MulliganDecision.Keep);
    }

    [Fact]
    public async Task ChooseCardsToBottom_WaitsUntilSubmitBottomCards()
    {
        var handler = new InteractiveDecisionHandler();
        var hand = new List<GameCard>
        {
            new() { Name = "Forest" },
            new() { Name = "Bear" }
        };
        var task = handler.ChooseCardsToBottom(hand, 1);

        task.IsCompleted.Should().BeFalse();

        var selected = new List<GameCard> { hand[0] };
        handler.SubmitBottomCards(selected);

        var result = await task;
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Forest");
    }

    [Fact]
    public void IsWaitingForAction_TrueWhileAwaiting()
    {
        var handler = new InteractiveDecisionHandler();
        handler.IsWaitingForAction.Should().BeFalse();

        _ = handler.GetAction(_state, _playerId);

        handler.IsWaitingForAction.Should().BeTrue();
    }

    [Fact]
    public async Task IsWaitingForAction_FalseAfterSubmit()
    {
        var handler = new InteractiveDecisionHandler();
        var task = handler.GetAction(_state, _playerId);

        handler.SubmitAction(GameAction.Pass(_playerId));
        await task;

        handler.IsWaitingForAction.Should().BeFalse();
    }

    [Fact]
    public void IsWaitingForMulligan_TrueWhileAwaiting()
    {
        var handler = new InteractiveDecisionHandler();
        _ = handler.GetMulliganDecision(new List<GameCard>(), 0);

        handler.IsWaitingForMulligan.Should().BeTrue();
    }

    [Fact]
    public async Task Cancellation_CancelsWaitingAction()
    {
        var handler = new InteractiveDecisionHandler();
        using var cts = new CancellationTokenSource();
        var task = handler.GetAction(_state, _playerId, cts.Token);

        cts.Cancel();

        var act = () => task;
        await act.Should().ThrowAsync<TaskCanceledException>();
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "InteractiveDecisionHandler" -v normal
```

Expected: FAIL — `InteractiveDecisionHandler` does not exist.

**Step 3: Implement**

Create `src/MtgDecker.Engine/InteractiveDecisionHandler.cs`:

```csharp
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public class InteractiveDecisionHandler : IPlayerDecisionHandler
{
    private TaskCompletionSource<GameAction>? _actionTcs;
    private TaskCompletionSource<MulliganDecision>? _mulliganTcs;
    private TaskCompletionSource<IReadOnlyList<GameCard>>? _bottomCardsTcs;

    public bool IsWaitingForAction => _actionTcs is { Task.IsCompleted: false };
    public bool IsWaitingForMulligan => _mulliganTcs is { Task.IsCompleted: false };
    public bool IsWaitingForBottomCards => _bottomCardsTcs is { Task.IsCompleted: false };

    public Task<GameAction> GetAction(GameState gameState, Guid playerId, CancellationToken ct = default)
    {
        _actionTcs = new TaskCompletionSource<GameAction>(TaskCreationOptions.RunContinuationsAsynchronously);
        ct.Register(() => _actionTcs.TrySetCanceled());
        return _actionTcs.Task;
    }

    public Task<MulliganDecision> GetMulliganDecision(IReadOnlyList<GameCard> hand, int mulliganCount, CancellationToken ct = default)
    {
        _mulliganTcs = new TaskCompletionSource<MulliganDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        ct.Register(() => _mulliganTcs.TrySetCanceled());
        return _mulliganTcs.Task;
    }

    public Task<IReadOnlyList<GameCard>> ChooseCardsToBottom(IReadOnlyList<GameCard> hand, int count, CancellationToken ct = default)
    {
        _bottomCardsTcs = new TaskCompletionSource<IReadOnlyList<GameCard>>(TaskCreationOptions.RunContinuationsAsynchronously);
        ct.Register(() => _bottomCardsTcs.TrySetCanceled());
        return _bottomCardsTcs.Task;
    }

    public void SubmitAction(GameAction action) =>
        _actionTcs?.TrySetResult(action);

    public void SubmitMulliganDecision(MulliganDecision decision) =>
        _mulliganTcs?.TrySetResult(decision);

    public void SubmitBottomCards(IReadOnlyList<GameCard> cards) =>
        _bottomCardsTcs?.TrySetResult(cards);
}
```

**Step 4: Run tests**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "InteractiveDecisionHandler" -v normal
```

Expected: All 7 tests pass.

**Step 5: Run all tests**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ -v normal
```

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/InteractiveDecisionHandler.cs tests/MtgDecker.Engine.Tests/InteractiveDecisionHandlerTests.cs
git commit -m "feat(engine): add InteractiveDecisionHandler with TCS bridge"
```

---

### Task 3: GameSession

**Files:**
- Create: `src/MtgDecker.Engine/GameSession.cs`
- Create: `tests/MtgDecker.Engine.Tests/GameSessionTests.cs`

**Context:** GameSession holds a game's full state: engine, handlers, players, decks. It manages the game lifecycle from lobby → playing → game over. The game loop runs on a background task.

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/GameSessionTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class GameSessionTests
{
    private List<GameCard> CreateDeck(int size = 60)
    {
        return new DeckBuilder()
            .AddLand("Forest", size / 3)
            .AddCard("Bear", size - size / 3, "Creature — Bear")
            .Build();
    }

    [Fact]
    public void Constructor_SetsGameId()
    {
        var session = new GameSession("ABC123");
        session.GameId.Should().Be("ABC123");
    }

    [Fact]
    public void JoinPlayer_FirstPlayer_ReturnsSeat1()
    {
        var session = new GameSession("ABC123");
        var seat = session.JoinPlayer("Alice", CreateDeck());

        seat.Should().Be(1);
        session.Player1Name.Should().Be("Alice");
        session.IsFull.Should().BeFalse();
    }

    [Fact]
    public void JoinPlayer_SecondPlayer_ReturnsSeat2()
    {
        var session = new GameSession("ABC123");
        session.JoinPlayer("Alice", CreateDeck());
        var seat = session.JoinPlayer("Bob", CreateDeck());

        seat.Should().Be(2);
        session.Player2Name.Should().Be("Bob");
        session.IsFull.Should().BeTrue();
    }

    [Fact]
    public void JoinPlayer_ThirdPlayer_Throws()
    {
        var session = new GameSession("ABC123");
        session.JoinPlayer("Alice", CreateDeck());
        session.JoinPlayer("Bob", CreateDeck());

        var act = () => session.JoinPlayer("Charlie", CreateDeck());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task StartAsync_InitializesEngineAndState()
    {
        var session = new GameSession("ABC123");
        session.JoinPlayer("Alice", CreateDeck());
        session.JoinPlayer("Bob", CreateDeck());

        await session.StartAsync();

        session.State.Should().NotBeNull();
        session.IsStarted.Should().BeTrue();
        session.Player1Handler.Should().NotBeNull();
        session.Player2Handler.Should().NotBeNull();
    }

    [Fact]
    public async Task StartAsync_NotFull_Throws()
    {
        var session = new GameSession("ABC123");
        session.JoinPlayer("Alice", CreateDeck());

        var act = () => session.StartAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task StartAsync_EngineWaitsForMulliganInput()
    {
        var session = new GameSession("ABC123");
        session.JoinPlayer("Alice", CreateDeck());
        session.JoinPlayer("Bob", CreateDeck());

        await session.StartAsync();

        // Engine is now waiting for P1's mulligan decision
        await Task.Delay(50); // Let background task start
        session.Player1Handler!.IsWaitingForMulligan.Should().BeTrue();
    }

    [Fact]
    public async Task Surrender_EndsGame()
    {
        var session = new GameSession("ABC123");
        session.JoinPlayer("Alice", CreateDeck());
        session.JoinPlayer("Bob", CreateDeck());
        await session.StartAsync();

        session.Surrender(1);

        session.IsGameOver.Should().BeTrue();
        session.Winner.Should().Be("Bob");
    }

    [Fact]
    public async Task OnStateChanged_FiresOnGameEvents()
    {
        var session = new GameSession("ABC123");
        session.JoinPlayer("Alice", CreateDeck());
        session.JoinPlayer("Bob", CreateDeck());

        int changeCount = 0;
        session.OnStateChanged += () => changeCount++;

        await session.StartAsync();
        await Task.Delay(50);

        // Submit mulligans to progress the game
        session.Player1Handler!.SubmitMulliganDecision(Enums.MulliganDecision.Keep);
        await Task.Delay(50);
        session.Player2Handler!.SubmitMulliganDecision(Enums.MulliganDecision.Keep);
        await Task.Delay(50);

        changeCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetHandler_ReturnsCorrectHandler()
    {
        var session = new GameSession("ABC123");
        session.JoinPlayer("Alice", CreateDeck());
        session.JoinPlayer("Bob", CreateDeck());
        await session.StartAsync();

        session.GetHandler(1).Should().BeSameAs(session.Player1Handler);
        session.GetHandler(2).Should().BeSameAs(session.Player2Handler);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "GameSessionTests" -v normal
```

**Step 3: Implement**

Create `src/MtgDecker.Engine/GameSession.cs`:

```csharp
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public class GameSession
{
    public string GameId { get; }
    public GameState? State { get; private set; }
    public InteractiveDecisionHandler? Player1Handler { get; private set; }
    public InteractiveDecisionHandler? Player2Handler { get; private set; }
    public string? Player1Name { get; private set; }
    public string? Player2Name { get; private set; }
    public bool IsFull => Player1Name != null && Player2Name != null;
    public bool IsStarted { get; private set; }
    public bool IsGameOver => State?.IsGameOver ?? false;
    public string? Winner { get; private set; }
    public event Action? OnStateChanged;

    private List<GameCard>? _player1Deck;
    private List<GameCard>? _player2Deck;
    private CancellationTokenSource? _cts;

    public GameSession(string gameId)
    {
        GameId = gameId;
    }

    public int JoinPlayer(string playerName, List<GameCard> deck)
    {
        if (Player1Name == null)
        {
            Player1Name = playerName;
            _player1Deck = deck;
            return 1;
        }
        if (Player2Name == null)
        {
            Player2Name = playerName;
            _player2Deck = deck;
            return 2;
        }
        throw new InvalidOperationException("Game is full.");
    }

    public async Task StartAsync()
    {
        if (!IsFull)
            throw new InvalidOperationException("Need two players to start.");

        Player1Handler = new InteractiveDecisionHandler();
        Player2Handler = new InteractiveDecisionHandler();

        var p1 = new Player(Guid.NewGuid(), Player1Name!, Player1Handler);
        var p2 = new Player(Guid.NewGuid(), Player2Name!, Player2Handler);

        foreach (var card in _player1Deck!) p1.Library.Add(card);
        foreach (var card in _player2Deck!) p2.Library.Add(card);

        State = new GameState(p1, p2);
        State.OnStateChanged += () => OnStateChanged?.Invoke();
        var engine = new GameEngine(State);

        IsStarted = true;
        _cts = new CancellationTokenSource();

        _ = Task.Run(() => RunGameLoopAsync(engine, _cts.Token));
    }

    private async Task RunGameLoopAsync(GameEngine engine, CancellationToken ct)
    {
        try
        {
            await engine.StartGameAsync(ct);
            State!.IsFirstTurn = true;

            while (!State.IsGameOver)
            {
                ct.ThrowIfCancellationRequested();
                await engine.RunTurnAsync(ct);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            OnStateChanged?.Invoke();
        }
    }

    public void Surrender(int playerSeat)
    {
        if (State == null) return;
        State.IsGameOver = true;
        Winner = playerSeat == 1 ? Player2Name : Player1Name;
        State.Log($"{(playerSeat == 1 ? Player1Name : Player2Name)} surrenders.");
        _cts?.Cancel();
    }

    public InteractiveDecisionHandler? GetHandler(int playerSeat) =>
        playerSeat == 1 ? Player1Handler : Player2Handler;
}
```

**Step 4: Run tests**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "GameSessionTests" -v normal
```

**Step 5: Run all tests**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ -v normal
```

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/GameSession.cs tests/MtgDecker.Engine.Tests/GameSessionTests.cs
git commit -m "feat(engine): add GameSession with game lifecycle and background loop"
```

---

### Task 4: GameSessionManager

**Files:**
- Create: `src/MtgDecker.Engine/GameSessionManager.cs`
- Create: `tests/MtgDecker.Engine.Tests/GameSessionManagerTests.cs`

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/GameSessionManagerTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;

namespace MtgDecker.Engine.Tests;

public class GameSessionManagerTests
{
    [Fact]
    public void CreateGame_ReturnsSessionWithId()
    {
        var manager = new GameSessionManager();
        var session = manager.CreateGame();

        session.Should().NotBeNull();
        session.GameId.Should().HaveLength(6);
    }

    [Fact]
    public void CreateGame_GeneratesUniqueIds()
    {
        var manager = new GameSessionManager();
        var ids = Enumerable.Range(0, 10)
            .Select(_ => manager.CreateGame().GameId)
            .ToList();

        ids.Distinct().Count().Should().Be(10);
    }

    [Fact]
    public void GetSession_ReturnsExistingSession()
    {
        var manager = new GameSessionManager();
        var session = manager.CreateGame();

        var retrieved = manager.GetSession(session.GameId);

        retrieved.Should().BeSameAs(session);
    }

    [Fact]
    public void GetSession_ReturnsNullForUnknownId()
    {
        var manager = new GameSessionManager();

        manager.GetSession("XXXXXX").Should().BeNull();
    }

    [Fact]
    public void RemoveSession_RemovesFromManager()
    {
        var manager = new GameSessionManager();
        var session = manager.CreateGame();

        manager.RemoveSession(session.GameId);

        manager.GetSession(session.GameId).Should().BeNull();
    }
}
```

**Step 2: Implement**

Create `src/MtgDecker.Engine/GameSessionManager.cs`:

```csharp
using System.Collections.Concurrent;

namespace MtgDecker.Engine;

public class GameSessionManager
{
    private readonly ConcurrentDictionary<string, GameSession> _sessions = new();

    public GameSession CreateGame()
    {
        var gameId = GenerateGameId();
        var session = new GameSession(gameId);
        _sessions[gameId] = session;
        return session;
    }

    public GameSession? GetSession(string gameId) =>
        _sessions.TryGetValue(gameId, out var session) ? session : null;

    public void RemoveSession(string gameId) =>
        _sessions.TryRemove(gameId, out _);

    private string GenerateGameId()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        string id;
        do
        {
            id = new string(Enumerable.Range(0, 6)
                .Select(_ => chars[Random.Shared.Next(chars.Length)])
                .ToArray());
        } while (_sessions.ContainsKey(id));
        return id;
    }
}
```

**Step 3: Run tests**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "GameSessionManagerTests" -v normal
```

**Step 4: Run all tests, then commit**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ -v normal
git add src/MtgDecker.Engine/GameSessionManager.cs tests/MtgDecker.Engine.Tests/GameSessionManagerTests.cs
git commit -m "feat(engine): add GameSessionManager with session lifecycle"
```

---

### Task 5: Web Project Wiring

**Files:**
- Modify: `src/MtgDecker.Web/MtgDecker.Web.csproj` (add Engine project reference)
- Modify: `src/MtgDecker.Web/Program.cs` (register GameSessionManager)
- Modify: `src/MtgDecker.Web/Components/Layout/MainLayout.razor` (add nav link)

**Step 1: Add project reference**

In `MtgDecker.Web.csproj`, add to the `<ItemGroup>` with project references:

```xml
<ProjectReference Include="..\..\src\MtgDecker.Engine\MtgDecker.Engine.csproj" />
```

**Step 2: Register GameSessionManager in Program.cs**

Add after the InMemoryLogStore registration:

```csharp
// Game session manager
builder.Services.AddSingleton<MtgDecker.Engine.GameSessionManager>();
```

**Step 3: Add nav link in MainLayout.razor**

Add before the "Import Data" nav link:

```razor
<MudNavLink Href="/game/new" Match="NavLinkMatch.Prefix"
            Icon="@Icons.Material.Filled.SportsEsports">Play Game</MudNavLink>
```

**Step 4: Build and verify**

```bash
dotnet build src/MtgDecker.Web/
```

**Step 5: Commit**

```bash
git add src/MtgDecker.Web/MtgDecker.Web.csproj src/MtgDecker.Web/Program.cs src/MtgDecker.Web/Components/Layout/MainLayout.razor
git commit -m "feat(web): wire Engine to Web project with GameSessionManager"
```

---

### Task 6: CardDisplay Component

**Files:**
- Create: `src/MtgDecker.Web/Components/Pages/Game/CardDisplay.razor`
- Create: `src/MtgDecker.Web/Components/Pages/Game/CardDisplay.razor.css`

**Context:** Shows a single card as a small thumbnail image. Tapped cards rotate 90 degrees. Card backs shown for opponent's hand. Click fires a callback.

**Step 1: Create directory**

```bash
mkdir -p src/MtgDecker.Web/Components/Pages/Game
```

**Step 2: Create CardDisplay.razor**

```razor
@namespace MtgDecker.Web.Components.Pages.Game

<div class="card-display @(Tapped ? "tapped" : "") @(Selected ? "selected" : "") @(Clickable ? "clickable" : "")"
     @onclick="HandleClick">
    @if (IsBack)
    {
        <div class="card-back">
            <MudIcon Icon="@Icons.Material.Filled.Help" Size="Size.Large" />
        </div>
    }
    else if (!string.IsNullOrEmpty(ImageUrl))
    {
        <img src="@ImageUrl" alt="@Name" loading="lazy" />
    }
    else
    {
        <div class="card-placeholder">
            <MudText Typo="Typo.caption" Align="Align.Center">@Name</MudText>
        </div>
    }
</div>

@code {
    [Parameter] public string? ImageUrl { get; set; }
    [Parameter] public string Name { get; set; } = "";
    [Parameter] public bool Tapped { get; set; }
    [Parameter] public bool IsBack { get; set; }
    [Parameter] public bool Selected { get; set; }
    [Parameter] public bool Clickable { get; set; } = true;
    [Parameter] public EventCallback OnClick { get; set; }

    private async Task HandleClick()
    {
        if (Clickable)
            await OnClick.InvokeAsync();
    }
}
```

**Step 3: Create CardDisplay.razor.css**

```css
.card-display {
    width: 100px;
    min-height: 140px;
    display: inline-block;
    margin: 4px;
    transition: transform 0.15s ease;
    position: relative;
}

.card-display.clickable {
    cursor: pointer;
}

.card-display.clickable:hover {
    filter: brightness(1.1);
}

.card-display.tapped {
    transform: rotate(90deg);
    margin: 20px 24px;
}

.card-display.selected {
    outline: 3px solid #C69B3C;
    border-radius: 8px;
}

.card-display img {
    width: 100%;
    border-radius: 6px;
    display: block;
}

.card-back {
    width: 100%;
    min-height: 140px;
    background: linear-gradient(135deg, #2D2540, #5D4E8C);
    border-radius: 6px;
    display: flex;
    align-items: center;
    justify-content: center;
    border: 2px solid #7B6BA8;
}

.card-placeholder {
    width: 100%;
    min-height: 140px;
    background: #231E30;
    border-radius: 6px;
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 8px;
    border: 1px solid #444;
}
```

**Step 4: Build and verify**

```bash
dotnet build src/MtgDecker.Web/
```

**Step 5: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/Game/CardDisplay.razor src/MtgDecker.Web/Components/Pages/Game/CardDisplay.razor.css
git commit -m "feat(web): add CardDisplay component with tap rotation"
```

---

### Task 7: ActionMenu Component

**Files:**
- Create: `src/MtgDecker.Web/Components/Pages/Game/ActionMenu.razor`

**Context:** A MudPopover that appears when a card is selected. Shows context-sensitive actions based on zone and ownership. All cards in all zones can be moved to any other zone.

**Step 1: Create ActionMenu.razor**

```razor
@using MtgDecker.Engine
@using MtgDecker.Engine.Enums
@namespace MtgDecker.Web.Components.Pages.Game

@if (Visible)
{
    <MudPaper Elevation="8" Class="action-menu pa-2">
        <MudStack Spacing="1">
            <MudText Typo="Typo.caption" Class="mb-1">@CardName</MudText>

            @if (IsOwnCard && CurrentZone == ZoneType.Hand)
            {
                <MudButton Size="Size.Small" Variant="Variant.Text" FullWidth="true"
                           StartIcon="@Icons.Material.Filled.PlayArrow"
                           OnClick="() => OnPlay.InvokeAsync()">Play</MudButton>
            }

            @if (IsOwnCard && CurrentZone == ZoneType.Battlefield)
            {
                <MudButton Size="Size.Small" Variant="Variant.Text" FullWidth="true"
                           StartIcon="@(IsTapped ? Icons.Material.Filled.Undo : Icons.Material.Filled.RotateRight)"
                           OnClick="() => OnTapToggle.InvokeAsync()">
                    @(IsTapped ? "Untap" : "Tap")
                </MudButton>
            }

            <MudDivider Class="my-1" />
            <MudText Typo="Typo.caption" Color="Color.Secondary">Move to...</MudText>

            @foreach (var zone in AvailableZones)
            {
                <MudButton Size="Size.Small" Variant="Variant.Text" FullWidth="true"
                           OnClick="() => OnMoveTo.InvokeAsync(zone)">@zone</MudButton>
            }

            <MudDivider Class="my-1" />
            <MudButton Size="Size.Small" Variant="Variant.Text" FullWidth="true"
                       Color="Color.Default"
                       OnClick="() => OnClose.InvokeAsync()">Cancel</MudButton>
        </MudStack>
    </MudPaper>
}

@code {
    [Parameter] public bool Visible { get; set; }
    [Parameter] public string CardName { get; set; } = "";
    [Parameter] public ZoneType CurrentZone { get; set; }
    [Parameter] public bool IsOwnCard { get; set; }
    [Parameter] public bool IsTapped { get; set; }
    [Parameter] public EventCallback OnPlay { get; set; }
    [Parameter] public EventCallback OnTapToggle { get; set; }
    [Parameter] public EventCallback<ZoneType> OnMoveTo { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    private IEnumerable<ZoneType> AvailableZones =>
        Enum.GetValues<ZoneType>().Where(z => z != CurrentZone);
}
```

**Step 2: Build, then commit**

```bash
dotnet build src/MtgDecker.Web/
git add src/MtgDecker.Web/Components/Pages/Game/ActionMenu.razor
git commit -m "feat(web): add ActionMenu component with context-sensitive actions"
```

---

### Task 8: GameLogPanel Component

**Files:**
- Create: `src/MtgDecker.Web/Components/Pages/Game/GameLogPanel.razor`
- Create: `src/MtgDecker.Web/Components/Pages/Game/GameLogPanel.razor.css`

**Step 1: Create GameLogPanel.razor**

```razor
@using MtgDecker.Engine
@namespace MtgDecker.Web.Components.Pages.Game

<MudPaper Class="game-log-panel pa-3" Elevation="2">
    <MudText Typo="Typo.subtitle2" Class="mb-2">Game Log</MudText>
    <div class="log-entries" @ref="_logContainer">
        @if (GameLog != null)
        {
            @foreach (var entry in GameLog)
            {
                <div class="log-entry">@entry</div>
            }
        }
    </div>
</MudPaper>

@code {
    [Parameter] public List<string>? GameLog { get; set; }

    private ElementReference _logContainer;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (GameLog?.Count > 0)
        {
            await JS.InvokeVoidAsync("scrollToBottom", _logContainer);
        }
    }

    [Inject] private IJSRuntime JS { get; set; } = default!;
}
```

**Step 2: Create GameLogPanel.razor.css**

```css
.game-log-panel {
    height: 100%;
    display: flex;
    flex-direction: column;
    background-color: var(--mud-palette-surface) !important;
}

.log-entries {
    flex: 1;
    overflow-y: auto;
    font-family: 'Cascadia Mono', 'Consolas', monospace;
    font-size: 0.75rem;
    line-height: 1.4;
}

.log-entry {
    padding: 2px 0;
    border-bottom: 1px solid rgba(255, 255, 255, 0.05);
}
```

**Note:** A small JS interop function `scrollToBottom` is needed. Add to `App.razor` or a separate JS file:

```javascript
window.scrollToBottom = (element) => {
    if (element) element.scrollTop = element.scrollHeight;
};
```

**Step 3: Build, then commit**

```bash
dotnet build src/MtgDecker.Web/
git add src/MtgDecker.Web/Components/Pages/Game/GameLogPanel.razor src/MtgDecker.Web/Components/Pages/Game/GameLogPanel.razor.css
git commit -m "feat(web): add GameLogPanel component with auto-scroll"
```

---

### Task 9: PlayerZone Component

**Files:**
- Create: `src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor`
- Create: `src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor.css`

**Context:** Renders one player's zones: battlefield (grid of cards), hand (row of cards), graveyard (pile showing count). Used twice on the game board — once for local player (face-up hand), once for opponent (card-back hand). Click a card to select it and show ActionMenu.

**Step 1: Create PlayerZone.razor**

```razor
@using MtgDecker.Engine
@using MtgDecker.Engine.Enums
@namespace MtgDecker.Web.Components.Pages.Game

<div class="player-zone @(IsOpponent ? "opponent" : "local")">
    <div class="zone-header">
        <MudText Typo="Typo.subtitle2">@PlayerName</MudText>
        @if (IsActivePlayer)
        {
            <MudChip T="string" Size="Size.Small" Color="Color.Primary">Active</MudChip>
        }
    </div>

    @* Battlefield *@
    <div class="zone battlefield">
        @foreach (var card in Battlefield)
        {
            <div style="position: relative; display: inline-block;">
                <CardDisplay Name="@card.Name"
                             ImageUrl="@card.ImageUrl"
                             Tapped="@card.IsTapped"
                             Selected="@(SelectedCard?.Id == card.Id)"
                             OnClick="() => SelectCard(card, ZoneType.Battlefield)" />
            </div>
        }
        @if (Battlefield.Count == 0)
        {
            <MudText Typo="Typo.caption" Class="empty-zone">Battlefield</MudText>
        }
    </div>

    @* Hand *@
    <div class="zone hand">
        @if (IsOpponent)
        {
            @for (int i = 0; i < Hand.Count; i++)
            {
                <CardDisplay Name="Card" IsBack="true" Clickable="@CanAct"
                             OnClick="() => SelectCard(Hand[i], ZoneType.Hand)" />
            }
        }
        else
        {
            @foreach (var card in Hand)
            {
                <CardDisplay Name="@card.Name"
                             ImageUrl="@card.ImageUrl"
                             Selected="@(SelectedCard?.Id == card.Id)"
                             OnClick="() => SelectCard(card, ZoneType.Hand)" />
            }
        }
        @if (Hand.Count == 0)
        {
            <MudText Typo="Typo.caption" Class="empty-zone">Hand (empty)</MudText>
        }
        else if (IsOpponent)
        {
            <MudText Typo="Typo.caption" Class="ml-2">@Hand.Count cards</MudText>
        }
    </div>

    @* Graveyard *@
    <div class="zone graveyard">
        <MudText Typo="Typo.caption">Graveyard (@Graveyard.Count)</MudText>
        @if (Graveyard.Count > 0)
        {
            <CardDisplay Name="@Graveyard[^1].Name"
                         ImageUrl="@Graveyard[^1].ImageUrl"
                         Selected="@(SelectedCard?.Id == Graveyard[^1].Id)"
                         OnClick="() => SelectCard(Graveyard[^1], ZoneType.Graveyard)" />
        }
    </div>

    @* Action Menu *@
    @if (SelectedCard != null)
    {
        <ActionMenu Visible="true"
                    CardName="@SelectedCard.Name"
                    CurrentZone="_selectedZone"
                    IsOwnCard="@(!IsOpponent)"
                    IsTapped="@SelectedCard.IsTapped"
                    OnPlay="HandlePlay"
                    OnTapToggle="HandleTapToggle"
                    OnMoveTo="HandleMoveTo"
                    OnClose="ClearSelection" />
    }
</div>

@code {
    [Parameter] public string PlayerName { get; set; } = "";
    [Parameter] public IReadOnlyList<GameCard> Battlefield { get; set; } = Array.Empty<GameCard>();
    [Parameter] public IReadOnlyList<GameCard> Hand { get; set; } = Array.Empty<GameCard>();
    [Parameter] public IReadOnlyList<GameCard> Graveyard { get; set; } = Array.Empty<GameCard>();
    [Parameter] public bool IsOpponent { get; set; }
    [Parameter] public bool IsActivePlayer { get; set; }
    [Parameter] public bool CanAct { get; set; }
    [Parameter] public Guid PlayerId { get; set; }
    [Parameter] public EventCallback<GameAction> OnAction { get; set; }

    private GameCard? SelectedCard;
    private ZoneType _selectedZone;

    private void SelectCard(GameCard card, ZoneType zone)
    {
        if (!CanAct && !IsOpponent) return;
        if (SelectedCard?.Id == card.Id)
        {
            ClearSelection();
            return;
        }
        SelectedCard = card;
        _selectedZone = zone;
    }

    private async Task HandlePlay()
    {
        if (SelectedCard == null) return;
        await OnAction.InvokeAsync(GameAction.PlayCard(PlayerId, SelectedCard.Id));
        ClearSelection();
    }

    private async Task HandleTapToggle()
    {
        if (SelectedCard == null) return;
        var action = SelectedCard.IsTapped
            ? GameAction.UntapCard(PlayerId, SelectedCard.Id)
            : GameAction.TapCard(PlayerId, SelectedCard.Id);
        await OnAction.InvokeAsync(action);
        ClearSelection();
    }

    private async Task HandleMoveTo(ZoneType destination)
    {
        if (SelectedCard == null) return;
        await OnAction.InvokeAsync(
            GameAction.MoveCard(PlayerId, SelectedCard.Id, _selectedZone, destination));
        ClearSelection();
    }

    private void ClearSelection()
    {
        SelectedCard = null;
    }
}
```

**Step 2: Create PlayerZone.razor.css**

```css
.player-zone {
    padding: 8px;
}

.zone-header {
    display: flex;
    align-items: center;
    gap: 8px;
    margin-bottom: 4px;
}

.zone {
    min-height: 60px;
    padding: 4px;
    margin-bottom: 4px;
}

.battlefield {
    min-height: 160px;
    border: 1px dashed rgba(255, 255, 255, 0.1);
    border-radius: 8px;
    padding: 8px;
    display: flex;
    flex-wrap: wrap;
    align-items: flex-start;
}

.hand {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
}

.graveyard {
    display: flex;
    align-items: center;
    gap: 8px;
}

.empty-zone {
    opacity: 0.3;
    padding: 40px;
}

.opponent .battlefield {
    border-color: rgba(255, 100, 100, 0.15);
}
```

**Step 3: Build, then commit**

```bash
dotnet build src/MtgDecker.Web/
git add src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor.css
git commit -m "feat(web): add PlayerZone component with battlefield, hand, graveyard"
```

---

### Task 10: MulliganDialog

**Files:**
- Create: `src/MtgDecker.Web/Components/Pages/Game/MulliganDialog.razor`

**Context:** Modal dialog for the London mulligan. Shows hand face-up. "Keep" or "Mulligan" buttons. After mulliganing and keeping, shows a card selection UI to pick cards to put on bottom.

**Step 1: Create MulliganDialog.razor**

```razor
@using MtgDecker.Engine
@using MtgDecker.Engine.Enums
@namespace MtgDecker.Web.Components.Pages.Game

<MudDialog>
    <TitleContent>
        <MudText Typo="Typo.h6">Mulligan Decision</MudText>
    </TitleContent>
    <DialogContent>
        @if (_choosingBottomCards)
        {
            <MudText Typo="Typo.body2" Class="mb-2">
                Select @_bottomCount card(s) to put on the bottom of your library.
                Selected: @_selectedBottom.Count / @_bottomCount
            </MudText>
            <div style="display: flex; flex-wrap: wrap; gap: 8px;">
                @foreach (var card in Hand)
                {
                    <div @onclick="() => ToggleBottomCard(card)"
                         style="cursor: pointer; outline: @(_selectedBottom.Contains(card) ? "3px solid #C69B3C" : "none"); border-radius: 6px;">
                        <CardDisplay Name="@card.Name" ImageUrl="@card.ImageUrl" Clickable="false" />
                    </div>
                }
            </div>
        }
        else
        {
            <MudText Typo="Typo.body2" Class="mb-2">
                @if (MulliganCount == 0)
                {
                    <span>Your opening hand (@Hand.Count cards):</span>
                }
                else
                {
                    <span>Mulligan #@MulliganCount — you drew 7, will keep @(7 - MulliganCount):</span>
                }
            </MudText>
            <div style="display: flex; flex-wrap: wrap; gap: 8px;">
                @foreach (var card in Hand)
                {
                    <CardDisplay Name="@card.Name" ImageUrl="@card.ImageUrl" Clickable="false" />
                }
            </div>
        }
    </DialogContent>
    <DialogActions>
        @if (_choosingBottomCards)
        {
            <MudButton Variant="Variant.Filled" Color="Color.Primary"
                       Disabled="@(_selectedBottom.Count != _bottomCount)"
                       OnClick="ConfirmBottomCards">Confirm</MudButton>
        }
        else
        {
            <MudButton Variant="Variant.Filled" Color="Color.Primary"
                       OnClick="KeepHand">Keep</MudButton>
            <MudButton Variant="Variant.Outlined" Color="Color.Warning"
                       Disabled="@(MulliganCount >= 6)"
                       OnClick="DoMulligan">Mulligan</MudButton>
        }
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;
    [Parameter] public IReadOnlyList<GameCard> Hand { get; set; } = Array.Empty<GameCard>();
    [Parameter] public int MulliganCount { get; set; }
    [Parameter] public InteractiveDecisionHandler Handler { get; set; } = default!;

    private bool _choosingBottomCards;
    private int _bottomCount;
    private readonly List<GameCard> _selectedBottom = new();

    private void KeepHand()
    {
        if (MulliganCount > 0)
        {
            _bottomCount = MulliganCount;
            _choosingBottomCards = true;
        }
        else
        {
            Handler.SubmitMulliganDecision(MulliganDecision.Keep);
            MudDialog.Close();
        }
    }

    private void DoMulligan()
    {
        Handler.SubmitMulliganDecision(MulliganDecision.Mulligan);
        MudDialog.Close();
    }

    private void ToggleBottomCard(GameCard card)
    {
        if (_selectedBottom.Contains(card))
            _selectedBottom.Remove(card);
        else if (_selectedBottom.Count < _bottomCount)
            _selectedBottom.Add(card);
    }

    private void ConfirmBottomCards()
    {
        Handler.SubmitMulliganDecision(MulliganDecision.Keep);
        Handler.SubmitBottomCards(_selectedBottom);
        MudDialog.Close();
    }
}
```

**Step 2: Build, then commit**

```bash
dotnet build src/MtgDecker.Web/
git add src/MtgDecker.Web/Components/Pages/Game/MulliganDialog.razor
git commit -m "feat(web): add MulliganDialog with keep/mulligan and bottom card selection"
```

---

### Task 11: GameLobby Page

**Files:**
- Create: `src/MtgDecker.Web/Components/Pages/GameLobby.razor`

**Context:** Entry point for games. Create a new game (pick deck, get game code) or join an existing game (enter code, pick deck). Uses existing `ListDecksQuery` to populate deck dropdown. When both players have joined, navigates to the game board.

**Step 1: Create GameLobby.razor**

```razor
@page "/game/new"
@page "/game/join/{GameId}"
@rendermode InteractiveServer
@using MtgDecker.Engine
@using MtgDecker.Domain.Entities
@inject GameSessionManager SessionManager
@inject IMediator Mediator
@inject NavigationManager Navigation

<PageTitle>Play Game - MtgDecker</PageTitle>

<MudContainer MaxWidth="MaxWidth.Small" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Play Game</MudText>

    @if (_errorMessage != null)
    {
        <MudAlert Severity="Severity.Error" Class="mb-4">@_errorMessage</MudAlert>
    }

    @if (_gameId == null)
    {
        @* Create or Join *@
        <MudGrid>
            <MudItem xs="12" md="6">
                <MudPaper Class="pa-4">
                    <MudText Typo="Typo.h6" Class="mb-3">Create New Game</MudText>
                    <MudSelect T="Guid?" Label="Select Your Deck" @bind-Value="_selectedDeckId" Class="mb-3">
                        @foreach (var deck in _decks)
                        {
                            <MudSelectItem T="Guid?" Value="@deck.Id">@deck.Name (@deck.Format)</MudSelectItem>
                        }
                    </MudSelect>
                    <MudTextField @bind-Value="_playerName" Label="Your Name" Class="mb-3" />
                    <MudButton Variant="Variant.Filled" Color="Color.Primary" FullWidth="true"
                               Disabled="@(_selectedDeckId == null || string.IsNullOrWhiteSpace(_playerName))"
                               OnClick="CreateGame">Create Game</MudButton>
                </MudPaper>
            </MudItem>

            <MudItem xs="12" md="6">
                <MudPaper Class="pa-4">
                    <MudText Typo="Typo.h6" Class="mb-3">Join Existing Game</MudText>
                    <MudTextField @bind-Value="_joinGameId" Label="Game Code" Class="mb-3" />
                    <MudSelect T="Guid?" Label="Select Your Deck" @bind-Value="_selectedDeckId" Class="mb-3">
                        @foreach (var deck in _decks)
                        {
                            <MudSelectItem T="Guid?" Value="@deck.Id">@deck.Name (@deck.Format)</MudSelectItem>
                        }
                    </MudSelect>
                    <MudTextField @bind-Value="_playerName" Label="Your Name" Class="mb-3" />
                    <MudButton Variant="Variant.Filled" Color="Color.Secondary" FullWidth="true"
                               Disabled="@(string.IsNullOrWhiteSpace(_joinGameId) || _selectedDeckId == null || string.IsNullOrWhiteSpace(_playerName))"
                               OnClick="JoinGame">Join Game</MudButton>
                </MudPaper>
            </MudItem>
        </MudGrid>
    }
    else
    {
        @* Waiting for opponent *@
        <MudPaper Class="pa-6 text-center">
            <MudText Typo="Typo.h5" Class="mb-2">Game Created!</MudText>
            <MudText Typo="Typo.body1" Class="mb-4">Share this code with your opponent:</MudText>
            <MudText Typo="Typo.h3" Color="Color.Secondary" Class="mb-4"
                     Style="font-family: monospace; letter-spacing: 8px;">@_gameId</MudText>
            <MudText Typo="Typo.body2" Class="mb-2">
                Or share this link: @(Navigation.BaseUri)game/join/@_gameId
            </MudText>
            <MudProgressLinear Indeterminate="true" Class="mt-4" />
            <MudText Typo="Typo.body2" Class="mt-2">Waiting for opponent to join...</MudText>
        </MudPaper>
    }
</MudContainer>

@code {
    [Parameter] public string? GameId { get; set; }

    private List<Deck> _decks = new();
    private Guid? _selectedDeckId;
    private string _playerName = "";
    private string? _joinGameId;
    private string? _gameId;
    private string? _errorMessage;

    // Hardcoded UserId matching existing app pattern
    private static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    protected override async Task OnInitializedAsync()
    {
        _decks = await Mediator.Send(new MtgDecker.Application.Decks.ListDecksQuery(UserId));

        if (GameId != null)
        {
            _joinGameId = GameId;
        }
    }

    private async Task CreateGame()
    {
        var deck = await LoadGameDeck(_selectedDeckId!.Value);
        if (deck == null) return;

        var session = SessionManager.CreateGame();
        session.JoinPlayer(_playerName, deck);
        _gameId = session.GameId;

        // Poll for second player (simple approach)
        _ = WaitForOpponentAsync(session);
    }

    private async Task JoinGame()
    {
        var session = SessionManager.GetSession(_joinGameId!);
        if (session == null)
        {
            _errorMessage = "Game not found.";
            return;
        }
        if (session.IsFull)
        {
            _errorMessage = "Game is already full.";
            return;
        }

        var deck = await LoadGameDeck(_selectedDeckId!.Value);
        if (deck == null) return;

        session.JoinPlayer(_playerName, deck);
        Navigation.NavigateTo($"/game/{session.GameId}");
    }

    private async Task WaitForOpponentAsync(GameSession session)
    {
        while (!session.IsFull)
        {
            await Task.Delay(1000);
        }
        await InvokeAsync(() => Navigation.NavigateTo($"/game/{session.GameId}"));
    }

    private async Task<List<GameCard>?> LoadGameDeck(Guid deckId)
    {
        var deck = await Mediator.Send(new MtgDecker.Application.Decks.GetDeckQuery(deckId));
        if (deck == null)
        {
            _errorMessage = "Deck not found.";
            return null;
        }

        var cardIds = deck.Entries.Select(e => e.CardId).Distinct().ToList();
        var cards = await Mediator.Send(new MtgDecker.Application.Cards.GetCardsByIdsQuery(cardIds));
        var cardMap = cards.ToDictionary(c => c.Id);

        var gameCards = new List<GameCard>();
        foreach (var entry in deck.Entries.Where(e => e.Category == MtgDecker.Domain.Enums.DeckCategory.MainDeck))
        {
            if (!cardMap.TryGetValue(entry.CardId, out var card)) continue;
            for (int i = 0; i < entry.Quantity; i++)
            {
                gameCards.Add(new GameCard
                {
                    Name = card.Name,
                    TypeLine = card.TypeLine,
                    ImageUrl = card.ImageUriSmall ?? card.ImageUri
                });
            }
        }

        return gameCards;
    }
}
```

**Step 2: Build, then commit**

```bash
dotnet build src/MtgDecker.Web/
git add src/MtgDecker.Web/Components/Pages/GameLobby.razor
git commit -m "feat(web): add GameLobby page with create/join game flow"
```

---

### Task 12: GamePage + GameBoard

**Files:**
- Create: `src/MtgDecker.Web/Components/Pages/GamePage.razor`
- Create: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor`
- Create: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor.css`

**Context:** `GamePage` is the route handler — it resolves the player's seat and manages the connection to the GameSession. `GameBoard` is the main UI component that assembles PlayerZone, GameLogPanel, phase indicator, and action buttons.

**Step 1: Create GamePage.razor**

```razor
@page "/game/{GameId}"
@rendermode InteractiveServer
@using MtgDecker.Engine
@using MtgDecker.Engine.Enums
@using MtgDecker.Web.Components.Pages.Game
@inject GameSessionManager SessionManager
@inject IDialogService DialogService
@implements IDisposable

<PageTitle>Game @GameId - MtgDecker</PageTitle>

@if (_session == null)
{
    <MudAlert Severity="Severity.Error" Class="ma-4">Game not found.</MudAlert>
}
else if (!_session.IsStarted && !_gameStarting)
{
    <MudContainer MaxWidth="MaxWidth.Small" Class="mt-4 text-center">
        <MudText Typo="Typo.h5">Starting game...</MudText>
        <MudProgressLinear Indeterminate="true" Class="mt-4" />
    </MudContainer>
}
else if (_session.State != null)
{
    <GameBoard State="_session.State"
               PlayerSeat="_playerSeat"
               Handler="_session.GetHandler(_playerSeat)"
               OnAction="HandleAction"
               OnSurrender="HandleSurrender"
               IsGameOver="_session.IsGameOver"
               Winner="_session.Winner" />
}

@code {
    [Parameter] public string GameId { get; set; } = "";

    private GameSession? _session;
    private int _playerSeat;
    private bool _gameStarting;
    private bool _mulliganDialogOpen;

    protected override async Task OnInitializedAsync()
    {
        _session = SessionManager.GetSession(GameId);
        if (_session == null) return;

        // Determine player seat (1 or 2 based on connection order)
        // For two-tab localhost: first navigator = P1, second = P2
        // If session already started, this is a reconnect — determine seat from URL param or session state
        _playerSeat = _session.IsStarted ? 2 : (_session.IsFull ? 2 : 1);

        _session.OnStateChanged += HandleStateChanged;

        if (_session.IsFull && !_session.IsStarted)
        {
            _gameStarting = true;
            await _session.StartAsync();
            _ = WatchForMulliganAsync();
        }
    }

    private async Task WatchForMulliganAsync()
    {
        var handler = _session!.GetHandler(_playerSeat);
        if (handler == null) return;

        // Wait for the engine to request mulligan decision
        while (!handler.IsWaitingForMulligan && !_session.IsGameOver)
        {
            await Task.Delay(50);
        }

        if (handler.IsWaitingForMulligan)
        {
            await InvokeAsync(async () =>
            {
                await ShowMulliganDialog(0);
            });
        }
    }

    private async Task ShowMulliganDialog(int mulliganCount)
    {
        if (_mulliganDialogOpen) return;
        _mulliganDialogOpen = true;

        var handler = _session!.GetHandler(_playerSeat)!;
        var player = _playerSeat == 1 ? _session.State!.Player1 : _session.State!.Player2;

        var parameters = new DialogParameters
        {
            { nameof(MulliganDialog.Hand), player.Hand.Cards },
            { nameof(MulliganDialog.MulliganCount), mulliganCount },
            { nameof(MulliganDialog.Handler), handler }
        };

        var options = new DialogOptions
        {
            DisableBackdropClick = true,
            CloseOnEscapeKey = false,
            MaxWidth = MaxWidth.Medium
        };

        var dialog = await DialogService.ShowAsync<MulliganDialog>("Mulligan", parameters, options);
        await dialog.Result;
        _mulliganDialogOpen = false;

        // Check if handler is waiting for another mulligan (player chose to mulligan)
        await Task.Delay(100); // Let engine process
        if (handler.IsWaitingForMulligan)
        {
            await ShowMulliganDialog(mulliganCount + 1);
        }
    }

    private void HandleAction(GameAction action)
    {
        var handler = _session?.GetHandler(_playerSeat);
        handler?.SubmitAction(action);
    }

    private void HandleSurrender()
    {
        _session?.Surrender(_playerSeat);
    }

    private void HandleStateChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        if (_session != null)
        {
            _session.OnStateChanged -= HandleStateChanged;
        }
    }
}
```

**Step 2: Create GameBoard.razor**

```razor
@using MtgDecker.Engine
@using MtgDecker.Engine.Enums
@namespace MtgDecker.Web.Components.Pages.Game

<div class="game-board">
    @if (IsGameOver)
    {
        <div class="game-over-overlay">
            <MudPaper Class="pa-6 text-center" Elevation="8">
                <MudText Typo="Typo.h4" Class="mb-2">Game Over</MudText>
                @if (Winner != null)
                {
                    <MudText Typo="Typo.h5" Color="Color.Secondary">@Winner wins!</MudText>
                }
                <MudButton Variant="Variant.Filled" Color="Color.Primary" Class="mt-4"
                           Href="/game/new">New Game</MudButton>
            </MudPaper>
        </div>
    }

    <div class="board-main">
        @* Opponent zone (top) *@
        <PlayerZone PlayerName="@OpponentPlayer.Name"
                    Battlefield="@OpponentPlayer.Battlefield.Cards"
                    Hand="@OpponentPlayer.Hand.Cards"
                    Graveyard="@OpponentPlayer.Graveyard.Cards"
                    IsOpponent="true"
                    IsActivePlayer="@(State.ActivePlayer == OpponentPlayer)"
                    CanAct="@HasPriority"
                    PlayerId="@OpponentPlayer.Id"
                    OnAction="OnAction" />

        @* Turn bar (middle) *@
        <MudPaper Class="turn-bar pa-2" Elevation="2">
            <div class="turn-bar-content">
                <MudText Typo="Typo.body2">
                    Turn @State.TurnNumber — @State.ActivePlayer.Name's turn
                </MudText>
                <MudChip T="string" Size="Size.Small" Color="Color.Primary">@State.CurrentPhase</MudChip>
                @if (HasPriority)
                {
                    <MudChip T="string" Size="Size.Small" Color="Color.Success">Your Priority</MudChip>
                    <MudButton Size="Size.Small" Variant="Variant.Filled" Color="Color.Primary"
                               OnClick="PassPriority">Pass Priority</MudButton>
                }
                else
                {
                    <MudChip T="string" Size="Size.Small" Color="Color.Default">Waiting...</MudChip>
                }
                <MudSpacer />
                <MudButton Size="Size.Small" Variant="Variant.Outlined" Color="Color.Error"
                           OnClick="() => OnSurrender.InvokeAsync()">Surrender</MudButton>
            </div>
        </MudPaper>

        @* Local player zone (bottom) *@
        <PlayerZone PlayerName="@LocalPlayer.Name"
                    Battlefield="@LocalPlayer.Battlefield.Cards"
                    Hand="@LocalPlayer.Hand.Cards"
                    Graveyard="@LocalPlayer.Graveyard.Cards"
                    IsOpponent="false"
                    IsActivePlayer="@(State.ActivePlayer == LocalPlayer)"
                    CanAct="@HasPriority"
                    PlayerId="@LocalPlayer.Id"
                    OnAction="OnAction" />
    </div>

    @* Game log (right) *@
    <GameLogPanel GameLog="@State.GameLog" />
</div>

@code {
    [Parameter] public GameState State { get; set; } = default!;
    [Parameter] public int PlayerSeat { get; set; }
    [Parameter] public InteractiveDecisionHandler? Handler { get; set; }
    [Parameter] public EventCallback<GameAction> OnAction { get; set; }
    [Parameter] public EventCallback OnSurrender { get; set; }
    [Parameter] public bool IsGameOver { get; set; }
    [Parameter] public string? Winner { get; set; }

    private Player LocalPlayer => PlayerSeat == 1 ? State.Player1 : State.Player2;
    private Player OpponentPlayer => PlayerSeat == 1 ? State.Player2 : State.Player1;
    private bool HasPriority => Handler?.IsWaitingForAction == true;

    private async Task PassPriority()
    {
        await OnAction.InvokeAsync(GameAction.Pass(LocalPlayer.Id));
    }
}
```

**Step 3: Create GameBoard.razor.css**

```css
.game-board {
    display: grid;
    grid-template-columns: 1fr 300px;
    height: calc(100vh - 80px);
    gap: 8px;
    padding: 8px;
}

.board-main {
    display: flex;
    flex-direction: column;
    gap: 4px;
    overflow: auto;
}

.turn-bar {
    flex-shrink: 0;
}

.turn-bar-content {
    display: flex;
    align-items: center;
    gap: 8px;
    flex-wrap: wrap;
}

.game-over-overlay {
    position: fixed;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    background: rgba(0, 0, 0, 0.7);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 9999;
}
```

**Step 4: Add scroll JS interop to App.razor**

In `src/MtgDecker.Web/Components/App.razor`, add before `</body>`:

```html
<script>
    window.scrollToBottom = (element) => {
        if (element) element.scrollTop = element.scrollHeight;
    };
</script>
```

**Step 5: Build**

```bash
dotnet build src/MtgDecker.Web/
```

**Step 6: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/GamePage.razor src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor.css src/MtgDecker.Web/Components/App.razor
git commit -m "feat(web): add GamePage and GameBoard with full game UI"
```

---

### Task 13: Manual Integration Testing

**Context:** Run the app, open two browser tabs, create a game, join, play through mulligan, play some turns, and verify everything works. Fix any issues found.

**Step 1: Run the app**

```bash
dotnet run --project src/MtgDecker.Web/
```

**Step 2: Test flow**

1. Open browser to `https://localhost:5001/game/new` (or the port shown)
2. Tab 1: Select a deck, enter name "Alice", click "Create Game"
3. Note the game code shown
4. Tab 2: Navigate to `https://localhost:5001/game/join/{code}`
5. Tab 2: Select a deck, enter name "Bob", click "Join Game"
6. Both tabs should show the game board
7. Both tabs should show mulligan dialog
8. Click "Keep" in both tabs
9. Verify: game board shows, turn 1, Alice is active
10. Alice clicks a card in hand → action menu appears → click "Play"
11. Verify: card appears on Alice's battlefield in both tabs
12. Click "Pass Priority" in both tabs to advance phases
13. Verify: phases advance, turn ends, Bob becomes active
14. Click "Surrender" in one tab → verify game over overlay in both

**Step 3: Fix any issues found**

Common issues to watch for:
- Component not re-rendering (missing `InvokeAsync(StateHasChanged)`)
- Card images not loading (check `ImageUrl` vs `ImageUri` mapping)
- Mulligan dialog not appearing (timing issue with `WatchForMulliganAsync`)
- Action menu not working (check `GameAction` factory methods)
- Priority not passing correctly (check handler `IsWaitingForAction`)

**Step 4: Run all engine tests to verify no regressions**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ -v normal
```

**Step 5: Final commit**

```bash
git add -A
git commit -m "feat(web): complete game UI integration with fixes"
```

---

## Dependency Order

```
Task 1 (GameState event)
  └→ Task 2 (InteractiveDecisionHandler)
       └→ Task 3 (GameSession)
            └→ Task 4 (GameSessionManager)
                 └→ Task 5 (Web wiring)
                      └→ Task 6-10 (UI components, can be parallel)
                           └→ Task 11 (GameLobby)
                                └→ Task 12 (GamePage + GameBoard)
                                     └→ Task 13 (Integration testing)
```

Tasks 6-10 (CardDisplay, ActionMenu, GameLogPanel, PlayerZone, MulliganDialog) are independent of each other and can be implemented in parallel or any order. They all depend on Task 5 (Web wiring) being done first.
