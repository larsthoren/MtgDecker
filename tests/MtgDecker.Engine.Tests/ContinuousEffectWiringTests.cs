using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class ContinuousEffectWiringTests
{
    private static (GameEngine engine, GameState state, Player p1, Player p2) Setup()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, p1, p2);
    }

    [Fact]
    public void Goblin_King_Auto_Buffs_Other_Goblins_From_CardDefinitions()
    {
        var (engine, state, p1, _) = Setup();

        var king = GameCard.Create("Goblin King", "Creature — Goblin");
        var grunt = new GameCard
        {
            Name = "Goblin Grunt", BasePower = 1, BaseToughness = 1,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };

        p1.Battlefield.Add(king);
        p1.Battlefield.Add(grunt);

        engine.RecalculateState();

        grunt.Power.Should().Be(2);
        grunt.Toughness.Should().Be(2);
        king.Power.Should().Be(2); // doesn't buff itself
    }

    [Fact]
    public void Exploration_Grants_Extra_Land_Drop_From_CardDefinitions()
    {
        var (engine, _, p1, _) = Setup();

        var exploration = GameCard.Create("Exploration", "Enchantment");
        p1.Battlefield.Add(exploration);

        engine.RecalculateState();

        p1.MaxLandDrops.Should().Be(2);
    }

    [Fact]
    public void Goblin_Warchief_Grants_Haste_From_CardDefinitions()
    {
        var (engine, _, p1, _) = Setup();

        var warchief = GameCard.Create("Goblin Warchief", "Creature — Goblin");
        var lackey = GameCard.Create("Goblin Lackey", "Creature — Goblin");

        p1.Battlefield.Add(warchief);
        p1.Battlefield.Add(lackey);

        engine.RecalculateState();

        lackey.ActiveKeywords.Should().Contain(Keyword.Haste);
        warchief.ActiveKeywords.Should().Contain(Keyword.Haste);
    }

    [Fact]
    public void Effects_Removed_When_Source_Leaves_Battlefield()
    {
        var (engine, state, p1, _) = Setup();

        var king = GameCard.Create("Goblin King", "Creature — Goblin");
        var grunt = new GameCard
        {
            Name = "Goblin Grunt", BasePower = 1, BaseToughness = 1,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };

        p1.Battlefield.Add(king);
        p1.Battlefield.Add(grunt);

        engine.RecalculateState();
        grunt.Power.Should().Be(2);

        p1.Battlefield.RemoveById(king.Id);
        engine.RecalculateState();

        grunt.Power.Should().Be(1);
        state.ActiveEffects.Should().BeEmpty();
    }

    [Fact]
    public void Two_Explorations_Grant_Three_Land_Drops()
    {
        var (engine, _, p1, _) = Setup();

        p1.Battlefield.Add(GameCard.Create("Exploration", "Enchantment"));
        p1.Battlefield.Add(GameCard.Create("Exploration", "Enchantment"));

        engine.RecalculateState();

        p1.MaxLandDrops.Should().Be(3);
    }

    [Fact]
    public void Goblin_King_Grants_Mountainwalk_To_Other_Goblins()
    {
        var (engine, state, p1, _) = Setup();

        var king = GameCard.Create("Goblin King", "Creature — Goblin");
        var grunt = new GameCard
        {
            Name = "Goblin Grunt", BasePower = 1, BaseToughness = 1,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };

        p1.Battlefield.Add(king);
        p1.Battlefield.Add(grunt);

        engine.RecalculateState();

        grunt.ActiveKeywords.Should().Contain(Keyword.Mountainwalk,
            "other Goblins should get mountainwalk from Goblin King");
        king.ActiveKeywords.Should().NotContain(Keyword.Mountainwalk,
            "Goblin King doesn't give mountainwalk to itself");
    }
}
