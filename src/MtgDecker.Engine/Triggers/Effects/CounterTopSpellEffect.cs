namespace MtgDecker.Engine.Triggers.Effects;

public class CounterTopSpellEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        // Counter the top spell on the stack (most recent opponent spell)
        var targetSpell = context.State.Stack
            .OfType<StackObject>()
            .FirstOrDefault(s => s.ControllerId != context.Controller.Id);

        if (targetSpell == null)
        {
            context.State.Log($"{context.Source.Name} fizzles (no spell to counter).");
            return Task.CompletedTask;
        }

        // Check if the target spell can't be countered
        if (CardDefinitions.TryGet(targetSpell.Card.Name, out var def) && def.CannotBeCountered)
        {
            context.State.Log($"{targetSpell.Card.Name} can't be countered.");
            return Task.CompletedTask;
        }

        context.State.StackRemove(targetSpell);
        var owner = context.State.GetPlayer(targetSpell.ControllerId);
        owner.Graveyard.Add(targetSpell.Card);
        context.State.Log($"{context.Source.Name} counters {targetSpell.Card.Name}.");

        return Task.CompletedTask;
    }
}
