using System.Text.Json.Serialization;

namespace MtgDecker.Infrastructure.Scryfall;

public class ScryfallCard
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("oracle_id")]
    public string OracleId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("mana_cost")]
    public string? ManaCost { get; set; }

    [JsonPropertyName("cmc")]
    public double Cmc { get; set; }

    [JsonPropertyName("type_line")]
    public string TypeLine { get; set; } = string.Empty;

    [JsonPropertyName("oracle_text")]
    public string? OracleText { get; set; }

    [JsonPropertyName("colors")]
    public List<string>? Colors { get; set; }

    [JsonPropertyName("color_identity")]
    public List<string>? ColorIdentity { get; set; }

    [JsonPropertyName("rarity")]
    public string Rarity { get; set; } = string.Empty;

    [JsonPropertyName("set")]
    public string SetCode { get; set; } = string.Empty;

    [JsonPropertyName("set_name")]
    public string SetName { get; set; } = string.Empty;

    [JsonPropertyName("collector_number")]
    public string? CollectorNumber { get; set; }

    [JsonPropertyName("layout")]
    public string? Layout { get; set; }

    [JsonPropertyName("image_uris")]
    public ScryfallImageUris? ImageUris { get; set; }

    [JsonPropertyName("legalities")]
    public Dictionary<string, string>? Legalities { get; set; }

    [JsonPropertyName("card_faces")]
    public List<ScryfallCardFace>? CardFaces { get; set; }

    [JsonPropertyName("prices")]
    public ScryfallPrices? Prices { get; set; }
}

public class ScryfallImageUris
{
    [JsonPropertyName("normal")]
    public string? Normal { get; set; }

    [JsonPropertyName("small")]
    public string? Small { get; set; }

    [JsonPropertyName("art_crop")]
    public string? ArtCrop { get; set; }
}

public class ScryfallCardFace
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("mana_cost")]
    public string? ManaCost { get; set; }

    [JsonPropertyName("type_line")]
    public string? TypeLine { get; set; }

    [JsonPropertyName("oracle_text")]
    public string? OracleText { get; set; }

    [JsonPropertyName("power")]
    public string? Power { get; set; }

    [JsonPropertyName("toughness")]
    public string? Toughness { get; set; }

    [JsonPropertyName("image_uris")]
    public ScryfallImageUris? ImageUris { get; set; }
}

public class ScryfallPrices
{
    [JsonPropertyName("usd")]
    public string? Usd { get; set; }

    [JsonPropertyName("usd_foil")]
    public string? UsdFoil { get; set; }

    [JsonPropertyName("eur")]
    public string? Eur { get; set; }

    [JsonPropertyName("eur_foil")]
    public string? EurFoil { get; set; }

    [JsonPropertyName("tix")]
    public string? Tix { get; set; }
}

public class ScryfallBulkDataResponse
{
    [JsonPropertyName("data")]
    public List<ScryfallBulkDataEntry> Data { get; set; } = new();
}

public class ScryfallBulkDataEntry
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("download_uri")]
    public string DownloadUri { get; set; } = string.Empty;

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
