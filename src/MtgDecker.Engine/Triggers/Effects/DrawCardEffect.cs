namespace MtgDecker.Engine.Triggers.Effects;

public class DrawCardEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var drawn = context.Controller.Library.DrawFromTop();
        if (drawn != null)
        {
            context.Controller.Hand.Add(drawn);
            context.State.Log($"{context.Controller.Name} draws a card.");
        }
        return Task.CompletedTask;
    }
}
