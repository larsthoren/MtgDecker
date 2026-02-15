using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class LegendaryRuleTests
{
    [Fact]
    public async Task Two_Legendaries_Same_Name_One_Goes_To_Graveyard()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var sanctum1 = new GameCard
        {
            Name = "Serra's Sanctum", CardTypes = CardType.Land, IsLegendary = true
        };
        var sanctum2 = new GameCard
        {
            Name = "Serra's Sanctum", CardTypes = CardType.Land, IsLegendary = true
        };

        p1.Battlefield.Add(sanctum1);
        p1.Battlefield.Add(sanctum2);

        // Player chooses to keep the first one (now uses ChooseTarget instead of ChooseCard)
        handler.EnqueueTarget(new TargetInfo(sanctum1.Id, p1.Id, ZoneType.Battlefield));

        await engine.CheckStateBasedActionsAsync();

        p1.Battlefield.Cards.Should().HaveCount(1);
        p1.Battlefield.Cards[0].Id.Should().Be(sanctum1.Id);
        p1.Graveyard.Cards.Should().HaveCount(1);
        p1.Graveyard.Cards[0].Id.Should().Be(sanctum2.Id);
    }

    [Fact]
    public async Task No_Legendary_Duplicates_No_Action()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var sanctum = new GameCard
        {
            Name = "Serra's Sanctum", CardTypes = CardType.Land, IsLegendary = true
        };
        var mountain = new GameCard
        {
            Name = "Mountain", CardTypes = CardType.Land
        };

        p1.Battlefield.Add(sanctum);
        p1.Battlefield.Add(mountain);

        await engine.CheckStateBasedActionsAsync();

        p1.Battlefield.Cards.Should().HaveCount(2);
        p1.Graveyard.Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task Three_Legendaries_Two_Go_To_Graveyard()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var s1 = new GameCard { Name = "Serra's Sanctum", CardTypes = CardType.Land, IsLegendary = true };
        var s2 = new GameCard { Name = "Serra's Sanctum", CardTypes = CardType.Land, IsLegendary = true };
        var s3 = new GameCard { Name = "Serra's Sanctum", CardTypes = CardType.Land, IsLegendary = true };

        p1.Battlefield.Add(s1);
        p1.Battlefield.Add(s2);
        p1.Battlefield.Add(s3);

        handler.EnqueueTarget(new TargetInfo(s2.Id, p1.Id, ZoneType.Battlefield));

        await engine.CheckStateBasedActionsAsync();

        p1.Battlefield.Cards.Should().HaveCount(1);
        p1.Battlefield.Cards[0].Id.Should().Be(s2.Id);
        p1.Graveyard.Cards.Should().HaveCount(2);
    }

    [Fact]
    public void SerrasSanctum_Is_Legendary_In_CardDefinitions()
    {
        var card = GameCard.Create("Serra's Sanctum", "Legendary Land");
        card.IsLegendary.Should().BeTrue();
    }

    [Fact]
    public async Task SBA_Loop_Kills_Zero_Toughness_Creature()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // A creature with 0 base toughness (kept alive by a buff)
        var weakling = new GameCard
        {
            Name = "Weakling", BasePower = 1, BaseToughness = 0,
            CardTypes = CardType.Creature
        };
        weakling.EffectiveToughness = 1; // buffed to survive
        p1.Battlefield.Add(weakling);

        // RecalculateState will reset EffectiveToughness to null (no active effects)
        // Then SBA should detect toughness <= 0 and kill it
        await engine.OnBoardChangedAsync();

        p1.Battlefield.Cards.Should().BeEmpty();
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Weakling");
    }

    [Fact]
    public async Task Legendary_Rule_Then_Recalculate_Runs_Clean()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var sanctum1 = new GameCard
        {
            Name = "Serra's Sanctum", CardTypes = CardType.Land, IsLegendary = true
        };
        var sanctum2 = new GameCard
        {
            Name = "Serra's Sanctum", CardTypes = CardType.Land, IsLegendary = true
        };

        p1.Battlefield.Add(sanctum1);
        p1.Battlefield.Add(sanctum2);

        handler.EnqueueTarget(new TargetInfo(sanctum1.Id, p1.Id, ZoneType.Battlefield));

        await engine.CheckStateBasedActionsAsync();

        // After legendary rule, only 1 sanctum remains
        p1.Battlefield.Cards.Should().HaveCount(1);
        // Effects should have been recalculated (no crash, clean state)
        state.ActiveEffects.Should().BeEmpty(); // no effects from lands
    }
}
