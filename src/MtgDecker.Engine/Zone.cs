using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public class Zone
{
    private readonly List<GameCard> _cards = new();

    public ZoneType Type { get; }
    public IReadOnlyList<GameCard> Cards => _cards.AsReadOnly();
    public int Count => _cards.Count;

    public Zone(ZoneType type) => Type = type;

    public void Add(GameCard card) => _cards.Add(card);

    public void AddToBottom(GameCard card) => _cards.Insert(0, card);

    public void AddRange(IEnumerable<GameCard> cards) => _cards.AddRange(cards);

    public GameCard? RemoveById(Guid cardId)
    {
        var card = _cards.FirstOrDefault(c => c.Id == cardId);
        if (card != null) _cards.Remove(card);
        return card;
    }

    public GameCard? DrawFromTop()
    {
        if (_cards.Count == 0) return null;
        var card = _cards[^1];
        _cards.RemoveAt(_cards.Count - 1);
        return card;
    }

    public IReadOnlyList<GameCard> PeekTop(int count)
    {
        if (_cards.Count == 0 || count <= 0) return [];
        var take = Math.Min(count, _cards.Count);
        return _cards.Skip(_cards.Count - take).Reverse().ToList();
    }

    public void Shuffle()
    {
        var rng = Random.Shared;
        for (int i = _cards.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
        }
    }

    public void Clear() => _cards.Clear();

    public bool Contains(Guid cardId) => _cards.Any(c => c.Id == cardId);
}
