using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Actions;

internal class PayManaFromPoolHandler : IActionHandler
{
    public async Task ExecuteAsync(GameAction action, GameEngine engine, GameState state, CancellationToken ct)
    {
        if (!state.IsMidCast)
            throw new InvalidOperationException("Not in mid-cast state.");

        var player = state.GetPlayer(action.PlayerId);
        var color = action.ManaProduced
            ?? throw new InvalidOperationException("PayManaFromPool requires a mana color.");

        if (player.ManaPool[color] <= 0)
            throw new InvalidOperationException($"No {color} mana in pool.");

        player.ManaPool.Deduct(color, 1);
        state.ApplyManaPayment(color);
        state.MidCastManuallyPaid[color] = state.MidCastManuallyPaid.GetValueOrDefault(color) + 1;
        state.Log($"{player.Name} pays {{{GameEngine.GetColorSymbol(color)}}} from pool.");

        if (state.IsFullyPaid)
        {
            await engine.CompleteMidCastAsync(state, player, ct);
        }
    }
}
