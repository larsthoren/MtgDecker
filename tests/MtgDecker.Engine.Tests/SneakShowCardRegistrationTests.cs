using FluentAssertions;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class SneakShowCardRegistrationTests
{
    [Theory]
    [InlineData("Show and Tell")]
    [InlineData("Sneak Attack")]
    [InlineData("Emrakul, the Aeons Torn")]
    [InlineData("Griselbrand")]
    [InlineData("Lotus Petal")]
    [InlineData("Spell Pierce")]
    [InlineData("Ancient Tomb")]
    [InlineData("City of Traitors")]
    [InlineData("Intuition")]
    public void Card_IsRegistered(string cardName)
    {
        CardDefinitions.TryGet(cardName, out var def).Should().BeTrue($"'{cardName}' should be registered");
        def.Should().NotBeNull();
    }

    [Fact]
    public void Emrakul_HasCorrectProperties()
    {
        CardDefinitions.TryGet("Emrakul, the Aeons Torn", out var def).Should().BeTrue();
        def!.Power.Should().Be(15);
        def.Toughness.Should().Be(15);
        def.IsLegendary.Should().BeTrue();
        def.ShuffleGraveyardOnDeath.Should().BeTrue();
        def.Subtypes.Should().Contain("Eldrazi");
        def.Triggers.Should().HaveCount(2);
        def.ContinuousEffects.Should().HaveCount(2);
    }

    [Fact]
    public void Griselbrand_HasPayLifeAbility()
    {
        CardDefinitions.TryGet("Griselbrand", out var def).Should().BeTrue();
        def!.ActivatedAbility.Should().NotBeNull();
        def.ActivatedAbility!.Cost.PayLife.Should().Be(7);
    }

    [Fact]
    public void AncientTomb_ProducesTwoColorlessWith2Damage()
    {
        CardDefinitions.TryGet("Ancient Tomb", out var def).Should().BeTrue();
        def!.ManaAbility.Should().NotBeNull();
        def.ManaAbility!.ProduceCount.Should().Be(2);
        def.ManaAbility.SelfDamage.Should().Be(2);
    }

    [Fact]
    public void CityOfTraitors_HasLandPlayedTrigger()
    {
        CardDefinitions.TryGet("City of Traitors", out var def).Should().BeTrue();
        def!.Triggers.Should().ContainSingle();
        def.Triggers[0].Event.Should().Be(GameEvent.LandPlayed);
    }

    [Fact]
    public void SneakAttack_HasActivatedAbility()
    {
        CardDefinitions.TryGet("Sneak Attack", out var def).Should().BeTrue();
        def!.ActivatedAbility.Should().NotBeNull();
    }

    [Fact]
    public void ShowAndTell_HasEffect()
    {
        CardDefinitions.TryGet("Show and Tell", out var def).Should().BeTrue();
        def!.Effect.Should().NotBeNull();
    }
}
