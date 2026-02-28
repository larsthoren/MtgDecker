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

    [Fact]
    public void Log_FiresOnStateChanged()
    {
        var state = new GameState(_player1, _player2);
        bool fired = false;
        state.OnStateChanged += () => fired = true;

        state.Log("test message");

        fired.Should().BeTrue();
    }

    [Fact]
    public void Players_ReturnsBothPlayers()
    {
        var state = TestHelper.CreateState();

        state.Players.Should().HaveCount(2);
        state.Players.Should().Contain(state.Player1);
        state.Players.Should().Contain(state.Player2);
    }

    [Fact]
    public void Players_ReturnsSameInstance()
    {
        var state = TestHelper.CreateState();

        state.Players.Should().BeSameAs(state.Players);
    }

    [Fact]
    public void GetCardController_ReturnsPlayer1_WhenCardOnPlayer1Battlefield()
    {
        var state = TestHelper.CreateState();
        var card = new GameCard { Name = "Test" };
        state.Player1.Battlefield.Add(card);

        state.GetCardController(card.Id).Should().Be(state.Player1);
    }

    [Fact]
    public void GetCardController_ReturnsPlayer2_WhenCardOnPlayer2Battlefield()
    {
        var state = TestHelper.CreateState();
        var card = new GameCard { Name = "Test" };
        state.Player2.Battlefield.Add(card);

        state.GetCardController(card.Id).Should().Be(state.Player2);
    }

    [Fact]
    public void GetCardController_ReturnsNull_WhenCardNotOnBattlefield()
    {
        var state = TestHelper.CreateState();

        state.GetCardController(Guid.NewGuid()).Should().BeNull();
    }

    [Fact]
    public void ComputeCostModification_ReturnsZero_WhenNoEffects()
    {
        var state = TestHelper.CreateState();
        var card = new GameCard { Name = "Lightning Bolt" };

        state.ComputeCostModification(card, state.Player1).Should().Be(0);
    }

    [Fact]
    public void ComputeCostModification_SumsApplicableEffects()
    {
        var state = TestHelper.CreateState();
        var card = new GameCard { Name = "Lightning Bolt", CardTypes = CardType.Instant };
        var source = new GameCard { Name = "Sphere of Resistance" };
        state.Player2.Battlefield.Add(source);

        state.ActiveEffects.Add(new ContinuousEffect(
            SourceId: source.Id,
            Type: ContinuousEffectType.ModifyCost,
            Applies: (_, _) => false,
            CostMod: 1,
            CostApplies: _ => true,
            CostAppliesToOpponent: true));

        state.ComputeCostModification(card, state.Player1).Should().Be(1);
    }

    [Fact]
    public void ComputeCostModification_SkipsOwnEffects_WhenCostAppliesToOpponent()
    {
        var state = TestHelper.CreateState();
        var card = new GameCard { Name = "Lightning Bolt" };
        var source = new GameCard { Name = "Sphere" };
        state.Player1.Battlefield.Add(source);

        state.ActiveEffects.Add(new ContinuousEffect(
            SourceId: source.Id,
            Type: ContinuousEffectType.ModifyCost,
            Applies: (_, _) => false,
            CostMod: 1,
            CostApplies: _ => true,
            CostAppliesToOpponent: true));

        // Player1 controls the source, so it shouldn't apply to Player1's spells
        state.ComputeCostModification(card, state.Player1).Should().Be(0);
    }

    [Fact]
    public async Task PerformDiscardAsync_MovesCardToGraveyard_WhenNoHandler()
    {
        var state = TestHelper.CreateState();
        var card = new GameCard { Name = "Test" };
        state.Player1.Hand.Add(card);

        await state.PerformDiscardAsync(card, state.Player1, state.Player2.Id);

        state.Player1.Hand.Cards.Should().NotContain(c => c.Id == card.Id);
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Id == card.Id);
        state.LastDiscardCausedByPlayerId.Should().Be(state.Player2.Id);
    }

    [Fact]
    public async Task PerformDiscardAsync_CallsHandler_WhenSet()
    {
        var state = TestHelper.CreateState();
        var card = new GameCard { Name = "Test" };
        state.Player1.Hand.Add(card);
        var handlerCalled = false;
        state.HandleDiscardAsync = (c, p, ct) => { handlerCalled = true; return Task.CompletedTask; };

        await state.PerformDiscardAsync(card, state.Player1, state.Player2.Id);

        handlerCalled.Should().BeTrue();
    }
}
