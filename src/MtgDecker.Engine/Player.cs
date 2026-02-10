using MtgDecker.Engine.Enums;

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

    public Player(Guid id, string name, IPlayerDecisionHandler decisionHandler)
    {
        Id = id;
        Name = name;
        DecisionHandler = decisionHandler;
        Library = new Zone(ZoneType.Library);
        Hand = new Zone(ZoneType.Hand);
        Battlefield = new Zone(ZoneType.Battlefield);
        Graveyard = new Zone(ZoneType.Graveyard);
    }

    public Zone GetZone(ZoneType type) => type switch
    {
        ZoneType.Library => Library,
        ZoneType.Hand => Hand,
        ZoneType.Battlefield => Battlefield,
        ZoneType.Graveyard => Graveyard,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
