using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Actions;

internal class CastSpellHandler : IActionHandler
{
    public async Task ExecuteAsync(GameAction action, GameEngine engine, GameState state, CancellationToken ct)
    {
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

        bool canPayMana = castEffectiveCost.HasPhyrexianCost
            ? castPlayer.ManaPool.CanPayWithPhyrexian(castEffectiveCost, castPlayer.Life)
            : castPlayer.ManaPool.CanPay(castEffectiveCost);
        bool canPayAlternate = def?.AlternateCost != null && engine.CanPayAlternateCost(def.AlternateCost, castPlayer, castCard);
        bool useAlternateCost = false;

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

        Dictionary<ManaColor, int> manaPaid;
        if (useAlternateCost)
        {
            await engine.PayAlternateCostAsync(def!.AlternateCost!, castPlayer, castCard, ct);
            manaPaid = new Dictionary<ManaColor, int>();
        }
        else
        {
            manaPaid = await engine.PayManaCostAsync(castEffectiveCost, castPlayer, ct);
            castPlayer.PendingManaTaps.Clear();
        }

        if (castingFromExileAdventure)
        {
            castPlayer.Exile.RemoveById(castCard.Id);
            castCard.IsOnAdventure = false;
        }
        else
        {
            castPlayer.Hand.RemoveById(castCard.Id);
        }
        var stackObj = new StackObject(castCard, castPlayer.Id, manaPaid, targets, state.StackCount);
        state.StackPush(stackObj);

        action.ManaCostPaid = castEffectiveCost;
        castPlayer.ActionHistory.Push(action);

        state.Log($"{castPlayer.Name} casts {castCard.Name}.");

        await engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, castCard, ct);
        await engine.QueueSelfCastTriggersAsync(castCard, castPlayer, ct);
    }
}
