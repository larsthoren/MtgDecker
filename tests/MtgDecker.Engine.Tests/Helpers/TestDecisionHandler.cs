using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests.Helpers;

public class TestDecisionHandler : IPlayerDecisionHandler
{
    private readonly Queue<GameAction> _actions = new();
    private readonly Queue<MulliganDecision> _mulliganDecisions = new();
    private readonly Queue<Func<IReadOnlyList<GameCard>, int, IReadOnlyList<GameCard>>> _bottomChoices = new();
    private readonly Queue<ManaColor> _manaColorChoices = new();
    private readonly Queue<Dictionary<ManaColor, int>> _genericPaymentChoices = new();
    private readonly Queue<IReadOnlyList<Guid>> _attackerQueue = new();
    private readonly Queue<Dictionary<Guid, Guid>> _blockerQueue = new();
    private readonly Queue<IReadOnlyList<Guid>> _blockerOrderQueue = new();
    private readonly Queue<TargetInfo?> _targetQueue = new();
    private readonly Queue<Guid?> _cardChoiceQueue = new();
    private readonly Queue<Func<IReadOnlyList<GameCard>, int, IReadOnlyList<GameCard>>> _discardChoices = new();
    private readonly Queue<Func<IReadOnlyList<GameCard>, IReadOnlyList<GameCard>>> _splitChoices = new();
    private readonly Queue<int> _pileChoices = new();

    public void EnqueueAction(GameAction action) => _actions.Enqueue(action);

    public void EnqueueMulligan(MulliganDecision decision) => _mulliganDecisions.Enqueue(decision);

    public void EnqueueBottomChoice(Func<IReadOnlyList<GameCard>, int, IReadOnlyList<GameCard>> chooser) =>
        _bottomChoices.Enqueue(chooser);

    public void EnqueueManaColor(ManaColor color) => _manaColorChoices.Enqueue(color);

    public void EnqueueGenericPayment(Dictionary<ManaColor, int> payment) => _genericPaymentChoices.Enqueue(payment);

    public void EnqueueAttackers(IReadOnlyList<Guid> attackerIds) => _attackerQueue.Enqueue(attackerIds);
    public void EnqueueBlockers(Dictionary<Guid, Guid> assignments) => _blockerQueue.Enqueue(assignments);
    public void EnqueueBlockerOrder(IReadOnlyList<Guid> order) => _blockerOrderQueue.Enqueue(order);
    public void EnqueueTarget(TargetInfo? target) => _targetQueue.Enqueue(target);
    public void EnqueueCardChoice(Guid? cardId) => _cardChoiceQueue.Enqueue(cardId);

    public void EnqueueDiscardChoice(Func<IReadOnlyList<GameCard>, int, IReadOnlyList<GameCard>> chooser) =>
        _discardChoices.Enqueue(chooser);

    public void EnqueueSplitChoice(Func<IReadOnlyList<GameCard>, IReadOnlyList<GameCard>> chooser) =>
        _splitChoices.Enqueue(chooser);
    public void EnqueuePileChoice(int pile) => _pileChoices.Enqueue(pile);

    public Action? OnBeforeAction { get; set; }

    public Task<GameAction> GetAction(GameState gameState, Guid playerId, CancellationToken ct = default)
    {
        OnBeforeAction?.Invoke();
        if (_actions.Count == 0)
            return Task.FromResult(GameAction.Pass(playerId));
        return Task.FromResult(_actions.Dequeue());
    }

    public Action? OnBeforeMulliganDecision { get; set; }

