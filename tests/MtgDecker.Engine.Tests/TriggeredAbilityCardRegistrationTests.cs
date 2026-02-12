using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class TriggeredAbilityCardRegistrationTests
{
    [Fact]
    public void GoblinSharpshooter_HasDiesTrigger_WithUntapSelfEffect()
    {
        CardDefinitions.TryGet("Goblin Sharpshooter", out var def).Should().BeTrue();
        def!.Triggers.Should().ContainSingle();
        var trigger = def.Triggers[0];
        trigger.Event.Should().Be(GameEvent.Dies);
        trigger.Condition.Should().Be(TriggerCondition.AnyCreatureDies);
        trigger.Effect.Should().BeOfType<UntapSelfEffect>();
    }

    [Fact]
    public void GoblinSharpshooter_StillHasActivatedAbility()
    {
        CardDefinitions.TryGet("Goblin Sharpshooter", out var def).Should().BeTrue();
        def!.ActivatedAbility.Should().NotBeNull();
    }

    [Fact]
    public void GoblinLackey_HasCombatDamageTrigger_WithPutCreatureEffect()
    {
        CardDefinitions.TryGet("Goblin Lackey", out var def).Should().BeTrue();
        def!.Triggers.Should().ContainSingle();
        var trigger = def.Triggers[0];
        trigger.Event.Should().Be(GameEvent.CombatDamageDealt);
        trigger.Condition.Should().Be(TriggerCondition.SelfDealsCombatDamage);
        trigger.Effect.Should().BeOfType<PutCreatureFromHandEffect>();
    }

    [Fact]
    public void GoblinPiledriver_HasAttackTrigger_WithPiledriverPumpEffect()
    {
        CardDefinitions.TryGet("Goblin Piledriver", out var def).Should().BeTrue();
        def!.Triggers.Should().ContainSingle();
        var trigger = def.Triggers[0];
        trigger.Event.Should().Be(GameEvent.CombatDamageDealt);
        trigger.Condition.Should().Be(TriggerCondition.SelfAttacks);
        trigger.Effect.Should().BeOfType<PiledriverPumpEffect>();
    }

    [Fact]
    public void GoblinPyromancer_HasETBTrigger_WithPyromancerEffect()
    {
        CardDefinitions.TryGet("Goblin Pyromancer", out var def).Should().BeTrue();
        def!.Triggers.Should().ContainSingle();
        var trigger = def.Triggers[0];
        trigger.Event.Should().Be(GameEvent.EnterBattlefield);
        trigger.Condition.Should().Be(TriggerCondition.Self);
        trigger.Effect.Should().BeOfType<PyromancerEffect>();
    }

    [Fact]
    public void ArgothianEnchantress_HasSpellCastTrigger_WithDrawCardEffect()
    {
        CardDefinitions.TryGet("Argothian Enchantress", out var def).Should().BeTrue();
        def!.Triggers.Should().ContainSingle();
        var trigger = def.Triggers[0];
        trigger.Event.Should().Be(GameEvent.SpellCast);
        trigger.Condition.Should().Be(TriggerCondition.ControllerCastsEnchantment);
        trigger.Effect.Should().BeOfType<DrawCardEffect>();
    }

    [Fact]
    public void EnchantressPresence_HasSpellCastTrigger_WithDrawCardEffect()
    {
        CardDefinitions.TryGet("Enchantress's Presence", out var def).Should().BeTrue();
        def!.Triggers.Should().ContainSingle();
        var trigger = def.Triggers[0];
        trigger.Event.Should().Be(GameEvent.SpellCast);
        trigger.Condition.Should().Be(TriggerCondition.ControllerCastsEnchantment);
        trigger.Effect.Should().BeOfType<DrawCardEffect>();
    }

    [Fact]
    public void MirrisGuile_HasUpkeepTrigger_WithRearrangeTopEffect()
    {
        CardDefinitions.TryGet("Mirri's Guile", out var def).Should().BeTrue();
        def!.Triggers.Should().ContainSingle();
        var trigger = def.Triggers[0];
        trigger.Event.Should().Be(GameEvent.Upkeep);
        trigger.Condition.Should().Be(TriggerCondition.Upkeep);
        trigger.Effect.Should().BeOfType<RearrangeTopEffect>();
    }

    [Fact]
    public void SylvanLibrary_HasUpkeepTrigger_WithSylvanLibraryEffect()
    {
        CardDefinitions.TryGet("Sylvan Library", out var def).Should().BeTrue();
        def!.Triggers.Should().ContainSingle();
        var trigger = def.Triggers[0];
        trigger.Event.Should().Be(GameEvent.Upkeep);
        trigger.Condition.Should().Be(TriggerCondition.Upkeep);
        trigger.Effect.Should().BeOfType<SylvanLibraryEffect>();
    }

    [Fact]
    public void GameCard_Create_CopiesTriggersFromCardDefinitions()
    {
        var card = GameCard.Create("Goblin Sharpshooter");
        card.Triggers.Should().ContainSingle();
        card.Triggers[0].Condition.Should().Be(TriggerCondition.AnyCreatureDies);
    }

    [Fact]
    public void GameCard_Create_ArgothianEnchantress_HasCorrectTypes()
    {
        var card = GameCard.Create("Argothian Enchantress");
        card.CardTypes.Should().HaveFlag(CardType.Creature);
        card.CardTypes.Should().HaveFlag(CardType.Enchantment);
        card.Subtypes.Should().Contain("Human");
        card.Subtypes.Should().Contain("Druid");
    }
}
