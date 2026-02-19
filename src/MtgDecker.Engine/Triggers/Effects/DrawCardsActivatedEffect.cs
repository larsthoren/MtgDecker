namespace MtgDecker.Engine.Triggers.Effects;

public class DrawCardsActivatedEffect(int count) : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        for (int i = 0; i < count; i++)
        {
            var drawn = context.Controller.Library.DrawFromTop();
            if (drawn != null)
            {
                context.Controller.Hand.Add(drawn);
            }
            else
            {
                context.State.IsGameOver = true;
                context.State.Winner = context.State.GetOpponent(context.Controller).Name;
                context.State.Log($"{context.Controller.Name} cannot draw — loses the game.");
                return Task.CompletedTask;
            }
        }
        context.State.Log($"{context.Controller.Name} draws {count} cards.");
        return Task.CompletedTask;
    }
}
