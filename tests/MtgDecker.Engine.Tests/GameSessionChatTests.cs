using FluentAssertions;
using MtgDecker.Engine;

namespace MtgDecker.Engine.Tests;

public class GameSessionChatTests
{
    [Fact]
    public void AddChatMessage_StoresMessage()
    {
        var session = new GameSession("test-1");
        session.JoinPlayer("Alice", new List<GameCard>());
        session.JoinPlayer("Bob", new List<GameCard>());

        session.AddChatMessage(1, "Hello!");

        session.ChatMessages.Should().HaveCount(1);
        session.ChatMessages[0].PlayerName.Should().Be("Alice");
        session.ChatMessages[0].Text.Should().Be("Hello!");
        session.ChatMessages[0].PlayerSeat.Should().Be(1);
    }

    [Fact]
    public void AddChatMessage_Player2_UsesCorrectName()
    {
        var session = new GameSession("test-2");
        session.JoinPlayer("Alice", new List<GameCard>());
        session.JoinPlayer("Bob", new List<GameCard>());

        session.AddChatMessage(2, "Hi there");

        session.ChatMessages.Should().HaveCount(1);
        session.ChatMessages[0].PlayerName.Should().Be("Bob");
    }

    [Fact]
    public void AddChatMessage_EmptyText_IsIgnored()
    {
        var session = new GameSession("test-3");
        session.JoinPlayer("Alice", new List<GameCard>());
        session.JoinPlayer("Bob", new List<GameCard>());

        session.AddChatMessage(1, "");
        session.AddChatMessage(1, "   ");

        session.ChatMessages.Should().BeEmpty();
    }

    [Fact]
    public void AddChatMessage_TruncatesAt200Chars()
    {
        var session = new GameSession("test-4");
        session.JoinPlayer("Alice", new List<GameCard>());
        session.JoinPlayer("Bob", new List<GameCard>());

        session.AddChatMessage(1, new string('x', 300));

        session.ChatMessages[0].Text.Should().HaveLength(200);
    }

    [Fact]
    public void ChatMessages_ReturnsSnapshot()
    {
        var session = new GameSession("test-5");
        session.JoinPlayer("Alice", new List<GameCard>());
        session.JoinPlayer("Bob", new List<GameCard>());

        session.AddChatMessage(1, "First");
        var snapshot = session.ChatMessages;
        session.AddChatMessage(2, "Second");

        snapshot.Should().HaveCount(1, "snapshot should not reflect later additions");
        session.ChatMessages.Should().HaveCount(2);
    }

    [Fact]
    public void AddChatMessage_FiresOnStateChanged()
    {
        var session = new GameSession("test-6");
        session.JoinPlayer("Alice", new List<GameCard>());
        session.JoinPlayer("Bob", new List<GameCard>());

        bool fired = false;
        session.OnStateChanged += () => fired = true;

        session.AddChatMessage(1, "Hello!");

        fired.Should().BeTrue();
    }
}
