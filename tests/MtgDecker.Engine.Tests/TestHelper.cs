using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public static class TestHelper
{
    public static GameState CreateState()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        return new GameState(p1, p2);
    }

    public static (GameState state, TestDecisionHandler h1, TestDecisionHandler h2) CreateStateWithHandlers()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        return (new GameState(p1, p2), h1, h2);
    }
}
