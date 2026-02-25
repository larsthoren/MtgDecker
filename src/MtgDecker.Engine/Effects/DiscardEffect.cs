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

    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
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
            state.LastDiscardCausedByPlayerId = spell.ControllerId; // Track who caused the discard
            if (state.HandleDiscardAsync != null)
                await state.HandleDiscardAsync(card, player, ct);
            else
            {
                player.Graveyard.Add(card);
                state.Log($"{player.Name} discards {card.Name}.");
            }
        }
    }
}
