using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine;

public class InteractiveDecisionHandler : IPlayerDecisionHandler
{
    private TaskCompletionSource<GameAction>? _actionTcs;
    private TaskCompletionSource<MulliganDecision>? _mulliganTcs;
    private TaskCompletionSource<IReadOnlyList<GameCard>>? _bottomCardsTcs;
    private TaskCompletionSource<ManaColor>? _manaColorTcs;
#pragma warning disable CS0649 // Left for potential future interactive generic payment UI
    private TaskCompletionSource<Dictionary<ManaColor, int>>? _genericPaymentTcs;
#pragma warning restore CS0649
    private TaskCompletionSource<IReadOnlyList<Guid>>? _attackersTcs;
    private TaskCompletionSource<Dictionary<Guid, Guid>>? _blockersTcs;
    private TaskCompletionSource<IReadOnlyList<Guid>>? _blockerOrderTcs;
    private TaskCompletionSource<TargetInfo>? _targetTcs;
    private TaskCompletionSource<Guid?>? _cardChoiceTcs;
    private TaskCompletionSource<bool>? _revealAckTcs;

    public bool IsWaitingForAction => _actionTcs is { Task.IsCompleted: false };
    public bool IsWaitingForMulligan => _mulliganTcs is { Task.IsCompleted: false };
    public bool IsWaitingForBottomCards => _bottomCardsTcs is { Task.IsCompleted: false };
    public bool IsWaitingForManaColor => _manaColorTcs is { Task.IsCompleted: false };
    public bool IsWaitingForGenericPayment => _genericPaymentTcs is { Task.IsCompleted: false };
    public bool IsWaitingForAttackers => _attackersTcs is { Task.IsCompleted: false };
    public bool IsWaitingForBlockers => _blockersTcs is { Task.IsCompleted: false };
    public bool IsWaitingForBlockerOrder => _blockerOrderTcs is { Task.IsCompleted: false };
    public bool IsWaitingForTarget => _targetTcs is { Task.IsCompleted: false };
    public string? TargetingSpellName { get; private set; }
    public IReadOnlyList<GameCard>? EligibleTargets { get; private set; }
    public bool IsWaitingForCardChoice => _cardChoiceTcs is { Task.IsCompleted: false };
    public bool IsWaitingForRevealAck => _revealAckTcs is { Task.IsCompleted: false };
    public IReadOnlyList<ManaColor>? ManaColorOptions { get; private set; }
    public IReadOnlyList<GameCard>? EligibleAttackers { get; private set; }
    public IReadOnlyList<GameCard>? EligibleBlockers { get; private set; }
    public IReadOnlyList<GameCard>? CurrentAttackers { get; private set; }
    public Guid? OrderingAttackerId { get; private set; }
    public IReadOnlyList<GameCard>? BlockersToOrder { get; private set; }
    public IReadOnlyList<GameCard>? CardChoiceOptions { get; private set; }
    public string? CardChoicePrompt { get; private set; }
    public bool CardChoiceOptional { get; private set; }
    public IReadOnlyList<GameCard>? RevealedCards { get; private set; }
    public IReadOnlyList<GameCard>? KeptCards { get; private set; }
    public string? RevealPrompt { get; private set; }

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

