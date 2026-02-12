using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class DeckOutTests
{
    private static (GameEngine engine, GameState state) CreateGame(int p1LibrarySize, int p2LibrarySize)
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Player 1", handler1);
        var p2 = new Player(Guid.NewGuid(), "Player 2", handler2);

        for (int i = 0; i < p1LibrarySize; i++)
            p1.Library.Add(new GameCard { Name = $"Card {i + 1}" });
        for (int i = 0; i < p2LibrarySize; i++)
            p2.Library.Add(new GameCard { Name = $"Card {i + 1}" });

        var state = new GameState(p1, p2);
        return (new GameEngine(state), state);
    }

    [Fact]
    public void DrawPhase_EmptyLibrary_SetsGameOverAndWinner()
    {
        var (engine, state) = CreateGame(0, 5);

        engine.ExecuteTurnBasedAction(Phase.Draw);

        state.IsGameOver.Should().BeTrue();
        state.Winner.Should().Be("Player 2");
        state.GameLog.Should().Contain(l => l.Contains("loses") && l.Contains("draw from an empty library"));
    }

    [Fact]
    public void DrawPhase_NonEmptyLibrary_DoesNotEndGame()
    {
        var (engine, state) = CreateGame(3, 5);

        engine.ExecuteTurnBasedAction(Phase.Draw);

        state.IsGameOver.Should().BeFalse();
        state.Winner.Should().BeNull();
        state.Player1.Hand.Count.Should().Be(1);
    }

    [Fact]
    public void DrawCards_EmptyLibrary_MidDraw_SetsGameOver()
    {
        var (engine, state) = CreateGame(2, 5);

        engine.DrawCards(state.Player1, 5);

        state.IsGameOver.Should().BeTrue();
        state.Winner.Should().Be("Player 2");
        state.Player1.Hand.Count.Should().Be(2);
    }

    [Fact]
    public void DrawCards_SufficientLibrary_DoesNotEndGame()
    {
        var (engine, state) = CreateGame(5, 5);

        engine.DrawCards(state.Player1, 3);

        state.IsGameOver.Should().BeFalse();
        state.Player1.Hand.Count.Should().Be(3);
    }
}
