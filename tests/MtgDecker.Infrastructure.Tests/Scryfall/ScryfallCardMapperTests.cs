using FluentAssertions;
using MtgDecker.Domain.Enums;
using MtgDecker.Infrastructure.Scryfall;

namespace MtgDecker.Infrastructure.Tests.Scryfall;

public class ScryfallCardMapperTests
{
    [Fact]
    public void MapToCard_SingleFaceCard_MapsAllFields()
    {
        var source = new ScryfallCard
        {
            Id = "abc-123",
            OracleId = "def-456",
            Name = "Lightning Bolt",
            ManaCost = "{R}",
            Cmc = 1.0,
            TypeLine = "Instant",
            OracleText = "Lightning Bolt deals 3 damage to any target.",
            Colors = new List<string> { "R" },
            ColorIdentity = new List<string> { "R" },
            Rarity = "uncommon",
            SetCode = "lea",
            SetName = "Limited Edition Alpha",
            CollectorNumber = "161",
            Layout = "normal",
            ImageUris = new ScryfallImageUris
            {
                Normal = "https://cards.scryfall.io/normal/front/bolt.jpg",
                Small = "https://cards.scryfall.io/small/front/bolt.jpg",
                ArtCrop = "https://cards.scryfall.io/art_crop/front/bolt.jpg"
            },
            Legalities = new Dictionary<string, string>
            {
                { "modern", "legal" },
                { "vintage", "restricted" },
                { "standard", "not_legal" },
                { "pauper", "legal" }
            }
        };

        var card = ScryfallCardMapper.MapToCard(source);

        card.ScryfallId.Should().Be("abc-123");
        card.OracleId.Should().Be("def-456");
        card.Name.Should().Be("Lightning Bolt");
        card.ManaCost.Should().Be("{R}");
        card.Cmc.Should().Be(1.0);
        card.TypeLine.Should().Be("Instant");
        card.OracleText.Should().Be("Lightning Bolt deals 3 damage to any target.");
        card.Colors.Should().Be("R");
        card.ColorIdentity.Should().Be("R");
        card.Rarity.Should().Be("uncommon");
        card.SetCode.Should().Be("lea");
        card.SetName.Should().Be("Limited Edition Alpha");
        card.CollectorNumber.Should().Be("161");
        card.Layout.Should().Be("normal");
        card.ImageUri.Should().Contain("bolt.jpg");
        card.ImageUriSmall.Should().Contain("small");
        card.ImageUriArtCrop.Should().Contain("art_crop");
        card.Faces.Should().BeEmpty();
    }

    [Fact]
    public void MapToCard_MapsLegalities()
    {
        var source = CreateMinimalCard();
        source.Legalities = new Dictionary<string, string>
        {
            { "modern", "legal" },
            { "vintage", "restricted" },
            { "standard", "not_legal" },
            { "legacy", "banned" }
        };

        var card = ScryfallCardMapper.MapToCard(source);

        card.Legalities.Should().HaveCount(4);
        card.IsLegalIn(Format.Modern).Should().BeTrue();
        card.IsRestrictedIn(Format.Vintage).Should().BeTrue();
        card.IsLegalIn(Format.Vintage).Should().BeTrue(); // restricted counts as legal
    }

    [Fact]
    public void MapToCard_MultiFaceCard_MapsFaces()
    {
        var source = CreateMinimalCard();
        source.Name = "Delver of Secrets // Insectile Aberration";
        source.Layout = "transform";
        source.ImageUris = null; // multi-face cards don't have top-level image_uris
        source.CardFaces = new List<ScryfallCardFace>
        {
            new()
            {
                Name = "Delver of Secrets",
                ManaCost = "{U}",
                TypeLine = "Creature — Human Wizard",
                OracleText = "At the beginning of your upkeep...",
                Power = "1",
                Toughness = "1",
                ImageUris = new ScryfallImageUris
                {
                    Normal = "https://cards.scryfall.io/normal/front/delver.jpg",
                    Small = "https://cards.scryfall.io/small/front/delver.jpg",
                    ArtCrop = "https://cards.scryfall.io/art_crop/front/delver.jpg"
                }
            },
            new()
            {
                Name = "Insectile Aberration",
                TypeLine = "Creature — Human Insect",
                OracleText = "Flying",
                Power = "3",
                Toughness = "2",
                ImageUris = new ScryfallImageUris
                {
                    Normal = "https://cards.scryfall.io/normal/back/aberration.jpg"
                }
            }
        };

        var card = ScryfallCardMapper.MapToCard(source);

        card.Faces.Should().HaveCount(2);
        card.Faces[0].Name.Should().Be("Delver of Secrets");
        card.Faces[0].Power.Should().Be("1");
        card.Faces[1].Name.Should().Be("Insectile Aberration");
        card.Faces[1].Power.Should().Be("3");
        // Front face image used as card image
        card.ImageUri.Should().Contain("delver.jpg");
    }

    [Fact]
    public void MapToCard_MultiColorCard_JoinsColors()
    {
        var source = CreateMinimalCard();
        source.Colors = new List<string> { "W", "U" };
        source.ColorIdentity = new List<string> { "W", "U", "B" };

        var card = ScryfallCardMapper.MapToCard(source);

        card.Colors.Should().Be("W,U");
        card.ColorIdentity.Should().Be("W,U,B");
    }

    [Fact]
    public void MapToCard_NullColors_DefaultsToEmpty()
    {
        var source = CreateMinimalCard();
        source.Colors = null;
        source.ColorIdentity = null;

        var card = ScryfallCardMapper.MapToCard(source);

        card.Colors.Should().BeEmpty();
        card.ColorIdentity.Should().BeEmpty();
    }

    private static ScryfallCard CreateMinimalCard()
    {
        return new ScryfallCard
        {
            Id = Guid.NewGuid().ToString(),
            OracleId = Guid.NewGuid().ToString(),
            Name = "Test Card",
            TypeLine = "Instant",
            Rarity = "common",
            SetCode = "tst",
            SetName = "Test Set"
        };
    }
}
