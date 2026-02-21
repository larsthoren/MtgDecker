using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Actions;

internal class FlashbackHandler : IActionHandler
{
    public async Task ExecuteAsync(GameAction action, GameEngine engine, GameState state, CancellationToken ct)
    {
        var fbPlayer = state.GetPlayer(action.PlayerId);
        var fbCard = fbPlayer.Graveyard.Cards.FirstOrDefault(c => c.Id == action.CardId);
        if (fbCard == null)
        {
            state.Log("Card not found in graveyard.");
            return;
        }

        if (!CardDefinitions.TryGet(fbCard.Name, out var fbDef) || fbDef.FlashbackCost == null)
        {
            state.Log($"{fbCard.Name} has no flashback.");
            return;
        }

        bool fbIsInstant = fbDef.CardTypes.HasFlag(CardType.Instant);
        bool fbHasFlash = fbDef.HasFlash;
        if (!fbIsInstant && !fbHasFlash && !engine.CanCastSorcery(fbPlayer.Id))
        {
            state.Log($"Cannot cast {fbCard.Name} at this time (sorcery-speed only).");
            return;
        }

        var fbCost = fbDef.FlashbackCost;

        if (fbCost.ManaCost != null && !fbPlayer.ManaPool.CanPay(fbCost.ManaCost))
        {
            state.Log($"Not enough mana for flashback of {fbCard.Name}.");
            return;
        }

        if (fbCost.LifeCost > 0 && fbPlayer.Life <= fbCost.LifeCost)
        {
            state.Log($"Not enough life for flashback of {fbCard.Name}.");
            return;
        }

        GameCard? fbSacTarget = null;
        if (fbCost.SacrificeCreature)
        {
            var fbCreatures = fbPlayer.Battlefield.Cards.Where(c => c.IsCreature).ToList();
            if (fbCreatures.Count == 0)
            {
                state.Log($"No creature to sacrifice for flashback of {fbCard.Name}.");
                return;
            }
            var chosenId = await fbPlayer.DecisionHandler.ChooseCard(
                fbCreatures, "Choose a creature to sacrifice for flashback", optional: false, ct);
            if (chosenId.HasValue)
                fbSacTarget = fbCreatures.FirstOrDefault(c => c.Id == chosenId.Value);
            if (fbSacTarget == null)
            {
                state.Log($"No creature chosen for flashback sacrifice.");
                return;
            }
        }

        // Use shared targeting helper
        var fbTargets = new List<TargetInfo>();
        if (fbDef.TargetFilter != null)
        {
            var result = await engine.FindAndChooseTargetsAsync(
                fbDef.TargetFilter, fbPlayer, fbPlayer.DecisionHandler, fbCard.Name, ct);

            if (result == null)
            {
                state.Log($"{fbPlayer.Name} cancels casting {fbCard.Name}.");
                return;
            }

            if (result.Count == 0)
            {
                state.Log($"No legal targets for {fbCard.Name}.");
                return;
            }

            fbTargets = result;
        }

        Dictionary<ManaColor, int> fbManaPaid = new();
        if (fbCost.ManaCost != null)
        {
            fbManaPaid = await engine.PayManaCostAsync(fbCost.ManaCost, fbPlayer, ct);
            fbPlayer.PendingManaTaps.Clear();
        }

        if (fbCost.LifeCost > 0)
        {
            fbPlayer.AdjustLife(-fbCost.LifeCost);
            state.Log($"{fbPlayer.Name} pays {fbCost.LifeCost} life for flashback.");
        }

        if (fbSacTarget != null)
        {
            await engine.FireLeaveBattlefieldTriggersAsync(fbSacTarget, fbPlayer, ct);
            fbPlayer.Battlefield.RemoveById(fbSacTarget.Id);
            fbPlayer.Graveyard.Add(fbSacTarget);
            state.Log($"{fbPlayer.Name} sacrifices {fbSacTarget.Name} for flashback.");
        }

        fbPlayer.Graveyard.RemoveById(fbCard.Id);
        var fbStackObj = new StackObject(fbCard, fbPlayer.Id, fbManaPaid, fbTargets, state.StackCount)
        {
            IsFlashback = true,
        };
        state.StackPush(fbStackObj);

        action.ManaCostPaid = fbCost.ManaCost;
        fbPlayer.ActionHistory.Push(action);

        state.Log($"{fbPlayer.Name} casts {fbCard.Name} (flashback).");
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, fbCard, ct);
    }
}
