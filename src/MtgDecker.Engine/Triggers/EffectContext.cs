namespace MtgDecker.Engine.Triggers;

public record EffectContext(GameState State, Player Controller, GameCard Source, IPlayerDecisionHandler DecisionHandler)
{
    public GameCard? Target { get; init; }
    public Guid? TargetPlayerId { get; init; }
    public Func<GameCard, Task>? FireLeaveBattlefieldTriggers { get; init; }
}
