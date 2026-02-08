using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;
using MtgDecker.Domain.ValueObjects;

namespace MtgDecker.Infrastructure.Scryfall;

public static class ScryfallCardMapper
{
    public static Card MapToCard(ScryfallCard source)
    {
        var card = new Card
        {
            Id = Guid.NewGuid(),
            ScryfallId = source.Id,
            OracleId = source.OracleId,
            Name = source.Name,
            ManaCost = source.ManaCost,
            Cmc = source.Cmc,
            TypeLine = source.TypeLine,
            OracleText = source.OracleText,
            Colors = source.Colors != null ? string.Join(",", source.Colors) : string.Empty,
            ColorIdentity = source.ColorIdentity != null ? string.Join(",", source.ColorIdentity) : string.Empty,
            Rarity = source.Rarity,
            SetCode = source.SetCode,
            SetName = source.SetName,
            CollectorNumber = source.CollectorNumber,
            Layout = source.Layout,
            ImageUri = source.ImageUris?.Normal,
            ImageUriSmall = source.ImageUris?.Small,
            ImageUriArtCrop = source.ImageUris?.ArtCrop,
            PriceUsd = ParsePrice(source.Prices?.Usd),
            PriceUsdFoil = ParsePrice(source.Prices?.UsdFoil),
            PriceEur = ParsePrice(source.Prices?.Eur),
            PriceEurFoil = ParsePrice(source.Prices?.EurFoil),
            PriceTix = ParsePrice(source.Prices?.Tix)
        };

        if (source.Legalities != null)
        {
            foreach (var (format, status) in source.Legalities)
            {
                var legalityStatus = ParseLegalityStatus(status);
                card.Legalities.Add(new CardLegality(format, legalityStatus));
            }
        }

        if (source.CardFaces is { Count: > 0 })
        {
            foreach (var face in source.CardFaces)
            {
                card.Faces.Add(new CardFace
                {
                    Id = Guid.NewGuid(),
                    Name = face.Name,
                    ManaCost = face.ManaCost,
                    TypeLine = face.TypeLine,
                    OracleText = face.OracleText,
                    Power = face.Power,
                    Toughness = face.Toughness,
                    ImageUri = face.ImageUris?.Normal
                });
            }

            // Multi-face cards without top-level image_uris: use front face image
            if (card.ImageUri == null && source.CardFaces[0].ImageUris?.Normal != null)
            {
                card.ImageUri = source.CardFaces[0].ImageUris!.Normal;
                card.ImageUriSmall = source.CardFaces[0].ImageUris!.Small;
                card.ImageUriArtCrop = source.CardFaces[0].ImageUris!.ArtCrop;
            }
        }

        return card;
    }

    private static decimal? ParsePrice(string? value)
        => decimal.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var result)
            ? result : null;

    private static LegalityStatus ParseLegalityStatus(string status) => status switch
    {
        "legal" => LegalityStatus.Legal,
        "banned" => LegalityStatus.Banned,
        "restricted" => LegalityStatus.Restricted,
        _ => LegalityStatus.NotLegal
    };
}
