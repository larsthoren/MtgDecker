using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine;

public interface IPlayerDecisionHandler
{
    Task<GameAction> GetAction(GameState gameState, Guid playerId, CancellationToken ct = default);
    Task<MulliganDecision> GetMulliganDecision(IReadOnlyList<GameCard> hand, int mulliganCount, CancellationToken ct = default);
    Task<IReadOnlyList<GameCard>> ChooseCardsToBottom(IReadOnlyList<GameCard> hand, int count, CancellationToken ct = default);
    Task<ManaColor> ChooseManaColor(IReadOnlyList<ManaColor> options, CancellationToken ct = default);
    Task<Dictionary<ManaColor, int>> ChooseGenericPayment(int genericAmount, Dictionary<ManaColor, int> available, CancellationToken ct = default);
}
