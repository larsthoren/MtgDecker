namespace MtgDecker.Application.Interfaces;

public interface IDeckParser
{
    string FormatName { get; }
    ParsedDeck Parse(string deckText);
}

public class ParsedDeck
{
    public List<ParsedDeckEntry> MainDeck { get; set; } = new();
    public List<ParsedDeckEntry> Sideboard { get; set; } = new();
}

public class ParsedDeckEntry
{
    public int Quantity { get; set; }
    public string CardName { get; set; } = string.Empty;
    public string? SetCode { get; set; }
    public string? CollectorNumber { get; set; }
}
