using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine;

/// <summary>
/// A single-use damage prevention shield (e.g., Circle of Protection).
/// Prevents the next instance of damage from a source of the specified color.
/// </summary>
public record DamagePreventionShield(ManaColor Color);

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
    public List<Guid> PendingManaTaps { get; } = new();
    public int LandsPlayedThisTurn { get; set; }
    public int MaxLandDrops { get; set; } = 1;
    public int CreaturesDiedThisTurn { get; set; }
    public int DrawsThisTurn { get; set; }
    public bool DrawStepDrawExempted { get; set; }
    public HashSet<Guid> PlaneswalkerAbilitiesUsedThisTurn { get; } = [];
    public int LifeLostThisTurn { get; set; }
    public bool PermanentLeftBattlefieldThisTurn { get; set; }
    public List<Emblem> Emblems { get; } = [];
    public List<DamagePreventionShield> DamagePreventionShields { get; } = [];

    public void AdjustLife(int delta, GameState? state = null)
    {
        if (delta > 0 && state != null)
        {
            var prevented = state.ActiveEffects.Any(e => e.Type == ContinuousEffectType.PreventLifeGain);
            if (prevented)
                return;
        }
        Life += delta;
        if (delta < 0)
            LifeLostThisTurn += -delta;
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

    public void ResetTurnState()
    {
        CreaturesDiedThisTurn = 0;
        DrawsThisTurn = 0;
        DrawStepDrawExempted = false;
        PlaneswalkerAbilitiesUsedThisTurn.Clear();
        LifeLostThisTurn = 0;
        PermanentLeftBattlefieldThisTurn = false;
        foreach (var card in Battlefield.Cards)
        {
            card.CarpetUsedThisTurn = false;
            card.AbilitiesActivatedThisTurn.Clear();
        }
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
