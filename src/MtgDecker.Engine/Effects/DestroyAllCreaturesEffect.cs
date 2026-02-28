namespace MtgDecker.Engine.Effects;

public class DestroyAllCreaturesEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        foreach (var player in state.Players)
        {
            var creatures = player.Battlefield.Cards.Where(c => c.IsCreature).ToList();
            foreach (var creature in creatures)
            {
                player.Battlefield.Remove(creature);
                player.Graveyard.Add(creature);
            }
        }
        state.Log($"{spell.Card.Name} destroys all creatures.");
    }
}
