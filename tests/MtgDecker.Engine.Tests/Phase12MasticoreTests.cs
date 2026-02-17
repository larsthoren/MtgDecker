using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class Phase12MasticoreTests
{
    [Fact]
    public void Masticore_HasUpkeepTriggerAndActivatedAbility()
    {
        CardDefinitions.TryGet("Masticore", out var def).Should().BeTrue();
        def!.Triggers.Should().ContainSingle();
        def.Triggers[0].Event.Should().Be(GameEvent.Upkeep);
        def.Triggers[0].Condition.Should().Be(TriggerCondition.Upkeep);
        def.Triggers[0].Effect.Should().BeOfType<MasticoreUpkeepEffect>();

        def.ActivatedAbility.Should().NotBeNull();
        def.ActivatedAbility!.Cost.ManaCost!.ToString().Should().Be("{2}");
        def.ActivatedAbility.Effect.Should().BeOfType<DealDamageEffect>();
    }

    [Fact]
    public async Task MasticoreUpkeep_DiscardCard_KeepsMasticore()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var masticore = GameCard.Create("Masticore");
        p1.Battlefield.Add(masticore);

        var cardInHand = new GameCard { Name = "Lightning Bolt", CardTypes = CardType.Instant };
        p1.Hand.Add(cardInHand);

        // Choose to discard Lightning Bolt
        handler.EnqueueCardChoice(cardInHand.Id);

        var effect = new MasticoreUpkeepEffect();
        var context = new EffectContext(state, p1, masticore, handler);
        await effect.Execute(context);

        // Masticore stays
        p1.Battlefield.Cards.Should().Contain(c => c.Id == masticore.Id);
        // Card discarded
        p1.Hand.Cards.Should().BeEmpty();
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Lightning Bolt");
    }

    [Fact]
    public async Task MasticoreUpkeep_NoHandCards_SacrificesMasticore()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var masticore = GameCard.Create("Masticore");
        p1.Battlefield.Add(masticore);
        // No cards in hand

        var effect = new MasticoreUpkeepEffect();
        var context = new EffectContext(state, p1, masticore, handler);
        await effect.Execute(context);

        // Masticore sacrificed
        p1.Battlefield.Cards.Should().NotContain(c => c.Id == masticore.Id);
        p1.Graveyard.Cards.Should().Contain(c => c.Id == masticore.Id);
    }

    [Fact]
    public async Task MasticoreUpkeep_DeclinesToDiscard_SacrificesMasticore()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var masticore = GameCard.Create("Masticore");
        p1.Battlefield.Add(masticore);

        p1.Hand.Add(new GameCard { Name = "Card1", CardTypes = CardType.Instant });

        // Decline to discard (null choice)
        handler.EnqueueCardChoice(null);

        var effect = new MasticoreUpkeepEffect();
        var context = new EffectContext(state, p1, masticore, handler);
        await effect.Execute(context);

        p1.Battlefield.Cards.Should().NotContain(c => c.Id == masticore.Id);
        p1.Graveyard.Cards.Should().Contain(c => c.Id == masticore.Id);
    }

    [Fact]
    public async Task MasticoreUpkeep_NotOnBattlefield_DoesNothing()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var masticore = GameCard.Create("Masticore");
        // NOT on battlefield

        var effect = new MasticoreUpkeepEffect();
        var context = new EffectContext(state, p1, masticore, handler);
        await effect.Execute(context);

        p1.Graveyard.Cards.Should().BeEmpty();
    }
}
