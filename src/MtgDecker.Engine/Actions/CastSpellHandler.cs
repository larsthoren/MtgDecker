using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Actions;

internal class CastSpellHandler : IActionHandler
{
    public async Task ExecuteAsync(GameAction action, GameEngine engine, GameState state, CancellationToken ct)
    {
        if (state.IsMidCast)
        {
            state.Log("Cannot cast another spell while mid-cast.");
            return;
        }

        // PreventSpellCasting: check if this player is prevented from casting spells
        var spellCaster = state.GetPlayer(action.PlayerId);
        if (state.ActiveEffects.Any(e =>
            e.Type == ContinuousEffectType.PreventSpellCasting
            && e.Applies(new GameCard(), spellCaster)))
        {
            state.Log($"{spellCaster.Name} can't cast spells this turn.");
            return;
        }

        // Meddling Mage: check if any Meddling Mage on the battlefield has named this card
        var castPlayerPre = state.GetPlayer(action.PlayerId);
        var candidateCard = castPlayerPre.Hand.Cards.FirstOrDefault(c => c.Id == action.CardId)
            ?? castPlayerPre.Exile.Cards.FirstOrDefault(c => c.Id == action.CardId && c.IsOnAdventure);
        if (candidateCard != null)
        {
            var opponent = state.Player1.Id == action.PlayerId ? state.Player2 : state.Player1;
            var meddlingMage = opponent.Battlefield.Cards
                .FirstOrDefault(c => c.ChosenName != null
                    && string.Equals(c.ChosenName, candidateCard.Name, StringComparison.OrdinalIgnoreCase));
            if (meddlingMage != null)
            {
                state.Log($"{candidateCard.Name} can't be cast — named by {meddlingMage.Name}.");
                return;
            }
        }

        var castPlayer = state.GetPlayer(action.PlayerId);
        var castCard = castPlayer.Hand.Cards.FirstOrDefault(c => c.Id == action.CardId);
        bool castingFromExileAdventure = false;

        if (castCard == null)
        {
            castCard = castPlayer.Exile.Cards.FirstOrDefault(c => c.Id == action.CardId && c.IsOnAdventure);
            if (castCard != null)
                castingFromExileAdventure = true;
        }

        if (castCard == null)
        {
            state.Log("Card not found in hand.");
            return;
        }

        CardDefinitions.TryGet(castCard.Name, out var def);

        ManaCost? baseCost = def?.ManaCost ?? castCard.ManaCost;
        if (baseCost == null)
        {
            state.Log($"Cannot cast {castCard.Name} — no mana cost defined.");
            return;
        }

        bool isInstant = def?.CardTypes.HasFlag(CardType.Instant) ?? castCard.CardTypes.HasFlag(CardType.Instant);
        bool hasFlash = def?.HasFlash ?? false;
        if (!isInstant && !hasFlash && !engine.CanCastSorcery(castPlayer.Id))
        {
            state.Log($"Cannot cast {castCard.Name} at this time (sorcery-speed only).");
            return;
        }

        var castEffectiveCost = baseCost;
        var castCostReduction = engine.ComputeCostModification(castCard, castPlayer);
        if (castCostReduction != 0)
            castEffectiveCost = castEffectiveCost.WithGenericReduction(-castCostReduction);

        // Delve: exile cards from graveyard to reduce generic cost
        IReadOnlyList<GameCard>? delveExiledCards = null;
        if (def?.HasDelve == true && castEffectiveCost.GenericCost > 0 && castPlayer.Graveyard.Count > 0)
        {
            var graveyardCards = castPlayer.Graveyard.Cards.ToList();
            var maxExile = Math.Min(castEffectiveCost.GenericCost, graveyardCards.Count);

            delveExiledCards = await castPlayer.DecisionHandler.ChooseCardsToExile(
                graveyardCards, maxExile, $"Exile cards for Delve ({castCard.Name})", ct);

            if (delveExiledCards.Count > 0)
                castEffectiveCost = castEffectiveCost.WithGenericReduction(delveExiledCards.Count);
        }

        bool canPayMana = castEffectiveCost.HasPhyrexianCost
            ? castPlayer.ManaPool.CanPayWithPhyrexian(castEffectiveCost, castPlayer.Life)
            : castPlayer.ManaPool.CanPay(castEffectiveCost);
        bool canPayAlternate = def?.AlternateCost != null && engine.CanPayAlternateCost(def.AlternateCost, castPlayer, castCard);
        bool useAlternateCost = action.UseAlternateCost;

        if (!useAlternateCost)
        {
            if (!canPayMana && !canPayAlternate)
            {
                state.Log($"Not enough mana to cast {castCard.Name}.");
                return;
            }

            if (canPayAlternate && !canPayMana)
            {
                useAlternateCost = true;
            }
            else if (canPayAlternate && canPayMana)
            {
                var choice = await castPlayer.DecisionHandler.ChooseCard(
                    [castCard], $"Pay mana for {castCard.Name}? (skip to use alternate cost)", optional: true, ct);
                useAlternateCost = !choice.HasValue;
            }
        }
        else if (!canPayAlternate)
        {
            state.Log($"Cannot pay alternate cost for {castCard.Name}.");
            return;
        }

        // Use shared targeting helper
        var targets = new List<TargetInfo>();
        if (def?.TargetFilter != null)
        {
            var result = await engine.FindAndChooseTargetsAsync(
                def.TargetFilter, castPlayer, castPlayer.DecisionHandler, castCard.Name, ct);

            if (result == null)
            {
                state.Log($"{castPlayer.Name} cancels casting {castCard.Name}.");
                return;
            }

            if (result.Count == 0)
            {
                state.Log($"No legal targets for {castCard.Name}.");
                return;
            }

            targets = result;
        }

        if (useAlternateCost)
        {
            await engine.PayAlternateCostAsync(def!.AlternateCost!, castPlayer, castCard, ct);

            // Kicker: prompt after paying base cost
            var isKicked = await engine.TryPayKickerAsync(castCard, castPlayer, ct);

            // Alternate cost fully pays — go to stack immediately
            if (castingFromExileAdventure)
            {
                castPlayer.Exile.RemoveById(castCard.Id);
                castCard.IsOnAdventure = false;
            }
            else
            {
                castPlayer.Hand.RemoveById(castCard.Id);
            }
            var stackObj = new StackObject(castCard, castPlayer.Id, new Dictionary<ManaColor, int>(), targets, state.StackCount) { IsKicked = isKicked };
            state.StackPush(stackObj);

            action.ManaCostPaid = castEffectiveCost;
            castPlayer.ActionHistory.Push(action);

            state.Log($"{castPlayer.Name} casts {castCard.Name}.");
            state.SpellsCastThisTurn++;

            await engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, castCard, ct);
            await engine.QueueSelfCastTriggersAsync(castCard, castPlayer, ct);
        }
        else
        {
            // Auto-deduct colored requirements
            var pool = castPlayer.ManaPool;
            var autoDeducted = new Dictionary<ManaColor, int>();
            foreach (var (color, required) in castEffectiveCost.ColorRequirements)
            {
                if (required > 0)
                {
                    pool.Deduct(color, required);
                    autoDeducted[color] = required;
                }
            }

            int remainingGeneric = castEffectiveCost.GenericCost;
            var remainingPhyrexian = new Dictionary<ManaColor, int>(castEffectiveCost.PhyrexianRequirements);

            if (remainingGeneric == 0 && remainingPhyrexian.Count == 0)
            {
                // Fully paid by colored auto-deduct — go to stack immediately
                castPlayer.PendingManaTaps.Clear();

                // Kicker: prompt after paying base cost
                var isKicked = await engine.TryPayKickerAsync(castCard, castPlayer, ct);

                if (castingFromExileAdventure)
                {
                    castPlayer.Exile.RemoveById(castCard.Id);
                    castCard.IsOnAdventure = false;
                }
                else
                {
                    castPlayer.Hand.RemoveById(castCard.Id);
                }
                var stackObj = new StackObject(castCard, castPlayer.Id, autoDeducted, targets, state.StackCount) { IsKicked = isKicked };
                state.StackPush(stackObj);

                action.ManaCostPaid = castEffectiveCost;
                castPlayer.ActionHistory.Push(action);

                state.Log($"{castPlayer.Name} casts {castCard.Name}.");
                state.SpellsCastThisTurn++;

                await engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, castCard, ct);
                await engine.QueueSelfCastTriggersAsync(castCard, castPlayer, ct);
            }
            else
            {
                // Enter mid-cast state — wait for manual payment
                state.BeginMidCast(castPlayer.Id, castCard, remainingGeneric, remainingPhyrexian);
                state.MidCastAutoDeducted = autoDeducted;

                // Store targets and action info on the state for CompleteMidCastAsync to use
                state.MidCastTargets = targets;
                state.MidCastAction = action;
                state.MidCastEffectiveCost = castEffectiveCost;
                state.MidCastFromExileAdventure = castingFromExileAdventure;

                castPlayer.PendingManaTaps.Clear();
                state.Log($"{castPlayer.Name} begins casting {castCard.Name}...");

                // Auto-resolve for non-manual-payment players (AI bots, test handlers)
                // Only handlers implementing IManualManaPayment use MTGO-style payment
                if (castPlayer.DecisionHandler is not IManualManaPayment)
                {
                    await engine.AutoResolveMidCastForAi(state, castPlayer, ct);
                }
            }
        }

        // Exile Delve cards
        if (delveExiledCards != null)
        {
            foreach (var exiled in delveExiledCards)
            {
                castPlayer.Graveyard.RemoveById(exiled.Id);
                castPlayer.Exile.Add(exiled);
            }
        }
    }
}
