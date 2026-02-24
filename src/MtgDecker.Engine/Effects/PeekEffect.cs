namespace MtgDecker.Engine.Effects;

/// <summary>
/// Peek - Look at target player's hand. Draw a card.
/// </summary>
public class PeekEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var controller = state.GetPlayer(spell.ControllerId);

        // Look at target player's hand
        if (spell.Targets.Count > 0)
        {
            var targetPlayer = state.GetPlayer(spell.Targets[0].PlayerId);
            await handler.RevealCards(
                targetPlayer.Hand.Cards, [],
                $"Peek: {targetPlayer.Name}'s hand", ct);
            state.Log($"{controller.Name} looks at {targetPlayer.Name}'s hand (Peek).");
        }

        // Draw a card
        var drawn = controller.Library.DrawFromTop();
        if (drawn != null)
        {
            controller.Hand.Add(drawn);
            state.Log($"{controller.Name} draws a card.");
        }
    }
}
