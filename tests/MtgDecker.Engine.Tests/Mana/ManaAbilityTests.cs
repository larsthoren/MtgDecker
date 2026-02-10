using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests.Mana;

public class ManaAbilityTests
{
    [Fact]
    public void Fixed_StoresColor()
    {
        var ability = ManaAbility.Fixed(ManaColor.Red);
        ability.FixedColor.Should().Be(ManaColor.Red);
    }

    [Fact]
    public void Fixed_HasFixedType()
    {
        var ability = ManaAbility.Fixed(ManaColor.Blue);
        ability.Type.Should().Be(ManaAbilityType.Fixed);
    }

    [Fact]
    public void Fixed_HasNoChoiceColors()
    {
        var ability = ManaAbility.Fixed(ManaColor.Green);
        ability.ChoiceColors.Should().BeNull();
    }

    [Fact]
    public void Choice_StoresAllOptions()
    {
        var ability = ManaAbility.Choice(ManaColor.Colorless, ManaColor.Red, ManaColor.Green);
        ability.ChoiceColors.Should().NotBeNull();
        ability.ChoiceColors.Should().HaveCount(3);
        ability.ChoiceColors.Should().ContainInOrder(ManaColor.Colorless, ManaColor.Red, ManaColor.Green);
    }

    [Fact]
    public void Choice_HasChoiceType()
    {
        var ability = ManaAbility.Choice(ManaColor.White, ManaColor.Black);
        ability.Type.Should().Be(ManaAbilityType.Choice);
    }

    [Fact]
    public void Choice_HasNoFixedColor()
    {
        var ability = ManaAbility.Choice(ManaColor.Red, ManaColor.Green);
        ability.FixedColor.Should().BeNull();
    }
}
