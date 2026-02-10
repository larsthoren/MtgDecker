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
