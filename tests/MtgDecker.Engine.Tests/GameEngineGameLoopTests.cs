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
        var libraryBefore = state.Player1.Library.Count;

        await engine.RunTurnAsync();

        // Draw happened (library decreased), but discard step trims hand back to 7
        state.Player1.Library.Count.Should().Be(libraryBefore - 1);
        state.Player1.Hand.Count.Should().Be(7, "discard step trims hand to max size");
        state.Player1.Graveyard.Count.Should().Be(1, "1 card discarded to hand size");
    }

    [Fact]
    public async Task RunTurnAsync_FirstPlayerSkipsDrawOnTurn1()
    {
        var engine = CreateEngineWithDecks(out var state, out _, out _);
        await engine.StartGameAsync();
        state.IsFirstTurn = true;
        var handBefore = state.Player1.Hand.Count;

        await engine.RunTurnAsync();

        state.Player1.Hand.Count.Should().Be(handBefore);
    }

    [Fact]
    public async Task RunTurnAsync_SecondTurn_PlayerDraws()
    {
        var engine = CreateEngineWithDecks(out var state, out _, out _);
        await engine.StartGameAsync();
        state.IsFirstTurn = true;

        await engine.RunTurnAsync();
        var libraryBefore = state.Player2.Library.Count;

        await engine.RunTurnAsync();

        // Draw happened (library decreased), discard step trims hand back to 7
        state.Player2.Library.Count.Should().Be(libraryBefore - 1);
        state.Player2.Hand.Count.Should().Be(7, "discard step trims hand to max size");
        state.Player2.Graveyard.Count.Should().Be(1, "1 card discarded to hand size");
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

        await engine.RunTurnAsync();
        await engine.RunTurnAsync();

        state.TurnNumber.Should().Be(3);
        state.ActivePlayer.Should().BeSameAs(state.Player1);
    }
}
