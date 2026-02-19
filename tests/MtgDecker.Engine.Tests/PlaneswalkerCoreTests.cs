using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class PlaneswalkerCoreTests
{
    [Fact]
    public void CardType_Planeswalker_HasCorrectFlagValue()
    {
        ((int)CardType.Planeswalker).Should().Be(64);
    }

    [Fact]
    public void GameCard_IsPlaneswalker_TrueWhenPlaneswalkerType()
    {
        var card = new GameCard { CardTypes = CardType.Planeswalker };
        card.IsPlaneswalker.Should().BeTrue();
    }

    [Fact]
    public void GameCard_IsPlaneswalker_FalseForCreature()
    {
        var card = new GameCard { CardTypes = CardType.Creature };
        card.IsPlaneswalker.Should().BeFalse();
    }

    [Fact]
    public void GameCard_IsPlaneswalker_TrueWhenCombinedWithCreature()
    {
        var card = new GameCard { CardTypes = CardType.Creature | CardType.Planeswalker };
        card.IsPlaneswalker.Should().BeTrue();
        card.IsCreature.Should().BeTrue();
    }
}
