namespace MtgDecker.Application.Interfaces;

public interface IDeckParser
{
    string FormatName { get; }
    ParsedDeck Parse(string deckText);
}
