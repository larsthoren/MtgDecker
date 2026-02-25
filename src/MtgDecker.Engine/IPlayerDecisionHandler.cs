using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine;

public interface IPlayerDecisionHandler
{
    Task<GameAction> GetAction(GameState gameState, Guid playerId, CancellationToken ct = default);
    Task<MulliganDecision> GetMulliganDecision(IReadOnlyList<GameCard> hand, int mulliganCount, CancellationToken ct = default);
    Task<IReadOnlyList<GameCard>> ChooseCardsToBottom(IReadOnlyList<GameCard> hand, int count, CancellationToken ct = default);
    Task<ManaColor> ChooseManaColor(IReadOnlyList<ManaColor> options, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> ChooseAttackers(IReadOnlyList<GameCard> eligibleAttackers, CancellationToken ct = default);
    Task<Dictionary<Guid, Guid?>> ChooseAttackerTargets(IReadOnlyList<GameCard> attackers, IReadOnlyList<GameCard> planeswalkers, CancellationToken ct = default);
    Task<Dictionary<Guid, Guid>> ChooseBlockers(IReadOnlyList<GameCard> eligibleBlockers, IReadOnlyList<GameCard> attackers, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> OrderBlockers(Guid attackerId, IReadOnlyList<GameCard> blockers, CancellationToken ct = default);
    Task<TargetInfo?> ChooseTarget(string spellName, IReadOnlyList<GameCard> eligibleTargets, Guid defaultOwnerId = default, CancellationToken ct = default);

    Task<Guid?> ChooseCard(IReadOnlyList<GameCard> options, string prompt,
        bool optional = false, CancellationToken ct = default);

    Task RevealCards(IReadOnlyList<GameCard> cards, IReadOnlyList<GameCard> kept,
        string prompt, CancellationToken ct = default);

    Task<IReadOnlyList<GameCard>> ChooseCardsToDiscard(IReadOnlyList<GameCard> hand, int discardCount, CancellationToken ct = default);

    Task<IReadOnlyList<GameCard>> SplitCards(IReadOnlyList<GameCard> cards, string prompt, CancellationToken ct = default);
    Task<int> ChoosePile(IReadOnlyList<GameCard> pile1, IReadOnlyList<GameCard> pile2, string prompt, CancellationToken ct = default);

    /// <summary>
    /// Reorder cards (e.g. Ponder). Player clicks cards one by one to place back on library
    /// (last placed = top), then chooses shuffle or no shuffle.
    /// Returns (ordered list where first = placed first = deepest, last = placed last = top, shuffle decision).
    /// </summary>
    Task<(IReadOnlyList<GameCard> ordered, bool shuffle)> ReorderCards(
        IReadOnlyList<GameCard> cards, string prompt, CancellationToken ct = default);

    Task<IReadOnlyList<GameCard>> ChooseCardsToExile(
        IReadOnlyList<GameCard> options, int maxCount, string prompt, CancellationToken ct = default);

    Task<string> ChooseCreatureType(string prompt, CancellationToken ct = default);
    Task<string> ChooseCardName(string prompt, CancellationToken ct = default);

    /// <summary>
    /// Asks the player whether to cast a card for its madness cost after discarding it.
    /// </summary>
    Task<bool> ChooseMadness(GameCard card, ManaCost madnessCost, CancellationToken ct = default);
}

/// <summary>
/// Marker interface indicating the decision handler uses MTGO-style manual mana payment.
/// When a player's handler implements this, generic/Phyrexian costs enter mid-cast state
/// requiring explicit PayManaFromPool/PayLifeForPhyrexian actions.
/// Otherwise, remaining costs are auto-resolved after colored mana deduction.
/// </summary>
public interface IManualManaPayment { }
