using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.AI;

/// <summary>
/// Heuristic AI bot that makes automated decisions for game play.
/// Implements mulligan logic, mana payment, action selection, combat decisions,
/// and card choice heuristics.
/// </summary>
public class AiBotDecisionHandler : IPlayerDecisionHandler
{
    /// <summary>
    /// Delay in milliseconds before each AI decision, allowing human players
    /// to follow the action. Set to 0 for instant decisions (e.g. simulations).
    /// </summary>
    public int ActionDelayMs { get; set; } = 1000;

    private async Task DelayAsync(CancellationToken ct)
    {
        if (ActionDelayMs > 0)
            await Task.Delay(ActionDelayMs, ct);
    }

    /// <summary>
    /// Selects an action using a land-first, fetch, tap-lands, greedy-cast heuristic.
    /// Only acts during main phases. Prioritizes playing a land (if available
    /// and land drop unused), then activates fetch lands if spells are in hand,
    /// then taps untapped lands with mana abilities, then casts the most expensive
    /// affordable spell.
    /// </summary>
    public async Task<GameAction> GetAction(GameState gameState, Guid playerId, CancellationToken ct = default)
    {
        if (gameState.CurrentPhase != Phase.MainPhase1 && gameState.CurrentPhase != Phase.MainPhase2)
            return GameAction.Pass(playerId);

        // Non-active player passes priority (this bot doesn't play instants)
        if (gameState.ActivePlayer.Id != playerId)
            return GameAction.Pass(playerId);

        var player = gameState.Player1.Id == playerId ? gameState.Player1 : gameState.Player2;
        var hand = player.Hand.Cards;

        // Priority 1: Play a land
        if (hand.Count > 0 && player.LandsPlayedThisTurn == 0)
        {
            var land = hand.FirstOrDefault(c => c.IsLand);
            if (land != null)
            {
                await DelayAsync(ct);
                return GameAction.PlayCard(playerId, land.Id);
            }
        }

        // Priority 2: Activate a fetch land if we have spells to cast
        var fetchLand = player.Battlefield.Cards
            .FirstOrDefault(c => !c.IsTapped && c.FetchAbility != null);
        if (fetchLand != null)
        {
            var hasSpellInHand = hand.Any(c => !c.IsLand && c.ManaCost != null);
            if (hasSpellInHand)
            {
                await DelayAsync(ct);
                return GameAction.ActivateFetch(playerId, fetchLand.Id);
            }
        }

        // Priority 2.5: Activate abilities on permanents (e.g., Mogg Fanatic, Skirk Prospector)
        var opponent = gameState.Player1.Id == playerId ? gameState.Player2 : gameState.Player1;
        var abilityAction = EvaluateActivatedAbilities(player, opponent, gameState);
        if (abilityAction != null)
        {
            await DelayAsync(ct);
            return abilityAction;
        }

        if (hand.Count == 0)
            return GameAction.Pass(playerId);

        // Priority 3: Tap an untapped land with a mana ability to build up mana pool
        // Only tap if the total available mana (pool + all untapped lands) can afford a spell.
        var untappedLands = player.Battlefield.Cards
            .Where(c => c.IsLand && !c.IsTapped && c.ManaAbility != null)
            .ToList();

        if (untappedLands.Count > 0)
        {
            var potentialMana = player.ManaPool.Total + untappedLands.Count;
            var cheapestSpellCmc = hand
                .Where(c => !c.IsLand && c.ManaCost != null)
                .Select(c =>
                {
                    var cost = c.ManaCost!;
                    var reduction = ComputeCostModification(gameState, c, player);
                    if (reduction != 0)
                        cost = cost.WithGenericReduction(-reduction);
                    return cost.ConvertedManaCost;
                })
                .DefaultIfEmpty(int.MaxValue)
                .Min();

            if (potentialMana >= cheapestSpellCmc)
            {
                await DelayAsync(ct);
                return GameAction.TapCard(playerId, untappedLands[0].Id);
            }
        }

        // Priority 4: Cast most expensive affordable spell (accounting for cost modification)
        // Only attempt sorcery-speed casts when the stack is empty (matches engine's CanCastSorcery check)
        if (gameState.StackCount > 0)
            return GameAction.Pass(playerId);

        var castable = hand
            .Where(c => !c.IsLand && c.ManaCost != null)
            .Select(c =>
            {
                var cost = c.ManaCost!;
                var reduction = ComputeCostModification(gameState, c, player);
                if (reduction != 0)
                    cost = cost.WithGenericReduction(-reduction);
                return (Card: c, EffectiveCost: cost);
            })
            .Where(x => player.ManaPool.CanPay(x.EffectiveCost))
            .OrderByDescending(x => x.Card.ManaCost!.ConvertedManaCost)
            .Select(x => x.Card)
            .FirstOrDefault();

        if (castable != null)
        {
            await DelayAsync(ct);
            // Use CastSpell for cards in the registry (proper stack/mana flow),
            // fall back to PlayCard for unregistered cards with ManaCost (immediate mana payment)
            if (CardDefinitions.TryGet(castable.Name, out _))
                return GameAction.CastSpell(playerId, castable.Id);
            return GameAction.PlayCard(playerId, castable.Id);
        }

        // Priority 5: Cycling — if a card can be cycled but not cast, cycle it
        foreach (var card in hand)
        {
            if (CardDefinitions.TryGet(card.Name, out var cycleDef) && cycleDef.CyclingCost != null)
            {
                if (player.ManaPool.CanPay(cycleDef.CyclingCost))
                {
                    // Only cycle if we can't afford to cast it
                    if (card.ManaCost == null || !player.ManaPool.CanPay(card.ManaCost))
                    {
                        await DelayAsync(ct);
                        return GameAction.Cycle(playerId, card.Id);
                    }
                }
            }
        }

        return GameAction.Pass(playerId);
    }

