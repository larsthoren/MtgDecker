using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine;

public class InteractiveDecisionHandler : IPlayerDecisionHandler, IManualManaPayment
{
    private TaskCompletionSource<GameAction>? _actionTcs;
    private TaskCompletionSource<MulliganDecision>? _mulliganTcs;
    private TaskCompletionSource<IReadOnlyList<GameCard>>? _bottomCardsTcs;
    private TaskCompletionSource<bool>? _bottomCardsReadyTcs;
    private TaskCompletionSource<ManaColor>? _manaColorTcs;
    private TaskCompletionSource<IReadOnlyList<Guid>>? _attackersTcs;
    private TaskCompletionSource<Dictionary<Guid, Guid?>>? _attackerTargetsTcs;
    private TaskCompletionSource<Dictionary<Guid, Guid>>? _blockersTcs;
    private TaskCompletionSource<IReadOnlyList<Guid>>? _blockerOrderTcs;
    private TaskCompletionSource<TargetInfo?>? _targetTcs;
    private TaskCompletionSource<Guid?>? _cardChoiceTcs;
    private TaskCompletionSource<bool>? _revealAckTcs;
    private TaskCompletionSource<IReadOnlyList<GameCard>>? _discardTcs;
    private TaskCompletionSource<IReadOnlyList<GameCard>>? _splitCardsTcs;
    private TaskCompletionSource<int>? _choosePileTcs;
    private TaskCompletionSource<(IReadOnlyList<GameCard> ordered, bool shuffle)>? _reorderTcs;

    public bool IsWaitingForAction => _actionTcs is { Task.IsCompleted: false };
    public bool IsWaitingForMulligan => _mulliganTcs is { Task.IsCompleted: false };
    public bool IsWaitingForBottomCards => _bottomCardsTcs is { Task.IsCompleted: false };
    public bool IsWaitingForManaColor => _manaColorTcs is { Task.IsCompleted: false };
    public bool IsWaitingForAttackers => _attackersTcs is { Task.IsCompleted: false };
    public bool IsWaitingForAttackerTargets => _attackerTargetsTcs is { Task.IsCompleted: false };
    public bool IsWaitingForBlockers => _blockersTcs is { Task.IsCompleted: false };
    public bool IsWaitingForBlockerOrder => _blockerOrderTcs is { Task.IsCompleted: false };
    public bool IsWaitingForTarget => _targetTcs is { Task.IsCompleted: false };
    public string? TargetingSpellName { get; private set; }
    public IReadOnlyList<GameCard>? EligibleTargets { get; private set; }
    public bool IsWaitingForCardChoice => _cardChoiceTcs is { Task.IsCompleted: false };
    public bool IsWaitingForRevealAck => _revealAckTcs is { Task.IsCompleted: false };
    public bool IsWaitingForDiscard => _discardTcs is { Task.IsCompleted: false };
    public IReadOnlyList<GameCard>? DiscardOptions { get; private set; }
    public int DiscardCount { get; private set; }
    public string? DiscardPrompt { get; private set; }
    public bool IsWaitingForSplit => _splitCardsTcs is { Task.IsCompleted: false };
    public IReadOnlyList<GameCard>? SplitOptions { get; private set; }
    public string? SplitPrompt { get; private set; }
    public bool IsWaitingForPileChoice => _choosePileTcs is { Task.IsCompleted: false };
    public IReadOnlyList<GameCard>? Pile1Options { get; private set; }
    public IReadOnlyList<GameCard>? Pile2Options { get; private set; }
    public string? PileChoicePrompt { get; private set; }
    public bool IsWaitingForReorder => _reorderTcs is { Task.IsCompleted: false };
    public IReadOnlyList<GameCard>? ReorderOptions { get; private set; }
    public string? ReorderPrompt { get; private set; }

    /// <summary>
    /// True when this handler is waiting for any player input, meaning the
    /// game engine has yielded and is not actively mutating game state.
    /// </summary>
    public bool IsWaitingForInput =>
        IsWaitingForAction || IsWaitingForMulligan || IsWaitingForBottomCards
        || IsWaitingForManaColor
        || IsWaitingForAttackers || IsWaitingForAttackerTargets
        || IsWaitingForBlockers || IsWaitingForBlockerOrder
        || IsWaitingForTarget || IsWaitingForCardChoice || IsWaitingForRevealAck
        || IsWaitingForDiscard || IsWaitingForSplit || IsWaitingForPileChoice
        || IsWaitingForReorder;

