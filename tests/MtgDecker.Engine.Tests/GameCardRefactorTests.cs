using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class GameCardRefactorTests
{
    [Fact]
    public void Power_Returns_BasePower_When_No_Effective_Set()
    {
        var card = new GameCard { BasePower = 3, BaseToughness = 4 };
        card.Power.Should().Be(3);
        card.Toughness.Should().Be(4);
    }

    [Fact]
    public void Power_Returns_EffectivePower_When_Set()
    {
        var card = new GameCard { BasePower = 3, BaseToughness = 4 };
        card.EffectivePower = 5;
        card.EffectiveToughness = 6;
        card.Power.Should().Be(5);
        card.Toughness.Should().Be(6);
    }

    [Fact]
    public void Power_Setter_Writes_To_BasePower()
    {
        var card = new GameCard();
        card.Power = 4;
        card.Toughness = 5;
        card.BasePower.Should().Be(4);
        card.BaseToughness.Should().Be(5);
    }

    [Fact]
    public void IsLegendary_Defaults_To_False()
    {
        var card = new GameCard();
        card.IsLegendary.Should().BeFalse();
    }

    [Fact]
    public void ActiveKeywords_Starts_Empty()
    {
        var card = new GameCard();
        card.ActiveKeywords.Should().BeEmpty();
    }

    [Fact]
    public void HasSummoningSickness_False_When_Has_Haste()
    {
        var card = new GameCard
        {
            BasePower = 2, BaseToughness = 2,
            CardTypes = CardType.Creature,
            TurnEnteredBattlefield = 5
        };
        card.ActiveKeywords.Add(Keyword.Haste);
        card.HasSummoningSickness(5).Should().BeFalse();
    }

    [Fact]
    public void HasSummoningSickness_True_Without_Haste()
    {
        var card = new GameCard
        {
            BasePower = 2, BaseToughness = 2,
            CardTypes = CardType.Creature,
            TurnEnteredBattlefield = 5
        };
        card.HasSummoningSickness(5).Should().BeTrue();
    }

    [Fact]
    public void FetchAbility_Can_Be_Set()
    {
        var card = new GameCard { FetchAbility = new FetchAbility(["Mountain", "Forest"]) };
        card.FetchAbility.Should().NotBeNull();
        card.FetchAbility!.SearchTypes.Should().Contain("Mountain");
    }
}
