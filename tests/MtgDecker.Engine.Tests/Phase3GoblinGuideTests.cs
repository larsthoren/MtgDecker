using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class Phase3GoblinGuideTests
{
    [Fact]
    public void GoblinGuide_HasAttackTrigger()
    {
        CardDefinitions.TryGet("Goblin Guide", out var def);

        def!.Triggers.Should().Contain(t =>
            t.Event == GameEvent.BeginCombat
            && t.Condition == TriggerCondition.SelfAttacks
            && t.Effect is GoblinGuideRevealEffect);
    }

    [Fact]
    public async Task GoblinGuideReveal_OpponentTopIsLand_GoesToHand()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);

        p2.Library.Clear();
        var land = new GameCard { Name = "Mountain", CardTypes = CardType.Land };
        var spell = new GameCard { Name = "Lightning Bolt" };
        p2.Library.Add(spell);  // bottom
        p2.Library.Add(land);   // top

        var guide = new GameCard { Name = "Goblin Guide" };
        var context = new EffectContext(state, p1, guide, h1);

        var effect = new GoblinGuideRevealEffect();
        await effect.Execute(context);

        p2.Hand.Cards.Should().Contain(c => c.Name == "Mountain",
            "land revealed by Goblin Guide goes to opponent's hand");
        p2.Library.Count.Should().Be(1, "one card removed from library");
    }

    [Fact]
    public async Task GoblinGuideReveal_OpponentTopIsNotLand_StaysOnTop()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);

        p2.Library.Clear();
        var nonLand = new GameCard { Name = "Lightning Bolt", CardTypes = CardType.Instant };
        p2.Library.Add(nonLand);

        var guide = new GameCard { Name = "Goblin Guide" };
        var context = new EffectContext(state, p1, guide, h1);

        var effect = new GoblinGuideRevealEffect();
        await effect.Execute(context);

        p2.Hand.Count.Should().Be(0, "non-land stays on top");
        p2.Library.Count.Should().Be(1);
    }

    [Fact]
    public async Task GoblinGuideReveal_EmptyLibrary_DoesNothing()
    {
        var h1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        p2.Library.Clear();

        var guide = new GameCard { Name = "Goblin Guide" };
        var context = new EffectContext(state, p1, guide, h1);

        var effect = new GoblinGuideRevealEffect();
        await effect.Execute(context); // should not throw

        p2.Hand.Count.Should().Be(0);
    }
}
