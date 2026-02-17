namespace MtgDecker.Engine.Triggers.Effects;

public class StandstillEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var state = context.State;
        var source = context.Source;
        var controller = context.Controller;

        // Sacrifice Standstill — only if still on the battlefield
        if (!controller.Battlefield.Contains(source.Id))
            return Task.CompletedTask;

        controller.Battlefield.RemoveById(source.Id);
        controller.Graveyard.Add(source);
        state.Log($"{source.Name} is sacrificed.");

        // The caster is the active player (who triggered SpellCast)
        var caster = state.ActivePlayer;

        // Each of the caster's opponents draws 3 cards
        var opponent = state.GetOpponent(caster);
        for (int i = 0; i < 3; i++)
        {
            var drawn = opponent.Library.DrawFromTop();
            if (drawn != null)
            {
                opponent.Hand.Add(drawn);
                state.Log($"{opponent.Name} draws a card ({drawn.Name}).");
            }
        }

        return Task.CompletedTask;
    }
}
