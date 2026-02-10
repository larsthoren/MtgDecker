using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class TestDecisionHandlerTests
{
    private readonly TestDecisionHandler _handler = new();
    private readonly Guid _playerId = Guid.NewGuid();

    [Fact]
    public async Task GetAction_ReturnsQueuedAction()
    {
        var expected = GameAction.TapCard(_playerId, Guid.NewGuid());
        _handler.EnqueueAction(expected);

        var result = await _handler.GetAction(null!, _playerId);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetAction_DefaultsToPass_WhenQueueEmpty()
    {
        var result = await _handler.GetAction(null!, _playerId);

        result.Type.Should().Be(ActionType.PassPriority);
        result.PlayerId.Should().Be(_playerId);
    }

    [Fact]
    public async Task GetMulliganDecision_ReturnsQueuedDecision()
    {
        _handler.EnqueueMulligan(MulliganDecision.Mulligan);

        var result = await _handler.GetMulliganDecision(Array.Empty<GameCard>(), 0);

        result.Should().Be(MulliganDecision.Mulligan);
    }

    [Fact]
    public async Task GetMulliganDecision_DefaultsToKeep()
    {
        var result = await _handler.GetMulliganDecision(Array.Empty<GameCard>(), 0);

        result.Should().Be(MulliganDecision.Keep);
    }

    [Fact]
    public async Task ChooseCardsToBottom_ReturnsFirstNCards_ByDefault()
    {
        var hand = new[]
        {
            new GameCard { Name = "A" },
            new GameCard { Name = "B" },
            new GameCard { Name = "C" }
        };

        var result = await _handler.ChooseCardsToBottom(hand, 2);

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("A");
        result[1].Name.Should().Be("B");
    }
}
