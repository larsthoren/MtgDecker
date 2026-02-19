using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class BowmastersEffectTests
{
    private static (EffectContext context, Player controller, Player opponent, GameState state,
        TestDecisionHandler handler) CreateContext()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var source = new GameCard { Name = "Orcish Bowmasters", BasePower = 1, BaseToughness = 1 };
        p1.Battlefield.Add(source);
        var context = new EffectContext(state, p1, source, h1);
        return (context, p1, p2, state, h1);
    }

    [Fact]
    public async Task BowmastersEffect_CreatesArmyAndDealsDamageToOpponent()
    {
        var (context, controller, opponent, state, handler) = CreateContext();

        // No target creature chosen — damage goes to opponent
        handler.EnqueueCardChoice(null); // decline creature target

        var effect = new BowmastersEffect();
        await effect.Execute(context);

        // Should have created an Orc Army token
        controller.Battlefield.Cards.Should().Contain(c =>
            c.IsToken && c.Subtypes.Contains("Army"));
        var army = controller.Battlefield.Cards.First(c => c.Subtypes.Contains("Army"));
        army.GetCounters(CounterType.PlusOnePlusOne).Should().Be(1);

        // Opponent should have taken 1 damage
        opponent.Life.Should().Be(19);
    }

    [Fact]
    public async Task BowmastersEffect_DealsDamageToTargetCreature()
    {
        var (context, controller, opponent, state, handler) = CreateContext();

        // Put a creature on opponent's battlefield
        var targetCreature = new GameCard
        {
            Name = "Bear",
            BasePower = 2,
            BaseToughness = 2,
            CardTypes = CardType.Creature,
        };
        opponent.Battlefield.Add(targetCreature);

        // Choose the creature as target
        handler.EnqueueCardChoice(targetCreature.Id);

        var effect = new BowmastersEffect();
        await effect.Execute(context);

        // Army created
        controller.Battlefield.Cards.Should().Contain(c => c.Subtypes.Contains("Army"));

        // Creature took 1 damage
        targetCreature.DamageMarked.Should().Be(1);
    }

    [Fact]
    public async Task BowmastersEffect_GrowsExistingArmy()
    {
        var (context, controller, opponent, state, handler) = CreateContext();

        // Pre-existing Army
        var army = new GameCard
        {
            Name = "Orc Army",
            BasePower = 0,
            BaseToughness = 0,
            CardTypes = CardType.Creature,
            Subtypes = ["Orc", "Army"],
            IsToken = true,
        };
        army.AddCounters(CounterType.PlusOnePlusOne, 2);
        controller.Battlefield.Add(army);

        handler.EnqueueCardChoice(null); // target opponent

        var effect = new BowmastersEffect();
        await effect.Execute(context);

        // Existing army should have grown
        army.GetCounters(CounterType.PlusOnePlusOne).Should().Be(3);
        // No new Army token created
        controller.Battlefield.Cards.Count(c => c.Subtypes.Contains("Army")).Should().Be(1);

        opponent.Life.Should().Be(19);
    }
}
