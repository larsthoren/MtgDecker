using FluentAssertions;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class SneakShowSideboardTests
{
    [Theory]
    [InlineData("Blood Moon")]
    [InlineData("Pyroclasm")]
    [InlineData("Flusterstorm")]
    [InlineData("Pyroblast")]
    [InlineData("Surgical Extraction")]
    [InlineData("Grafdigger's Cage")]
    [InlineData("Wipe Away")]
    public void SideboardCard_IsRegistered(string cardName)
    {
        CardDefinitions.TryGet(cardName, out var def).Should().BeTrue($"'{cardName}' should be registered");
        def.Should().NotBeNull();
    }

    [Fact]
    public void BloodMoon_HasContinuousEffect()
    {
        CardDefinitions.TryGet("Blood Moon", out var def).Should().BeTrue();
        def!.ContinuousEffects.Should().ContainSingle();
    }

    [Fact]
    public void SurgicalExtraction_HasAlternateCost()
    {
        CardDefinitions.TryGet("Surgical Extraction", out var def).Should().BeTrue();
        def!.AlternateCost.Should().NotBeNull();
        def.AlternateCost!.LifeCost.Should().Be(2);
    }

    [Fact]
    public void Pyroclasm_IsSorceryWithEffect()
    {
        CardDefinitions.TryGet("Pyroclasm", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Sorcery);
        def.Effect.Should().NotBeNull();
    }

    [Fact]
    public void Flusterstorm_IsInstantWithCounterEffect()
    {
        CardDefinitions.TryGet("Flusterstorm", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Instant);
        def.TargetFilter.Should().NotBeNull();
        def.Effect.Should().NotBeNull();
    }

    [Fact]
    public void Pyroblast_IsInstantTargetingSpells()
    {
        CardDefinitions.TryGet("Pyroblast", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Instant);
        def.TargetFilter.Should().NotBeNull();
        def.Effect.Should().NotBeNull();
    }

    [Fact]
    public void GrafdiggersCage_IsArtifact()
    {
        CardDefinitions.TryGet("Grafdigger's Cage", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Artifact);
    }

    [Fact]
    public void WipeAway_IsInstantWithBounceEffect()
    {
        CardDefinitions.TryGet("Wipe Away", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Instant);
        def.TargetFilter.Should().NotBeNull();
        def.Effect.Should().NotBeNull();
    }
}
