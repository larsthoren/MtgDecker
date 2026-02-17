namespace MtgDecker.Engine.Triggers.Effects;

public class DynamicDrawAndLoseLifeEffect(Func<Player, int> countFunc) : IEffect
{
    public Func<Player, int> CountFunc { get; } = countFunc;

    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var count = CountFunc(context.Controller);
        if (count <= 0)
        {
            context.State.Log($"{context.Source.Name} triggers but finds no matching creatures.");
            return Task.CompletedTask;
        }

        for (int i = 0; i < count; i++)
        {
            var drawn = context.Controller.Library.DrawFromTop();
            if (drawn != null)
                context.Controller.Hand.Add(drawn);
        }
        context.Controller.AdjustLife(-count);
        context.State.Log($"{context.Controller.Name} draws {count} and loses {count} life from {context.Source.Name}.");
        return Task.CompletedTask;
    }
}
