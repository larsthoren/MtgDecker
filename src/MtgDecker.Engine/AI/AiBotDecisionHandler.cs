using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.AI;

/// <summary>
/// Heuristic AI bot that makes automated decisions for game play.
/// Implements mulligan logic, mana payment, action selection, combat decisions,
/// and card choice heuristics.
/// </summary>
public class AiBotDecisionHandler : IPlayerDecisionHandler
{
    /// <summary>
    /// Selects an action using a land-first, greedy-cast heuristic.
    /// Only acts during main phases. Prioritizes playing a land (if available
    /// and land drop unused), then casts the most expensive affordable spell.
    /// </summary>
    public Task<GameAction> GetAction(GameState gameState, Guid playerId, CancellationToken ct = default)
    {
        if (gameState.CurrentPhase != Phase.MainPhase1 && gameState.CurrentPhase != Phase.MainPhase2)
            return Task.FromResult(GameAction.Pass(playerId));

        var player = gameState.Player1.Id == playerId ? gameState.Player1 : gameState.Player2;
        var hand = player.Hand.Cards;

        if (hand.Count == 0)
            return Task.FromResult(GameAction.Pass(playerId));

        // Priority 1: Play a land
        if (player.LandsPlayedThisTurn == 0)
        {
            var land = hand.FirstOrDefault(c => c.IsLand);
            if (land != null)
                return Task.FromResult(GameAction.PlayCard(playerId, land.Id));
        }

        // Priority 2: Cast most expensive affordable spell
        var castable = hand
            .Where(c => !c.IsLand && c.ManaCost != null && player.ManaPool.CanPay(c.ManaCost))
            .OrderByDescending(c => c.ManaCost!.ConvertedManaCost)
            .FirstOrDefault();

        if (castable != null)
            return Task.FromResult(GameAction.PlayCard(playerId, castable.Id));

        return Task.FromResult(GameAction.Pass(playerId));
    }

    /// <summary>
    /// Decides whether to mulligan based on land count relative to hand size.
    /// Always keeps at 4 or fewer cards. For larger hands, requires an acceptable
    /// land count range that narrows as the hand gets smaller.
    /// </summary>
    public Task<MulliganDecision> GetMulliganDecision(IReadOnlyList<GameCard> hand, int mulliganCount, CancellationToken ct = default)
    {
        // Always keep at 4 or fewer cards
        if (hand.Count <= 4)
            return Task.FromResult(MulliganDecision.Keep);

        var landCount = hand.Count(c => c.IsLand);
        var (minLands, maxLands) = GetAcceptableLandRange(hand.Count);

        var decision = landCount >= minLands && landCount <= maxLands
            ? MulliganDecision.Keep
            : MulliganDecision.Mulligan;

        return Task.FromResult(decision);
    }

    /// <summary>
    /// Chooses cards to put on the bottom of the library after a mulligan.
    /// Prioritizes bottoming excess lands, then cheapest spells.
    /// </summary>
    public Task<IReadOnlyList<GameCard>> ChooseCardsToBottom(IReadOnlyList<GameCard> hand, int count, CancellationToken ct = default)
    {
        var result = new List<GameCard>();
        var remaining = new List<GameCard>(hand);

        // First, bottom excess lands (keep ~3 lands in a 7-card hand, scale for smaller)
        var targetLands = Math.Max(2, (int)Math.Round(remaining.Count * 0.4));
        var currentLands = remaining.Count(c => c.IsLand);

        while (result.Count < count && currentLands > targetLands)
        {
            var land = remaining.FirstOrDefault(c => c.IsLand);
            if (land is null) break;
            result.Add(land);
            remaining.Remove(land);
            currentLands--;
        }

        // Then bottom cheapest spells
        if (result.Count < count)
        {
            var spells = remaining
                .Where(c => !c.IsLand)
                .OrderBy(c => c.ManaCost?.ConvertedManaCost ?? 0)
                .ToList();

            foreach (var spell in spells)
            {
                if (result.Count >= count) break;
                result.Add(spell);
            }
        }

        // Fallback: if still not enough (shouldn't happen), take any remaining
        if (result.Count < count)
        {
            foreach (var card in remaining)
            {
                if (result.Count >= count) break;
                if (!result.Contains(card))
                    result.Add(card);
            }
        }

        return Task.FromResult<IReadOnlyList<GameCard>>(result);
    }

    /// <summary>
    /// Prefers colored mana over colorless when given a choice.
    /// </summary>
    public Task<ManaColor> ChooseManaColor(IReadOnlyList<ManaColor> options, CancellationToken ct = default)
    {
        // Prefer colored mana over colorless
        var colored = options.FirstOrDefault(c => c != ManaColor.Colorless);
        if (options.Any(c => c != ManaColor.Colorless))
            return Task.FromResult(colored);

        return Task.FromResult(options[0]);
    }

