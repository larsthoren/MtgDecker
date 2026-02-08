namespace MtgDecker.Domain.Entities;

public class CardFace
{
    public Guid Id { get; set; }
    public Guid CardId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ManaCost { get; set; }
    public string? TypeLine { get; set; }
    public string? OracleText { get; set; }
    public string? ImageUri { get; set; }
    public string? Power { get; set; }
    public string? Toughness { get; set; }
}
