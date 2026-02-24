using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

// ReSharper disable InconsistentNaming

namespace MtgDecker.Engine.Tests;

/// <summary>
/// Tests for Pyrokinesis divided damage: deals 4 damage divided among
/// any number of target creatures.
/// </summary>
public class PyrokinesisDividedDamageTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler h1, TestDecisionHandler h2) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, h1, h2);
    }

    [Fact]
    public void Pyrokinesis_UsesDividedDamageEffect()
    {
        CardDefinitions.TryGet("Pyrokinesis", out var def).Should().BeTrue();
        def!.Effect.Should().BeOfType<DividedDamageEffect>();
        ((DividedDamageEffect)def.Effect!).TotalDamage.Should().Be(4);
    }

    [Fact]
    public void Pyrokinesis_HasNoTargetFilter()
    {
        // DividedDamageEffect handles its own targeting during resolution
        CardDefinitions.TryGet("Pyrokinesis", out var def).Should().BeTrue();
        def!.TargetFilter.Should().BeNull();
    }

    [Fact]
    public async Task DividedDamage_AllDamageToSingleCreature()
    {
        var (engine, state, h1, h2) = CreateSetup();

        var creature = new GameCard
        {
            Name = "Tarmogoyf",
            CardTypes = CardType.Creature,
            BasePower = 0,
            BaseToughness = 5,
        };
        state.Player2.Battlefield.Add(creature);

        var spell = new StackObject(
            GameCard.Create("Pyrokinesis", "Instant"),
            state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(),
            0);

        var effect = new DividedDamageEffect(4);

        // Choose the same creature 4 times
        h1.EnqueueCardChoice(creature.Id);
        h1.EnqueueCardChoice(creature.Id);
        h1.EnqueueCardChoice(creature.Id);
        h1.EnqueueCardChoice(creature.Id);

        await effect.ResolveAsync(state, spell, h1);

        creature.DamageMarked.Should().Be(4);
    }

    [Fact]
    public async Task DividedDamage_SplitAcrossMultipleCreatures()
    {
        var (engine, state, h1, h2) = CreateSetup();

        var creature1 = new GameCard
        {
            Name = "Goblin Lackey",
            CardTypes = CardType.Creature,
            BasePower = 1,
            BaseToughness = 1,
        };
        var creature2 = new GameCard
        {
            Name = "Goblin Piledriver",
            CardTypes = CardType.Creature,
            BasePower = 1,
            BaseToughness = 2,
        };
        state.Player2.Battlefield.Add(creature1);
        state.Player2.Battlefield.Add(creature2);

        var spell = new StackObject(
            GameCard.Create("Pyrokinesis", "Instant"),
            state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(),
            0);

        var effect = new DividedDamageEffect(4);

        // 1 damage to Lackey, 3 to Piledriver
        h1.EnqueueCardChoice(creature1.Id);
        h1.EnqueueCardChoice(creature2.Id);
        h1.EnqueueCardChoice(creature2.Id);
        h1.EnqueueCardChoice(creature2.Id);

        await effect.ResolveAsync(state, spell, h1);

        creature1.DamageMarked.Should().Be(1);
        creature2.DamageMarked.Should().Be(3);
    }

    [Fact]
    public async Task DividedDamage_EvenSplit()
    {
        var (engine, state, h1, h2) = CreateSetup();

        var creature1 = new GameCard
        {
            Name = "Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        var creature2 = new GameCard
        {
            Name = "Wolf",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        state.Player2.Battlefield.Add(creature1);
        state.Player2.Battlefield.Add(creature2);

        var spell = new StackObject(
            GameCard.Create("Pyrokinesis", "Instant"),
            state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(),
            0);

        var effect = new DividedDamageEffect(4);

        // 2 damage to each
        h1.EnqueueCardChoice(creature1.Id);
        h1.EnqueueCardChoice(creature1.Id);
        h1.EnqueueCardChoice(creature2.Id);
        h1.EnqueueCardChoice(creature2.Id);

        await effect.ResolveAsync(state, spell, h1);

        creature1.DamageMarked.Should().Be(2);
        creature2.DamageMarked.Should().Be(2);
    }

    [Fact]
    public async Task DividedDamage_NoCreatures_NoEffect()
    {
        var (engine, state, h1, h2) = CreateSetup();

        // No creatures on opponent's battlefield

        var spell = new StackObject(
            GameCard.Create("Pyrokinesis", "Instant"),
            state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(),
            0);

        var effect = new DividedDamageEffect(4);

        await effect.ResolveAsync(state, spell, h1);

        state.GameLog.Should().Contain(l => l.Contains("no valid targets"));
    }
}
