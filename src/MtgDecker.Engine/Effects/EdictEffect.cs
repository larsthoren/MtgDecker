namespace MtgDecker.Engine.Effects;

public class EdictEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        if (spell.Targets.Count == 0) return;
        var target = spell.Targets[0];
        var player = state.GetPlayer(target.PlayerId);
        // Sacrifice the smallest creature (opponent's "worst" choice simplified)
        var creature = player.Battlefield.Cards
            .Where(c => c.IsCreature)
            .OrderBy(c => (c.Power ?? 0) + (c.Toughness ?? 0))
            .FirstOrDefault();
        if (creature == null) return;
        player.Battlefield.Remove(creature);
        player.Graveyard.Add(creature);
        state.Log($"{player.Name} sacrifices {creature.Name} to {spell.Card.Name}.");
    }
}
