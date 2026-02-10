using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class ManaPoolClearingTests
{
    private (GameEngine engine, GameState state) CreateSetup()
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
        return (engine, state);
    }

    [Fact]
    public async Task ManaPool_ClearedAfterPhase()
    {
        var (engine, state) = CreateSetup();
        await engine.StartGameAsync();

        state.Player1.ManaPool.Add(ManaColor.Red, 3);
        state.Player1.ManaPool.Total.Should().Be(3);

        await engine.RunTurnAsync();

        state.Player1.ManaPool.Total.Should().Be(0);
    }

    [Fact]
    public async Task ManaPool_ClearedForBothPlayers()
    {
        var (engine, state) = CreateSetup();
        await engine.StartGameAsync();

        state.Player1.ManaPool.Add(ManaColor.Blue, 2);
        state.Player2.ManaPool.Add(ManaColor.Green, 4);

        await engine.RunTurnAsync();

        state.Player1.ManaPool.Total.Should().Be(0);
        state.Player2.ManaPool.Total.Should().Be(0);
    }

    [Fact]
    public async Task LandsPlayedThisTurn_ResetsAtTurnStart()
    {
        var (engine, state) = CreateSetup();
        await engine.StartGameAsync();

        state.ActivePlayer.LandsPlayedThisTurn = 1;

        var activeBeforeTurn = state.ActivePlayer;
        await engine.RunTurnAsync();

        activeBeforeTurn.LandsPlayedThisTurn.Should().Be(0);
    }
}
