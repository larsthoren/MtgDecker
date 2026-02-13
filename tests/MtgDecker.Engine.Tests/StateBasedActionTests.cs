using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class StateBasedActionTests
{
    [Fact]
    public async Task CheckStateBasedActions_LifeAtZero_SetsGameOver()
    {
        var p1 = new Player(Guid.NewGuid(), "Player 1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        p1.AdjustLife(-20); // life = 0

        await engine.CheckStateBasedActionsAsync();

        state.IsGameOver.Should().BeTrue();
        state.Winner.Should().Be("Player 2");
    }

    [Fact]
    public async Task CheckStateBasedActions_LifeBelowZero_SetsGameOver()
    {
        var p1 = new Player(Guid.NewGuid(), "Player 1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        p2.AdjustLife(-25); // life = -5

        await engine.CheckStateBasedActionsAsync();

        state.IsGameOver.Should().BeTrue();
        state.Winner.Should().Be("Player 1");
    }

    [Fact]
    public async Task CheckStateBasedActions_BothAlive_DoesNotEndGame()
    {
        var p1 = new Player(Guid.NewGuid(), "Player 1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        await engine.CheckStateBasedActionsAsync();

        state.IsGameOver.Should().BeFalse();
        state.Winner.Should().BeNull();
    }

    [Fact]
    public async Task CheckStateBasedActions_BothAtZero_SetsGameOver()
    {
        var p1 = new Player(Guid.NewGuid(), "Player 1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        p1.AdjustLife(-20);
        p2.AdjustLife(-20);

        await engine.CheckStateBasedActionsAsync();

        // Both at 0 — draw (both lose simultaneously)
        state.IsGameOver.Should().BeTrue();
    }

    [Fact]
    public async Task CreatureWithLethalDamage_Dies()
    {
        // Setup: 2/2 creature on battlefield with 2 damage marked
        var p1 = new Player(Guid.NewGuid(), "Player 1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var creature = new GameCard
        {
            Name = "Grizzly Bears",
            CardTypes = CardType.Creature,
            Power = 2,
            Toughness = 2
        };
        p1.Battlefield.Add(creature);
        creature.DamageMarked = 2;

        await engine.CheckStateBasedActionsAsync();

        p1.Battlefield.Cards.Should().NotContain(creature);
        p1.Graveyard.Cards.Should().Contain(creature);
    }

    [Fact]
    public async Task CreatureWithExcessDamage_Dies()
    {
        // 1/1 with 3 damage marked -> still dies
        var p1 = new Player(Guid.NewGuid(), "Player 1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var creature = new GameCard
        {
            Name = "Llanowar Elves",
            CardTypes = CardType.Creature,
            Power = 1,
            Toughness = 1
        };
        p1.Battlefield.Add(creature);
        creature.DamageMarked = 3;

        await engine.CheckStateBasedActionsAsync();

        p1.Battlefield.Cards.Should().NotContain(creature);
        p1.Graveyard.Cards.Should().Contain(creature);
    }

    [Fact]
    public async Task CreatureWithNonLethalDamage_Survives()
    {
        // 3/3 with 2 damage marked -> stays on battlefield
        var p1 = new Player(Guid.NewGuid(), "Player 1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var creature = new GameCard
        {
            Name = "Centaur Courser",
            CardTypes = CardType.Creature,
            Power = 3,
            Toughness = 3
        };
        p1.Battlefield.Add(creature);
        creature.DamageMarked = 2;

        await engine.CheckStateBasedActionsAsync();

        p1.Battlefield.Cards.Should().Contain(creature);
        p1.Graveyard.Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task PlayerAtZeroLife_LosesGame()
    {
        var p1 = new Player(Guid.NewGuid(), "Player 1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        p1.AdjustLife(-20); // life = 0

        await engine.CheckStateBasedActionsAsync();

        state.IsGameOver.Should().BeTrue();
        state.Winner.Should().Be("Player 2");
        state.GameLog.Should().Contain(m => m.Contains("Player 1") && m.Contains("loses"));
    }

    [Fact]
    public async Task PlayerAtNegativeLife_LosesGame()
    {
        var p1 = new Player(Guid.NewGuid(), "Player 1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        p2.AdjustLife(-30); // life = -10

        await engine.CheckStateBasedActionsAsync();

        state.IsGameOver.Should().BeTrue();
        state.Winner.Should().Be("Player 1");
        state.GameLog.Should().Contain(m => m.Contains("Player 2") && m.Contains("loses"));
    }

    [Fact]
    public void DamageClears_AtEndOfTurn()
    {
        // Creature with damage survives, at end of turn damage resets to 0
        var p1 = new Player(Guid.NewGuid(), "Player 1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var creature = new GameCard
        {
            Name = "Centaur Courser",
            CardTypes = CardType.Creature,
            Power = 3,
            Toughness = 3
        };
        p1.Battlefield.Add(creature);
        creature.DamageMarked = 2;

        engine.ClearDamage();

        creature.DamageMarked.Should().Be(0);
        p1.Battlefield.Cards.Should().Contain(creature);
    }

    [Fact]
    public async Task LethalDamage_ClearsOnCreatureDeath()
    {
        // When a creature dies from lethal damage, its DamageMarked resets
        var p1 = new Player(Guid.NewGuid(), "Player 1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var creature = new GameCard
        {
            Name = "Grizzly Bears",
            CardTypes = CardType.Creature,
            Power = 2,
            Toughness = 2
        };
        p1.Battlefield.Add(creature);
        creature.DamageMarked = 2;

        await engine.CheckStateBasedActionsAsync();

        creature.DamageMarked.Should().Be(0);
    }

    [Fact]
    public async Task LethalDamage_LogsCreatureDeath()
    {
        var p1 = new Player(Guid.NewGuid(), "Player 1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var creature = new GameCard
        {
            Name = "Grizzly Bears",
            CardTypes = CardType.Creature,
            Power = 2,
            Toughness = 2
        };
        p1.Battlefield.Add(creature);
        creature.DamageMarked = 2;

        await engine.CheckStateBasedActionsAsync();

        state.GameLog.Should().Contain(m => m.Contains("Grizzly Bears") && m.Contains("lethal damage"));
    }

    [Fact]
    public async Task MultipleCreaturesWithLethalDamage_AllDie()
    {
        var p1 = new Player(Guid.NewGuid(), "Player 1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var bear1 = new GameCard { Name = "Bear 1", CardTypes = CardType.Creature, Power = 2, Toughness = 2 };
        var bear2 = new GameCard { Name = "Bear 2", CardTypes = CardType.Creature, Power = 2, Toughness = 2 };
        var survivor = new GameCard { Name = "Survivor", CardTypes = CardType.Creature, Power = 4, Toughness = 4 };
        p1.Battlefield.Add(bear1);
        p1.Battlefield.Add(bear2);
        p1.Battlefield.Add(survivor);
        bear1.DamageMarked = 2;
        bear2.DamageMarked = 3;
        survivor.DamageMarked = 1;

        await engine.CheckStateBasedActionsAsync();

        p1.Battlefield.Cards.Should().NotContain(bear1);
        p1.Battlefield.Cards.Should().NotContain(bear2);
        p1.Battlefield.Cards.Should().Contain(survivor);
        p1.Graveyard.Cards.Should().HaveCount(2);
    }

    [Fact]
    public async Task LethalDamageOnPlayer2Creatures_Dies()
    {
        // Verify SBAs check both players' battlefields
        var p1 = new Player(Guid.NewGuid(), "Player 1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var creature = new GameCard
        {
            Name = "Goblin",
            CardTypes = CardType.Creature,
            Power = 1,
            Toughness = 1
        };
        p2.Battlefield.Add(creature);
        creature.DamageMarked = 1;

        await engine.CheckStateBasedActionsAsync();

        p2.Battlefield.Cards.Should().BeEmpty();
        p2.Graveyard.Cards.Should().Contain(creature);
    }

    [Fact]
    public async Task TokenWithLethalDamage_GoesToGraveyardThenCeasesToExist()
    {
        // MTG rules: tokens go to graveyard then cease to exist (SBA 704.5d)
        var p1 = new Player(Guid.NewGuid(), "Player 1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var token = new GameCard
        {
            Name = "Goblin Token",
            CardTypes = CardType.Creature,
            Power = 1,
            Toughness = 1,
            IsToken = true
        };
        p1.Battlefield.Add(token);
        token.DamageMarked = 1;

        await engine.CheckStateBasedActionsAsync();

        p1.Battlefield.Cards.Should().BeEmpty();
        p1.Graveyard.Cards.Should().BeEmpty("tokens cease to exist after going to graveyard");
    }
}
