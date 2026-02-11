namespace MtgDecker.Engine.Triggers;

public record EffectContext(GameState State, Player Controller, GameCard Source, IPlayerDecisionHandler DecisionHandler);