    /// <summary>
    /// Decides whether to mulligan based on land count relative to hand size.
    /// Always keeps at 4 or fewer cards. For larger hands, requires an acceptable
    /// land count range that narrows as the hand gets smaller.
    /// </summary>
    public async Task<MulliganDecision> GetMulliganDecision(IReadOnlyList<GameCard> hand, int mulliganCount, CancellationToken ct = default)
    {
        await DelayAsync(ct);

        // Always keep at 4 or fewer cards
        if (hand.Count <= 4)
            return MulliganDecision.Keep;

        var landCount = hand.Count(c => c.IsLand);
        var (minLands, maxLands) = GetAcceptableLandRange(hand.Count);

        return landCount >= minLands && landCount <= maxLands
            ? MulliganDecision.Keep
            : MulliganDecision.Mulligan;
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
    public async Task<IReadOnlyList<Guid>> ChooseAttackers(IReadOnlyList<GameCard> eligibleAttackers,
        CancellationToken ct = default)
    {
        await DelayAsync(ct);
        var attackerIds = eligibleAttackers.Select(c => c.Id).ToList();
        return attackerIds;
    }

    /// <summary>
    /// Blocks when a creature can kill the attacker (power >= attacker toughness).
    /// Uses the smallest sufficient blocker. Prioritizes blocking the biggest
    /// attacker first to maximize value. Each blocker is only assigned once.
    /// </summary>
    public async Task<Dictionary<Guid, Guid>> ChooseBlockers(IReadOnlyList<GameCard> eligibleBlockers,
        IReadOnlyList<GameCard> attackers, CancellationToken ct = default)
    {
        await DelayAsync(ct);
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

        return assignments;
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
    public async Task<Guid?> ChooseCard(IReadOnlyList<GameCard> options, string prompt, bool optional = false, CancellationToken ct = default)
    {
        if (options.Count == 0)
            return optional ? null : throw new InvalidOperationException("No options available for required card choice");

        await DelayAsync(ct);

        // Prefer highest CMC (most impactful card)
        var best = options
            .OrderByDescending(c => c.ManaCost?.ConvertedManaCost ?? 0)
            .First();

        return best.Id;
    }

    /// <summary>
    /// Auto-acknowledges card reveals (no decision needed).
    /// </summary>
    public Task RevealCards(IReadOnlyList<GameCard> cards, IReadOnlyList<GameCard> kept, string prompt, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Chooses a target for a spell. Picks the opponent's creature with highest power,
    /// falling back to the first eligible target.
    /// </summary>
    public async Task<TargetInfo?> ChooseTarget(string spellName, IReadOnlyList<GameCard> eligibleTargets, Guid defaultOwnerId = default, CancellationToken ct = default)
    {
        await DelayAsync(ct);
        var best = eligibleTargets
            .OrderByDescending(c => c.Power ?? 0)
            .ThenByDescending(c => c.ManaCost?.ConvertedManaCost ?? 0)
            .First();

        return new TargetInfo(best.Id, defaultOwnerId, Enums.ZoneType.Battlefield);
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

    /// <summary>
    /// Evaluates activated abilities on permanents and returns an action if one is worth activating.
    /// Heuristics:
    /// - DealDamageEffect: Activate if it can kill an opponent's creature.
    /// - AddManaEffect (Skirk Prospector): Activate if sacrificing enables casting a spell that needs exactly 1 more mana.
    /// </summary>
    private static GameAction? EvaluateActivatedAbilities(Player player, Player opponent, GameState gameState)
    {
        foreach (var permanent in player.Battlefield.Cards.ToList())
        {
            if (!CardDefinitions.TryGet(permanent.Name, out var def) || def.ActivatedAbility == null)
                continue;

            var ability = def.ActivatedAbility;
            var cost = ability.Cost;

            // Skip if tap cost and already tapped
            if (cost.TapSelf && permanent.IsTapped)
                continue;

            // Skip if mana cost can't be paid
            if (cost.ManaCost != null && !player.ManaPool.CanPay(cost.ManaCost))
                continue;

            // Skip if sacrifice subtype needed but none available
            if (cost.SacrificeSubtype != null)
            {
                var hasSacTarget = player.Battlefield.Cards
                    .Any(c => c.IsCreature && c.Subtypes.Contains(cost.SacrificeSubtype, StringComparer.OrdinalIgnoreCase));
                if (!hasSacTarget)
                    continue;
            }

            // DealDamageEffect heuristic: activate if it can kill an opponent creature
            if (ability.Effect is DealDamageEffect dealDamage)
            {
                var damageAmount = dealDamage.Amount;
                var killableTarget = opponent.Battlefield.Cards
                    .Where(c => c.IsCreature)
                    .FirstOrDefault(c => (c.Toughness ?? 0) - c.DamageMarked <= damageAmount);

                if (killableTarget != null)
                    return GameAction.ActivateAbility(player.Id, permanent.Id, targetId: killableTarget.Id);

                // Don't activate DealDamage without a good target
                continue;
            }

            // ExileCreatureEffect heuristic: exile the biggest opponent threat
            if (ability.Effect is ExileCreatureEffect)
            {
                // Check counter availability
                if (cost.RemoveCounterType.HasValue
                    && permanent.GetCounters(cost.RemoveCounterType.Value) <= 0)
                    continue;

                var biggestThreat = opponent.Battlefield.Cards
                    .Where(c => c.IsCreature)
                    .OrderByDescending(c => c.Power ?? 0)
                    .FirstOrDefault();

                if (biggestThreat != null)
                    return GameAction.ActivateAbility(player.Id, permanent.Id, targetId: biggestThreat.Id);

                continue;
            }

            // AddManaEffect heuristic: activate if sacrificing enables casting a spell
            if (ability.Effect is AddManaEffect)
            {
                // Check if there's a spell in hand that needs exactly 1 more mana
                var currentManaTotal = player.ManaPool.Total;
                var hasSpellNeedingOneMana = player.Hand.Cards
                    .Where(c => !c.IsLand && c.ManaCost != null)
                    .Any(c =>
                    {
                        var spellCost = c.ManaCost!;
                        // Apply cost modification
                        var reduction = ComputeCostModification(gameState, c, player);
                        if (reduction != 0)
                            spellCost = spellCost.WithGenericReduction(-reduction);

                        // Check if we need exactly 1 more mana to cast
                        return spellCost.ConvertedManaCost == currentManaTotal + 1
                            && player.ManaPool.CanPay(spellCost) == false;
                    });

                if (hasSpellNeedingOneMana)
                    return GameAction.ActivateAbility(player.Id, permanent.Id);

                // Don't sacrifice creatures for mana without good reason
                continue;
            }
        }

        return null;
    }

    private static int ComputeCostModification(GameState gameState, GameCard card, Player caster)
    {
        return gameState.ActiveEffects
            .Where(e => e.Type == ContinuousEffectType.ModifyCost
                   && e.CostApplies != null
                   && e.CostApplies(card)
                   && IsCostEffectApplicable(gameState, e, caster))
            .Sum(e => e.CostMod);
    }

    private static bool IsCostEffectApplicable(GameState gameState, ContinuousEffect effect, Player caster)
    {
        if (!effect.CostAppliesToOpponent) return true;

        var effectController = gameState.Player1.Battlefield.Contains(effect.SourceId) ? gameState.Player1
            : gameState.Player2.Battlefield.Contains(effect.SourceId) ? gameState.Player2 : null;

        return effectController != null && effectController.Id != caster.Id;
    }
}
