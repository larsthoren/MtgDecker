using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests.Helpers;

public class TestDecisionHandler : IPlayerDecisionHandler
{
    private readonly Queue<GameAction> _actions = new();
    private readonly Queue<MulliganDecision> _mulliganDecisions = new();
    private readonly Queue<Func<IReadOnlyList<GameCard>, int, IReadOnlyList<GameCard>>> _bottomChoices = new();

    public void EnqueueAction(GameAction action) => _actions.Enqueue(action);

    public void EnqueueMulligan(MulliganDecision decision) => _mulliganDecisions.Enqueue(decision);

    public void EnqueueBottomChoice(Func<IReadOnlyList<GameCard>, int, IReadOnlyList<GameCard>> chooser) =>
        _bottomChoices.Enqueue(chooser);

    public Task<GameAction> GetAction(GameState gameState, Guid playerId, CancellationToken ct = default)
    {
        if (_actions.Count == 0)
            return Task.FromResult(GameAction.Pass(playerId));
        return Task.FromResult(_actions.Dequeue());
    }

    public Task<MulliganDecision> GetMulliganDecision(IReadOnlyList<GameCard> hand, int mulliganCount, CancellationToken ct = default)
    {
        if (_mulliganDecisions.Count == 0)
            return Task.FromResult(MulliganDecision.Keep);
        return Task.FromResult(_mulliganDecisions.Dequeue());
    }

    public Task<IReadOnlyList<GameCard>> ChooseCardsToBottom(IReadOnlyList<GameCard> hand, int count, CancellationToken ct = default)
    {
        if (_bottomChoices.Count == 0)
            return Task.FromResult<IReadOnlyList<GameCard>>(hand.Take(count).ToList());
        return Task.FromResult(_bottomChoices.Dequeue()(hand, count));
    }
}
