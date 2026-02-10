using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class GameActionTests
{
    private readonly Guid _playerId = Guid.NewGuid();
    private readonly Guid _cardId = Guid.NewGuid();

    [Fact]
    public void Pass_CreatesPassAction()
    {
        var action = GameAction.Pass(_playerId);

        action.Type.Should().Be(ActionType.PassPriority);
        action.PlayerId.Should().Be(_playerId);
        action.CardId.Should().BeNull();
    }

    [Fact]
    public void PlayCard_CreatesPlayAction()
    {
        var action = GameAction.PlayCard(_playerId, _cardId);

        action.Type.Should().Be(ActionType.PlayCard);
        action.PlayerId.Should().Be(_playerId);
        action.CardId.Should().Be(_cardId);
        action.SourceZone.Should().Be(ZoneType.Hand);
        action.DestinationZone.Should().Be(ZoneType.Battlefield);
    }

    [Fact]
    public void TapCard_CreatesTapAction()
    {
        var action = GameAction.TapCard(_playerId, _cardId);

        action.Type.Should().Be(ActionType.TapCard);
        action.PlayerId.Should().Be(_playerId);
        action.CardId.Should().Be(_cardId);
    }

    [Fact]
    public void UntapCard_CreatesUntapAction()
    {
        var action = GameAction.UntapCard(_playerId, _cardId);

        action.Type.Should().Be(ActionType.UntapCard);
        action.PlayerId.Should().Be(_playerId);
        action.CardId.Should().Be(_cardId);
    }

    [Fact]
    public void MoveCard_CreatesMoveAction()
    {
        var action = GameAction.MoveCard(_playerId, _cardId, ZoneType.Battlefield, ZoneType.Graveyard);

        action.Type.Should().Be(ActionType.MoveCard);
        action.PlayerId.Should().Be(_playerId);
        action.CardId.Should().Be(_cardId);
        action.SourceZone.Should().Be(ZoneType.Battlefield);
        action.DestinationZone.Should().Be(ZoneType.Graveyard);
    }
}
