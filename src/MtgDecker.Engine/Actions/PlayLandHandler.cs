using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Actions;

internal class PlayLandHandler : IActionHandler
{
    public async Task ExecuteAsync(GameAction action, GameEngine engine, GameState state, CancellationToken ct)
    {
        var player = state.GetPlayer(action.PlayerId);
        var playCard = player.Hand.Cards.FirstOrDefault(c => c.Id == action.CardId);
        if (playCard == null) return;

        if (!playCard.IsLand)
        {
            state.Log($"{playCard.Name} is not a land — use CastSpell instead.");
            return;
        }

        if (state.ActivePlayer.Id != action.PlayerId)
        {
            state.Log($"Cannot play land — only the active player can play lands.");
            return;
        }

        if (state.CurrentPhase != Phase.MainPhase1 && state.CurrentPhase != Phase.MainPhase2)
        {
            state.Log($"Cannot play land — lands can only be played during main phases.");
            return;
        }

        if (state.StackCount > 0)
        {
            state.Log($"Cannot play land — the stack must be empty.");
            return;
        }

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
}
