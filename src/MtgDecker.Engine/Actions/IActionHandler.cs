using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Actions;

internal interface IActionHandler
{
    Task ExecuteAsync(GameAction action, GameEngine engine, GameState state, CancellationToken ct);
}
