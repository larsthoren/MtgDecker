using FluentAssertions;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;
using MtgDecker.Domain.ValueObjects;

namespace MtgDecker.Domain.Tests.Entities;

public class CardTests
{
    [Fact]
    public void IsBasicLand_WithBasicLandTypeLine_ReturnsTrue()
    {
        var card = CreateCard(typeLine: "Basic Land — Mountain");
        card.IsBasicLand.Should().BeTrue();
    }

    [Fact]
    public void IsBasicLand_WithNonBasicLand_ReturnsFalse()
    {
        var card = CreateCard(typeLine: "Land");
        card.IsBasicLand.Should().BeFalse();
    }

    [Fact]
    public void IsBasicLand_WithSnowBasicLand_ReturnsTrue()
    {
        var card = CreateCard(typeLine: "Basic Snow Land — Island");
        card.IsBasicLand.Should().BeTrue();
    }

    [Fact]
    public void IsLegalIn_WhenLegal_ReturnsTrue()
    {
        var card = CreateCard();
        card.Legalities.Add(new CardLegality("modern", LegalityStatus.Legal));

        card.IsLegalIn(Format.Modern).Should().BeTrue();
    }

    [Fact]
    public void IsLegalIn_WhenBanned_ReturnsFalse()
    {
        var card = CreateCard();
        card.Legalities.Add(new CardLegality("modern", LegalityStatus.Banned));

        card.IsLegalIn(Format.Modern).Should().BeFalse();
    }

    [Fact]
    public void IsLegalIn_WhenRestricted_ReturnsTrue()
    {
        var card = CreateCard();
        card.Legalities.Add(new CardLegality("vintage", LegalityStatus.Restricted));

        card.IsLegalIn(Format.Vintage).Should().BeTrue();
    }

    [Fact]
    public void IsLegalIn_WhenNotInList_ReturnsFalse()
    {
        var card = CreateCard();

        card.IsLegalIn(Format.Modern).Should().BeFalse();
    }

    [Fact]
    public void IsRestrictedIn_WhenRestricted_ReturnsTrue()
    {
        var card = CreateCard();
        card.Legalities.Add(new CardLegality("vintage", LegalityStatus.Restricted));

        card.IsRestrictedIn(Format.Vintage).Should().BeTrue();
    }

    [Fact]
    public void HasMultipleFaces_WithFaces_ReturnsTrue()
    {
        var card = CreateCard();
        card.Faces.Add(new CardFace { Name = "Front" });
        card.Faces.Add(new CardFace { Name = "Back" });

        card.HasMultipleFaces.Should().BeTrue();
    }

    [Fact]
    public void HasMultipleFaces_WithNoFaces_ReturnsFalse()
    {
        var card = CreateCard();

        card.HasMultipleFaces.Should().BeFalse();
    }

    private static Card CreateCard(
        string name = "Test Card",
        string typeLine = "Creature — Human")
    {
        return new Card
        {
            Id = Guid.NewGuid(),
            ScryfallId = Guid.NewGuid().ToString(),
            OracleId = Guid.NewGuid().ToString(),
            Name = name,
            TypeLine = typeLine,
            Rarity = "common",
            SetCode = "tst",
            SetName = "Test Set"
        };
    }
}
