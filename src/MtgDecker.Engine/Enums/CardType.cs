namespace MtgDecker.Engine.Enums;

[Flags]
public enum CardType
{
    None = 0,
    Land = 1,
    Creature = 2,
    Enchantment = 4,
    Instant = 8,
    Sorcery = 16,
    Artifact = 32
}
