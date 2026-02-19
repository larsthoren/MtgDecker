using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

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

    [Fact]
    public void GameCard_Loyalty_ReadsFromLoyaltyCounters()
    {
        var card = new GameCard { CardTypes = CardType.Planeswalker };
        card.AddCounters(CounterType.Loyalty, 4);
        card.Loyalty.Should().Be(4);
    }

    [Fact]
    public void GameCard_Loyalty_ZeroWhenNoCounters()
    {
        var card = new GameCard { CardTypes = CardType.Planeswalker };
        card.Loyalty.Should().Be(0);
    }

    [Fact]
    public void CardDefinition_StartingLoyalty_CanBeSet()
    {
        var def = new CardDefinition(null, null, null, null, CardType.Planeswalker)
        {
            StartingLoyalty = 4,
        };
        def.StartingLoyalty.Should().Be(4);
    }

    [Fact]
    public async Task Planeswalker_EntersWithLoyaltyCounters_WhenCast()
    {
        // Register a test planeswalker in CardDefinitions for this test
        var testPwName = "Test Planeswalker ETB";
        CardDefinitions.Register(new CardDefinition(
            ManaCost.Parse("{2}{U}{U}"), null, null, null, CardType.Planeswalker)
        {
            Name = testPwName,
            StartingLoyalty = 4,
        });

        try
        {
            var handler = new TestDecisionHandler();
            var state = new GameState(
                new Player(Guid.NewGuid(), "P1", handler),
                new Player(Guid.NewGuid(), "P2", handler));
            state.CurrentPhase = Phase.MainPhase1;

            var engine = new GameEngine(state);

            // Give player enough mana to cast (2UU)
            state.Player1.ManaPool.Add(ManaColor.Blue, 2);
            state.Player1.ManaPool.Add(ManaColor.Colorless, 2);

            var pw = GameCard.Create(testPwName, "Legendary Planeswalker — Test");
            state.Player1.Hand.Add(pw);

            // Cast the planeswalker
            await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, pw.Id));

            // The planeswalker should be on the battlefield with loyalty counters
            state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == pw.Id);
            pw.GetCounters(CounterType.Loyalty).Should().Be(4,
                "Planeswalker should enter with starting loyalty as loyalty counters");
            pw.Loyalty.Should().Be(4);
        }
        finally
        {
            CardDefinitions.Unregister(testPwName);
        }
    }
}