    public IReadOnlyList<ManaColor>? ManaColorOptions { get; private set; }
    public IReadOnlyList<GameCard>? EligibleAttackers { get; private set; }
    public IReadOnlyList<GameCard>? AttackerTargetAttackers { get; private set; }
    public IReadOnlyList<GameCard>? AttackerTargetPlaneswalkers { get; private set; }
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

    public PhaseStopSettings PhaseStops { get; } = new();

    public bool ShouldAutoPass(Phase phase, CombatStep combatStep, bool stackEmpty)
    {
        if (!stackEmpty) return false;
        if (phase == Phase.Combat && combatStep != CombatStep.None)
            return !PhaseStops.ShouldStop(combatStep);
        return !PhaseStops.ShouldStop(phase);
    }

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
        _bottomCardsReadyTcs?.TrySetResult(true);
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

    public void SubmitAction(GameAction action) =>
        _actionTcs?.TrySetResult(action);

    public void SubmitMulliganDecision(MulliganDecision decision) =>
        _mulliganTcs?.TrySetResult(decision);

    public async Task SubmitBottomCardsAsync(IReadOnlyList<GameCard> cards)
    {
        // The engine creates _bottomCardsTcs after processing the Keep decision.
        // Due to RunContinuationsAsynchronously, there's a brief window where
        // the TCS doesn't exist yet. Wait for the ready signal.
        if (_bottomCardsTcs?.TrySetResult(cards) == true)
            return;

        _bottomCardsReadyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await _bottomCardsReadyTcs.Task;
        _bottomCardsTcs?.TrySetResult(cards);
    }

    public void SubmitManaColor(ManaColor color)
    {
        ManaColorOptions = null;
        _manaColorTcs?.TrySetResult(color);
    }

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

