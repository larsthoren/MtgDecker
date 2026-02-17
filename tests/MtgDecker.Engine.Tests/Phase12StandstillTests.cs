using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class Phase12StandstillTests
{
    [Fact]
    public void Standstill_HasAnyPlayerCastsSpellTrigger()
    {
        CardDefinitions.TryGet("Standstill", out var def).Should().BeTrue();
        def!.Triggers.Should().ContainSingle();
        def.Triggers[0].Event.Should().Be(GameEvent.SpellCast);
        def.Triggers[0].Condition.Should().Be(TriggerCondition.AnyPlayerCastsSpell);
        def.Triggers[0].Effect.Should().BeOfType<StandstillEffect>();
    }

    [Fact]
    public async Task StandstillEffect_SacrificesSelfAndOpponentDraws3()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.ActivePlayer = p1; // P1 is the caster

        var standstill = GameCard.Create("Standstill");
        p1.Battlefield.Add(standstill);

        // Put some cards in P2's library for drawing
        for (int i = 0; i < 5; i++)
            p2.Library.Add(new GameCard { Name = $"Card {i}" });

        var effect = new StandstillEffect();
        var context = new EffectContext(state, p1, standstill, handler);
        await effect.Execute(context);

        // Standstill should be sacrificed
        p1.Battlefield.Cards.Should().NotContain(c => c.Id == standstill.Id);
        p1.Graveyard.Cards.Should().Contain(c => c.Id == standstill.Id);

        // Caster is P1 (active player). P1's opponent is P2. P2 should draw 3.
        p2.Hand.Cards.Should().HaveCount(3);
    }

    [Fact]
    public async Task StandstillEffect_OpponentControlsStandstill_CasterOpponentDraws()
    {
        // P2 controls Standstill, P1 casts a spell
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", handler2);
        var state = new GameState(p1, p2);
        state.ActivePlayer = p1; // P1 is the caster

        var standstill = GameCard.Create("Standstill");
        p2.Battlefield.Add(standstill); // P2 controls it

        // Put cards in P2's library (P2 is the opponent of the caster P1)
        for (int i = 0; i < 5; i++)
            p2.Library.Add(new GameCard { Name = $"Card {i}" });

        var effect = new StandstillEffect();
        var context = new EffectContext(state, p2, standstill, handler2); // controller is P2
        await effect.Execute(context);

        // Standstill sacrificed from P2's battlefield
        p2.Battlefield.Cards.Should().NotContain(c => c.Id == standstill.Id);
        p2.Graveyard.Cards.Should().Contain(c => c.Id == standstill.Id);

        // Caster is P1. Caster's opponent is P2. P2 draws 3.
        p2.Hand.Cards.Should().HaveCount(3);
    }

    [Fact]
    public async Task StandstillEffect_NotOnBattlefield_DoesNothing()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.ActivePlayer = p1;

        var standstill = GameCard.Create("Standstill");
        // NOT on battlefield

        var effect = new StandstillEffect();
        var context = new EffectContext(state, p1, standstill, handler);
        await effect.Execute(context);

        p1.Graveyard.Cards.Should().BeEmpty();
        p2.Hand.Cards.Should().BeEmpty();
    }

    [Fact]
    public void CollectBoardTriggers_AnyPlayerCastsSpell_MatchesAnySpell()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        state.ActivePlayer = p1;

        var standstill = GameCard.Create("Standstill");
        p2.Battlefield.Add(standstill);

        var castSpell = new GameCard { Name = "Lightning Bolt", CardTypes = CardType.Instant };

        // Queue board triggers -- Standstill should trigger
        engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, castSpell);

        state.StackCount.Should().BeGreaterThan(0);
    }
}
