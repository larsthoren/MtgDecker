namespace MtgDecker.Domain.Services;

public class SampleHandSimulator
{
    private readonly List<Guid> _originalLibrary;
    private List<Guid> _library = new();
    private readonly List<Guid> _hand = new();
    private readonly Random _rng = new();

    public IReadOnlyList<Guid> Hand => _hand;
    public int LibraryCount => _library.Count;
    public int MulliganCount { get; private set; }

    public SampleHandSimulator(List<Guid> cardIds)
    {
        _originalLibrary = new List<Guid>(cardIds);
    }

    public static SampleHandSimulator FromDeckEntries(List<(Guid CardId, int Quantity)> entries)
    {
        var cardIds = new List<Guid>();
        foreach (var (cardId, qty) in entries)
        {
            for (int i = 0; i < qty; i++)
                cardIds.Add(cardId);
        }
        return new SampleHandSimulator(cardIds);
    }

    public void NewGame()
    {
        _hand.Clear();
        MulliganCount = 0;
        Shuffle();
        Draw(7);
    }

    public bool DrawCard()
    {
        if (_library.Count == 0) return false;
        _hand.Add(_library[0]);
        _library.RemoveAt(0);
        return true;
    }

    public bool Mulligan()
    {
        var newHandSize = 7 - (MulliganCount + 1);
        if (newHandSize < 1) return false;

        MulliganCount++;
        _hand.Clear();
        Shuffle();
        Draw(newHandSize);
        return true;
    }

    private void Shuffle()
    {
        _library = new List<Guid>(_originalLibrary);
        for (int i = _library.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (_library[i], _library[j]) = (_library[j], _library[i]);
        }
    }

    private void Draw(int count)
    {
        var toDraw = Math.Min(count, _library.Count);
        for (int i = 0; i < toDraw; i++)
        {
            _hand.Add(_library[0]);
            _library.RemoveAt(0);
        }
    }
}
