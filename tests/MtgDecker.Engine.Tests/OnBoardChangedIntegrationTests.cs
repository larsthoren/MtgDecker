using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class OnBoardChangedIntegrationTests
{
    [Fact]
    public void Goblin_King_Buffs_Apply_After_Direct_Zone_Add()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        var king = GameCard.Create("Goblin King", "Creature — Goblin");
        p1.Battlefield.Add(king);

        // Place a goblin creature directly onto the battlefield
        var grunt = new GameCard
        {
            Name = "Grunt", BasePower = 1, BaseToughness = 1,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };
        p1.Battlefield.Add(grunt);
        grunt.TurnEnteredBattlefield = state.TurnNumber;

        // Recalculate to apply buffs
        engine.RecalculateState();

        grunt.Power.Should().Be(2);
        grunt.Toughness.Should().Be(2);
    }

    [Fact]
    public void Haste_From_Warchief_Allows_Immediate_Attack()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.TurnNumber = 3;
        var engine = new GameEngine(state);

        var warchief = GameCard.Create("Goblin Warchief", "Creature — Goblin");
        warchief.TurnEnteredBattlefield = 1; // already in play
        p1.Battlefield.Add(warchief);

        // New goblin entering this turn
        var lackey = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        lackey.TurnEnteredBattlefield = 3; // just entered
        p1.Battlefield.Add(lackey);

        engine.RecalculateState();

        // Lackey has haste from Warchief — no summoning sickness
        lackey.HasSummoningSickness(3).Should().BeFalse();
    }

    [Fact]
    public async Task Exploration_Allows_Two_Land_Drops_Via_PlayCard()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        var exploration = GameCard.Create("Exploration", "Enchantment");
        p1.Battlefield.Add(exploration);
        engine.RecalculateState(); // must call first to set MaxLandDrops = 2

        var land1 = new GameCard { Name = "Forest", CardTypes = CardType.Land };
        var land2 = new GameCard { Name = "Mountain", CardTypes = CardType.Land };
        p1.Hand.Add(land1);
        p1.Hand.Add(land2);

        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, land1.Id));
        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, land2.Id));

        p1.Battlefield.Cards.Should().HaveCount(3); // exploration + 2 lands
        p1.LandsPlayedThisTurn.Should().Be(2);
    }
}
