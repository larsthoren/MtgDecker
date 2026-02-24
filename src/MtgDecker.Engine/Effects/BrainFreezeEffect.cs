namespace MtgDecker.Engine.Effects;

/// <summary>
/// Brain Freeze: Target player mills 3 cards. Storm.
/// Storm: when cast, copy the spell for each spell cast before it this turn.
/// Simplified: resolve mill effect (storm count + 1) times.
/// </summary>
public class BrainFreezeEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        if (spell.Targets.Count == 0) return;
        var target = spell.Targets[0];
        var targetPlayer = state.GetPlayer(target.PlayerId);

        // Storm count: spells cast this turn minus 1 (Brain Freeze itself)
        // Total copies = storm count, total resolutions = storm count + 1
        var stormCount = Math.Max(0, state.SpellsCastThisTurn - 1);
        var totalResolutions = 1 + stormCount;

        if (stormCount > 0)
            state.Log($"Storm count: {stormCount} (milling {totalResolutions * 3} total cards).");

        for (int i = 0; i < totalResolutions; i++)
        {
            MillCards(state, targetPlayer, 3);
        }
    }

    private static void MillCards(GameState state, Player player, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var card = player.Library.DrawFromTop();
            if (card != null)
            {
                player.Graveyard.Add(card);
            }
            else
            {
                // Deck-out: player loses
                var winner = state.GetOpponent(player);
                state.IsGameOver = true;
                state.Winner = winner.Name;
                state.Log($"{player.Name} loses — cannot draw from an empty library.");
                return;
            }
        }
        state.Log($"{player.Name} mills {count} cards.");
    }
}
