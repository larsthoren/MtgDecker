namespace MtgDecker.Engine.Enums;

/// <summary>
/// Classifies spells by when/how the AI should play them.
/// </summary>
public enum SpellRole
{
    /// <summary>Creatures, sorceries, enchantments — play in main phase with empty stack.</summary>
    Proactive,
    /// <summary>Only cast in response to opponent's spell on the stack.</summary>
    Counterspell,
    /// <summary>Instant-speed removal — cast reactively during combat or end of turn.</summary>
    InstantRemoval,
    /// <summary>Instant-speed utility — card draw, mana, etc. Cast at end of opponent's turn.</summary>
    InstantUtility,
}
