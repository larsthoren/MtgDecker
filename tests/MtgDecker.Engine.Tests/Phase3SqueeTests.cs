using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class Phase3SqueeTests
{
    [Fact]
    public void Squee_HasGraveyardUpkeepTrigger()
    {
        CardDefinitions.TryGet("Squee, Goblin Nabob", out var def);

        def!.Triggers.Should().Contain(t =>
            t.Event == GameEvent.Upkeep
            && t.Condition == TriggerCondition.SelfInGraveyardDuringUpkeep
            && t.Effect is ReturnSelfFromGraveyardEffect);
    }

    [Fact]
    public async Task ReturnSelfFromGraveyard_PlayerAccepts_MovesToHand()
    {
        var h1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var squee = new GameCard { Name = "Squee, Goblin Nabob" };
        p1.Graveyard.Add(squee);

        h1.EnqueueCardChoice(squee.Id);

        var context = new EffectContext(state, p1, squee, h1);
        var effect = new ReturnSelfFromGraveyardEffect();
        await effect.Execute(context);

        p1.Hand.Cards.Should().Contain(c => c.Name == "Squee, Goblin Nabob");
        p1.Graveyard.Cards.Should().NotContain(c => c.Name == "Squee, Goblin Nabob");
    }

    [Fact]
    public async Task ReturnSelfFromGraveyard_PlayerDeclines_StaysInGraveyard()
    {
        var h1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var squee = new GameCard { Name = "Squee, Goblin Nabob" };
        p1.Graveyard.Add(squee);

        h1.EnqueueCardChoice(null);

        var context = new EffectContext(state, p1, squee, h1);
        var effect = new ReturnSelfFromGraveyardEffect();
        await effect.Execute(context);

        p1.Hand.Count.Should().Be(0);
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Squee, Goblin Nabob");
    }

    [Fact]
    public async Task ReturnSelfFromGraveyard_SqueeNotInGraveyard_DoesNothing()
    {
        var h1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var squee = new GameCard { Name = "Squee, Goblin Nabob" };
        // NOT in graveyard

        var context = new EffectContext(state, p1, squee, h1);
        var effect = new ReturnSelfFromGraveyardEffect();
        await effect.Execute(context);

        p1.Hand.Count.Should().Be(0);
    }
}
