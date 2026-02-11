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
    Task<IReadOnlyList<Guid>> ChooseAttackers(IReadOnlyList<GameCard> eligibleAttackers, CancellationToken ct = default);
    Task<Dictionary<Guid, Guid>> ChooseBlockers(IReadOnlyList<GameCard> eligibleBlockers, IReadOnlyList<GameCard> attackers, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> OrderBlockers(Guid attackerId, IReadOnlyList<GameCard> blockers, CancellationToken ct = default);
    Task<TargetInfo> ChooseTarget(string spellName, IReadOnlyList<GameCard> eligibleTargets, Guid defaultOwnerId = default, CancellationToken ct = default);
}
