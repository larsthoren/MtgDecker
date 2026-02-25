using MtgDecker.Engine.Effects;
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

    // Pre-planned action queue: when the bot decides to cast a spell, it enqueues
    // the tap actions first, then the cast action. Subsequent GetAction calls drain the queue.
    private readonly Queue<GameAction> _plannedActions = new();

    // Cached opponent/self info for combat decisions (populated in GetAction,
    // consumed by ChooseAttackers/ChooseBlockers interface methods)
    private Player? _lastOpponent;
    private int _lastSelfLife = 20;

    private async Task DelayAsync(CancellationToken ct)
    {
        if (ActionDelayMs > 0)
            await Task.Delay(ActionDelayMs, ct);
    }

    /// <summary>
    /// Selects an action using a planned-queue, land-first, fetch, tap-lands, greedy-cast heuristic.
    /// Drains pre-planned actions first (tap sequence + cast). Only acts during main phases.
    /// Prioritizes playing a land, then fetches, then activated abilities, then plans and
    /// enqueues a tap+cast sequence for the best proactive spell.
    /// </summary>
    public async Task<GameAction> GetAction(GameState gameState, Guid playerId, CancellationToken ct = default)
    {
        // Drain the planned action queue first
        if (_plannedActions.Count > 0)
        {
            await DelayAsync(ct);
            return _plannedActions.Dequeue();
        }

        var player = gameState.Player1.Id == playerId ? gameState.Player1 : gameState.Player2;
        var opponent = gameState.Player1.Id == playerId ? gameState.Player2 : gameState.Player1;
        var hand = player.Hand.Cards;

        // Cache opponent/self info for combat decisions
        _lastOpponent = opponent;
        _lastSelfLife = player.Life;

        // Non-active player: evaluate reactive plays (works in any phase)
        if (gameState.ActivePlayer.Id != playerId)
        {
            var reaction = EvaluateReaction(player, opponent, gameState, playerId);
            if (reaction != null)
            {
                await DelayAsync(ct);
                return reaction;
            }
            return GameAction.Pass(playerId);
        }

        if (gameState.CurrentPhase != Phase.MainPhase1 && gameState.CurrentPhase != Phase.MainPhase2)
            return GameAction.Pass(playerId);

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
        var abilityAction = EvaluateActivatedAbilities(player, opponent, gameState);
        if (abilityAction != null)
        {
            await DelayAsync(ct);
            return abilityAction;
        }

        if (hand.Count == 0)
            return GameAction.Pass(playerId);

        // Priority 3: Only attempt sorcery-speed casts when the stack is empty
        if (gameState.StackCount > 0)
            return GameAction.Pass(playerId);

        // Priority 3: Plan tap sequence + cast for the best proactive spell
        var untappedLands = player.Battlefield.Cards
            .Where(c => c.IsLand && !c.IsTapped && GetLandManaAbility(c) != null)
            .ToList();

        // Priority 3.5: Cast ramp spell if it enables an otherwise unaffordable spell
        var rampAction = EvaluateRampSpell(hand, player, gameState, playerId, untappedLands);
        if (rampAction != null)
        {
            await DelayAsync(ct);
            return rampAction;
        }

        var bestSpell = ChooseBestProactiveSpell(hand, player, gameState, untappedLands);
        if (bestSpell != null)
        {
            var effectiveCost = GetEffectiveCost(bestSpell, gameState, player);

            // Check if pool already has enough mana
            if (player.ManaPool.CanPay(effectiveCost))
            {
                // No taps needed, cast directly
                await DelayAsync(ct);
                return GameAction.CastSpell(playerId, bestSpell.Id);
            }

            // Plan the tap sequence for the remaining cost after pool contribution
            var tapIds = PlanTapSequence(untappedLands, effectiveCost);
            if (tapIds.Count > 0)
            {
                // Cache needed colors so ChooseManaColor picks the right color
                _neededColors.Clear();
                foreach (var color in effectiveCost.ColorRequirements.Keys)
                    _neededColors.Add(color);

                // Enqueue remaining taps (after the first) and the cast
                for (int i = 1; i < tapIds.Count; i++)
                    _plannedActions.Enqueue(GameAction.TapCard(playerId, tapIds[i]));
                _plannedActions.Enqueue(GameAction.CastSpell(playerId, bestSpell.Id));

                // Return the first tap action immediately
                await DelayAsync(ct);
                return GameAction.TapCard(playerId, tapIds[0]);
            }
        }

        // Priority 4: Cycling — if a card can be cycled but not cast, cycle it
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
    /// Plans the optimal set of lands to tap for a given mana cost.
    /// Prefers fixed-color lands for colored requirements (saving choice lands for flexibility),
    /// and uses least-flexible lands first for generic costs.
    /// Returns empty list if the cost cannot be met.
    /// </summary>
    internal static List<Guid> PlanTapSequence(IReadOnlyList<GameCard> untappedLands, ManaCost cost)
    {
        if (cost.ConvertedManaCost == 0)
            return [];

        var availableLands = untappedLands.ToList();
        var selectedIds = new List<Guid>();

        // Step 1: Satisfy colored requirements, preferring fixed-color lands
        foreach (var (color, required) in cost.ColorRequirements)
        {
            for (int i = 0; i < required; i++)
            {
                // First: find a fixed-color land that produces this color
                var fixedLand = availableLands.FirstOrDefault(land =>
                {
                    var ability = GetLandManaAbility(land);
                    return ability?.FixedColor == color;
                });

                if (fixedLand != null)
                {
                    selectedIds.Add(fixedLand.Id);
                    availableLands.Remove(fixedLand);
                    continue;
                }

                // Second: find a choice land that can produce this color
                var choiceLand = availableLands
                    .Where(land =>
                    {
                        var ability = GetLandManaAbility(land);
                        return ability?.ChoiceColors?.Contains(color) == true
                            || ability?.DynamicColor == color;
                    })
                    // Prefer lands with fewer choices (less flexible)
                    .OrderBy(land => GetLandManaAbility(land)?.ChoiceColors?.Count ?? 1)
                    .FirstOrDefault();

                if (choiceLand != null)
                {
                    selectedIds.Add(choiceLand.Id);
                    availableLands.Remove(choiceLand);
                    continue;
                }

                // Cannot satisfy this color requirement
                return [];
            }
        }

        // Step 2: Satisfy generic cost with least-flexible lands first
        var genericRemaining = cost.GenericCost;
        if (genericRemaining > 0)
        {
            // Sort by flexibility: fixed-color (1) < dynamic (1) < fewer choices < more choices
            var sortedByFlexibility = availableLands
                .OrderBy(land =>
                {
                    var ability = GetLandManaAbility(land);
                    if (ability?.FixedColor != null) return 1;
                    if (ability?.DynamicColor != null) return 2;
                    return ability?.ChoiceColors?.Count ?? 0;
                })
                .ToList();

            foreach (var land in sortedByFlexibility)
            {
                if (genericRemaining <= 0) break;
                selectedIds.Add(land.Id);
                genericRemaining--;
            }

            if (genericRemaining > 0)
                return []; // Not enough lands
        }

        return selectedIds;
    }

    /// <summary>
    /// Looks up the ManaAbility for a land, checking CardDefinitions first, then falling back
    /// to the land's own ManaAbility property.
    /// </summary>
    private static ManaAbility? GetLandManaAbility(GameCard land)
    {
        if (CardDefinitions.TryGet(land.Name, out var def) && def.ManaAbility != null)
            return def.ManaAbility;
        return land.ManaAbility;
    }

    /// <summary>
    /// Finds the best proactive spell to cast from hand. Filters to Proactive-only spells,
    /// checks affordability against pool + untapped lands, returns the highest CMC option.
    /// </summary>
    internal static GameCard? ChooseBestProactiveSpell(IReadOnlyList<GameCard> hand, Player player,
        GameState gameState, List<GameCard> untappedLands)
    {
        var producibleColors = GetProducibleColors(player, untappedLands);
        var potentialMana = player.ManaPool.Total + untappedLands.Count;

        return hand
            .Where(c => !c.IsLand && c.ManaCost != null)
            .Where(c =>
            {
                // Only cast proactive spells during main phase
                if (CardDefinitions.TryGet(c.Name, out var def))
                {
                    if (def.SpellRole != SpellRole.Proactive)
                        return false;
                }
                else
                {
                    // Unknown cards: only cast if they're not instants (assume proactive)
                    if (c.CardTypes.HasFlag(CardType.Instant))
                        return false;
                }
                return true;
            })
            .Select(c =>
            {
                var effectiveCost = GetEffectiveCost(c, gameState, player);
                return (Card: c, EffectiveCost: effectiveCost);
            })
            .Where(x =>
            {
                // Check total mana affordability
                if (x.EffectiveCost.ConvertedManaCost > potentialMana)
                    return false;

                // Check all color requirements can be produced
                return x.EffectiveCost.ColorRequirements.All(kvp =>
                    producibleColors.Contains(kvp.Key));
            })
            .OrderByDescending(x => x.Card.ManaCost!.ConvertedManaCost)
            .Select(x => x.Card)
            .FirstOrDefault();
    }

    /// <summary>
    /// Computes the effective cost of a card after applying cost modifications (e.g., Goblin Warchief).
    /// </summary>
    private static ManaCost GetEffectiveCost(GameCard card, GameState gameState, Player player)
    {
        var cost = card.ManaCost!;
        var reduction = ComputeCostModification(gameState, card, player);
        if (reduction != 0)
            cost = cost.WithGenericReduction(-reduction);
        return cost;
    }

    /// <summary>
    /// Collects all mana colors producible from the player's current mana pool
    /// and all untapped lands' mana abilities.
    /// </summary>
    private static HashSet<ManaColor> GetProducibleColors(Player player, List<GameCard> untappedLands)
    {
        var producibleColors = new HashSet<ManaColor>();
        foreach (var (color, _) in player.ManaPool.Available)
            producibleColors.Add(color);

        foreach (var land in untappedLands)
        {
            var ability = GetLandManaAbility(land);
            if (ability == null) continue;
            if (ability.FixedColor.HasValue)
                producibleColors.Add(ability.FixedColor.Value);
            if (ability.ChoiceColors != null)
                foreach (var c in ability.ChoiceColors)
                    producibleColors.Add(c);
            if (ability.DynamicColor.HasValue)
                producibleColors.Add(ability.DynamicColor.Value);
        }

        return producibleColors;
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
    /// Selects attackers using smart evaluation: considers opponent blockers, evasion,
    /// lethal damage, and favorable trades. Delegates to the testable static overload.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> ChooseAttackers(IReadOnlyList<GameCard> eligibleAttackers,
        CancellationToken ct = default)
    {
        await DelayAsync(ct);
        var opponentCreatures = _lastOpponent?.Battlefield.Cards
            .Where(c => c.IsCreature).ToList()
            ?? [];
        var opponentLife = _lastOpponent?.Life ?? 20;
        return ChooseAttackers(eligibleAttackers, opponentCreatures, opponentLife);
    }

    /// <summary>
    /// Testable static overload for attacker selection with full opponent context.
    /// Logic:
    /// 1. If total power >= opponentLife, attack with all (lethal).
    /// 2. If opponent has no creatures, attack with all.
    /// 3. Per attacker: attack if evasion, can't die to any blocker, or favorable trade.
    /// </summary>
    internal static IReadOnlyList<Guid> ChooseAttackers(IReadOnlyList<GameCard> eligibleAttackers,
        IReadOnlyList<GameCard> opponentCreatures, int opponentLife)
    {
        if (eligibleAttackers.Count == 0)
            return [];

        // Lethal check: if total power >= opponent life, attack with everything
        var totalPower = eligibleAttackers.Sum(a => a.Power ?? 0);
        if (totalPower >= opponentLife)
            return eligibleAttackers.Select(a => a.Id).ToList();

        // No opponent creatures: attack with everything
        if (opponentCreatures.Count == 0)
            return eligibleAttackers.Select(a => a.Id).ToList();

        var attackerIds = new List<Guid>();
        foreach (var attacker in eligibleAttackers)
        {
            var attackerPower = attacker.Power ?? 0;
            var attackerToughness = attacker.Toughness ?? 0;

            // Evasion: flying attacker with no opponent creatures that can block it
            if (attacker.ActiveKeywords.Contains(Keyword.Flying)
                && !opponentCreatures.Any(b => CanBlock(b, attacker)))
            {
                attackerIds.Add(attacker.Id);
                continue;
            }

            // Check if any opponent creature can profitably block this attacker
            var canDieToBlocker = opponentCreatures
                .Where(b => CanBlock(b, attacker))
                .Any(b => (b.Power ?? 0) >= attackerToughness);

            if (!canDieToBlocker)
            {
                // Attacker wouldn't die to any blocker — safe to attack
                attackerIds.Add(attacker.Id);
                continue;
            }

            // Attacker would die, but check if it's a favorable trade (can kill a blocker back)
            var canKillABlocker = opponentCreatures
                .Where(b => CanBlock(b, attacker))
                .Any(b => attackerPower >= (b.Toughness ?? 0));

            if (canKillABlocker)
            {
                attackerIds.Add(attacker.Id);
                continue;
            }

            // Would die without killing anything — don't attack
        }

        return attackerIds;
    }

    /// <summary>
    /// Determines whether a blocker can legally block an attacker.
    /// Flying attackers can only be blocked by creatures with Flying.
    /// </summary>
    private static bool CanBlock(GameCard blocker, GameCard attacker)
    {
        if (attacker.ActiveKeywords.Contains(Keyword.Flying))
            return blocker.ActiveKeywords.Contains(Keyword.Flying);
        return true;
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
    /// Selects blockers using smart evaluation: favorable trades, and chump-blocking
    /// only when damage would be lethal. Delegates to the testable static overload.
    /// </summary>
    public async Task<Dictionary<Guid, Guid>> ChooseBlockers(IReadOnlyList<GameCard> eligibleBlockers,
        IReadOnlyList<GameCard> attackers, CancellationToken ct = default)
    {
        await DelayAsync(ct);
        return ChooseBlockers(eligibleBlockers, attackers, playerLife: _lastSelfLife);
    }

    /// <summary>
    /// Testable static overload for blocker selection with player life context.
    /// Logic:
    /// 1. Calculate total unblocked damage.
    /// 2. For each attacker (biggest first): favorable block with smallest sufficient blocker.
    /// 3. If no favorable block and damage is lethal: chump-block with smallest creature.
    /// </summary>
    internal static Dictionary<Guid, Guid> ChooseBlockers(IReadOnlyList<GameCard> eligibleBlockers,
        IReadOnlyList<GameCard> attackers, int playerLife)
    {
        var assignments = new Dictionary<Guid, Guid>();
        var usedBlockers = new HashSet<Guid>();

        // Calculate total unblocked attack damage
        var remainingDamage = attackers.Sum(a => a.Power ?? 0);
        var mustBlockForSurvival = remainingDamage >= playerLife;

        foreach (var attacker in attackers.OrderByDescending(a => a.Power ?? 0))
        {
            var attackerToughness = attacker.Toughness ?? 0;
            var attackerPower = attacker.Power ?? 0;

            // Look for favorable block: blocker that can kill the attacker, smallest first
            var favorableBlocker = eligibleBlockers
                .Where(b => !usedBlockers.Contains(b.Id))
                .Where(b => CanBlock(b, attacker))
                .Where(b => (b.Power ?? 0) >= attackerToughness)
                .OrderBy(b => b.Power ?? 0)
                .FirstOrDefault();

            if (favorableBlocker != null)
            {
                assignments[favorableBlocker.Id] = attacker.Id;
                usedBlockers.Add(favorableBlocker.Id);
                remainingDamage -= attackerPower;
                mustBlockForSurvival = remainingDamage >= playerLife;
                continue;
            }

            // No favorable block — chump only if damage would be lethal
            if (mustBlockForSurvival)
            {
                var chumpBlocker = eligibleBlockers
                    .Where(b => !usedBlockers.Contains(b.Id))
                    .Where(b => CanBlock(b, attacker))
                    .OrderBy(b => b.Power ?? 0)
                    .FirstOrDefault();

                if (chumpBlocker != null)
                {
                    assignments[chumpBlocker.Id] = attacker.Id;
                    usedBlockers.Add(chumpBlocker.Id);
                    remainingDamage -= attackerPower;
                    mustBlockForSurvival = remainingDamage >= playerLife;
                }
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
    /// Chooses a creature type. AI picks "Goblin" as a common relevant type.
    /// </summary>
    public async Task<string> ChooseCreatureType(string prompt, CancellationToken ct = default)
    {
        await DelayAsync(ct);
        return "Goblin";
    }

    /// <summary>
    /// Chooses a card name. AI picks "Lightning Bolt" as a common spell.
    /// </summary>
    public async Task<string> ChooseCardName(string prompt, CancellationToken ct = default)
    {
        await DelayAsync(ct);
        return "Lightning Bolt";
    }

    /// <summary>
    /// AI always casts madness cards when able — free or cheap spells are high value.
    /// </summary>
    public Task<bool> ChooseMadness(GameCard card, ManaCost madnessCost, CancellationToken ct = default)
    {
        return Task.FromResult(true);
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
    /// Evaluates whether to play a reactive spell (counterspell, removal).
    /// Returns null if no reactive play is warranted.
    /// </summary>
    private static GameAction? EvaluateReaction(Player player, Player opponent, GameState gameState, Guid playerId)
    {
        // Check for counterspells when opponent has spell on stack
        if (gameState.StackCount > 0)
        {
            var topOfStack = gameState.StackPeekTop();
            if (topOfStack != null && topOfStack.ControllerId != playerId)
            {
                var counterAction = EvaluateCounterspell(player, opponent, gameState, playerId, topOfStack);
                if (counterAction != null) return counterAction;
            }
        }

        // Instant removal: during combat or end step, when stack is empty
        if (gameState.StackCount == 0
            && (gameState.CurrentPhase == Phase.Combat || gameState.CurrentPhase == Phase.End))
        {
            var removalAction = EvaluateInstantRemoval(player, opponent, playerId);
            if (removalAction != null) return removalAction;
        }

        // InstantUtility: cast during opponent's end step with empty stack
        if (gameState.StackCount == 0 && gameState.CurrentPhase == Phase.End)
        {
            var utilityAction = EvaluateInstantUtility(player, playerId);
            if (utilityAction != null) return utilityAction;
        }

        return null;
    }

    /// <summary>
    /// Evaluates whether to cast instant-speed removal on opponent creatures.
    /// Targets the biggest creature by power.
    /// </summary>
    private static GameAction? EvaluateInstantRemoval(Player player, Player opponent, Guid playerId)
    {
        var opponentCreatures = opponent.Battlefield.Cards
            .Where(c => c.IsCreature)
            .OrderByDescending(c => c.Power ?? 0)
            .ToList();

        if (opponentCreatures.Count == 0) return null;

        foreach (var card in player.Hand.Cards)
        {
            if (!CardDefinitions.TryGet(card.Name, out var def)) continue;
            if (def.SpellRole != SpellRole.InstantRemoval) continue;
            if (def.ManaCost == null) continue;

            // Check if we can pay the mana cost
            if (!player.ManaPool.CanPay(def.ManaCost))
            {
                // Check alternate cost (Snuff Out: 4 life)
                if (def.AlternateCost != null && def.AlternateCost.LifeCost > 0
                    && player.Life > def.AlternateCost.LifeCost + 5)
                {
                    return GameAction.CastSpell(playerId, card.Id, useAlternateCost: true);
                }
                continue;
            }

            // Target the biggest creature
            return GameAction.CastSpell(playerId, card.Id);
        }

        return null;
    }

    /// <summary>
    /// Evaluates whether to cast an instant-speed utility spell (Brainstorm, Impulse, etc.)
    /// during opponent's end step. Cast if mana is available in pool.
    /// </summary>
    private static GameAction? EvaluateInstantUtility(Player player, Guid playerId)
    {
        foreach (var card in player.Hand.Cards)
        {
            if (!CardDefinitions.TryGet(card.Name, out var def)) continue;
            if (def.SpellRole != SpellRole.InstantUtility) continue;
            if (def.ManaCost == null) continue;

            if (player.ManaPool.CanPay(def.ManaCost))
                return GameAction.CastSpell(playerId, card.Id);
        }

        return null;
    }

    /// <summary>
    /// Checks if casting a ramp spell (e.g., Dark Ritual) would enable casting a proactive spell
    /// that the bot currently cannot afford. Returns the ramp spell cast action if so.
    /// </summary>
    private GameAction? EvaluateRampSpell(IReadOnlyList<GameCard> hand, Player player,
        GameState gameState, Guid playerId, List<GameCard> untappedLands)
    {
        var potentialMana = player.ManaPool.Total + untappedLands.Count;

        foreach (var card in hand)
        {
            if (!CardDefinitions.TryGet(card.Name, out var def)) continue;
            if (def.SpellRole != SpellRole.Ramp) continue;
            if (def.ManaCost == null) continue;

            // Check if we can afford to cast the ramp spell itself
            var canPayFromPool = player.ManaPool.CanPay(def.ManaCost);
            List<Guid> rampTapIds = [];
            if (!canPayFromPool)
            {
                rampTapIds = PlanTapSequence(untappedLands, def.ManaCost);
                if (rampTapIds.Count == 0) continue;
            }

            // Calculate mana gain from ramp spell
            var rampManaGain = 0;
            if (def.Effect is AddManaSpellEffect addMana)
                rampManaGain = addMana.Amount;

            if (rampManaGain == 0) continue;

            var potentialManaAfterRamp = potentialMana + rampManaGain - def.ManaCost.ConvertedManaCost;

            // Check if any proactive spell in hand becomes affordable with the extra mana
            var hasUnaffordableSpellThatBecomesAffordable = hand.Any(spell =>
            {
                if (spell.Id == card.Id) return false;
                if (spell.IsLand) return false;
                if (spell.ManaCost == null) return false;

                if (CardDefinitions.TryGet(spell.Name, out var spellDef))
                {
                    if (spellDef.SpellRole != SpellRole.Proactive)
                        return false;
                }
                else if (spell.CardTypes.HasFlag(CardType.Instant)) return false;

                var effectiveCost = GetEffectiveCost(spell, gameState, player);

                // Currently unaffordable
                if (effectiveCost.ConvertedManaCost <= potentialMana) return false;

                // Affordable after ramp
                return effectiveCost.ConvertedManaCost <= potentialManaAfterRamp;
            });

            if (hasUnaffordableSpellThatBecomesAffordable)
            {
                if (!canPayFromPool)
                {
                    // Need to tap lands first, then cast ramp spell
                    for (int i = 1; i < rampTapIds.Count; i++)
                        _plannedActions.Enqueue(GameAction.TapCard(playerId, rampTapIds[i]));
                    _plannedActions.Enqueue(GameAction.CastSpell(playerId, card.Id));
                    return GameAction.TapCard(playerId, rampTapIds[0]);
                }
                return GameAction.CastSpell(playerId, card.Id);
            }
        }

        return null;
    }

    /// <summary>
    /// Evaluates whether to counter an opponent's spell on the stack.
    /// Checks hand for counterspells and applies heuristics per counter type.
    /// </summary>
    private static GameAction? EvaluateCounterspell(Player player, Player opponent, GameState gameState, Guid playerId, IStackObject targetSpell)
    {
        var hand = player.Hand.Cards;
        var opponentUntappedLands = opponent.Battlefield.Cards.Count(c => c.IsLand && !c.IsTapped);

        // Get the spell's CMC from the stack object
        var spellCmc = 0;
        if (targetSpell is StackObject so)
            spellCmc = so.Card.ManaCost?.ConvertedManaCost ?? 0;

        foreach (var card in hand)
        {
            if (!CardDefinitions.TryGet(card.Name, out var def)) continue;
            if (def.SpellRole != SpellRole.Counterspell) continue;

            // --- Hard counters (Counterspell, Absorb) ---
            if (def.Effect is CounterSpellEffect or CounterAndGainLifeEffect)
            {
                if (def.AlternateCost == null && def.ManaCost != null && player.ManaPool.CanPay(def.ManaCost))
                {
                    if (spellCmc >= 3)
                        return GameAction.CastSpell(playerId, card.Id);
                }
            }

            // --- Daze (soft counter — return Island to hand) ---
            if (card.Name == "Daze")
            {
                if (opponentUntappedLands == 0)
                {
                    var hasIsland = player.Battlefield.Cards.Any(c => c.Subtypes.Contains("Island"));
                    if (hasIsland)
                        return GameAction.CastSpell(playerId, card.Id, useAlternateCost: true);
                }
                continue;
            }

            // --- Conditional counters (Mana Leak, Spell Pierce, Flusterstorm, Prohibit) ---
            if (def.Effect is ConditionalCounterEffect conditional)
            {
                if (def.ManaCost != null && player.ManaPool.CanPay(def.ManaCost))
                {
                    if (opponentUntappedLands < conditional.GenericCost)
                        return GameAction.CastSpell(playerId, card.Id);
                }
                continue;
            }

            // --- Force of Will (exile blue card, pay 1 life) ---
            if (card.Name == "Force of Will")
            {
                if (spellCmc >= 4)
                {
                    var hasBlueCardToExile = hand.Any(c => c.Id != card.Id
                        && c.ManaCost != null && c.ManaCost.ColorRequirements.ContainsKey(ManaColor.Blue));
                    if (hasBlueCardToExile && player.Life > 1)
                        return GameAction.CastSpell(playerId, card.Id, useAlternateCost: true);
                }
                continue;
            }

            // --- Pyroblast (counter blue spells) ---
            if (card.Name == "Pyroblast")
            {
                if (targetSpell is StackObject pyrTarget && pyrTarget.Card.ManaCost?.ColorRequirements.ContainsKey(ManaColor.Blue) == true)
                {
                    if (def.ManaCost != null && player.ManaPool.CanPay(def.ManaCost))
                        return GameAction.CastSpell(playerId, card.Id);
                }
                continue;
            }
        }

        return null;
    }

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
            if (!CardDefinitions.TryGet(permanent.Name, out var def) || def.ActivatedAbilities.Count == 0)
                continue;

            for (int abilityIdx = 0; abilityIdx < def.ActivatedAbilities.Count; abilityIdx++)
            {
                var ability = def.ActivatedAbilities[abilityIdx];
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
                        return GameAction.ActivateAbility(player.Id, permanent.Id, targetId: killableTarget.Id, abilityIndex: abilityIdx);

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
                        return GameAction.ActivateAbility(player.Id, permanent.Id, targetId: biggestThreat.Id, abilityIndex: abilityIdx);

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
                        return GameAction.ActivateAbility(player.Id, permanent.Id, abilityIndex: abilityIdx);

                    // Don't sacrifice creatures for mana without good reason
                    continue;
                }
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
