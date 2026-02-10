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
