using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class SneakAttackTests
{
    [Fact]
    public async Task SneakAttack_PutsCreatureFromHand()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();

        var creature = new GameCard { Name = "Emrakul", CardTypes = CardType.Creature, Power = 15, Toughness = 15 };
        state.Player1.Hand.Add(creature);

        h1.EnqueueCardChoice(creature.Id);

        var source = new GameCard { Name = "Sneak Attack", CardTypes = CardType.Enchantment };
        var ctx = new EffectContext(state, state.Player1, source, h1);

        var effect = new SneakAttackPutEffect();
        await effect.Execute(ctx);

        state.Player1.Battlefield.Cards.Should().Contain(c => c.Name == "Emrakul");
        state.Player1.Hand.Cards.Should().NotContain(c => c.Name == "Emrakul");
    }

    [Fact]
    public async Task SneakAttack_CreatureGainsHaste()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();

        var creature = new GameCard { Name = "Emrakul", CardTypes = CardType.Creature };
        state.Player1.Hand.Add(creature);
        h1.EnqueueCardChoice(creature.Id);

        var source = new GameCard { Name = "Sneak Attack", CardTypes = CardType.Enchantment };
        var ctx = new EffectContext(state, state.Player1, source, h1);

        var effect = new SneakAttackPutEffect();
        await effect.Execute(ctx);

        // Haste should be granted via continuous effect
        state.ActiveEffects.Should().Contain(e =>
            e.Type == ContinuousEffectType.GrantKeyword
            && e.GrantedKeyword == Keyword.Haste);
    }

    [Fact]
    public async Task SneakAttack_RegistersEndOfTurnSacrifice()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();

        var creature = new GameCard { Name = "Emrakul", CardTypes = CardType.Creature };
        state.Player1.Hand.Add(creature);
        h1.EnqueueCardChoice(creature.Id);

        var source = new GameCard { Name = "Sneak Attack", CardTypes = CardType.Enchantment };
        var ctx = new EffectContext(state, state.Player1, source, h1);

        var effect = new SneakAttackPutEffect();
        await effect.Execute(ctx);

        state.DelayedTriggers.Should().ContainSingle(d => d.FireOn == GameEvent.EndStep);
    }

    [Fact]
    public async Task SneakAttack_NoCreaturesInHand_DoesNothing()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();

        // Only a non-creature in hand
        var sorcery = new GameCard { Name = "Ponder", CardTypes = CardType.Sorcery };
        state.Player1.Hand.Add(sorcery);

        var source = new GameCard { Name = "Sneak Attack", CardTypes = CardType.Enchantment };
        var ctx = new EffectContext(state, state.Player1, source, h1);

        var effect = new SneakAttackPutEffect();
        await effect.Execute(ctx);

        state.Player1.Battlefield.Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task SneakAttack_PlayerDeclinesChoice_DoesNothing()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();

        var creature = new GameCard { Name = "Emrakul", CardTypes = CardType.Creature };
        state.Player1.Hand.Add(creature);
        h1.EnqueueCardChoice((Guid?)null); // Decline

        var source = new GameCard { Name = "Sneak Attack", CardTypes = CardType.Enchantment };
        var ctx = new EffectContext(state, state.Player1, source, h1);

        var effect = new SneakAttackPutEffect();
        await effect.Execute(ctx);

        state.Player1.Battlefield.Cards.Should().BeEmpty();
        state.Player1.Hand.Cards.Should().Contain(c => c.Name == "Emrakul");
        state.ActiveEffects.Should().BeEmpty();
        state.DelayedTriggers.Should().BeEmpty();
    }

    [Fact]
    public async Task SneakAttack_SacrificeEffectTargetsCorrectCard()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();

        var creature = new GameCard { Name = "Emrakul", CardTypes = CardType.Creature };
        state.Player1.Hand.Add(creature);
        h1.EnqueueCardChoice(creature.Id);

        var source = new GameCard { Name = "Sneak Attack", CardTypes = CardType.Enchantment };
        var ctx = new EffectContext(state, state.Player1, source, h1);

        var effect = new SneakAttackPutEffect();
        await effect.Execute(ctx);

        var delayedTrigger = state.DelayedTriggers.Single();
        var sacrificeEffect = delayedTrigger.Effect.Should().BeOfType<SacrificeSpecificCardEffect>().Subject;
        sacrificeEffect.CardId.Should().Be(creature.Id);
    }

    [Fact]
    public async Task SneakAttack_EmptyHand_DoesNothing()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();

        // Empty hand
        var source = new GameCard { Name = "Sneak Attack", CardTypes = CardType.Enchantment };
        var ctx = new EffectContext(state, state.Player1, source, h1);

        var effect = new SneakAttackPutEffect();
        await effect.Execute(ctx);

        state.Player1.Battlefield.Cards.Should().BeEmpty();
        state.ActiveEffects.Should().BeEmpty();
        state.DelayedTriggers.Should().BeEmpty();
    }
}
