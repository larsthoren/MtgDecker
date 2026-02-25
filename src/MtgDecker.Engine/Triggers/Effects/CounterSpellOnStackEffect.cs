namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Presence of the Master: Whenever a player casts an enchantment spell, counter it.
/// This is a triggered ability that counters the most recently cast enchantment on the stack.
/// </summary>
public class CounterSpellOnStackEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        // Find the most recent enchantment spell on the stack (the one that triggered this)
        var enchantmentSpell = context.State.Stack
            .OfType<StackObject>()
            .LastOrDefault(s => s.Card.CardTypes.HasFlag(Enums.CardType.Enchantment));

        if (enchantmentSpell == null)
        {
            context.State.Log($"{context.Source.Name} fizzles (enchantment spell already resolved).");
            return Task.CompletedTask;
        }

        // Don't counter Presence of the Master itself
        if (enchantmentSpell.Card.Name == context.Source.Name)
        {
            return Task.CompletedTask;
        }

        // Check if spell can't be countered
        if (CardDefinitions.TryGet(enchantmentSpell.Card.Name, out var def) && def.CannotBeCountered)
        {
            context.State.Log($"{enchantmentSpell.Card.Name} can't be countered.");
            return Task.CompletedTask;
        }

        // Remove from stack and put into graveyard
        context.State.StackRemove(enchantmentSpell);
        var owner = context.State.GetPlayer(enchantmentSpell.ControllerId);
        owner.Graveyard.Add(enchantmentSpell.Card);

        context.State.Log($"{enchantmentSpell.Card.Name} is countered by {context.Source.Name}.");

        return Task.CompletedTask;
    }
}
