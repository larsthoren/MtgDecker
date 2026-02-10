using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public class InteractiveDecisionHandler : IPlayerDecisionHandler
{
    private TaskCompletionSource<GameAction>? _actionTcs;
    private TaskCompletionSource<MulliganDecision>? _mulliganTcs;
    private TaskCompletionSource<IReadOnlyList<GameCard>>? _bottomCardsTcs;

    public bool IsWaitingForAction => _actionTcs is { Task.IsCompleted: false };
    public bool IsWaitingForMulligan => _mulliganTcs is { Task.IsCompleted: false };
    public bool IsWaitingForBottomCards => _bottomCardsTcs is { Task.IsCompleted: false };

    public Task<GameAction> GetAction(GameState gameState, Guid playerId, CancellationToken ct = default)
    {
        _actionTcs = new TaskCompletionSource<GameAction>(TaskCreationOptions.RunContinuationsAsynchronously);
        ct.Register(() => _actionTcs.TrySetCanceled());
        return _actionTcs.Task;
    }

    public Task<MulliganDecision> GetMulliganDecision(IReadOnlyList<GameCard> hand, int mulliganCount, CancellationToken ct = default)
    {
        _mulliganTcs = new TaskCompletionSource<MulliganDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        ct.Register(() => _mulliganTcs.TrySetCanceled());
        return _mulliganTcs.Task;
    }

    public Task<IReadOnlyList<GameCard>> ChooseCardsToBottom(IReadOnlyList<GameCard> hand, int count, CancellationToken ct = default)
    {
        _bottomCardsTcs = new TaskCompletionSource<IReadOnlyList<GameCard>>(TaskCreationOptions.RunContinuationsAsynchronously);
        ct.Register(() => _bottomCardsTcs.TrySetCanceled());
        return _bottomCardsTcs.Task;
    }

    public void SubmitAction(GameAction action) =>
        _actionTcs?.TrySetResult(action);

    public void SubmitMulliganDecision(MulliganDecision decision) =>
        _mulliganTcs?.TrySetResult(decision);

    public void SubmitBottomCards(IReadOnlyList<GameCard> cards) =>
        _bottomCardsTcs?.TrySetResult(cards);
}
