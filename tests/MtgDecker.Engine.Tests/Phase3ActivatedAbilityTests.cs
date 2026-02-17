using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class Phase3ActivatedAbilityTests
{
    // === Nantuko Shade ===

    [Fact]
    public void NantukoShade_HasPumpAbility()
    {
        CardDefinitions.TryGet("Nantuko Shade", out var def);

        def!.ActivatedAbility.Should().NotBeNull();
        def.ActivatedAbility!.Cost.ManaCost.Should().NotBeNull();
        def.ActivatedAbility.Cost.ManaCost!.ColorRequirements
            .Should().ContainKey(ManaColor.Black);
        def.ActivatedAbility.Effect.Should().BeOfType<PumpSelfEffect>();
    }

    // === Ravenous Baloth ===

    [Fact]
    public void RavenousBaloth_HasBeastSacrificeAbility()
    {
        CardDefinitions.TryGet("Ravenous Baloth", out var def);

        def!.ActivatedAbility.Should().NotBeNull();
        def.ActivatedAbility!.Cost.SacrificeSubtype.Should().Be("Beast");
        def.ActivatedAbility.Effect.Should().BeOfType<GainLifeEffect>();
        ((GainLifeEffect)def.ActivatedAbility.Effect).Amount.Should().Be(4);
    }

    // === Zuran Orb ===

    [Fact]
    public void ZuranOrb_HasLandSacrificeAbility()
    {
        CardDefinitions.TryGet("Zuran Orb", out var def);

        def!.ActivatedAbility.Should().NotBeNull();
        def.ActivatedAbility!.Cost.SacrificeCardType.Should().Be(CardType.Land);
        def.ActivatedAbility.Effect.Should().BeOfType<GainLifeEffect>();
        ((GainLifeEffect)def.ActivatedAbility.Effect).Amount.Should().Be(2);
    }
}
