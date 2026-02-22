namespace MtgDecker.Engine.Actions;

internal class PayLifeForPhyrexianHandler : IActionHandler
{
    public async Task ExecuteAsync(GameAction action, GameEngine engine, GameState state, CancellationToken ct)
    {
        if (!state.IsMidCast)
            throw new InvalidOperationException("Not in mid-cast state.");

        if (state.TotalRemainingPhyrexian == 0)
            throw new InvalidOperationException("No Phyrexian cost remaining.");

        var player = state.GetPlayer(action.PlayerId);

        if (player.Life < 1)
            throw new InvalidOperationException("Not enough life to pay Phyrexian cost.");

        if (!state.ApplyLifePayment())
            throw new InvalidOperationException("No Phyrexian cost remaining.");

        player.AdjustLife(-2);
        state.Log($"{player.Name} pays 2 life for Phyrexian mana.");

        if (state.IsFullyPaid)
        {
            await engine.CompleteMidCastAsync(state, player, ct);
        }
    }
}