    public Task<ManaColor> ChooseManaColor(IReadOnlyList<ManaColor> options, CancellationToken ct = default)
    {
        ManaColorOptions = options;
        _manaColorTcs = new TaskCompletionSource<ManaColor>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ct.Register(() =>
        {
            ManaColorOptions = null;
            _manaColorTcs.TrySetCanceled();
        });
        _manaColorTcs.Task.ContinueWith(_ => registration.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
        OnWaitingForInput?.Invoke();
        return _manaColorTcs.Task;
    }

    public Task<Dictionary<ManaColor, int>> ChooseGenericPayment(int genericAmount, Dictionary<ManaColor, int> available, CancellationToken ct = default)
    {
        var payment = new Dictionary<ManaColor, int>();
        var remaining = genericAmount;
        foreach (var (color, amount) in available.OrderByDescending(kv => kv.Value))
        {
            if (remaining <= 0) break;
            var take = Math.Min(amount, remaining);
            payment[color] = take;
            remaining -= take;
        }
        return Task.FromResult(payment);
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

    public void SubmitManaColor(ManaColor color)
    {
        ManaColorOptions = null;
        _manaColorTcs?.TrySetResult(color);
    }

    public void SubmitGenericPayment(Dictionary<ManaColor, int> payment) =>
        _genericPaymentTcs?.TrySetResult(payment);

    public Task<IReadOnlyList<Guid>> ChooseAttackers(IReadOnlyList<GameCard> eligibleAttackers, CancellationToken ct = default)
    {
        EligibleAttackers = eligibleAttackers;
        _attackersTcs = new TaskCompletionSource<IReadOnlyList<Guid>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ct.Register(() => { EligibleAttackers = null; _attackersTcs.TrySetCanceled(); });
        _attackersTcs.Task.ContinueWith(_ => registration.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
        OnWaitingForInput?.Invoke();
        return _attackersTcs.Task;
    }

    public Task<Dictionary<Guid, Guid>> ChooseBlockers(IReadOnlyList<GameCard> eligibleBlockers, IReadOnlyList<GameCard> attackers, CancellationToken ct = default)
    {
        EligibleBlockers = eligibleBlockers;
        CurrentAttackers = attackers;
        _blockersTcs = new TaskCompletionSource<Dictionary<Guid, Guid>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ct.Register(() => { EligibleBlockers = null; CurrentAttackers = null; _blockersTcs.TrySetCanceled(); });
        _blockersTcs.Task.ContinueWith(_ => registration.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
        OnWaitingForInput?.Invoke();
        return _blockersTcs.Task;
    }

    public Task<IReadOnlyList<Guid>> OrderBlockers(Guid attackerId, IReadOnlyList<GameCard> blockers, CancellationToken ct = default)
    {
        OrderingAttackerId = attackerId;
        BlockersToOrder = blockers;
        _blockerOrderTcs = new TaskCompletionSource<IReadOnlyList<Guid>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ct.Register(() => { OrderingAttackerId = null; BlockersToOrder = null; _blockerOrderTcs.TrySetCanceled(); });
        _blockerOrderTcs.Task.ContinueWith(_ => registration.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
        OnWaitingForInput?.Invoke();
        return _blockerOrderTcs.Task;
    }

    public Task<TargetInfo> ChooseTarget(string spellName, IReadOnlyList<GameCard> eligibleTargets, Guid defaultOwnerId = default, CancellationToken ct = default)
    {
        TargetingSpellName = spellName;
        EligibleTargets = eligibleTargets;
        _targetTcs = new TaskCompletionSource<TargetInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ct.Register(() => { TargetingSpellName = null; EligibleTargets = null; _targetTcs.TrySetCanceled(); });
        _targetTcs.Task.ContinueWith(_ => registration.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
        OnWaitingForInput?.Invoke();
        return _targetTcs.Task;
    }

    public void SubmitTarget(TargetInfo target)
    {
        TargetingSpellName = null;
        EligibleTargets = null;
        _targetTcs?.TrySetResult(target);
    }

    public void SubmitAttackers(IReadOnlyList<Guid> attackerIds)
    {
        EligibleAttackers = null;
        _attackersTcs?.TrySetResult(attackerIds);
    }

    public void SubmitBlockers(Dictionary<Guid, Guid> assignments)
    {
        EligibleBlockers = null;
        CurrentAttackers = null;
        _blockersTcs?.TrySetResult(assignments);
    }

    public void SubmitBlockerOrder(IReadOnlyList<Guid> orderedBlockerIds)
    {
        OrderingAttackerId = null;
        BlockersToOrder = null;
        _blockerOrderTcs?.TrySetResult(orderedBlockerIds);
    }

    public Task<Guid?> ChooseCard(IReadOnlyList<GameCard> options, string prompt,
        bool optional = false, CancellationToken ct = default)
    {
        CardChoiceOptions = options;
        CardChoicePrompt = prompt;
        CardChoiceOptional = optional;
        _cardChoiceTcs = new TaskCompletionSource<Guid?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ct.Register(() => { CardChoiceOptions = null; CardChoicePrompt = null; _cardChoiceTcs.TrySetCanceled(); });
        _cardChoiceTcs.Task.ContinueWith(_ => registration.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
        OnWaitingForInput?.Invoke();
        return _cardChoiceTcs.Task;
    }

    public Task RevealCards(IReadOnlyList<GameCard> cards, IReadOnlyList<GameCard> kept,
        string prompt, CancellationToken ct = default)
    {
        RevealedCards = cards;
        KeptCards = kept;
        RevealPrompt = prompt;
        _revealAckTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ct.Register(() => { RevealedCards = null; KeptCards = null; RevealPrompt = null; _revealAckTcs.TrySetCanceled(); });
        _revealAckTcs.Task.ContinueWith(_ => registration.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
        OnWaitingForInput?.Invoke();
        return _revealAckTcs.Task;
    }

    public void SubmitCardChoice(Guid? cardId)
    {
        CardChoiceOptions = null;
        CardChoicePrompt = null;
        _cardChoiceTcs?.TrySetResult(cardId);
    }

    public void AcknowledgeReveal()
    {
        RevealedCards = null;
        KeptCards = null;
        RevealPrompt = null;
        _revealAckTcs?.TrySetResult(true);
    }
}
