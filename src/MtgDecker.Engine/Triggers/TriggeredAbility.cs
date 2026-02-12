namespace MtgDecker.Engine.Triggers;

public class TriggeredAbility(GameCard source, Player controller, Trigger trigger)
{
    public GameCard Source { get; } = source;
    public Player Controller { get; } = controller;
    public Trigger Trigger { get; } = trigger;

    public async Task ResolveAsync(GameState state, CancellationToken ct = default)
    {
        var context = new EffectContext(state, Controller, Source, Controller.DecisionHandler);
        await Trigger.Effect.Execute(context, ct);
    }
}
