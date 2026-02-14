using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public class Zone
{
    private readonly List<GameCard> _cards = new();
    private readonly object _lock = new();

    public ZoneType Type { get; }

    /// <summary>
    /// Returns a snapshot of the cards to avoid collection-modified-during-enumeration
    /// when the Blazor UI iterates while the game engine (running on Task.Run) mutates.
    /// </summary>
    public IReadOnlyList<GameCard> Cards
    {
        get { lock (_lock) { return _cards.ToList(); } }
    }

    public int Count { get { lock (_lock) { return _cards.Count; } } }

    public Zone(ZoneType type) => Type = type;

    public void Add(GameCard card) { lock (_lock) { _cards.Add(card); } }

    public void AddToTop(GameCard card) { lock (_lock) { _cards.Add(card); } }

    public void AddToBottom(GameCard card) { lock (_lock) { _cards.Insert(0, card); } }

    public void AddRange(IEnumerable<GameCard> cards) { lock (_lock) { _cards.AddRange(cards); } }

    public GameCard? RemoveById(Guid cardId)
    {
        lock (_lock)
        {
            var card = _cards.FirstOrDefault(c => c.Id == cardId);
            if (card != null) _cards.Remove(card);
            return card;
        }
    }

    public GameCard? DrawFromTop()
    {
        lock (_lock)
        {
            if (_cards.Count == 0) return null;
            var card = _cards[^1];
            _cards.RemoveAt(_cards.Count - 1);
            return card;
        }
    }

    public IReadOnlyList<GameCard> PeekTop(int count)
    {
        lock (_lock)
        {
            if (_cards.Count == 0 || count <= 0) return [];
            var take = Math.Min(count, _cards.Count);
            return _cards.Skip(_cards.Count - take).Reverse().ToList();
        }
    }

    public void Shuffle()
    {
        lock (_lock)
        {
            var rng = Random.Shared;
            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
            }
        }
    }

    public bool Remove(GameCard card) { lock (_lock) { return _cards.Remove(card); } }

    public void Clear() { lock (_lock) { _cards.Clear(); } }

    public bool Contains(Guid cardId) { lock (_lock) { return _cards.Any(c => c.Id == cardId); } }
}
