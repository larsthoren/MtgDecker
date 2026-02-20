namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Kaito's 0 ability: Surveil 2. Then draw a card for each opponent who lost life this turn.
/// Composes with the existing SurveilEffect for the surveil portion.
/// Draws directly from library to hand (DrawCardEffect pattern).
/// </summary>
public class SurveilAndDrawEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        // Step 1: Surveil 2
        var surveil = new SurveilEffect(2);
        await surveil.Execute(context, ct);

        // Step 2: Draw a card for each opponent who lost life this turn
        // In a two-player game there is exactly one opponent.
        var opponent = context.State.GetOpponent(context.Controller);
        var opponentsWhoLostLife = opponent.LifeLostThisTurn > 0 ? 1 : 0;

        for (int i = 0; i < opponentsWhoLostLife; i++)
        {
            var drawn = context.Controller.Library.DrawFromTop();
            if (drawn != null)
            {
                context.Controller.Hand.Add(drawn);
                context.State.Log($"{context.Controller.Name} draws a card.");
            }
        }
    }
}
