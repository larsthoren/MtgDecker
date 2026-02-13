using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public class TargetFilter
{
    private readonly Func<GameCard, ZoneType, bool> _predicate;

    private TargetFilter(Func<GameCard, ZoneType, bool> predicate)
    {
        _predicate = predicate;
    }

    public bool IsLegal(GameCard card, ZoneType zone) => _predicate(card, zone);

    public static TargetFilter Creature() => new((card, zone) =>
        zone == ZoneType.Battlefield && card.IsCreature);

    public static TargetFilter EnchantmentOrArtifact() => new((card, zone) =>
        zone == ZoneType.Battlefield &&
        (card.CardTypes.HasFlag(CardType.Enchantment) || card.CardTypes.HasFlag(CardType.Artifact)));

    public static TargetFilter CreatureOrPlayer() => new((card, zone) =>
        (zone == ZoneType.Battlefield && card.IsCreature) || zone == ZoneType.None);

    public static TargetFilter Player() => new((card, zone) => zone == ZoneType.None);
}
