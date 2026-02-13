namespace MtgDecker.Engine.Triggers.Effects;

public class DrawAndLoseLifeEffect : IEffect
{
    public int DrawCount { get; }
    public int LifeLoss { get; }

    public DrawAndLoseLifeEffect(int drawCount = 1, int lifeLoss = 1)
    {
        DrawCount = drawCount;
        LifeLoss = lifeLoss;
    }

    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        for (int i = 0; i < DrawCount; i++)
        {
            var drawn = context.Controller.Library.DrawFromTop();
            if (drawn != null)
                context.Controller.Hand.Add(drawn);
        }
        context.Controller.AdjustLife(-LifeLoss);
        context.State.Log($"{context.Controller.Name} draws {DrawCount} and loses {LifeLoss} life from {context.Source.Name}.");
        return Task.CompletedTask;
    }
}
