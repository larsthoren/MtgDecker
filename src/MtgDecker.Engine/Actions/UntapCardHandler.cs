namespace MtgDecker.Engine.Actions;

internal class UntapCardHandler : IActionHandler
{
    public Task ExecuteAsync(GameAction action, GameEngine engine, GameState state, CancellationToken ct)
    {
        var player = state.GetPlayer(action.PlayerId);
        var untapTarget = player.Battlefield.Cards.FirstOrDefault(c => c.Id == action.CardId);
        if (untapTarget != null && untapTarget.IsTapped)
        {
            untapTarget.IsTapped = false;
            player.ActionHistory.Push(action);
            state.Log($"{player.Name} untaps {untapTarget.Name}.");
        }
        return Task.CompletedTask;
    }
}
