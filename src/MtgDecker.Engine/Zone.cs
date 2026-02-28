using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public class Zone
{
    private readonly List<GameCard> _cards = new();
    private readonly HashSet<Guid> _cardIds = new();
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

    public void Add(GameCard card) { lock (_lock) { _cards.Add(card); _cardIds.Add(card.Id); } }

    public void AddToTop(GameCard card) { lock (_lock) { _cards.Add(card); _cardIds.Add(card.Id); } }

    public void AddToBottom(GameCard card) { lock (_lock) { _cards.Insert(0, card); _cardIds.Add(card.Id); } }

    public void AddRange(IEnumerable<GameCard> cards)
    {
        lock (_lock)
        {
            var list = cards as IList<GameCard> ?? cards.ToList();
            _cards.AddRange(list);
            foreach (var card in list)
                _cardIds.Add(card.Id);
        }
    }

    public GameCard? RemoveById(Guid cardId)
    {
        lock (_lock)
        {
            var index = _cards.FindIndex(c => c.Id == cardId);
            if (index < 0) return null;
            var card = _cards[index];
            _cards.RemoveAt(index);
            _cardIds.Remove(cardId);
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
            _cardIds.Remove(card.Id);
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

    public bool Remove(GameCard card)
    {
        lock (_lock)
        {
            var removed = _cards.Remove(card);
            if (removed) _cardIds.Remove(card.Id);
            return removed;
        }
    }

    public void Clear() { lock (_lock) { _cards.Clear(); _cardIds.Clear(); } }

    public bool Contains(Guid cardId) { lock (_lock) { return _cardIds.Contains(cardId); } }
}
