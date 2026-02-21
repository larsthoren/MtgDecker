using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Actions;

internal class CastAdventureHandler : IActionHandler
{
    public async Task ExecuteAsync(GameAction action, GameEngine engine, GameState state, CancellationToken ct)
    {
        var advPlayer = state.GetPlayer(action.PlayerId);
        var advCard = advPlayer.Hand.Cards.FirstOrDefault(c => c.Id == action.CardId);
        if (advCard == null)
        {
            state.Log("Card not found in hand.");
            return;
        }

        if (!CardDefinitions.TryGet(advCard.Name, out var advDef) || advDef.Adventure == null)
        {
            state.Log($"{advCard.Name} has no adventure.");
            return;
        }

        var adventure = advDef.Adventure;

        bool advIsInstant = advDef.CardTypes.HasFlag(CardType.Instant) || advDef.HasFlash;
        if (!advIsInstant && !engine.CanCastSorcery(advPlayer.Id))
        {
            state.Log($"Cannot cast {adventure.Name} at this time (sorcery-speed only).");
            return;
        }

        var advEffectiveCost = adventure.Cost;
        var advCostReduction = engine.ComputeCostModification(advCard, advPlayer);
        if (advCostReduction != 0)
            advEffectiveCost = advEffectiveCost.WithGenericReduction(-advCostReduction);

        if (!advPlayer.ManaPool.CanPay(advEffectiveCost))
        {
            state.Log($"Not enough mana to cast {adventure.Name}.");
            return;
        }

        // Use shared targeting helper instead of inline targeting
        var advTargets = new List<TargetInfo>();
        if (adventure.Filter != null)
        {
            var result = await engine.FindAndChooseTargetsAsync(
                adventure.Filter, advPlayer, advPlayer.DecisionHandler, adventure.Name, ct);

            if (result == null)
            {
                state.Log($"{advPlayer.Name} cancels casting {adventure.Name}.");
                return;
            }

            if (result.Count == 0)
            {
                state.Log($"No legal targets for {adventure.Name}.");
                return;
            }

            advTargets = result;
        }

        var advManaPaid = await engine.PayManaCostAsync(advEffectiveCost, advPlayer, ct);
        advPlayer.PendingManaTaps.Clear();

        advPlayer.Hand.RemoveById(advCard.Id);
        var advStackObj = new StackObject(advCard, advPlayer.Id, advManaPaid, advTargets, state.StackCount)
        {
            IsAdventure = true,
        };
        state.StackPush(advStackObj);

        action.ManaCostPaid = advEffectiveCost;
        advPlayer.ActionHistory.Push(action);

        state.Log($"{advPlayer.Name} casts {adventure.Name} (adventure of {advCard.Name}).");
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, advCard, ct);
    }
}
