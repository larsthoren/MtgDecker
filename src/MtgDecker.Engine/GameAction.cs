using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine;

public class GameAction
{
    private GameAction() { }

    public ActionType Type { get; init; }
    public Guid PlayerId { get; init; }
    public Guid? CardId { get; init; }
    public ZoneType? SourceZone { get; init; }
    public ZoneType? DestinationZone { get; internal set; }
    public Guid? TargetCardId { get; init; }
    public Guid? TargetPlayerId { get; init; }

    // Undo metadata — set by GameEngine during ExecuteAction
    public bool IsManaAbility { get; internal set; }
    public ManaColor? ManaProduced { get; internal set; }
    public List<ManaColor>? BonusManaProduced { get; internal set; }
    public bool PainDamageDealt { get; internal set; }
    public ManaCost? ManaCostPaid { get; internal set; }
    public Dictionary<ManaColor, int>? ActualManaPaid { get; internal set; }
    public bool IsLandDrop { get; internal set; }

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

    public static GameAction CastSpell(Guid playerId, Guid cardId) => new()
    {
        Type = ActionType.CastSpell,
        PlayerId = playerId,
        CardId = cardId,
        SourceZone = ZoneType.Hand
    };

    public static GameAction ActivateFetch(Guid playerId, Guid cardId) => new()
    {
        Type = ActionType.ActivateFetch,
        PlayerId = playerId,
        CardId = cardId
    };

    public static GameAction ActivateAbility(Guid playerId, Guid cardId,
        Guid? targetId = null, Guid? targetPlayerId = null) => new()
    {
        Type = ActionType.ActivateAbility,
        PlayerId = playerId,
        CardId = cardId,
        TargetCardId = targetId,
        TargetPlayerId = targetPlayerId,
    };

    public static GameAction Cycle(Guid playerId, Guid cardId) => new()
    {
        Type = ActionType.Cycle,
        PlayerId = playerId,
        CardId = cardId,
        SourceZone = ZoneType.Hand,
    };
}
