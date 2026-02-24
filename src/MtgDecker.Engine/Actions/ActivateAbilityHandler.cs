using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Actions;

internal class ActivateAbilityHandler : IActionHandler
{
    public async Task ExecuteAsync(GameAction action, GameEngine engine, GameState state, CancellationToken ct)
    {
        var player = state.GetPlayer(action.PlayerId);
        var abilitySource = player.Battlefield.Cards.FirstOrDefault(c => c.Id == action.CardId);
        if (abilitySource == null) return;

        // Check if activated abilities are prevented (e.g. Null Rod for artifacts, Cursed Totem for creatures)
        if (state.ActiveEffects.Any(e =>
            e.Type == ContinuousEffectType.PreventActivatedAbilities
            && e.Applies(abilitySource, player)))
        {
            state.Log($"{abilitySource.Name}'s activated abilities can't be activated.");
            return;
        }

        if (abilitySource.IsCreature && abilitySource.AbilitiesRemoved)
        {
            state.Log($"{abilitySource.Name} has lost its abilities — cannot activate.");
            return;
        }

        ActivatedAbility? ability = abilitySource.TokenActivatedAbility;
        if (ability == null)
        {
            if (CardDefinitions.TryGet(abilitySource.Name, out var abilityDef) && abilityDef.ActivatedAbilities.Count > 0)
            {
                var index = action.AbilityIndex ?? 0;
                if (index >= 0 && index < abilityDef.ActivatedAbilities.Count)
                    ability = abilityDef.ActivatedAbilities[index];
            }
        }

        if (ability == null)
        {
            state.Log($"{abilitySource.Name} has no activated ability.");
            return;
        }

        // Once-per-turn check
        var abilityIndex = action.AbilityIndex ?? 0;
        if (ability.OncePerTurn && abilitySource.AbilitiesActivatedThisTurn.Contains(abilityIndex))
        {
            state.Log($"{abilitySource.Name}'s ability can only be activated once each turn.");
            return;
        }

        var cost = ability.Cost;

        // Check for activated ability cost modifications (e.g., Gloom)
        var effectiveCost = cost.ManaCost;
        if (effectiveCost != null)
        {
            var extraCost = state.ActiveEffects
                .Where(e => e.Type == ContinuousEffectType.ModifyActivatedAbilityCost
                       && e.ActivatedAbilityCostApplies != null
                       && e.ActivatedAbilityCostApplies(abilitySource))
                .Sum(e => e.CostMod);

            if (extraCost > 0)
                effectiveCost = effectiveCost.WithGenericReduction(-extraCost);
        }

        if (ability.Condition != null && !ability.Condition(player))
        {
            state.Log($"Cannot activate {abilitySource.Name} — condition not met.");
            return;
        }

        if (cost.TapSelf && abilitySource.IsTapped)
        {
            state.Log($"Cannot activate {abilitySource.Name} — already tapped.");
            return;
        }

        if (cost.TapSelf && abilitySource.IsCreature && abilitySource.HasSummoningSickness(state.TurnNumber))
        {
            state.Log($"{abilitySource.Name} has summoning sickness.");
            return;
        }

        if (cost.PayLife > 0 && player.Life < cost.PayLife)
        {
            state.Log($"Cannot activate {abilitySource.Name} — not enough life (need {cost.PayLife}, have {player.Life}).");
            return;
        }

        if (effectiveCost != null && !player.ManaPool.CanPay(effectiveCost))
        {
            state.Log($"Cannot activate {abilitySource.Name} — not enough mana.");
            return;
        }

        if (cost.RemoveCounterType.HasValue)
        {
            if (abilitySource.GetCounters(cost.RemoveCounterType.Value) <= 0)
            {
                state.Log($"Cannot activate {abilitySource.Name} — no {cost.RemoveCounterType.Value} counters.");
                return;
            }
        }

        GameCard? sacrificeTarget = null;
        if (cost.SacrificeSubtype != null)
        {
            var eligible = player.Battlefield.Cards
                .Where(c => c.IsCreature && c.Subtypes.Contains(cost.SacrificeSubtype, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (eligible.Count == 0)
            {
                state.Log($"Cannot activate {abilitySource.Name} — no eligible {cost.SacrificeSubtype} to sacrifice.");
                return;
            }

            var chosenId = await player.DecisionHandler.ChooseCard(
                eligible, $"Choose a {cost.SacrificeSubtype} to sacrifice", optional: false, ct);

            if (chosenId.HasValue)
                sacrificeTarget = eligible.FirstOrDefault(c => c.Id == chosenId.Value);

            if (sacrificeTarget == null)
            {
                state.Log($"Cannot activate {abilitySource.Name} — no sacrifice target chosen.");
                return;
            }
        }

        GameCard? sacrificeByType = null;
        if (cost.SacrificeCardType.HasValue)
        {
            var eligible = player.Battlefield.Cards
                .Where(c => c.CardTypes.HasFlag(cost.SacrificeCardType.Value))
                .ToList();

            if (eligible.Count == 0)
            {
                state.Log($"Cannot activate {abilitySource.Name} — no {cost.SacrificeCardType.Value} to sacrifice.");
                return;
            }

            var chosenId = await player.DecisionHandler.ChooseCard(
                eligible, $"Choose a {cost.SacrificeCardType.Value} to sacrifice", optional: false, ct);

            if (chosenId.HasValue)
                sacrificeByType = eligible.FirstOrDefault(c => c.Id == chosenId.Value);

            if (sacrificeByType == null)
            {
                state.Log($"Cannot activate {abilitySource.Name} — no sacrifice target chosen.");
                return;
            }
        }

        GameCard? discardTarget = null;
        if (cost.DiscardCardType.HasValue)
        {
            var eligible = player.Hand.Cards
                .Where(c => c.CardTypes.HasFlag(cost.DiscardCardType.Value))
                .ToList();

            if (eligible.Count == 0)
            {
                state.Log($"Cannot activate {abilitySource.Name} — no {cost.DiscardCardType.Value} in hand to discard.");
                return;
            }

            var chosenId = await player.DecisionHandler.ChooseCard(
                eligible, $"Choose a {cost.DiscardCardType.Value} to discard", optional: false, ct);

            if (chosenId.HasValue)
                discardTarget = eligible.FirstOrDefault(c => c.Id == chosenId.Value);

            if (discardTarget == null)
            {
                state.Log($"Cannot activate {abilitySource.Name} — no discard target chosen.");
                return;
            }
        }
        else if (cost.DiscardAny)
        {
            var eligible = player.Hand.Cards.ToList();

            if (eligible.Count == 0)
            {
                state.Log($"Cannot activate {abilitySource.Name} — no cards in hand to discard.");
                return;
            }

            var chosenId = await player.DecisionHandler.ChooseCard(
                eligible, "Choose a card to discard", optional: false, ct);

            if (chosenId.HasValue)
                discardTarget = eligible.FirstOrDefault(c => c.Id == chosenId.Value);

            if (discardTarget == null)
            {
                state.Log($"Cannot activate {abilitySource.Name} — no discard target chosen.");
                return;
            }
        }

        List<GameCard>? discardTargets = null;
        if (cost.DiscardCount > 0)
        {
            var eligible = player.Hand.Cards.ToList();
            if (eligible.Count < cost.DiscardCount)
            {
                state.Log($"Cannot activate {abilitySource.Name} — not enough cards in hand to discard (need {cost.DiscardCount}, have {eligible.Count}).");
                return;
            }

            discardTargets = [];
            for (int i = 0; i < cost.DiscardCount; i++)
            {
                var remaining = player.Hand.Cards.Where(c => !discardTargets.Contains(c)).ToList();
                var chosenId = await player.DecisionHandler.ChooseCard(
                    remaining, $"Choose a card to discard ({i + 1}/{cost.DiscardCount})", optional: false, ct);
                if (chosenId.HasValue)
                {
                    var card = remaining.FirstOrDefault(c => c.Id == chosenId.Value);
                    if (card != null) discardTargets.Add(card);
                }
            }

            if (discardTargets.Count < cost.DiscardCount)
            {
                state.Log($"Cannot activate {abilitySource.Name} — not enough discard targets chosen.");
                return;
            }
        }

        List<GameCard>? exileTargets = null;
        if (cost.ExileFromGraveyardCount > 0)
        {
            if (player.Graveyard.Count < cost.ExileFromGraveyardCount)
            {
                state.Log($"Cannot activate {abilitySource.Name} — not enough cards in graveyard (need {cost.ExileFromGraveyardCount}, have {player.Graveyard.Count}).");
                return;
            }

            exileTargets = [];
            for (int i = 0; i < cost.ExileFromGraveyardCount; i++)
            {
                var eligible = player.Graveyard.Cards.Where(c => !exileTargets.Contains(c)).ToList();
                var chosenId = await player.DecisionHandler.ChooseCard(
                    eligible, $"Choose a card to exile from graveyard ({i + 1}/{cost.ExileFromGraveyardCount})", optional: false, ct);
                if (chosenId.HasValue)
                {
                    var card = eligible.FirstOrDefault(c => c.Id == chosenId.Value);
                    if (card != null) exileTargets.Add(card);
                }
            }

            if (exileTargets.Count < cost.ExileFromGraveyardCount)
            {
                state.Log($"Cannot activate {abilitySource.Name} — not enough exile targets chosen.");
                return;
            }
        }

        if (effectiveCost != null)
        {
            await engine.PayManaCostAsync(effectiveCost, player, ct);
            player.PendingManaTaps.Clear();
        }

        if (cost.TapSelf)
            abilitySource.IsTapped = true;

        if (cost.SacrificeSelf)
        {
            await engine.FireLeaveBattlefieldTriggersAsync(abilitySource, player, ct);
            player.Battlefield.RemoveById(abilitySource.Id);
            player.Graveyard.Add(abilitySource);
            state.Log($"{player.Name} sacrifices {abilitySource.Name}.");
        }

        if (cost.ReturnSelfToHand)
        {
            await engine.FireLeaveBattlefieldTriggersAsync(abilitySource, player, ct);
            player.Battlefield.RemoveById(abilitySource.Id);
            player.Hand.Add(abilitySource);
            state.Log($"{player.Name} returns {abilitySource.Name} to hand.");
        }

        if (sacrificeTarget != null)
        {
            await engine.FireLeaveBattlefieldTriggersAsync(sacrificeTarget, player, ct);
            player.Battlefield.RemoveById(sacrificeTarget.Id);
            player.Graveyard.Add(sacrificeTarget);
            state.Log($"{player.Name} sacrifices {sacrificeTarget.Name}.");
        }

        if (sacrificeByType != null)
        {
            await engine.FireLeaveBattlefieldTriggersAsync(sacrificeByType, player, ct);
            player.Battlefield.RemoveById(sacrificeByType.Id);
            player.Graveyard.Add(sacrificeByType);
            state.Log($"{player.Name} sacrifices {sacrificeByType.Name}.");
        }

        if (cost.RemoveCounterType.HasValue)
            abilitySource.RemoveCounter(cost.RemoveCounterType.Value);

        if (discardTarget != null)
        {
            player.Hand.RemoveById(discardTarget.Id);
            await engine.HandleDiscardAsync(discardTarget, player, ct);
            engine.QueueDiscardTriggers(player);
        }

        if (discardTargets != null)
        {
            foreach (var card in discardTargets)
            {
                player.Hand.RemoveById(card.Id);
                await engine.HandleDiscardAsync(card, player, ct);
                engine.QueueDiscardTriggers(player);
            }
        }

        if (cost.PayLife > 0)
        {
            player.AdjustLife(-cost.PayLife);
            state.Log($"{player.Name} pays {cost.PayLife} life.");
        }

        if (exileTargets != null)
        {
            foreach (var card in exileTargets)
            {
                player.Graveyard.RemoveById(card.Id);
                player.Exile.Add(card);
                state.Log($"{card.Name} is exiled from {player.Name}'s graveyard.");
            }
        }

        GameCard? effectTarget = null;
        if (action.TargetCardId.HasValue)
        {
            effectTarget = state.Player1.Battlefield.Cards.FirstOrDefault(c => c.Id == action.TargetCardId.Value)
                        ?? state.Player2.Battlefield.Cards.FirstOrDefault(c => c.Id == action.TargetCardId.Value);
        }
        else if (ability.TargetFilter != null && !action.TargetPlayerId.HasValue)
        {
            var opponent = state.GetOpponent(player);
            IEnumerable<GameCard> searchPool = ability.TargetOwnOnly
                ? player.Battlefield.Cards
                : player.Battlefield.Cards.Concat(opponent.Battlefield.Cards);
            var eligible = searchPool
                .Where(c => ability.TargetFilter(c) && !engine.CannotBeTargetedBy(c, player))
                .ToList();

            if (eligible.Count == 0)
            {
                state.Log($"No legal targets for {abilitySource.Name}.");
                return;
            }

            var target = await player.DecisionHandler.ChooseTarget(
                abilitySource.Name, eligible, opponent.Id, ct);

            if (target == null)
            {
                state.Log($"{player.Name} cancels activating {abilitySource.Name}.");
                return;
            }

            effectTarget = eligible.FirstOrDefault(c => c.Id == target.CardId);
        }

        if (effectTarget != null && engine.CannotBeTargetedBy(effectTarget, player))
        {
            var reason = engine.HasShroud(effectTarget) ? "shroud" : "hexproof";
            state.Log($"{effectTarget.Name} has {reason} — cannot be targeted.");
            return;
        }

        if (action.TargetPlayerId.HasValue && engine.HasPlayerShroud(action.TargetPlayerId.Value))
        {
            var targetPlayerName = action.TargetPlayerId.Value == state.Player1.Id
                ? state.Player1.Name : state.Player2.Name;
            state.Log($"{targetPlayerName} has shroud — cannot be targeted.");
            return;
        }

        var stackObj = new TriggeredAbilityStackObject(abilitySource, player.Id, ability.Effect, effectTarget)
        {
            TargetPlayerId = action.TargetPlayerId,
        };
        state.StackPush(stackObj);
        state.Log($"{abilitySource.Name}'s ability is put on the stack.");

        // Track once-per-turn activation
        if (ability.OncePerTurn)
            abilitySource.AbilitiesActivatedThisTurn.Add(abilityIndex);

        player.ActionHistory.Push(action);
    }
}
