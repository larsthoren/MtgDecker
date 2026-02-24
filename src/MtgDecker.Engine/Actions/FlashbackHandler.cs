using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Actions;

internal class FlashbackHandler : IActionHandler
{
    public async Task ExecuteAsync(GameAction action, GameEngine engine, GameState state, CancellationToken ct)
    {
        var fbPlayer = state.GetPlayer(action.PlayerId);

        // PreventSpellCasting: check if this player is prevented from casting spells
        if (state.ActiveEffects.Any(e =>
            e.Type == ContinuousEffectType.PreventSpellCasting
            && e.Applies(new GameCard(), fbPlayer)))
        {
            state.Log($"{fbPlayer.Name} can't cast spells this turn.");
            return;
        }

        var fbCard = fbPlayer.Graveyard.Cards.FirstOrDefault(c => c.Id == action.CardId);
        if (fbCard == null)
        {
            state.Log("Card not found in graveyard.");
            return;
        }

        // Meddling Mage: check if any opponent's Meddling Mage has named this card
        var fbOpponent = state.Player1.Id == action.PlayerId ? state.Player2 : state.Player1;
        var fbMeddlingMage = fbOpponent.Battlefield.Cards
            .FirstOrDefault(c => c.ChosenName != null
                && string.Equals(c.ChosenName, fbCard.Name, StringComparison.OrdinalIgnoreCase));
        if (fbMeddlingMage != null)
        {
            state.Log($"{fbCard.Name} can't be cast — named by {fbMeddlingMage.Name}.");
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

        // Exile blue cards from graveyard (Flash of Insight)
        IReadOnlyList<GameCard>? fbExiledBlueCards = null;
        if (fbCost.ExileBlueCardsFromGraveyard > 0)
        {
            // Blue cards in graveyard (excluding the flashback card itself, which is being cast)
            var blueCards = fbPlayer.Graveyard.Cards
                .Where(c => c.Id != fbCard.Id && c.Colors.Contains(ManaColor.Blue))
                .ToList();
            if (blueCards.Count == 0)
            {
                state.Log($"No blue cards in graveyard for flashback of {fbCard.Name}.");
                return;
            }
            // Player chooses how many blue cards to exile (at least 1)
            fbExiledBlueCards = await fbPlayer.DecisionHandler.ChooseCardsToExile(
                blueCards, blueCards.Count,
                $"Exile blue cards from graveyard for flashback of {fbCard.Name} (X = number exiled)", ct);
            if (fbExiledBlueCards.Count == 0)
            {
                state.Log($"No blue cards chosen for flashback of {fbCard.Name}.");
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

        int? fbXValue = null;
        if (fbExiledBlueCards != null)
        {
            foreach (var exiled in fbExiledBlueCards)
            {
                fbPlayer.Graveyard.RemoveById(exiled.Id);
                fbPlayer.Exile.Add(exiled);
            }
            fbXValue = fbExiledBlueCards.Count;
            state.Log($"{fbPlayer.Name} exiles {fbXValue} blue card(s) for flashback of {fbCard.Name} (X={fbXValue}).");
        }

        fbPlayer.Graveyard.RemoveById(fbCard.Id);
        var fbStackObj = new StackObject(fbCard, fbPlayer.Id, fbManaPaid, fbTargets, state.StackCount)
        {
            IsFlashback = true,
            XValue = fbXValue,
        };
        state.StackPush(fbStackObj);

        action.ManaCostPaid = fbCost.ManaCost;
        fbPlayer.ActionHistory.Push(action);

        state.Log($"{fbPlayer.Name} casts {fbCard.Name} (flashback).");
        state.SpellsCastThisTurn++;
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, fbCard, ct);
    }
}
