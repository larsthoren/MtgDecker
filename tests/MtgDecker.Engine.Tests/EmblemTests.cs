using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class EmblemTests
{
    [Fact]
    public void Emblem_CanBeCreated()
    {
        var emblem = new Emblem("Ninjas you control get +1/+1.",
            new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyPowerToughness,
                (card, _) => card.Subtypes.Contains("Ninja"),
                PowerMod: 1, ToughnessMod: 1));

        emblem.Description.Should().Be("Ninjas you control get +1/+1.");
        emblem.Effect.Should().NotBeNull();
    }

    [Fact]
    public void Player_Emblems_StartsEmpty()
    {
        var handler = new TestDecisionHandler();
        var player = new Player(Guid.NewGuid(), "Test", handler);
        player.Emblems.Should().BeEmpty();
    }

    [Fact]
    public void Emblem_Effect_ModifiesCreatures()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var ninja = new GameCard
        {
            Name = "Test Ninja",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
            Subtypes = ["Ninja"],
        };
        p1.Battlefield.Add(ninja);

        // P1 gets a ninja emblem
        p1.Emblems.Add(new Emblem("Ninjas you control get +1/+1.",
            new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyPowerToughness,
                (card, _) => card.Subtypes.Contains("Ninja"),
                PowerMod: 1, ToughnessMod: 1,
                ControllerOnly: true)));

        engine.RecalculateState();

        ninja.Power.Should().Be(3);
        ninja.Toughness.Should().Be(3);
    }

    [Fact]
    public void Emblem_ControllerOnly_DoesNotAffectOpponentCreatures()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P2 has a ninja
        var opponentNinja = new GameCard
        {
            Name = "Opponent Ninja",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
            Subtypes = ["Ninja"],
        };
        p2.Battlefield.Add(opponentNinja);

        // P1's emblem (ControllerOnly)
        p1.Emblems.Add(new Emblem("Ninjas you control get +1/+1.",
            new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyPowerToughness,
                (card, _) => card.Subtypes.Contains("Ninja"),
                PowerMod: 1, ToughnessMod: 1,
                ControllerOnly: true)));

        engine.RecalculateState();

        // Opponent's ninja should NOT be affected
        opponentNinja.Power.Should().Be(2);
        opponentNinja.Toughness.Should().Be(2);
    }

    [Fact]
    public void MultipleEmblems_StackEffects()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var ninja = new GameCard
        {
            Name = "Test Ninja",
            CardTypes = CardType.Creature,
            BasePower = 1,
            BaseToughness = 1,
            Subtypes = ["Ninja"],
        };
        p1.Battlefield.Add(ninja);

        // Two emblems
        p1.Emblems.Add(new Emblem("Ninjas +1/+1",
            new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyPowerToughness,
                (card, _) => card.Subtypes.Contains("Ninja"),
                PowerMod: 1, ToughnessMod: 1, ControllerOnly: true)));
        p1.Emblems.Add(new Emblem("Ninjas +1/+1 again",
            new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyPowerToughness,
                (card, _) => card.Subtypes.Contains("Ninja"),
                PowerMod: 1, ToughnessMod: 1, ControllerOnly: true)));

        engine.RecalculateState();

        ninja.Power.Should().Be(3);
        ninja.Toughness.Should().Be(3);
    }
}
