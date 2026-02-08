using FluentAssertions;
using MtgDecker.Domain.Enums;
using MtgDecker.Domain.Rules;

namespace MtgDecker.Domain.Tests.Rules;

public class FormatRulesTests
{
    [Theory]
    [InlineData(Format.Vintage, 60)]
    [InlineData(Format.Legacy, 60)]
    [InlineData(Format.Premodern, 60)]
    [InlineData(Format.Modern, 60)]
    [InlineData(Format.Pauper, 60)]
    [InlineData(Format.Commander, 100)]
    public void GetMinDeckSize_ReturnsCorrectSize(Format format, int expected)
    {
        FormatRules.GetMinDeckSize(format).Should().Be(expected);
    }

    [Theory]
    [InlineData(Format.Vintage, null)]
    [InlineData(Format.Legacy, null)]
    [InlineData(Format.Premodern, null)]
    [InlineData(Format.Modern, null)]
    [InlineData(Format.Pauper, null)]
    [InlineData(Format.Commander, 100)]
    public void GetMaxDeckSize_ReturnsCorrectSize(Format format, int? expected)
    {
        FormatRules.GetMaxDeckSize(format).Should().Be(expected);
    }

    [Theory]
    [InlineData(Format.Vintage, 4)]
    [InlineData(Format.Legacy, 4)]
    [InlineData(Format.Premodern, 4)]
    [InlineData(Format.Modern, 4)]
    [InlineData(Format.Pauper, 4)]
    [InlineData(Format.Commander, 1)]
    public void GetMaxCopies_ReturnsCorrectLimit(Format format, int expected)
    {
        FormatRules.GetMaxCopies(format).Should().Be(expected);
    }

    [Theory]
    [InlineData(Format.Vintage, true)]
    [InlineData(Format.Legacy, true)]
    [InlineData(Format.Premodern, true)]
    [InlineData(Format.Modern, true)]
    [InlineData(Format.Pauper, true)]
    [InlineData(Format.Commander, false)]
    public void HasSideboard_ReturnsCorrectValue(Format format, bool expected)
    {
        FormatRules.HasSideboard(format).Should().Be(expected);
    }

    [Theory]
    [InlineData(Format.Vintage, 15)]
    [InlineData(Format.Legacy, 15)]
    [InlineData(Format.Modern, 15)]
    [InlineData(Format.Pauper, 15)]
    [InlineData(Format.Premodern, 15)]
    public void GetMaxSideboardSize_ReturnsCorrectSize(Format format, int expected)
    {
        FormatRules.GetMaxSideboardSize(format).Should().Be(expected);
    }

    [Fact]
    public void GetMaxSideboardSize_Commander_ReturnsZero()
    {
        FormatRules.GetMaxSideboardSize(Format.Commander).Should().Be(0);
    }

    [Theory]
    [InlineData(Format.Vintage, "vintage")]
    [InlineData(Format.Legacy, "legacy")]
    [InlineData(Format.Premodern, "premodern")]
    [InlineData(Format.Modern, "modern")]
    [InlineData(Format.Pauper, "pauper")]
    [InlineData(Format.Commander, "commander")]
    public void GetScryfallName_ReturnsCorrectApiName(Format format, string expected)
    {
        FormatRules.GetScryfallName(format).Should().Be(expected);
    }
}
