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

    // --- ManaPool.CanPayWithPhyrexian tests ---

    [Fact]
    public void CanPayWithPhyrexian_EnoughMana_ReturnsTrue()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Black, 2);
        pool.Add(ManaColor.Colorless, 1);
        var cost = ManaCost.Parse("{1}{B/P}{B/P}");
        pool.CanPayWithPhyrexian(cost, 20).Should().BeTrue();
    }

    [Fact]
    public void CanPayWithPhyrexian_NoManaButEnoughLife_ReturnsTrue()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Colorless, 1);
        var cost = ManaCost.Parse("{1}{B/P}{B/P}");
        // Need 1 generic (have it) + 2 Phyrexian black at 2 life each = 4 life
        pool.CanPayWithPhyrexian(cost, 5).Should().BeTrue();
    }

    [Fact]
    public void CanPayWithPhyrexian_NotEnoughGenericMana_ReturnsFalse()
    {
        var pool = new ManaPool();
        // No mana at all, need {1}{B/P}{B/P} = 1 generic + 2 Phyrexian
        var cost = ManaCost.Parse("{1}{B/P}{B/P}");
        pool.CanPayWithPhyrexian(cost, 20).Should().BeFalse(); // Can't pay generic
    }

    [Fact]
    public void CanPayWithPhyrexian_LifeTooLow_ReturnsFalse()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Colorless, 1);
        var cost = ManaCost.Parse("{1}{B/P}{B/P}");
        // Need 4 life for 2 Phyrexian, but only have 3
        pool.CanPayWithPhyrexian(cost, 3).Should().BeFalse();
    }

    [Fact]
    public void CanPayWithPhyrexian_MixedPayment_ReturnsTrue()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Black, 1);
        pool.Add(ManaColor.Colorless, 1);
        var cost = ManaCost.Parse("{1}{B/P}{B/P}");
        // 1 generic (have it) + 1 black mana for 1st Phyrexian + 2 life for 2nd
        pool.CanPayWithPhyrexian(cost, 3).Should().BeTrue();
    }

    [Fact]
    public void CanPayWithPhyrexian_NonPhyrexianCost_DelegatesToCanPay()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Red, 2);
        pool.Add(ManaColor.Colorless, 2);
        var cost = ManaCost.Parse("{2}{R}{R}");
        pool.CanPayWithPhyrexian(cost, 20).Should().BeTrue();
    }

    [Fact]
    public void CanPayWithPhyrexian_NonPhyrexianCost_NotEnough_ReturnsFalse()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Red, 1);
        var cost = ManaCost.Parse("{2}{R}{R}");
        pool.CanPayWithPhyrexian(cost, 20).Should().BeFalse();
    }

    [Fact]
    public void CanPayWithPhyrexian_SinglePhyrexian_LifeOnly()
    {
        var pool = new ManaPool();
        var cost = ManaCost.Parse("{B/P}");
        // No mana, but enough life
        pool.CanPayWithPhyrexian(cost, 3).Should().BeTrue();
    }

    [Fact]
    public void CanPayWithPhyrexian_SinglePhyrexian_LifeExactlyEqual_ReturnsFalse()
    {
        var pool = new ManaPool();
        var cost = ManaCost.Parse("{B/P}");
        // Life exactly equal to cost (2) — strict inequality means can't pay
        pool.CanPayWithPhyrexian(cost, 2).Should().BeFalse();
    }
}
