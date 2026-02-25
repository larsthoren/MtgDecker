namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Counter top spell effect for Decree of Silence cycling trigger.
/// Oracle: "When you cycle Decree of Silence, you may counter target spell."
/// </summary>
public class CounterTopSpellEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var opponentSpells = context.State.Stack
            .OfType<StackObject>()
            .Where(s => s.ControllerId != context.Controller.Id)
            .ToList();

        if (opponentSpells.Count == 0)
        {
            context.State.Log($"{context.Source.Name} fizzles (no spell to counter).");
            return;
        }

        // "You may" — ask if they want to counter, and which spell
        var cards = opponentSpells.Select(s => s.Card).ToList();
        var chosen = await context.DecisionHandler.ChooseCard(cards, "Counter a spell?", optional: true, ct: ct);

        if (!chosen.HasValue)
        {
            context.State.Log($"{context.Controller.Name} declines to counter.");
            return;
        }

        var targetSpell = opponentSpells.FirstOrDefault(s => s.Card.Id == chosen.Value);
        if (targetSpell == null) return;

        // Check if the target spell can't be countered
        if (CardDefinitions.TryGet(targetSpell.Card.Name, out var def) && def.CannotBeCountered)
        {
            context.State.Log($"{targetSpell.Card.Name} can't be countered.");
            return;
        }

        context.State.StackRemove(targetSpell);
        var owner = context.State.GetPlayer(targetSpell.ControllerId);
        owner.Graveyard.Add(targetSpell.Card);
        context.State.Log($"{context.Source.Name} counters {targetSpell.Card.Name}.");
    }
}