    public Task<MulliganDecision> GetMulliganDecision(IReadOnlyList<GameCard> hand, int mulliganCount, CancellationToken ct = default)
    {
        OnBeforeMulliganDecision?.Invoke();
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

    public Task<ManaColor> ChooseManaColor(IReadOnlyList<ManaColor> options, CancellationToken ct = default)
    {
        if (_manaColorChoices.Count == 0)
            return Task.FromResult(options[0]);
        return Task.FromResult(_manaColorChoices.Dequeue());
    }

    public Task<Dictionary<ManaColor, int>> ChooseGenericPayment(int genericAmount, Dictionary<ManaColor, int> available, CancellationToken ct = default)
    {
        if (_genericPaymentChoices.Count == 0)
        {
            var payment = new Dictionary<ManaColor, int>();
            var remaining = genericAmount;
            foreach (var (color, amount) in available)
            {
                if (remaining <= 0) break;
                var take = Math.Min(amount, remaining);
                if (take > 0)
                {
                    payment[color] = take;
                    remaining -= take;
                }
            }
            return Task.FromResult(payment);
        }
        return Task.FromResult(_genericPaymentChoices.Dequeue());
    }

    public Task<IReadOnlyList<Guid>> ChooseAttackers(IReadOnlyList<GameCard> eligibleAttackers, CancellationToken ct = default)
        => Task.FromResult(_attackerQueue.Count > 0 ? _attackerQueue.Dequeue() : (IReadOnlyList<Guid>)Array.Empty<Guid>());

    public Task<Dictionary<Guid, Guid>> ChooseBlockers(IReadOnlyList<GameCard> eligibleBlockers, IReadOnlyList<GameCard> attackers, CancellationToken ct = default)
        => Task.FromResult(_blockerQueue.Count > 0 ? _blockerQueue.Dequeue() : new Dictionary<Guid, Guid>());

    public Task<IReadOnlyList<Guid>> OrderBlockers(Guid attackerId, IReadOnlyList<GameCard> blockers, CancellationToken ct = default)
        => Task.FromResult(_blockerOrderQueue.Count > 0 ? _blockerOrderQueue.Dequeue() : (IReadOnlyList<Guid>)blockers.Select(b => b.Id).ToList());

    public Task<TargetInfo?> ChooseTarget(string spellName, IReadOnlyList<GameCard> eligibleTargets, Guid defaultOwnerId = default, CancellationToken ct = default)
    {
        if (_targetQueue.Count > 0)
            return Task.FromResult(_targetQueue.Dequeue());
        var card = eligibleTargets[0];
        return Task.FromResult<TargetInfo?>(new TargetInfo(card.Id, defaultOwnerId, Enums.ZoneType.Battlefield));
    }

    public Task<Guid?> ChooseCard(IReadOnlyList<GameCard> options, string prompt,
        bool optional = false, CancellationToken ct = default)
    {
        if (_cardChoiceQueue.Count > 0)
            return Task.FromResult(_cardChoiceQueue.Dequeue());
        // Default: choose first if available, null if optional
        return Task.FromResult(options.Count > 0 ? options[0].Id : (Guid?)null);
    }

    public Task RevealCards(IReadOnlyList<GameCard> cards, IReadOnlyList<GameCard> kept,
        string prompt, CancellationToken ct = default)
    {
        // Test handler auto-acknowledges reveals
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<GameCard>> ChooseCardsToDiscard(IReadOnlyList<GameCard> hand, int discardCount, CancellationToken ct = default)
    {
        if (_discardChoices.Count == 0)
            return Task.FromResult<IReadOnlyList<GameCard>>(hand.Take(discardCount).ToList());
        return Task.FromResult(_discardChoices.Dequeue()(hand, discardCount));
    }

    public Task<IReadOnlyList<GameCard>> SplitCards(IReadOnlyList<GameCard> cards, string prompt, CancellationToken ct = default)
    {
        if (_splitChoices.Count == 0)
            return Task.FromResult<IReadOnlyList<GameCard>>(cards.Take(cards.Count / 2).ToList());
        return Task.FromResult(_splitChoices.Dequeue()(cards));
    }

    public Task<int> ChoosePile(IReadOnlyList<GameCard> pile1, IReadOnlyList<GameCard> pile2, string prompt, CancellationToken ct = default)
    {
        if (_pileChoices.Count == 0)
            return Task.FromResult(1);
        return Task.FromResult(_pileChoices.Dequeue());
    }
}
