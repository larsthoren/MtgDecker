using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Actions;

internal class CancelCastHandler : IActionHandler
{
    public Task ExecuteAsync(GameAction action, GameEngine engine, GameState state, CancellationToken ct)
    {
        if (!state.IsMidCast)
            throw new InvalidOperationException("Not in mid-cast state.");

        var player = state.GetPlayer(action.PlayerId);
        var card = state.PendingCastCard!;

        // Refund auto-deducted colored mana
        foreach (var (color, amount) in state.MidCastAutoDeducted)
        {
            player.ManaPool.Add(color, amount);
        }

        // Refund manually paid mana
        foreach (var (color, amount) in state.MidCastManuallyPaid)
        {
            player.ManaPool.Add(color, amount);
        }

        // Refund life paid for Phyrexian symbols
        if (state.MidCastLifePaid > 0)
        {
            player.AdjustLife(state.MidCastLifePaid);
        }

        // Card is still in hand during mid-cast (not removed until CompleteMidCastAsync)
        state.Log($"{player.Name} cancels casting {card.Name}.");
        state.ClearMidCast();

        return Task.CompletedTask;
    }
}
