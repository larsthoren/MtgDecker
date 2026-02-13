namespace MtgDecker.Engine.Effects;

public class DiscardEffect : SpellEffect
{
    public int Count { get; }
    public Func<GameCard, bool>? Filter { get; }

    public DiscardEffect(int count = 1, Func<GameCard, bool>? filter = null)
    {
        Count = count;
        Filter = filter;
    }

    public override void Resolve(GameState state, StackObject spell)
    {
        if (spell.Targets.Count == 0) return;
        var target = spell.Targets[0];
        var player = state.GetPlayer(target.PlayerId);
        var candidates = Filter != null
            ? player.Hand.Cards.Where(Filter).ToList()
            : player.Hand.Cards.ToList();

        for (int i = 0; i < Count && candidates.Count > 0; i++)
        {
            var card = candidates[0];
            candidates.RemoveAt(0);
            player.Hand.Remove(card);
            player.Graveyard.Add(card);
            state.Log($"{player.Name} discards {card.Name}.");
        }
    }
}
