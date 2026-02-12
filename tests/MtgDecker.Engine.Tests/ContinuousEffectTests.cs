using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class ContinuousEffectTests
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
    public void Lord_Buffs_Other_Creatures_Of_Same_Subtype()
    {
        var (engine, state, p1, _) = Setup();

        var kingId = Guid.NewGuid();
        var king = new GameCard
        {
            Id = kingId, Name = "Goblin King", BasePower = 2, BaseToughness = 2,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };
        var grunt = new GameCard
        {
            Name = "Goblin Grunt", BasePower = 1, BaseToughness = 1,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };

        p1.Battlefield.Add(king);
        p1.Battlefield.Add(grunt);

        state.ActiveEffects.Add(new ContinuousEffect(
            kingId, ContinuousEffectType.ModifyPowerToughness,
            (card, _) => card.IsCreature && card.Subtypes.Contains("Goblin"),
            PowerMod: 1, ToughnessMod: 1));

        engine.RecalculateState();

        // King should NOT buff itself
        king.Power.Should().Be(2);
        king.Toughness.Should().Be(2);
        // Grunt should be buffed
        grunt.Power.Should().Be(2);
        grunt.Toughness.Should().Be(2);
    }

    [Fact]
    public void Multiple_Lords_Stack()
    {
        var (engine, state, p1, _) = Setup();

        var king1Id = Guid.NewGuid();
        var king2Id = Guid.NewGuid();
        var king1 = new GameCard
        {
            Id = king1Id, Name = "King 1", BasePower = 2, BaseToughness = 2,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };
        var king2 = new GameCard
        {
            Id = king2Id, Name = "King 2", BasePower = 2, BaseToughness = 2,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };
        var grunt = new GameCard
        {
            Name = "Grunt", BasePower = 1, BaseToughness = 1,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };

        p1.Battlefield.Add(king1);
        p1.Battlefield.Add(king2);
        p1.Battlefield.Add(grunt);

        state.ActiveEffects.Add(new ContinuousEffect(
            king1Id, ContinuousEffectType.ModifyPowerToughness,
            (card, _) => card.IsCreature && card.Subtypes.Contains("Goblin"),
            PowerMod: 1, ToughnessMod: 1));
        state.ActiveEffects.Add(new ContinuousEffect(
            king2Id, ContinuousEffectType.ModifyPowerToughness,
            (card, _) => card.IsCreature && card.Subtypes.Contains("Goblin"),
            PowerMod: 1, ToughnessMod: 1));

        engine.RecalculateState();

        // Each king buffs the other (+1/+1) but not itself
        king1.Power.Should().Be(3);
        king2.Power.Should().Be(3);
        // Grunt gets +2/+2
        grunt.Power.Should().Be(3);
        grunt.Toughness.Should().Be(3);
    }

    [Fact]
    public void Keyword_Grant_Adds_To_ActiveKeywords()
    {
        var (engine, state, p1, _) = Setup();

        var warchiefId = Guid.NewGuid();
        var warchief = new GameCard
        {
            Id = warchiefId, Name = "Warchief", BasePower = 2, BaseToughness = 2,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };
        var goblin = new GameCard
        {
            Name = "Goblin", BasePower = 1, BaseToughness = 1,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };
        var elf = new GameCard
        {
            Name = "Elf", BasePower = 1, BaseToughness = 1,
            CardTypes = CardType.Creature, Subtypes = ["Elf"]
        };

        p1.Battlefield.Add(warchief);
        p1.Battlefield.Add(goblin);
        p1.Battlefield.Add(elf);

        state.ActiveEffects.Add(new ContinuousEffect(
            warchiefId, ContinuousEffectType.GrantKeyword,
            (card, _) => card.IsCreature && card.Subtypes.Contains("Goblin"),
            GrantedKeyword: Keyword.Haste));

        engine.RecalculateState();

        goblin.ActiveKeywords.Should().Contain(Keyword.Haste);
        warchief.ActiveKeywords.Should().Contain(Keyword.Haste);
        elf.ActiveKeywords.Should().NotContain(Keyword.Haste);
    }

    [Fact]
    public void ExtraLandDrop_Updates_MaxLandDrops()
    {
        var (engine, state, p1, _) = Setup();

        var exploration = new GameCard
        {
            Name = "Exploration", CardTypes = CardType.Enchantment
        };
        p1.Battlefield.Add(exploration);

        state.ActiveEffects.Add(new ContinuousEffect(
            exploration.Id, ContinuousEffectType.ExtraLandDrop,
            (_, _) => true, ExtraLandDrops: 1));

        engine.RecalculateState();
        p1.MaxLandDrops.Should().Be(2);
    }

    [Fact]
    public void Recalculate_Resets_Effective_When_No_Effects()
    {
        var (engine, state, p1, _) = Setup();

        var goblin = new GameCard
        {
            Name = "Goblin", BasePower = 1, BaseToughness = 1,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };
        goblin.EffectivePower = 5; // leftover
        p1.Battlefield.Add(goblin);

        engine.RecalculateState();

        goblin.EffectivePower.Should().BeNull();
        goblin.Power.Should().Be(1);
    }

    [Fact]
    public void Keywords_Cleared_And_Rebuilt_On_Recalculate()
    {
        var (engine, state, p1, _) = Setup();

        var goblin = new GameCard
        {
            Name = "Goblin", BasePower = 1, BaseToughness = 1,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };
        goblin.ActiveKeywords.Add(Keyword.Haste); // leftover from previous calc
        p1.Battlefield.Add(goblin);

        // No keyword effects — keywords should be cleared
        engine.RecalculateState();

        goblin.ActiveKeywords.Should().BeEmpty();
    }

    [Fact]
    public void UntilEndOfTurn_Effect_Applies_Before_Cleanup()
    {
        var (engine, state, p1, _) = Setup();

        var goblin = new GameCard
        {
            Name = "Goblin", BasePower = 1, BaseToughness = 1,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };
        p1.Battlefield.Add(goblin);

        state.ActiveEffects.Add(new ContinuousEffect(
            Guid.NewGuid(), ContinuousEffectType.ModifyPowerToughness,
            (card, _) => card.IsCreature && card.Subtypes.Contains("Goblin"),
            PowerMod: 3, UntilEndOfTurn: true));

        engine.RecalculateState();
        goblin.Power.Should().Be(4);
    }
}
