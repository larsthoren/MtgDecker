namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// When Xantid Swarm attacks, the defending player can't cast spells this turn.
/// Applies a PreventSpellCasting continuous effect until end of turn targeting the opponent.
/// </summary>
public class XantidSwarmEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        // Identify the opponent (defending player)
        var opponent = context.State.Player1.Id == context.Controller.Id
            ? context.State.Player2
            : context.State.Player1;

        var opponentId = opponent.Id;
        var effect = new ContinuousEffect(
            context.Source.Id,
            ContinuousEffectType.PreventSpellCasting,
            (_, player) => player.Id == opponentId,
            UntilEndOfTurn: true);
        context.State.ActiveEffects.Add(effect);

        context.State.Log($"{context.Source.Name} attacks — {opponent.Name} can't cast spells this turn.");
        return Task.CompletedTask;
    }
}
