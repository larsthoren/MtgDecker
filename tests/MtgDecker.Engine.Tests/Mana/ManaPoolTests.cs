using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests.Mana;

public class ManaPoolTests
{
    [Fact]
    public void Add_IncreasesColorAmount()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Red, 3);
        pool[ManaColor.Red].Should().Be(3);
    }

    [Fact]
    public void Add_MultipleColors_TracksIndependently()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Red, 2);
        pool.Add(ManaColor.Blue, 1);
        pool[ManaColor.Red].Should().Be(2);
        pool[ManaColor.Blue].Should().Be(1);
    }

    [Fact]
    public void Add_SameColorTwice_Accumulates()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Green, 2);
        pool.Add(ManaColor.Green, 3);
        pool[ManaColor.Green].Should().Be(5);
    }

    [Fact]
    public void Add_ZeroOrNegativeAmount_DoesNothing()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.White, 0);
        pool.Add(ManaColor.White, -1);
        pool[ManaColor.White].Should().Be(0);
    }

    [Fact]
    public void Total_SumsAllColors()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Red, 2);
        pool.Add(ManaColor.Blue, 1);
        pool.Add(ManaColor.Green, 3);
        pool.Total.Should().Be(6);
    }

    [Fact]
    public void Indexer_UnaddedColor_ReturnsZero()
    {
        var pool = new ManaPool();
        pool[ManaColor.Black].Should().Be(0);
    }

    [Fact]
    public void CanPay_ExactMana_ReturnsTrue()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Red, 2);
        pool.Add(ManaColor.Blue, 1);
        var cost = ManaCost.Parse("{U}{R}{R}");
        pool.CanPay(cost).Should().BeTrue();
    }

    [Fact]
    public void CanPay_InsufficientColor_ReturnsFalse()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Red, 1);
        pool.Add(ManaColor.Blue, 1);
        var cost = ManaCost.Parse("{R}{R}");
        pool.CanPay(cost).Should().BeFalse();
    }

    [Fact]
    public void CanPay_InsufficientTotal_ReturnsFalse()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Red, 1);
        var cost = ManaCost.Parse("{2}{R}");
        pool.CanPay(cost).Should().BeFalse();
    }

    [Fact]
    public void CanPay_GenericPaidByAnyColor_ReturnsTrue()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Red, 1);
        pool.Add(ManaColor.Green, 2);
        var cost = ManaCost.Parse("{2}{R}");
        pool.CanPay(cost).Should().BeTrue();
    }

    [Fact]
    public void Pay_DeductsColoredFirst_ThenGeneric()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Red, 2);
        pool.Add(ManaColor.Green, 3);
        var cost = ManaCost.Parse("{2}{R}");
        var result = pool.Pay(cost);
        result.Should().BeTrue();
        pool[ManaColor.Red].Should().Be(1);
        pool[ManaColor.Green].Should().Be(1);
    }

    [Fact]
    public void Pay_InsufficientMana_ReturnsFalse_NoChange()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Red, 1);
        var cost = ManaCost.Parse("{2}{R}");
        var result = pool.Pay(cost);
        result.Should().BeFalse();
        pool[ManaColor.Red].Should().Be(1);
    }

    [Fact]
    public void Pay_ExactAmount_EmptiesPool()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.White, 2);
        var cost = ManaCost.Parse("{W}{W}");
        var result = pool.Pay(cost);
        result.Should().BeTrue();
        pool[ManaColor.White].Should().Be(0);
        pool.Total.Should().Be(0);
    }

    [Fact]
    public void Clear_EmptiesPool()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Red, 3);
        pool.Add(ManaColor.Blue, 2);
        pool.Clear();
        pool.Total.Should().Be(0);
        pool[ManaColor.Red].Should().Be(0);
        pool[ManaColor.Blue].Should().Be(0);
    }

    [Fact]
    public void Available_ReturnsOnlyNonZeroColors()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Red, 2);
        pool.Add(ManaColor.Blue, 0);
        pool.Add(ManaColor.Green, 1);
        var available = pool.Available;
        available.Should().HaveCount(2);
        available.Should().ContainKey(ManaColor.Red).WhoseValue.Should().Be(2);
        available.Should().ContainKey(ManaColor.Green).WhoseValue.Should().Be(1);
        available.Should().NotContainKey(ManaColor.Blue);
    }

    [Fact]
    public void CanPay_ZeroCost_AlwaysTrue()
    {
        var pool = new ManaPool();
        var cost = ManaCost.Parse("{0}");
        pool.CanPay(cost).Should().BeTrue();
    }

    [Fact]
    public void Pay_GenericCost_PaysFromLargestPoolFirst()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Red, 1);
        pool.Add(ManaColor.Green, 3);
        var cost = ManaCost.Parse("{2}");

        pool.Pay(cost).Should().BeTrue();
        pool[ManaColor.Green].Should().Be(1); // Largest pool paid first
        pool[ManaColor.Red].Should().Be(1);   // Untouched
    }

    [Fact]
    public void Total_ReturnsZero_WhenEmpty()
    {
        var pool = new ManaPool();
        pool.Total.Should().Be(0);
    }

    [Fact]
    public void Total_AfterDeduct_Decreases()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Red, 3);
        pool.Add(ManaColor.Blue, 2);
        pool.Deduct(ManaColor.Red, 1);
        pool.Total.Should().Be(4);
    }
}
