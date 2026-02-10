using FluentAssertions;
using MtgDecker.Engine;

namespace MtgDecker.Engine.Tests;

public class GameSessionManagerTests
{
    [Fact]
    public void CreateGame_ReturnsSessionWithId()
    {
        var manager = new GameSessionManager();
        var session = manager.CreateGame();

        session.Should().NotBeNull();
        session.GameId.Should().HaveLength(6);
    }

    [Fact]
    public void CreateGame_GeneratesUniqueIds()
    {
        var manager = new GameSessionManager();
        var ids = Enumerable.Range(0, 10)
            .Select(_ => manager.CreateGame().GameId)
            .ToList();

        ids.Distinct().Count().Should().Be(10);
    }

    [Fact]
    public void GetSession_ReturnsExistingSession()
    {
        var manager = new GameSessionManager();
        var session = manager.CreateGame();

        var retrieved = manager.GetSession(session.GameId);

        retrieved.Should().BeSameAs(session);
    }

    [Fact]
    public void GetSession_ReturnsNullForUnknownId()
    {
        var manager = new GameSessionManager();

        manager.GetSession("XXXXXX").Should().BeNull();
    }

    [Fact]
    public void RemoveSession_RemovesFromManager()
    {
        var manager = new GameSessionManager();
        var session = manager.CreateGame();

        manager.RemoveSession(session.GameId);

        manager.GetSession(session.GameId).Should().BeNull();
    }
}
