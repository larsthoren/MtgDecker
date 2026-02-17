using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class Phase12TerravoreTests
{
    [Fact]
    public void Terravore_HasDynamicPowerToughness()
    {
        CardDefinitions.TryGet("Terravore", out var def).Should().BeTrue();
        def!.DynamicBasePower.Should().NotBeNull();
        def.DynamicBaseToughness.Should().NotBeNull();
    }

    [Fact]
    public void Terravore_NoLandsInGraveyards_ZeroZero()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var terravore = GameCard.Create("Terravore");
        p1.Battlefield.Add(terravore);
        engine.RecalculateState();

        terravore.Power.Should().Be(0);
        terravore.Toughness.Should().Be(0);
    }

    [Fact]
    public void Terravore_LandsInP1Graveyard_MatchesCount()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var terravore = GameCard.Create("Terravore");
        p1.Battlefield.Add(terravore);

        // Add 3 lands to P1 graveyard
        p1.Graveyard.Add(new GameCard { Name = "Forest", CardTypes = CardType.Land });
        p1.Graveyard.Add(new GameCard { Name = "Mountain", CardTypes = CardType.Land });
        p1.Graveyard.Add(new GameCard { Name = "Island", CardTypes = CardType.Land });

        engine.RecalculateState();

        terravore.Power.Should().Be(3);
        terravore.Toughness.Should().Be(3);
    }

    [Fact]
    public void Terravore_LandsInBothGraveyards_CountsBoth()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var terravore = GameCard.Create("Terravore");
        p1.Battlefield.Add(terravore);

        p1.Graveyard.Add(new GameCard { Name = "Forest", CardTypes = CardType.Land });
        p2.Graveyard.Add(new GameCard { Name = "Swamp", CardTypes = CardType.Land });
        p2.Graveyard.Add(new GameCard { Name = "Plains", CardTypes = CardType.Land });

        engine.RecalculateState();

        terravore.Power.Should().Be(3);
        terravore.Toughness.Should().Be(3);
    }

    [Fact]
    public void Terravore_NonLandCardsInGraveyard_NotCounted()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var terravore = GameCard.Create("Terravore");
        p1.Battlefield.Add(terravore);

        p1.Graveyard.Add(new GameCard { Name = "Forest", CardTypes = CardType.Land });
        p1.Graveyard.Add(new GameCard { Name = "Lightning Bolt", CardTypes = CardType.Instant });
        p1.Graveyard.Add(new GameCard { Name = "Goblin", CardTypes = CardType.Creature });

        engine.RecalculateState();

        terravore.Power.Should().Be(1);
        terravore.Toughness.Should().Be(1);
    }

    [Fact]
    public void Terravore_PowerChangesWhenGraveyardChanges()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var terravore = GameCard.Create("Terravore");
        p1.Battlefield.Add(terravore);

        engine.RecalculateState();
        terravore.Power.Should().Be(0);

        p1.Graveyard.Add(new GameCard { Name = "Forest", CardTypes = CardType.Land });
        engine.RecalculateState();
        terravore.Power.Should().Be(1);

        p2.Graveyard.Add(new GameCard { Name = "Swamp", CardTypes = CardType.Land });
        engine.RecalculateState();
        terravore.Power.Should().Be(2);
    }
}
