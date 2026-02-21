using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Actions;

internal class PlayCardHandler : IActionHandler
{
    public async Task ExecuteAsync(GameAction action, GameEngine engine, GameState state, CancellationToken ct)
    {
        var player = state.GetPlayer(action.PlayerId);
        var playCard = player.Hand.Cards.FirstOrDefault(c => c.Id == action.CardId);
        if (playCard == null) return;

        if (playCard.IsLand)
        {
            if (player.LandsPlayedThisTurn >= player.MaxLandDrops)
            {
                state.Log($"{player.Name} cannot play another land this turn.");
                return;
            }
            player.Hand.RemoveById(playCard.Id);
            player.Battlefield.Add(playCard);
            playCard.TurnEnteredBattlefield = state.TurnNumber;
            if (playCard.EntersTapped) playCard.IsTapped = true;
            player.LandsPlayedThisTurn++;
            action.IsLandDrop = true;
            action.DestinationZone = ZoneType.Battlefield;
            player.ActionHistory.Push(action);
            state.Log($"{player.Name} plays {playCard.Name} (land drop).");
            engine.ApplyEntersWithCounters(playCard);
            await engine.QueueSelfTriggersOnStackAsync(GameEvent.EnterBattlefield, playCard, player, ct);
            await engine.OnBoardChangedAsync(ct);

            await engine.QueueBoardTriggersOnStackAsync(GameEvent.LandPlayed, playCard, ct);
        }
        else if (playCard.ManaCost != null)
        {
            var effectiveCost = playCard.ManaCost;
            var costReduction = engine.ComputeCostModification(playCard, player);
            if (costReduction != 0)
                effectiveCost = effectiveCost.WithGenericReduction(-costReduction);

            if (!player.ManaPool.CanPay(effectiveCost))
            {
                state.Log($"{player.Name} cannot cast {playCard.Name} — not enough mana.");
                return;
            }

            var playManaPaid = await engine.PayManaCostAsync(effectiveCost, player, ct);
            player.PendingManaTaps.Clear();

            player.Hand.RemoveById(playCard.Id);
            bool isInstantOrSorcery = playCard.CardTypes.HasFlag(CardType.Instant)
                                    || playCard.CardTypes.HasFlag(CardType.Sorcery);
            if (isInstantOrSorcery)
            {
                player.Graveyard.Add(playCard);
                action.DestinationZone = ZoneType.Graveyard;
                state.Log($"{player.Name} casts {playCard.Name} (→ graveyard).");
            }
            else
            {
                player.Battlefield.Add(playCard);
                playCard.TurnEnteredBattlefield = state.TurnNumber;
                if (playCard.EntersTapped) playCard.IsTapped = true;
                action.DestinationZone = ZoneType.Battlefield;
                state.Log($"{player.Name} casts {playCard.Name}.");

                await engine.TryAttachAuraAsync(playCard, player, ct);

                engine.ApplyEntersWithCounters(playCard);
                await engine.QueueSelfTriggersOnStackAsync(GameEvent.EnterBattlefield, playCard, player, ct);
                await engine.OnBoardChangedAsync(ct);
            }
            await engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, playCard, ct);
            action.ManaCostPaid = effectiveCost;
            action.ActualManaPaid = playManaPaid;
            player.ActionHistory.Push(action);
        }
        else
        {
            state.Log($"{playCard.Name} is not supported in the engine (no card definition).");
        }
    }
}
