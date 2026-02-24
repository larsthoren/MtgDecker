using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

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

    public static TargetFilter Enchantment() => new((card, zone) =>
        zone == ZoneType.Battlefield && card.CardTypes.HasFlag(CardType.Enchantment));

    public static TargetFilter CreatureOrPlayer() => new((card, zone) =>
        (zone == ZoneType.Battlefield && card.IsCreature) || zone == ZoneType.None);

    public static TargetFilter Player() => new((card, zone) => zone == ZoneType.None);

    public static TargetFilter Spell() => new((card, zone) => zone == ZoneType.Stack);

    public static TargetFilter SpellOrPermanent() => new((card, zone) =>
        zone == ZoneType.Stack || zone == ZoneType.Battlefield);

    public static TargetFilter NonBlackCreature() => new((card, zone) =>
        zone == ZoneType.Battlefield && card.IsCreature
        && (card.ManaCost == null || !card.ManaCost.ColorRequirements.ContainsKey(ManaColor.Black)));

    public static TargetFilter CreatureWithCMCAtMost(int maxCmc) => new((card, zone) =>
        zone == ZoneType.Battlefield && card.IsCreature
        && (card.ManaCost?.ConvertedManaCost ?? 0) <= maxCmc);

    public static TargetFilter AnyPermanent() => new((card, zone) => zone == ZoneType.Battlefield);

    public static TargetFilter NoncreatureSpell() => new((card, zone) =>
        zone == ZoneType.Stack && !card.CardTypes.HasFlag(CardType.Creature));

    public static TargetFilter InstantOrSorcerySpell() => new((card, zone) =>
        zone == ZoneType.Stack &&
        (card.CardTypes.HasFlag(CardType.Instant) || card.CardTypes.HasFlag(CardType.Sorcery)));

    public static TargetFilter NonlandPermanent() => new((card, zone) =>
        zone == ZoneType.Battlefield && !card.IsLand);

    public static TargetFilter Artifact() => new((card, zone) =>
        zone == ZoneType.Battlefield && card.CardTypes.HasFlag(CardType.Artifact));

    public static TargetFilter ArtifactOrEnchantmentSpell() => new((card, zone) =>
        zone == ZoneType.Stack &&
        (card.CardTypes.HasFlag(CardType.Artifact) || card.CardTypes.HasFlag(CardType.Enchantment)));

    public static TargetFilter AnySpellOnStack() => new((card, zone) => zone == ZoneType.Stack);
}
