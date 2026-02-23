using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class ActivatedAbilityCardRegistrationTests
{
    [Fact]
    public void MoggFanatic_HasActivatedAbility()
    {
        CardDefinitions.TryGet("Mogg Fanatic", out var def).Should().BeTrue();
        def!.ActivatedAbility.Should().NotBeNull();
        def.ActivatedAbility!.Effect.Should().BeOfType<DealDamageEffect>();
        def.ActivatedAbility.Cost.SacrificeSelf.Should().BeTrue();
        def.ActivatedAbility.Cost.TapSelf.Should().BeFalse();
        def.ActivatedAbility.Cost.ManaCost.Should().BeNull();
        def.ActivatedAbility.CanTargetPlayer.Should().BeTrue();
    }

    [Fact]
    public void SiegeGangCommander_HasActivatedAbility_AndTriggers()
    {
        CardDefinitions.TryGet("Siege-Gang Commander", out var def).Should().BeTrue();
        def!.Triggers.Should().HaveCount(1); // ETB still present
        def.ActivatedAbility.Should().NotBeNull();
        def.ActivatedAbility!.Effect.Should().BeOfType<DealDamageEffect>();
        def.ActivatedAbility.Cost.SacrificeSubtype.Should().Be("Goblin");
        def.ActivatedAbility.Cost.ManaCost.Should().NotBeNull();
        def.ActivatedAbility.Cost.ManaCost!.GenericCost.Should().Be(1);
        def.ActivatedAbility.Cost.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Red);
        def.ActivatedAbility.CanTargetPlayer.Should().BeTrue();
    }

    [Fact]
    public void GoblinSharpshooter_HasActivatedAbility()
    {
        CardDefinitions.TryGet("Goblin Sharpshooter", out var def).Should().BeTrue();
        def!.ActivatedAbility.Should().NotBeNull();
        def.ActivatedAbility!.Effect.Should().BeOfType<DealDamageEffect>();
        def.ActivatedAbility.Cost.TapSelf.Should().BeTrue();
        def.ActivatedAbility.Cost.SacrificeSelf.Should().BeFalse();
        def.ActivatedAbility.CanTargetPlayer.Should().BeTrue();
    }

    [Fact]
    public void SkirkProspector_HasActivatedAbility()
    {
        CardDefinitions.TryGet("Skirk Prospector", out var def).Should().BeTrue();
        def!.ActivatedAbility.Should().NotBeNull();
        def.ActivatedAbility!.Effect.Should().BeOfType<AddManaEffect>();
        def.ActivatedAbility.Cost.SacrificeSubtype.Should().Be("Goblin");
        def.ActivatedAbility.Cost.ManaCost.Should().BeNull();
        def.ActivatedAbility.CanTargetPlayer.Should().BeFalse();
    }

    [Fact]
    public void GoblinTinkerer_HasActivatedAbility()
    {
        CardDefinitions.TryGet("Goblin Tinkerer", out var def).Should().BeTrue();
        def!.ActivatedAbility.Should().NotBeNull();
        def.ActivatedAbility!.Effect.Should().BeOfType<DestroyTargetEffect>();
        def.ActivatedAbility.Cost.TapSelf.Should().BeTrue();
        def.ActivatedAbility.Cost.SacrificeSelf.Should().BeFalse();
        def.ActivatedAbility.TargetFilter.Should().NotBeNull();
        // Verify target filter accepts artifacts
        var artifact = new GameCard { Name = "Sol Ring", CardTypes = CardType.Artifact };
        def.ActivatedAbility.TargetFilter!(artifact).Should().BeTrue();
        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature };
        def.ActivatedAbility.TargetFilter!(creature).Should().BeFalse();
    }

    [Fact]
    public void SealOfCleansing_HasActivatedAbility()
    {
        CardDefinitions.TryGet("Seal of Cleansing", out var def).Should().BeTrue();
        def!.ActivatedAbility.Should().NotBeNull();
        def.ActivatedAbility!.Effect.Should().BeOfType<DestroyTargetEffect>();
        def.ActivatedAbility.Cost.SacrificeSelf.Should().BeTrue();
        // Verify target filter accepts enchantments and artifacts
        var enchantment = new GameCard { Name = "Test", CardTypes = CardType.Enchantment };
        def.ActivatedAbility.TargetFilter!(enchantment).Should().BeTrue();
        var artifact = new GameCard { Name = "Test", CardTypes = CardType.Artifact };
        def.ActivatedAbility.TargetFilter!(artifact).Should().BeTrue();
        var creature = new GameCard { Name = "Test", CardTypes = CardType.Creature };
        def.ActivatedAbility.TargetFilter!(creature).Should().BeFalse();
    }

    [Fact]
    public void SterlingGrove_HasActivatedAbility()
    {
        CardDefinitions.TryGet("Sterling Grove", out var def).Should().BeTrue();
        def!.ActivatedAbility.Should().NotBeNull();
        def.ActivatedAbility!.Effect.Should().BeOfType<SearchLibraryToTopEffect>();
        def.ActivatedAbility.Cost.SacrificeSelf.Should().BeTrue();
        def.ActivatedAbility.Cost.ManaCost.Should().NotBeNull();
        def.ActivatedAbility.Cost.ManaCost!.GenericCost.Should().Be(1);
    }

    [Fact]
    public void RishadanPort_HasActivatedAbility()
    {
        CardDefinitions.TryGet("Rishadan Port", out var def).Should().BeTrue();
        def!.ActivatedAbility.Should().NotBeNull();
        def.ActivatedAbility!.Effect.Should().BeOfType<TapTargetEffect>();
        def.ActivatedAbility.Cost.TapSelf.Should().BeTrue();
        def.ActivatedAbility.Cost.ManaCost.Should().NotBeNull();
        def.ActivatedAbility.Cost.ManaCost!.GenericCost.Should().Be(1);
        // Verify target filter accepts lands
        var land = new GameCard { Name = "Island", CardTypes = CardType.Land };
        def.ActivatedAbility.TargetFilter!(land).Should().BeTrue();
        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature };
        def.ActivatedAbility.TargetFilter!(creature).Should().BeFalse();
    }

    [Fact]
    public void Wasteland_HasActivatedAbility()
    {
        CardDefinitions.TryGet("Wasteland", out var def).Should().BeTrue();
        def!.ActivatedAbility.Should().NotBeNull();
        def.ActivatedAbility!.Effect.Should().BeOfType<DestroyTargetEffect>();
        def.ActivatedAbility.Cost.TapSelf.Should().BeTrue();
        def.ActivatedAbility.Cost.SacrificeSelf.Should().BeTrue();
        // Verify target filter accepts lands
        var land = new GameCard { Name = "Tropical Island", CardTypes = CardType.Land };
        def.ActivatedAbility.TargetFilter!(land).Should().BeTrue();
    }
}