    /// <summary>
    /// Pays generic mana costs using colorless first, then from the largest pools
    /// to preserve color diversity.
    /// </summary>
    public Task<Dictionary<ManaColor, int>> ChooseGenericPayment(int genericAmount, Dictionary<ManaColor, int> available, CancellationToken ct = default)
    {
        var payment = new Dictionary<ManaColor, int>();
        var remaining = genericAmount;
        var pool = new Dictionary<ManaColor, int>(available);

        // Pay with colorless first
        if (pool.TryGetValue(ManaColor.Colorless, out var colorless) && colorless > 0)
        {
            var pay = Math.Min(colorless, remaining);
            payment[ManaColor.Colorless] = pay;
            pool[ManaColor.Colorless] -= pay;
            remaining -= pay;
        }

        // Then pay from largest pools to preserve color diversity
        while (remaining > 0)
        {
            var largest = pool
                .Where(kvp => kvp.Value > 0 && kvp.Key != ManaColor.Colorless)
                .OrderByDescending(kvp => kvp.Value)
                .FirstOrDefault();

            if (largest.Value == 0 && largest.Key == default)
                break; // No more mana available

            var color = largest.Key;
            var pay = 1; // Pay one at a time from largest pool to spread evenly
            payment.TryGetValue(color, out var current);
            payment[color] = current + pay;
            pool[color] -= pay;
            remaining -= pay;
        }

        return Task.FromResult(payment);
    }

    /// <summary>
    /// Attacks with all eligible creatures. The engine already filters for
    /// summoning sickness, so every creature passed here is ready to attack.
    /// </summary>
    public Task<IReadOnlyList<Guid>> ChooseAttackers(IReadOnlyList<GameCard> eligibleAttackers,
        CancellationToken ct = default)
    {
        var attackerIds = eligibleAttackers.Select(c => c.Id).ToList();
        return Task.FromResult<IReadOnlyList<Guid>>(attackerIds);
    }

    /// <summary>
    /// Blocks when a creature can kill the attacker (power >= attacker toughness).
    /// Uses the smallest sufficient blocker. Prioritizes blocking the biggest
    /// attacker first to maximize value. Each blocker is only assigned once.
    /// </summary>
    public Task<Dictionary<Guid, Guid>> ChooseBlockers(IReadOnlyList<GameCard> eligibleBlockers,
        IReadOnlyList<GameCard> attackers, CancellationToken ct = default)
    {
        var assignments = new Dictionary<Guid, Guid>();
        var usedBlockers = new HashSet<Guid>();

        foreach (var attacker in attackers.OrderByDescending(a => a.Power ?? 0))
        {
            var bestBlocker = eligibleBlockers
                .Where(b => !usedBlockers.Contains(b.Id))
                .Where(b => (b.Power ?? 0) >= (attacker.Toughness ?? 0))
                .OrderBy(b => b.Power ?? 0)
                .FirstOrDefault();

            if (bestBlocker != null)
            {
                assignments[bestBlocker.Id] = attacker.Id;
                usedBlockers.Add(bestBlocker.Id);
            }
        }

        return Task.FromResult(assignments);
    }

    /// <summary>
    /// Orders blockers by toughness ascending so damage kills the smallest first,
    /// maximizing the chance of killing multiple blockers.
    /// </summary>
    public Task<IReadOnlyList<Guid>> OrderBlockers(Guid attackerId, IReadOnlyList<GameCard> blockers,
        CancellationToken ct = default)
    {
        var ordered = blockers.OrderBy(b => b.Toughness ?? 0).Select(b => b.Id).ToList();
        return Task.FromResult<IReadOnlyList<Guid>>(ordered);
    }

    /// <summary>
    /// Chooses a card from options, preferring higher CMC creatures.
    /// Returns null if optional and no options are available.
    /// </summary>
    public Task<Guid?> ChooseCard(IReadOnlyList<GameCard> options, string prompt, bool optional = false, CancellationToken ct = default)
    {
        if (options.Count == 0)
            return Task.FromResult<Guid?>(optional ? null : throw new InvalidOperationException("No options available for required card choice"));

        // Prefer highest CMC (most impactful card)
        var best = options
            .OrderByDescending(c => c.ManaCost?.ConvertedManaCost ?? 0)
            .First();

        return Task.FromResult<Guid?>(best.Id);
    }

    /// <summary>
    /// Auto-acknowledges card reveals (no decision needed).
    /// </summary>
    public Task RevealCards(IReadOnlyList<GameCard> cards, IReadOnlyList<GameCard> kept, string prompt, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns the acceptable land range for a given hand size.
    /// 7 cards: 2-5 lands, 6 cards: 2-4, 5 cards: 1-4.
    /// </summary>
    private static (int min, int max) GetAcceptableLandRange(int handSize) => handSize switch
    {
        >= 7 => (2, 5),
        6 => (2, 4),
        5 => (1, 4),
        _ => (0, handSize) // Always keep at 4 or fewer (handled before this)
    };
}
