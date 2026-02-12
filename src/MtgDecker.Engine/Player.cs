using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine;

public class Player
{
    public Guid Id { get; }
    public string Name { get; }
    public IPlayerDecisionHandler DecisionHandler { get; }

    public Zone Library { get; }
    public Zone Hand { get; }
    public Zone Battlefield { get; }
    public Zone Graveyard { get; }
    public Zone Exile { get; }
    public int Life { get; private set; } = 20;
    public Stack<GameAction> ActionHistory { get; } = new();
    public ManaPool ManaPool { get; } = new();
    public int LandsPlayedThisTurn { get; set; }
    public int MaxLandDrops { get; set; } = 1;

    public void AdjustLife(int delta)
    {
        Life += delta;
    }

    public Player(Guid id, string name, IPlayerDecisionHandler decisionHandler)
    {
        Id = id;
        Name = name;
        DecisionHandler = decisionHandler;
        Library = new Zone(ZoneType.Library);
        Hand = new Zone(ZoneType.Hand);
        Battlefield = new Zone(ZoneType.Battlefield);
        Graveyard = new Zone(ZoneType.Graveyard);
        Exile = new Zone(ZoneType.Exile);
    }

    public Zone GetZone(ZoneType type) => type switch
    {
        ZoneType.Library => Library,
        ZoneType.Hand => Hand,
        ZoneType.Battlefield => Battlefield,
        ZoneType.Graveyard => Graveyard,
        ZoneType.Exile => Exile,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
