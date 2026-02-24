namespace MtgDecker.Engine.Effects;

/// <summary>
/// Accumulated Knowledge - Draw a card, then draw cards equal to the number of
/// cards named Accumulated Knowledge in all graveyards.
/// </summary>
public class AccumulatedKnowledgeEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        var player = state.GetPlayer(spell.ControllerId);

        // Count Accumulated Knowledge in ALL graveyards (before this spell goes to graveyard)
        var akCount = state.Player1.Graveyard.Cards
            .Count(c => c.Name.Equals("Accumulated Knowledge", StringComparison.OrdinalIgnoreCase))
            + state.Player2.Graveyard.Cards
            .Count(c => c.Name.Equals("Accumulated Knowledge", StringComparison.OrdinalIgnoreCase));

        // Draw 1 + count in graveyards
        var totalDraw = 1 + akCount;
        var drawn = 0;
        for (int i = 0; i < totalDraw; i++)
        {
            var card = player.Library.DrawFromTop();
            if (card == null) break;
            player.Hand.Add(card);
            drawn++;
        }
        state.Log($"{player.Name} draws {drawn} card(s) (Accumulated Knowledge, {akCount} in graveyards).");
    }
}