    public Task<TargetInfo?> ChooseTarget(string spellName, IReadOnlyList<GameCard> eligibleTargets, Guid defaultOwnerId = default, CancellationToken ct = default)
    {
        TargetingSpellName = spellName;
        EligibleTargets = eligibleTargets;
        _targetTcs = new TaskCompletionSource<TargetInfo?>(TaskCreationOptions.RunContinuationsAsynchronously);
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

    public void CancelTarget()
    {
        TargetingSpellName = null;
        EligibleTargets = null;
        _targetTcs?.TrySetResult(null);
    }

    public void SubmitAttackers(IReadOnlyList<Guid> attackerIds)
    {
        EligibleAttackers = null;
        _attackersTcs?.TrySetResult(attackerIds);
    }

    public Task<Dictionary<Guid, Guid?>> ChooseAttackerTargets(IReadOnlyList<GameCard> attackers, IReadOnlyList<GameCard> planeswalkers, CancellationToken ct = default)
    {
        AttackerTargetAttackers = attackers;
        AttackerTargetPlaneswalkers = planeswalkers;
        _attackerTargetsTcs = new TaskCompletionSource<Dictionary<Guid, Guid?>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ct.Register(() => { AttackerTargetAttackers = null; AttackerTargetPlaneswalkers = null; _attackerTargetsTcs.TrySetCanceled(); });
        _attackerTargetsTcs.Task.ContinueWith(_ => registration.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
        OnWaitingForInput?.Invoke();
        return _attackerTargetsTcs.Task;
    }

    public void SubmitAttackerTargets(Dictionary<Guid, Guid?> targets)
    {
        AttackerTargetAttackers = null;
        AttackerTargetPlaneswalkers = null;
        _attackerTargetsTcs?.TrySetResult(targets);
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

    public Task<IReadOnlyList<GameCard>> ChooseCardsToDiscard(IReadOnlyList<GameCard> hand, int discardCount, CancellationToken ct = default)
    {
        DiscardOptions = hand;
        DiscardCount = discardCount;
        DiscardPrompt = $"Discard {discardCount} card(s) to hand size";
        _discardTcs = new TaskCompletionSource<IReadOnlyList<GameCard>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ct.Register(() => { DiscardOptions = null; DiscardCount = 0; DiscardPrompt = null; _discardTcs.TrySetCanceled(); });
        _discardTcs.Task.ContinueWith(_ => registration.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
        OnWaitingForInput?.Invoke();
        return _discardTcs.Task;
    }

    public void SubmitDiscard(IReadOnlyList<GameCard> cards)
    {
        DiscardOptions = null;
        DiscardCount = 0;
        DiscardPrompt = null;
        _discardTcs?.TrySetResult(cards);
    }

    public Task<IReadOnlyList<GameCard>> SplitCards(IReadOnlyList<GameCard> cards, string prompt, CancellationToken ct = default)
    {
        SplitOptions = cards;
        SplitPrompt = prompt;
        _splitCardsTcs = new TaskCompletionSource<IReadOnlyList<GameCard>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ct.Register(() => { SplitOptions = null; SplitPrompt = null; _splitCardsTcs.TrySetCanceled(); });
        _splitCardsTcs.Task.ContinueWith(_ => registration.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
        OnWaitingForInput?.Invoke();
        return _splitCardsTcs.Task;
    }

    public void SubmitSplit(IReadOnlyList<GameCard> pile1Cards)
    {
        SplitOptions = null;
        SplitPrompt = null;
        _splitCardsTcs?.TrySetResult(pile1Cards);
    }

    public Task<int> ChoosePile(IReadOnlyList<GameCard> pile1, IReadOnlyList<GameCard> pile2, string prompt, CancellationToken ct = default)
    {
        Pile1Options = pile1;
        Pile2Options = pile2;
        PileChoicePrompt = prompt;
        _choosePileTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ct.Register(() => { Pile1Options = null; Pile2Options = null; PileChoicePrompt = null; _choosePileTcs.TrySetCanceled(); });
        _choosePileTcs.Task.ContinueWith(_ => registration.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
        OnWaitingForInput?.Invoke();
        return _choosePileTcs.Task;
    }

    public void SubmitPileChoice(int pile)
    {
        Pile1Options = null;
        Pile2Options = null;
        PileChoicePrompt = null;
        _choosePileTcs?.TrySetResult(pile);
    }

    public Task<(IReadOnlyList<GameCard> ordered, bool shuffle)> ReorderCards(
        IReadOnlyList<GameCard> cards, string prompt, CancellationToken ct = default)
    {
        ReorderOptions = cards;
        ReorderPrompt = prompt;
        _reorderTcs = new TaskCompletionSource<(IReadOnlyList<GameCard>, bool)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ct.Register(() => { ReorderOptions = null; ReorderPrompt = null; _reorderTcs.TrySetCanceled(); });
        _reorderTcs.Task.ContinueWith(_ => registration.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
        OnWaitingForInput?.Invoke();
        return _reorderTcs.Task;
    }

    public void SubmitReorder(IReadOnlyList<GameCard> ordered, bool shuffle)
    {
        ReorderOptions = null;
        ReorderPrompt = null;
        _reorderTcs?.TrySetResult((ordered, shuffle));
    }



    // Reuses the discard UI state (DiscardOptions/DiscardCount/DiscardPrompt) since the
    // interaction is identical (select N cards, confirm). Safe because the engine processes
    // costs sequentially — discard and exile never overlap.
    public async Task<IReadOnlyList<GameCard>> ChooseCardsToExile(
        IReadOnlyList<GameCard> options, int maxCount, string prompt, CancellationToken ct = default)
    {
        DiscardOptions = options.ToList();
        DiscardCount = maxCount;
        DiscardPrompt = prompt;
        _discardTcs = new TaskCompletionSource<IReadOnlyList<GameCard>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ct.Register(() => { DiscardOptions = null; DiscardCount = 0; DiscardPrompt = null; _discardTcs.TrySetCanceled(); });
        _ = _discardTcs.Task.ContinueWith(_ => registration.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
        OnWaitingForInput?.Invoke();

        var result = await _discardTcs.Task;
        DiscardOptions = null;
        DiscardCount = 0;
        DiscardPrompt = null;
        return result;
    }
}
