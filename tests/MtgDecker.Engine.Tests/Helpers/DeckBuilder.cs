using MtgDecker.Engine;

namespace MtgDecker.Engine.Tests.Helpers;

public class DeckBuilder
{
    private readonly List<GameCard> _cards = new();

    public DeckBuilder AddCard(string name, int count, string typeLine = "Creature")
    {
        for (int i = 0; i < count; i++)
        {
            _cards.Add(new GameCard
            {
                Name = name,
                TypeLine = typeLine
            });
        }
        return this;
    }

    public DeckBuilder AddLand(string name, int count)
    {
        return AddCard(name, count, $"Basic Land — {name}");
    }

    public List<GameCard> Build() => new(_cards);
}
