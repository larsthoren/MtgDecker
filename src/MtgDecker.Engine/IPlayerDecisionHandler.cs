using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public interface IPlayerDecisionHandler
{
    Task<GameAction> GetAction(GameState gameState, Guid playerId, CancellationToken ct = default);
    Task<MulliganDecision> GetMulliganDecision(IReadOnlyList<GameCard> hand, int mulliganCount, CancellationToken ct = default);
    Task<IReadOnlyList<GameCard>> ChooseCardsToBottom(IReadOnlyList<GameCard> hand, int count, CancellationToken ct = default);
}
