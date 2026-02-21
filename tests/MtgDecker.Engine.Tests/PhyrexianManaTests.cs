using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests;

public class PhyrexianManaTests
{
    [Fact]
    public void Parse_SinglePhyrexianBlack_HasPhyrexianRequirement()
    {
        var cost = ManaCost.Parse("{B/P}");
        cost.PhyrexianRequirements.Should().ContainKey(ManaColor.Black);
        cost.PhyrexianRequirements[ManaColor.Black].Should().Be(1);
        cost.ColorRequirements.Should().BeEmpty();
        cost.GenericCost.Should().Be(0);
        cost.ConvertedManaCost.Should().Be(1);
    }

    [Fact]
    public void Parse_DoublePhyrexianBlack_HasCorrectCount()
    {
        var cost = ManaCost.Parse("{B/P}{B/P}");
        cost.PhyrexianRequirements[ManaColor.Black].Should().Be(2);
        cost.ConvertedManaCost.Should().Be(2);
    }

    [Fact]
    public void Parse_MixedCost_Dismember()
    {
        var cost = ManaCost.Parse("{1}{B/P}{B/P}");
        cost.GenericCost.Should().Be(1);
        cost.PhyrexianRequirements[ManaColor.Black].Should().Be(2);
        cost.ColorRequirements.Should().BeEmpty();
        cost.ConvertedManaCost.Should().Be(3);
    }

    [Fact]
    public void Parse_AllPhyrexianColors()
    {
        ManaCost.Parse("{U/P}").PhyrexianRequirements.Should().ContainKey(ManaColor.Blue);
        ManaCost.Parse("{R/P}").PhyrexianRequirements.Should().ContainKey(ManaColor.Red);
        ManaCost.Parse("{G/P}").PhyrexianRequirements.Should().ContainKey(ManaColor.Green);
        ManaCost.Parse("{W/P}").PhyrexianRequirements.Should().ContainKey(ManaColor.White);
    }

    [Fact]
    public void Parse_NormalCost_HasEmptyPhyrexianRequirements()
    {
        var cost = ManaCost.Parse("{2}{R}{R}");
        cost.PhyrexianRequirements.Should().BeEmpty();
    }

    [Fact]
    public void ToString_PhyrexianCost_OutputsCorrectFormat()
    {
        var cost = ManaCost.Parse("{1}{B/P}{B/P}");
        cost.ToString().Should().Be("{1}{B/P}{B/P}");
    }

    [Fact]
    public void ToString_SinglePhyrexian_OutputsCorrectFormat()
    {
        var cost = ManaCost.Parse("{U/P}");
        cost.ToString().Should().Be("{U/P}");
    }

    [Fact]
    public void HasPhyrexianCost_WithPhyrexian_ReturnsTrue()
    {
        ManaCost.Parse("{B/P}").HasPhyrexianCost.Should().BeTrue();
    }

    [Fact]
    public void HasPhyrexianCost_WithoutPhyrexian_ReturnsFalse()
    {
        ManaCost.Parse("{2}{R}").HasPhyrexianCost.Should().BeFalse();
    }

    [Fact]
    public void WithGenericReduction_PreservesPhyrexian()
    {
        var cost = ManaCost.Parse("{2}{B/P}");
        var reduced = cost.WithGenericReduction(1);
        reduced.GenericCost.Should().Be(1);
        reduced.PhyrexianRequirements[ManaColor.Black].Should().Be(1);
    }
}
