namespace MtgDecker.Engine.Effects;

public class DestroyAllLandsEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        foreach (var player in new[] { state.Player1, state.Player2 })
        {
            var lands = player.Battlefield.Cards.Where(c => c.IsLand).ToList();
            foreach (var land in lands)
            {
                // Track who caused land destruction for Sacred Ground
                state.LastLandDestroyedByPlayerId = spell.ControllerId;
                player.Battlefield.Remove(land);
                player.Graveyard.Add(land);
            }
        }
        state.Log($"{spell.Card.Name} destroys all lands.");
    }
}
