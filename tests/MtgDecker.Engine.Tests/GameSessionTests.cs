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

        Func<Task> act = () => session.StartAsync();
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
        session.Player1Handler!.SubmitMulliganDecision(MtgDecker.Engine.Enums.MulliganDecision.Keep);
        await Task.Delay(50);
        session.Player2Handler!.SubmitMulliganDecision(MtgDecker.Engine.Enums.MulliganDecision.Keep);
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

    [Fact]
    public async Task AdjustLife_ChangesPlayerLife()
    {
        var session = new GameSession("ABC123");
        session.JoinPlayer("Alice", CreateDeck());
        session.JoinPlayer("Bob", CreateDeck());
        await session.StartAsync();

        session.AdjustLife(1, -3);

        session.State!.Player1.Life.Should().Be(17);
    }

    [Fact]
    public async Task AdjustLife_CanAdjustOpponentLife()
    {
        var session = new GameSession("ABC123");
        session.JoinPlayer("Alice", CreateDeck());
        session.JoinPlayer("Bob", CreateDeck());
        await session.StartAsync();

        session.AdjustLife(2, -5);

        session.State!.Player2.Life.Should().Be(15);
    }

    [Fact]
    public async Task AdjustLife_AtZero_EndsGame()
    {
        var session = new GameSession("ABC123");
        session.JoinPlayer("Alice", CreateDeck());
        session.JoinPlayer("Bob", CreateDeck());
        await session.StartAsync();

        session.AdjustLife(1, -20);

        session.State!.Player1.Life.Should().Be(0);
        session.IsGameOver.Should().BeTrue();
        session.Winner.Should().Be("Bob");
    }

    [Fact]
    public async Task AdjustLife_BelowZero_EndsGame()
    {
        var session = new GameSession("ABC123");
        session.JoinPlayer("Alice", CreateDeck());
        session.JoinPlayer("Bob", CreateDeck());
        await session.StartAsync();

        session.AdjustLife(2, -25);

        session.State!.Player2.Life.Should().BeLessThanOrEqualTo(0);
        session.IsGameOver.Should().BeTrue();
        session.Winner.Should().Be("Alice");
    }

    [Fact]
    public async Task AdjustLife_LogsChange()
    {
        var session = new GameSession("ABC123");
        session.JoinPlayer("Alice", CreateDeck());
        session.JoinPlayer("Bob", CreateDeck());
        await session.StartAsync();

        session.AdjustLife(1, -3);

        session.State!.GameLog.Should().Contain(l => l.Contains("20") && l.Contains("17"));
    }
}
