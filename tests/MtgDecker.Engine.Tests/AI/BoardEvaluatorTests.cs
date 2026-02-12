using FluentAssertions;
using MtgDecker.Engine.AI;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests.AI;

public class BoardEvaluatorTests
{
    private static (GameState state, Player player, Player opponent) CreateGame()
    {
        var p1 = new Player(Guid.NewGuid(), "Player 1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        return (state, p1, p2);
    }

    [Fact]
    public void EmptyBoard_EqualLife_ReturnsZero()
    {
        var (state, player, _) = CreateGame();
        var score = BoardEvaluator.Evaluate(state, player);
        score.Should().Be(0.0);
    }

    [Fact]
    public void LifeAdvantage_IncreasesScore()
    {
        var (state, player, opponent) = CreateGame();
        opponent.AdjustLife(-10);
        var score = BoardEvaluator.Evaluate(state, player);
        score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CreatureOnBoard_IncreasesScore()
    {
        var (state, player, _) = CreateGame();
        player.Battlefield.Add(new GameCard { Name = "Bear", Power = 2, Toughness = 2, CardTypes = CardType.Creature });
        var score = BoardEvaluator.Evaluate(state, player);
        score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CardsInHand_IncreasesScore()
    {
        var (state, player, _) = CreateGame();
        player.Hand.Add(new GameCard { Name = "Card 1" });
        player.Hand.Add(new GameCard { Name = "Card 2" });
        var score = BoardEvaluator.Evaluate(state, player);
        score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void UntappedLands_IncreasesScore()
    {
        var (state, player, _) = CreateGame();
        player.Battlefield.Add(new GameCard { Name = "Mountain", CardTypes = CardType.Land, IsTapped = false });
        var score = BoardEvaluator.Evaluate(state, player);
        score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void OpponentCreatures_DecreasesScore()
    {
        var (state, player, opponent) = CreateGame();
        opponent.Battlefield.Add(new GameCard { Name = "Bear", Power = 2, Toughness = 2, CardTypes = CardType.Creature });
        var score = BoardEvaluator.Evaluate(state, player);
        score.Should().BeLessThan(0);
    }

    [Fact]
    public void Evaluate_IsSymmetric()
    {
        var (state, p1, p2) = CreateGame();
        p1.Battlefield.Add(new GameCard { Name = "Bear", Power = 2, Toughness = 2, CardTypes = CardType.Creature });
        var p1Score = BoardEvaluator.Evaluate(state, p1);
        var p2Score = BoardEvaluator.Evaluate(state, p2);
        p1Score.Should().BeGreaterThan(0);
        p2Score.Should().BeLessThan(0);
        p1Score.Should().BeApproximately(-p2Score, 0.001);
    }
}
