namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Tamiyo, Seasoned Scholar -7 ability:
/// Draw cards equal to half the number of cards in your library, rounded up.
/// You get an emblem with "You have no maximum hand size."
/// </summary>
public class TamiyoUltimateEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var controller = context.Controller;
        var state = context.State;

        int cardsToDraw = (controller.Library.Count + 1) / 2;

        for (int i = 0; i < cardsToDraw; i++)
        {
            var card = controller.Library.DrawFromTop();
            if (card == null) break;
            controller.Hand.Add(card);
            controller.DrawsThisTurn++;
        }
        state.Log($"{controller.Name} draws {cardsToDraw} cards.");

        // Emblem: "You have no maximum hand size." — cosmetic; engine doesn't enforce hand size.
        controller.Emblems.Add(new Emblem(
            "You have no maximum hand size.",
            new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantPlayerShroud,
                (_, _) => false)
        ));
        state.Log($"{controller.Name} gets an emblem: no maximum hand size.");

        return Task.CompletedTask;
    }
}
