using MtgDecker.Domain.Enums;
using MtgDecker.Domain.Rules;
using MtgDecker.Domain.ValueObjects;

namespace MtgDecker.Domain.Entities;

public class Card
{
    public Guid Id { get; set; }
    public string ScryfallId { get; set; } = string.Empty;
    public string OracleId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ManaCost { get; set; }
    public double Cmc { get; set; }
    public string TypeLine { get; set; } = string.Empty;
    public string? OracleText { get; set; }
    public string Colors { get; set; } = string.Empty;
    public string ColorIdentity { get; set; } = string.Empty;
    public string Rarity { get; set; } = string.Empty;
    public string SetCode { get; set; } = string.Empty;
    public string SetName { get; set; } = string.Empty;
    public string? CollectorNumber { get; set; }
    public string? ImageUri { get; set; }
    public string? ImageUriSmall { get; set; }
    public string? ImageUriArtCrop { get; set; }
    public string? Layout { get; set; }
    public string? Power { get; set; }
    public string? Toughness { get; set; }

    public decimal? PriceUsd { get; set; }
    public decimal? PriceUsdFoil { get; set; }
    public decimal? PriceEur { get; set; }
    public decimal? PriceEurFoil { get; set; }
    public decimal? PriceTix { get; set; }

    public List<CardFace> Faces { get; set; } = new();
    public List<CardLegality> Legalities { get; set; } = new();

    public bool IsBasicLand => TypeLine.Contains("Basic", StringComparison.OrdinalIgnoreCase)
                               && TypeLine.Contains("Land", StringComparison.OrdinalIgnoreCase);

    public bool HasMultipleFaces => Faces.Count > 1;

    public bool IsLegalIn(Format format)
    {
        var scryfallName = FormatRules.GetScryfallName(format);
        var legality = Legalities.FirstOrDefault(l => l.FormatName == scryfallName);
        return legality?.Status is LegalityStatus.Legal or LegalityStatus.Restricted;
    }

    public bool IsRestrictedIn(Format format)
    {
        var scryfallName = FormatRules.GetScryfallName(format);
        var legality = Legalities.FirstOrDefault(l => l.FormatName == scryfallName);
        return legality?.Status == LegalityStatus.Restricted;
    }
}
