using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public class GameAction
{
    public ActionType Type { get; init; }
    public Guid PlayerId { get; init; }
    public Guid? CardId { get; init; }
    public ZoneType? SourceZone { get; init; }
    public ZoneType? DestinationZone { get; init; }

    public static GameAction Pass(Guid playerId) => new()
    {
        Type = ActionType.PassPriority,
        PlayerId = playerId
    };

    public static GameAction PlayCard(Guid playerId, Guid cardId) => new()
    {
        Type = ActionType.PlayCard,
        PlayerId = playerId,
        CardId = cardId,
        SourceZone = ZoneType.Hand,
        DestinationZone = ZoneType.Battlefield
    };

    public static GameAction TapCard(Guid playerId, Guid cardId) => new()
    {
        Type = ActionType.TapCard,
        PlayerId = playerId,
        CardId = cardId
    };

    public static GameAction UntapCard(Guid playerId, Guid cardId) => new()
    {
        Type = ActionType.UntapCard,
        PlayerId = playerId,
        CardId = cardId
    };

    public static GameAction MoveCard(Guid playerId, Guid cardId, ZoneType from, ZoneType to) => new()
    {
        Type = ActionType.MoveCard,
        PlayerId = playerId,
        CardId = cardId,
        SourceZone = from,
        DestinationZone = to
    };
}
