using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class ExtraLandDropTests
{
    [Fact]
    public void Player_MaxLandDrops_Defaults_To_1()
    {
        var handler = new TestDecisionHandler();
        var player = new Player(Guid.NewGuid(), "Test", handler);
        player.MaxLandDrops.Should().Be(1);
    }

    [Fact]
    public async Task Player_Can_Play_Two_Lands_When_MaxLandDrops_Is_2()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        // Use Exploration to grant extra land drop (RecalculateState resets MaxLandDrops)
        var exploration = GameCard.Create("Exploration", "Enchantment");
        p1.Battlefield.Add(exploration);
        engine.RecalculateState();

        var land1 = new GameCard { Name = "Forest", CardTypes = CardType.Land };
        var land2 = new GameCard { Name = "Mountain", CardTypes = CardType.Land };
        p1.Hand.Add(land1);
        p1.Hand.Add(land2);

        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, land1.Id));
        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, land2.Id));

        p1.Battlefield.Cards.Should().HaveCount(3); // exploration + 2 lands
        p1.LandsPlayedThisTurn.Should().Be(2);
    }

    [Fact]
    public async Task Player_Cannot_Play_Third_Land_When_MaxLandDrops_Is_2()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        // Use Exploration to grant extra land drop (RecalculateState resets MaxLandDrops)
        var exploration = GameCard.Create("Exploration", "Enchantment");
        p1.Battlefield.Add(exploration);
        engine.RecalculateState();

        var land1 = new GameCard { Name = "F1", CardTypes = CardType.Land };
        var land2 = new GameCard { Name = "F2", CardTypes = CardType.Land };
        var land3 = new GameCard { Name = "F3", CardTypes = CardType.Land };
        p1.Hand.Add(land1);
        p1.Hand.Add(land2);
        p1.Hand.Add(land3);

        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, land1.Id));
        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, land2.Id));
        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, land3.Id));

        p1.Battlefield.Cards.Should().HaveCount(3); // exploration + 2 lands
        p1.Hand.Cards.Should().HaveCount(1);
    }

    [Fact]
    public void GameState_Has_ActiveEffects_List()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.ActiveEffects.Should().NotBeNull();
        state.ActiveEffects.Should().BeEmpty();
    }
}
