using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests.Mana;

public class ManaCostTests
{
    [Fact]
    public void Parse_SingleColor_ParsesCorrectly()
    {
        var cost = ManaCost.Parse("{R}");
        cost.ColorRequirements.Should().ContainKey(ManaColor.Red).WhoseValue.Should().Be(1);
        cost.GenericCost.Should().Be(0);
        cost.ConvertedManaCost.Should().Be(1);
    }

    [Fact]
    public void Parse_GenericPlusColor_ParsesCorrectly()
    {
        var cost = ManaCost.Parse("{1}{R}");
        cost.ColorRequirements.Should().ContainKey(ManaColor.Red).WhoseValue.Should().Be(1);
        cost.GenericCost.Should().Be(1);
        cost.ConvertedManaCost.Should().Be(2);
    }

    [Fact]
    public void Parse_TwoSameColor_ParsesCorrectly()
    {
        var cost = ManaCost.Parse("{1}{R}{R}");
        cost.ColorRequirements.Should().ContainKey(ManaColor.Red).WhoseValue.Should().Be(2);
        cost.GenericCost.Should().Be(1);
        cost.ConvertedManaCost.Should().Be(3);
    }

    [Fact]
    public void Parse_TwoDifferentColors_ParsesCorrectly()
    {
        var cost = ManaCost.Parse("{G}{W}");
        cost.ColorRequirements.Should().ContainKey(ManaColor.Green).WhoseValue.Should().Be(1);
        cost.ColorRequirements.Should().ContainKey(ManaColor.White).WhoseValue.Should().Be(1);
        cost.GenericCost.Should().Be(0);
        cost.ConvertedManaCost.Should().Be(2);
    }

    [Fact]
    public void Parse_GenericOnly_ParsesCorrectly()
    {
        var cost = ManaCost.Parse("{3}");
        cost.ColorRequirements.Should().BeEmpty();
        cost.GenericCost.Should().Be(3);
        cost.ConvertedManaCost.Should().Be(3);
    }

    [Fact]
    public void Parse_ComplexCost_ParsesCorrectly()
    {
        var cost = ManaCost.Parse("{3}{R}{R}");
        cost.ColorRequirements.Should().ContainKey(ManaColor.Red).WhoseValue.Should().Be(2);
        cost.GenericCost.Should().Be(3);
        cost.ConvertedManaCost.Should().Be(5);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsZero()
    {
        var cost = ManaCost.Parse("");
        cost.ConvertedManaCost.Should().Be(0);
        cost.GenericCost.Should().Be(0);
        cost.ColorRequirements.Should().BeEmpty();
    }

    [Fact]
    public void Parse_Null_ReturnsZero()
    {
        var cost = ManaCost.Parse(null);
        cost.ConvertedManaCost.Should().Be(0);
        cost.GenericCost.Should().Be(0);
        cost.ColorRequirements.Should().BeEmpty();
    }

    [Fact]
    public void Parse_LargeGeneric_ParsesCorrectly()
    {
        var cost = ManaCost.Parse("{2}{W}{W}");
        cost.ColorRequirements.Should().ContainKey(ManaColor.White).WhoseValue.Should().Be(2);
        cost.GenericCost.Should().Be(2);
        cost.ConvertedManaCost.Should().Be(4);
    }

    [Fact]
    public void ConvertedManaCost_SumsCorrectly()
    {
        var cost = ManaCost.Parse("{1}{G}");
        cost.ConvertedManaCost.Should().Be(2);
    }

    [Fact]
    public void Zero_HasNoCost()
    {
        var cost = ManaCost.Zero;
        cost.ConvertedManaCost.Should().Be(0);
        cost.GenericCost.Should().Be(0);
        cost.ColorRequirements.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ColorlessMana_ParsesCorrectly()
    {
        var cost = ManaCost.Parse("{C}");
        cost.ColorRequirements.Should().ContainKey(ManaColor.Colorless).WhoseValue.Should().Be(1);
        cost.GenericCost.Should().Be(0);
        cost.ConvertedManaCost.Should().Be(1);
    }
}
