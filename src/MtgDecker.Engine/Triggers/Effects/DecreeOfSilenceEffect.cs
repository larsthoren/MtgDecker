using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class DecreeOfSilenceEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var decree = context.Source;

        // Decree no longer on battlefield — fizzled
        if (!context.Controller.Battlefield.Contains(decree.Id)) return Task.CompletedTask;

        // Counter the top spell on the stack (the one that triggered this)
        var targetSpell = context.State.Stack
            .OfType<StackObject>()
            .FirstOrDefault(s => s.ControllerId != context.Controller.Id);

        if (targetSpell != null)
        {
            // Check if the target spell can't be countered
            if (CardDefinitions.TryGet(targetSpell.Card.Name, out var def) && def.CannotBeCountered)
            {
                context.State.Log($"{targetSpell.Card.Name} can't be countered.");
            }
            else
            {
                context.State.StackRemove(targetSpell);
                var owner = context.State.GetPlayer(targetSpell.ControllerId);
                owner.Graveyard.Add(targetSpell.Card);
                context.State.Log($"Decree of Silence counters {targetSpell.Card.Name}.");
            }
        }

        // Add a depletion counter
        decree.AddCounters(CounterType.Depletion, 1);
        var depletionCount = decree.GetCounters(CounterType.Depletion);
        context.State.Log($"Decree of Silence now has {depletionCount} depletion counter(s).");

        // If 3 or more depletion counters, sacrifice Decree of Silence
        if (depletionCount >= 3)
        {
            context.Controller.Battlefield.RemoveById(decree.Id);
            context.Controller.Graveyard.Add(decree);
            context.State.Log("Decree of Silence is sacrificed (3 depletion counters).");
        }

        return Task.CompletedTask;
    }
}
