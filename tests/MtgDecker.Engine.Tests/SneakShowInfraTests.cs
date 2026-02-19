using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests;

public class SneakShowInfraTests
{
    [Fact]
    public void ManaAbility_FixedMultiple_ProducesCorrectCount()
    {
        var ability = ManaAbility.FixedMultiple(ManaColor.Colorless, 2);
        ability.FixedColor.Should().Be(ManaColor.Colorless);
        ability.ProduceCount.Should().Be(2);
        ability.SelfDamage.Should().Be(0);
    }

    [Fact]
    public void ManaAbility_FixedMultipleWithDamage_HasSelfDamage()
    {
        var ability = ManaAbility.FixedMultiple(ManaColor.Colorless, 2, selfDamage: 2);
        ability.SelfDamage.Should().Be(2);
        ability.ProduceCount.Should().Be(2);
    }

    [Fact]
    public void ManaAbility_Fixed_HasProduceCountOne()
    {
        var ability = ManaAbility.Fixed(ManaColor.Red);
        ability.ProduceCount.Should().Be(1);
        ability.SelfDamage.Should().Be(0);
    }

    [Fact]
    public void GameState_ExtraTurns_StartsEmpty()
    {
        var state = TestHelper.CreateState();
        state.ExtraTurns.Should().BeEmpty();
    }

    [Fact]
    public void ActivatedAbilityCost_PayLife_DefaultsToZero()
    {
        var cost = new ActivatedAbilityCost();
        cost.PayLife.Should().Be(0);
    }

    [Fact]
    public void ActivatedAbilityCost_PayLife_CanBeSet()
    {
        var cost = new ActivatedAbilityCost(PayLife: 7);
        cost.PayLife.Should().Be(7);
    }

    [Fact]
    public void CardDefinition_ShuffleGraveyardOnDeath_DefaultsFalse()
    {
        var def = new CardDefinition(null, null, null, null, CardType.Creature);
        def.ShuffleGraveyardOnDeath.Should().BeFalse();
    }

    [Fact]
    public void CardDefinition_ShuffleGraveyardOnDeath_CanBeSet()
    {
        var def = new CardDefinition(null, null, null, null, CardType.Creature) { ShuffleGraveyardOnDeath = true };
        def.ShuffleGraveyardOnDeath.Should().BeTrue();
    }
}
