using FluentAssertions;
using MtgDecker.Domain.Enums;
using MtgDecker.Domain.ValueObjects;

namespace MtgDecker.Domain.Tests.ValueObjects;

public class CardLegalityTests
{
    [Fact]
    public void Equals_SameFormatAndStatus_ReturnsTrue()
    {
        var a = new CardLegality("modern", LegalityStatus.Legal);
        var b = new CardLegality("modern", LegalityStatus.Legal);

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentStatus_ReturnsFalse()
    {
        var a = new CardLegality("modern", LegalityStatus.Legal);
        var b = new CardLegality("modern", LegalityStatus.Banned);

        a.Should().NotBe(b);
    }

    [Fact]
    public void Equals_DifferentFormat_ReturnsFalse()
    {
        var a = new CardLegality("modern", LegalityStatus.Legal);
        var b = new CardLegality("legacy", LegalityStatus.Legal);

        a.Should().NotBe(b);
    }

    [Fact]
    public void GetHashCode_EqualObjects_ReturnSameHash()
    {
        var a = new CardLegality("modern", LegalityStatus.Legal);
        var b = new CardLegality("modern", LegalityStatus.Legal);

        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
