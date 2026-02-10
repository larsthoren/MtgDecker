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

    public event Action? OnWaitingForInput;

    public Task<GameAction> GetAction(GameState gameState, Guid playerId, CancellationToken ct = default)
    {
        _actionTcs = new TaskCompletionSource<GameAction>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ct.Register(() => _actionTcs.TrySetCanceled());
        _actionTcs.Task.ContinueWith(_ => registration.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
        OnWaitingForInput?.Invoke();
        return _actionTcs.Task;
    }

    public Task<MulliganDecision> GetMulliganDecision(IReadOnlyList<GameCard> hand, int mulliganCount, CancellationToken ct = default)
    {
        _mulliganTcs = new TaskCompletionSource<MulliganDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ct.Register(() => _mulliganTcs.TrySetCanceled());
        _mulliganTcs.Task.ContinueWith(_ => registration.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
        OnWaitingForInput?.Invoke();
        return _mulliganTcs.Task;
    }

    public Task<IReadOnlyList<GameCard>> ChooseCardsToBottom(IReadOnlyList<GameCard> hand, int count, CancellationToken ct = default)
    {
        _bottomCardsTcs = new TaskCompletionSource<IReadOnlyList<GameCard>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ct.Register(() => _bottomCardsTcs.TrySetCanceled());
        _bottomCardsTcs.Task.ContinueWith(_ => registration.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
        OnWaitingForInput?.Invoke();
        return _bottomCardsTcs.Task;
    }

    public void SubmitAction(GameAction action) =>
        _actionTcs?.TrySetResult(action);

    public void SubmitMulliganDecision(MulliganDecision decision) =>
        _mulliganTcs?.TrySetResult(decision);

    public async Task SubmitBottomCardsAsync(IReadOnlyList<GameCard> cards)
    {
        // The engine creates _bottomCardsTcs after processing the Keep decision.
        // Due to RunContinuationsAsynchronously, there's a brief window where
        // the TCS doesn't exist yet. Wait for it.
        for (int i = 0; i < 50; i++)
        {
            if (_bottomCardsTcs?.TrySetResult(cards) == true)
                return;
            await Task.Delay(10);
        }
    }
}
