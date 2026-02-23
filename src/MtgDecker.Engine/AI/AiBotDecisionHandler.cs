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

    // Colors needed by spells in hand, cached by GetAction for ChooseManaColor to consult
    private HashSet<ManaColor> _neededColors = new();

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
            // Compute needed colors from spells in hand
            var neededColorsForLand = new HashSet<ManaColor>();
            foreach (var spell in hand.Where(c => !c.IsLand && c.ManaCost != null))
                foreach (var color in spell.ManaCost!.ColorRequirements.Keys)
                    neededColorsForLand.Add(color);

            var land = ChooseLandToPlay(hand, neededColorsForLand);
            if (land != null)
            {
                await DelayAsync(ct);
                return GameAction.PlayLand(playerId, land.Id);
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
        // Only tap if the total available mana (pool + all untapped lands) can afford a spell
        // AND the producible colors can satisfy at least one spell's color requirements.
        var untappedLands = player.Battlefield.Cards
            .Where(c => c.IsLand && !c.IsTapped && c.ManaAbility != null)
            .ToList();

        if (untappedLands.Count > 0)
        {
            var potentialMana = player.ManaPool.Total + untappedLands.Count;

            if (CanAffordAnySpell(hand, untappedLands, player, gameState, potentialMana))
            {
                // Cache needed colors so ChooseManaColor picks the right color
                _neededColors.Clear();
                foreach (var spell in hand.Where(c => !c.IsLand && c.ManaCost != null))
                    foreach (var color in spell.ManaCost!.ColorRequirements.Keys)
                        _neededColors.Add(color);

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
            return GameAction.CastSpell(playerId, castable.Id);
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
    /// Picks a mana color from the available options, preferring colors needed
    /// by spells in hand, then any colored mana over colorless.
    /// </summary>
    public Task<ManaColor> ChooseManaColor(IReadOnlyList<ManaColor> options, CancellationToken ct = default)
    {
        // Prefer a color needed by spells in hand
        foreach (var option in options)
        {
            if (_neededColors.Contains(option))
                return Task.FromResult(option);
        }

        // Fallback: prefer colored mana over colorless
        foreach (var option in options)
        {
            if (option != ManaColor.Colorless)
                return Task.FromResult(option);
        }

        return Task.FromResult(options[0]);
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
    /// Chooses attacker targets when the defending player controls planeswalkers.
    /// Heuristic: Attack planeswalkers with low loyalty that can be killed this turn,
    /// otherwise attack the player.
    /// </summary>
    public async Task<Dictionary<Guid, Guid?>> ChooseAttackerTargets(IReadOnlyList<GameCard> attackers,
        IReadOnlyList<GameCard> planeswalkers, CancellationToken ct = default)
    {
        await DelayAsync(ct);
        var targets = new Dictionary<Guid, Guid?>();

        // Sort attackers by power descending to allocate biggest hitters first
        var sortedAttackers = attackers.OrderByDescending(a => a.Power ?? 0).ToList();
        var pwLoyaltyRemaining = planeswalkers.ToDictionary(pw => pw.Id, pw => pw.Loyalty);

        foreach (var attacker in sortedAttackers)
        {
            var power = attacker.Power ?? 0;
            // Find a PW that this attacker can finish off
            var killablePw = planeswalkers
                .Where(pw => pwLoyaltyRemaining[pw.Id] > 0 && pwLoyaltyRemaining[pw.Id] <= power)
                .OrderBy(pw => pwLoyaltyRemaining[pw.Id])
                .FirstOrDefault();

            if (killablePw != null)
            {
                targets[attacker.Id] = killablePw.Id;
                pwLoyaltyRemaining[killablePw.Id] -= power;
            }
            else
            {
                // Default: attack the player
                targets[attacker.Id] = null;
            }
        }

        return targets;
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
    /// Discards cards to meet maximum hand size. Prioritizes discarding
    /// highest CMC spells first (least likely to cast), then excess lands.
    /// </summary>
    public async Task<IReadOnlyList<GameCard>> ChooseCardsToDiscard(IReadOnlyList<GameCard> hand, int discardCount, CancellationToken ct = default)
    {
        await DelayAsync(ct);
        var result = new List<GameCard>();
        var remaining = new List<GameCard>(hand);

        // Discard highest CMC non-land cards first
        var spellsByDescCmc = remaining
            .Where(c => !c.IsLand)
            .OrderByDescending(c => c.ManaCost?.ConvertedManaCost ?? 0)
            .ToList();

        foreach (var spell in spellsByDescCmc)
        {
            if (result.Count >= discardCount) break;
            result.Add(spell);
            remaining.Remove(spell);
        }

        // Then excess lands
        foreach (var land in remaining.Where(c => c.IsLand).ToList())
        {
            if (result.Count >= discardCount) break;
            result.Add(land);
        }

        return result;
    }

    /// <summary>
    /// Splits cards into two piles. Puts the single highest-CMC card alone in pile 1,
    /// everything else in pile 2 (classic 1-4 split heuristic).
    /// </summary>
    public async Task<IReadOnlyList<GameCard>> SplitCards(IReadOnlyList<GameCard> cards, string prompt, CancellationToken ct = default)
    {
        await DelayAsync(ct);
        if (cards.Count <= 1)
            return cards.ToList();
        var best = cards.OrderByDescending(c => c.ManaCost?.ConvertedManaCost ?? 0).First();
        return new List<GameCard> { best };
    }

    /// <summary>
    /// Picks the pile with more cards (greedy — more cards = more value).
    /// </summary>
    public async Task<int> ChoosePile(IReadOnlyList<GameCard> pile1, IReadOnlyList<GameCard> pile2, string prompt, CancellationToken ct = default)
    {
        await DelayAsync(ct);
        return pile1.Count >= pile2.Count ? 1 : 2;
    }

    /// <summary>
    /// Keeps original order and doesn't shuffle (keeps the known top card).
    /// </summary>
    public async Task<(IReadOnlyList<GameCard> ordered, bool shuffle)> ReorderCards(
        IReadOnlyList<GameCard> cards, string prompt, CancellationToken ct = default)
    {
        await DelayAsync(ct);
        return (cards.ToList(), false);
    }

    /// <summary>
    /// Chooses cards to exile for delve or similar effects.
    /// AI: exile as many as possible to maximize cost reduction.
    /// </summary>
    public Task<IReadOnlyList<GameCard>> ChooseCardsToExile(
        IReadOnlyList<GameCard> options, int maxCount, string prompt, CancellationToken ct = default)
    {
        // AI: exile as many as possible to maximize cost reduction
        return Task.FromResult<IReadOnlyList<GameCard>>(options.Take(maxCount).ToList());
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
    /// Ranks lands in hand and returns the best one to play.
    /// Priority: color-matching basic > color-matching dual > non-matching basic > utility > City of Traitors/Ancient Tomb.
    /// </summary>
    internal static GameCard? ChooseLandToPlay(IReadOnlyList<GameCard> hand, HashSet<ManaColor> neededColors)
    {
        var lands = hand.Where(c => c.IsLand).ToList();
        if (lands.Count == 0) return null;

        return lands
            .OrderByDescending(land => ScoreLand(land, neededColors))
            .First();
    }

    private static int ScoreLand(GameCard land, HashSet<ManaColor> neededColors)
    {
        var score = 0;
        var producesNeededColor = false;

        if (CardDefinitions.TryGet(land.Name, out var def) && def.ManaAbility != null)
        {
            var ability = def.ManaAbility;
            if (ability.FixedColor.HasValue && neededColors.Contains(ability.FixedColor.Value))
                producesNeededColor = true;
            if (ability.ChoiceColors != null && ability.ChoiceColors.Any(c => neededColors.Contains(c)))
                producesNeededColor = true;
            if (ability.DynamicColor.HasValue && neededColors.Contains(ability.DynamicColor.Value))
                producesNeededColor = true;
        }

        // Basic lands that produce needed colors are best
        if (land.IsBasicLand && producesNeededColor) score += 100;
        // Non-basic that produces needed colors (duals, pain lands)
        else if (producesNeededColor) score += 80;
        // Basic land not matching needed color (still fine for generic mana)
        else if (land.IsBasicLand) score += 60;

        // Penalize self-damage lands (Ancient Tomb)
        if (CardDefinitions.TryGet(land.Name, out var dmgDef) && dmgDef.ManaAbility?.SelfDamage > 0)
            score -= 30;

        // Heavily penalize City of Traitors (sacrifices when you play another land)
        if (land.Name == "City of Traitors") score -= 50;

        return score;
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
                    .Where(c => c.IsCreature
                        && !c.ActiveKeywords.Contains(Enums.Keyword.Shroud)
                        && !c.ActiveKeywords.Contains(Enums.Keyword.Hexproof))
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
                    .Where(c => c.IsCreature
                        && !c.ActiveKeywords.Contains(Enums.Keyword.Shroud)
                        && !c.ActiveKeywords.Contains(Enums.Keyword.Hexproof))
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

    /// <summary>
    /// Checks whether at least one spell in hand can be cast given the producible colors
    /// from the mana pool and all untapped lands' mana abilities.
    /// </summary>
    private static bool CanAffordAnySpell(IReadOnlyList<GameCard> hand, List<GameCard> untappedLands,
        Player player, GameState gameState, int potentialMana)
    {
        // Collect all producible colors from pool + untapped land abilities
        var producibleColors = new HashSet<ManaColor>();
        foreach (var (color, _) in player.ManaPool.Available)
        {
            producibleColors.Add(color);
        }
        foreach (var land in untappedLands)
        {
            if (land.ManaAbility == null) continue;
            if (land.ManaAbility.FixedColor.HasValue)
                producibleColors.Add(land.ManaAbility.FixedColor.Value);
            if (land.ManaAbility.ChoiceColors != null)
                foreach (var c in land.ManaAbility.ChoiceColors)
                    producibleColors.Add(c);
            if (land.ManaAbility.DynamicColor.HasValue)
                producibleColors.Add(land.ManaAbility.DynamicColor.Value);
        }

        return hand
            .Where(c => !c.IsLand && c.ManaCost != null)
            .Any(c =>
            {
                var cost = c.ManaCost!;
                var reduction = ComputeCostModification(gameState, c, player);
                if (reduction != 0)
                    cost = cost.WithGenericReduction(-reduction);

                // Check CMC affordability
                if (cost.ConvertedManaCost > potentialMana)
                    return false;

                // Check all color requirements can be produced
                return cost.ColorRequirements.All(kvp =>
                    producibleColors.Contains(kvp.Key));
            });
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
