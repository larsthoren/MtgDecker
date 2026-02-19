using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class OrcishBowmastersTests
{
    [Fact]
    public void CardDefinition_OrcishBowmasters_IsRegistered()
    {
        CardDefinitions.TryGet("Orcish Bowmasters", out var def).Should().BeTrue();
        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(2); // {1}{B}
        def.Power.Should().Be(1);
        def.Toughness.Should().Be(1);
        def.CardTypes.Should().Be(CardType.Creature);
        def.HasFlash.Should().BeTrue();
        def.Subtypes.Should().Contain("Orc").And.Contain("Archer");
    }

    [Fact]
    public void CardDefinition_OrcishBowmasters_HasETBTrigger()
    {
        CardDefinitions.TryGet("Orcish Bowmasters", out var def).Should().BeTrue();
        def!.Triggers.Should().Contain(t =>
            t.Event == GameEvent.EnterBattlefield
            && t.Condition == TriggerCondition.Self
            && t.Effect is BowmastersEffect);
    }

    [Fact]
    public void CardDefinition_OrcishBowmasters_HasDrawTrigger()
    {
        CardDefinitions.TryGet("Orcish Bowmasters", out var def).Should().BeTrue();
        def!.Triggers.Should().Contain(t =>
            t.Event == GameEvent.DrawCard
            && t.Condition == TriggerCondition.OpponentDrawsExceptFirst
            && t.Effect is BowmastersEffect);
    }

    [Fact]
    public void GameCard_Create_OrcishBowmasters_LoadsFromRegistry()
    {
        var card = GameCard.Create("Orcish Bowmasters");

        card.ManaCost.Should().NotBeNull();
        card.BasePower.Should().Be(1);
        card.BaseToughness.Should().Be(1);
        card.CardTypes.Should().Be(CardType.Creature);
        card.Subtypes.Should().Contain("Orc");
    }
}
