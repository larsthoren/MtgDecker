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
        def!.ActivatedAbilities.Should().NotBeEmpty();
        def.ActivatedAbilities[0].Effect.Should().BeOfType<DealDamageEffect>();
        def.ActivatedAbilities[0].Cost.SacrificeSelf.Should().BeTrue();
        def.ActivatedAbilities[0].Cost.TapSelf.Should().BeFalse();
        def.ActivatedAbilities[0].Cost.ManaCost.Should().BeNull();
        def.ActivatedAbilities[0].CanTargetPlayer.Should().BeTrue();
    }

    [Fact]
    public void SiegeGangCommander_HasActivatedAbility_AndTriggers()
    {
        CardDefinitions.TryGet("Siege-Gang Commander", out var def).Should().BeTrue();
        def!.Triggers.Should().HaveCount(1); // ETB still present
        def.ActivatedAbilities.Should().NotBeEmpty();
        def.ActivatedAbilities[0].Effect.Should().BeOfType<DealDamageEffect>();
        def.ActivatedAbilities[0].Cost.SacrificeSubtype.Should().Be("Goblin");
        def.ActivatedAbilities[0].Cost.ManaCost.Should().NotBeNull();
        def.ActivatedAbilities[0].Cost.ManaCost!.GenericCost.Should().Be(1);
        def.ActivatedAbilities[0].Cost.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Red);
        def.ActivatedAbilities[0].CanTargetPlayer.Should().BeTrue();
    }

    [Fact]
    public void GoblinSharpshooter_HasActivatedAbility()
    {
        CardDefinitions.TryGet("Goblin Sharpshooter", out var def).Should().BeTrue();
        def!.ActivatedAbilities.Should().NotBeEmpty();
        def.ActivatedAbilities[0].Effect.Should().BeOfType<DealDamageEffect>();
        def.ActivatedAbilities[0].Cost.TapSelf.Should().BeTrue();
        def.ActivatedAbilities[0].Cost.SacrificeSelf.Should().BeFalse();
        def.ActivatedAbilities[0].CanTargetPlayer.Should().BeTrue();
    }

    [Fact]
    public void SkirkProspector_HasActivatedAbility()
    {
        CardDefinitions.TryGet("Skirk Prospector", out var def).Should().BeTrue();
        def!.ActivatedAbilities.Should().NotBeEmpty();
        def.ActivatedAbilities[0].Effect.Should().BeOfType<AddManaEffect>();
        def.ActivatedAbilities[0].Cost.SacrificeSubtype.Should().Be("Goblin");
        def.ActivatedAbilities[0].Cost.ManaCost.Should().BeNull();
        def.ActivatedAbilities[0].CanTargetPlayer.Should().BeFalse();
    }

    [Fact]
    public void GoblinTinkerer_HasActivatedAbility()
    {
        CardDefinitions.TryGet("Goblin Tinkerer", out var def).Should().BeTrue();
        def!.ActivatedAbilities.Should().NotBeEmpty();
        def.ActivatedAbilities[0].Effect.Should().BeOfType<DestroyTargetEffect>();
        def.ActivatedAbilities[0].Cost.TapSelf.Should().BeTrue();
        def.ActivatedAbilities[0].Cost.SacrificeSelf.Should().BeFalse();
        def.ActivatedAbilities[0].TargetFilter.Should().NotBeNull();
        // Verify target filter accepts artifacts
        var artifact = new GameCard { Name = "Sol Ring", CardTypes = CardType.Artifact };
        def.ActivatedAbilities[0].TargetFilter!(artifact).Should().BeTrue();
        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature };
        def.ActivatedAbilities[0].TargetFilter!(creature).Should().BeFalse();
    }

    [Fact]
    public void SealOfCleansing_HasActivatedAbility()
    {
        CardDefinitions.TryGet("Seal of Cleansing", out var def).Should().BeTrue();
        def!.ActivatedAbilities.Should().NotBeEmpty();
        def.ActivatedAbilities[0].Effect.Should().BeOfType<DestroyTargetEffect>();
        def.ActivatedAbilities[0].Cost.SacrificeSelf.Should().BeTrue();
        // Verify target filter accepts enchantments and artifacts
        var enchantment = new GameCard { Name = "Test", CardTypes = CardType.Enchantment };
        def.ActivatedAbilities[0].TargetFilter!(enchantment).Should().BeTrue();
        var artifact = new GameCard { Name = "Test", CardTypes = CardType.Artifact };
        def.ActivatedAbilities[0].TargetFilter!(artifact).Should().BeTrue();
        var creature = new GameCard { Name = "Test", CardTypes = CardType.Creature };
        def.ActivatedAbilities[0].TargetFilter!(creature).Should().BeFalse();
    }

    [Fact]
    public void SterlingGrove_HasActivatedAbility()
    {
        CardDefinitions.TryGet("Sterling Grove", out var def).Should().BeTrue();
        def!.ActivatedAbilities.Should().NotBeEmpty();
        def.ActivatedAbilities[0].Effect.Should().BeOfType<SearchLibraryEffect>();
        def.ActivatedAbilities[0].Cost.SacrificeSelf.Should().BeTrue();
        def.ActivatedAbilities[0].Cost.ManaCost.Should().NotBeNull();
        def.ActivatedAbilities[0].Cost.ManaCost!.GenericCost.Should().Be(1);
    }

    [Fact]
    public void RishadanPort_HasActivatedAbility()
    {
        CardDefinitions.TryGet("Rishadan Port", out var def).Should().BeTrue();
        def!.ActivatedAbilities.Should().NotBeEmpty();
        def.ActivatedAbilities[0].Effect.Should().BeOfType<TapTargetEffect>();
        def.ActivatedAbilities[0].Cost.TapSelf.Should().BeTrue();
        def.ActivatedAbilities[0].Cost.ManaCost.Should().NotBeNull();
        def.ActivatedAbilities[0].Cost.ManaCost!.GenericCost.Should().Be(1);
        // Verify target filter accepts lands
        var land = new GameCard { Name = "Island", CardTypes = CardType.Land };
        def.ActivatedAbilities[0].TargetFilter!(land).Should().BeTrue();
        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature };
        def.ActivatedAbilities[0].TargetFilter!(creature).Should().BeFalse();
    }

    [Fact]
    public void Wasteland_HasActivatedAbility()
    {
        CardDefinitions.TryGet("Wasteland", out var def).Should().BeTrue();
        def!.ActivatedAbilities.Should().NotBeEmpty();
        def.ActivatedAbilities[0].Effect.Should().BeOfType<DestroyTargetEffect>();
        def.ActivatedAbilities[0].Cost.TapSelf.Should().BeTrue();
        def.ActivatedAbilities[0].Cost.SacrificeSelf.Should().BeTrue();
        // Verify target filter accepts lands
        var land = new GameCard { Name = "Tropical Island", CardTypes = CardType.Land };
        def.ActivatedAbilities[0].TargetFilter!(land).Should().BeTrue();
    }
}
