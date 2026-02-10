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
    public async Task ExecuteAction_PlayCard_MovesFromHandToBattlefield()
    {
        var engine = CreateEngine(out var state, out var p1);
        // Use a non-land card without ManaCost to use sandbox path
        var card = new GameCard { Name = "TestCard", TypeLine = "Creature" };
        p1.Hand.Add(card);

        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, card.Id));

        p1.Hand.Count.Should().Be(0);
        p1.Battlefield.Count.Should().Be(1);
        p1.Battlefield.Cards[0].Should().BeSameAs(card);
    }

    [Fact]
    public async Task ExecuteAction_PlayCard_LogsAction()
    {
        var engine = CreateEngine(out var state, out var p1);
        // Use a non-land card without ManaCost to use sandbox path
        var card = new GameCard { Name = "TestCard", TypeLine = "Creature" };
        p1.Hand.Add(card);

        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, card.Id));

        state.GameLog.Should().Contain(msg => msg.Contains("Alice") && msg.Contains("TestCard"));
    }

    [Fact]
    public async Task ExecuteAction_TapCard_TapsUntappedCard()
    {
        var engine = CreateEngine(out _, out var p1);
        var card = new GameCard { Name = "Forest", TypeLine = "Basic Land — Forest" };
        p1.Battlefield.Add(card);

        await engine.ExecuteAction(GameAction.TapCard(p1.Id, card.Id));

        card.IsTapped.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAction_TapCard_IgnoresAlreadyTapped()
    {
        var engine = CreateEngine(out var state, out var p1);
        var card = new GameCard { Name = "Forest", TypeLine = "Basic Land", IsTapped = true };
        p1.Battlefield.Add(card);
        state.GameLog.Clear();

        await engine.ExecuteAction(GameAction.TapCard(p1.Id, card.Id));

        card.IsTapped.Should().BeTrue();
        state.GameLog.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAction_UntapCard_UntapsTappedCard()
    {
        var engine = CreateEngine(out _, out var p1);
        var card = new GameCard { Name = "Forest", TypeLine = "Basic Land", IsTapped = true };
        p1.Battlefield.Add(card);

        await engine.ExecuteAction(GameAction.UntapCard(p1.Id, card.Id));

        card.IsTapped.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAction_UntapCard_IgnoresAlreadyUntapped()
    {
        var engine = CreateEngine(out var state, out var p1);
        var card = new GameCard { Name = "Forest", TypeLine = "Basic Land" };
        p1.Battlefield.Add(card);
        state.GameLog.Clear();

        await engine.ExecuteAction(GameAction.UntapCard(p1.Id, card.Id));

        card.IsTapped.Should().BeFalse();
        state.GameLog.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAction_MoveCard_MovesBetweenZones()
    {
        var engine = CreateEngine(out _, out var p1);
        var card = new GameCard { Name = "Bear", TypeLine = "Creature — Bear" };
        p1.Battlefield.Add(card);

        await engine.ExecuteAction(GameAction.MoveCard(p1.Id, card.Id, ZoneType.Battlefield, ZoneType.Graveyard));

        p1.Battlefield.Count.Should().Be(0);
        p1.Graveyard.Count.Should().Be(1);
        p1.Graveyard.Cards[0].Should().BeSameAs(card);
    }

    [Fact]
    public async Task ExecuteAction_UnknownPlayerId_Throws()
    {
        var engine = CreateEngine(out _, out _);
        var unknownId = Guid.NewGuid();

        var act = () => engine.ExecuteAction(GameAction.PlayCard(unknownId, Guid.NewGuid()));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{unknownId}*");
    }
}
