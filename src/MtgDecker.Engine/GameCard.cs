using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine;

public class GameCard
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
    public string TypeLine { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public bool IsTapped { get; set; }

    // Resolved from CardDefinitions registry
    public ManaCost? ManaCost { get; set; }
    public ManaAbility? ManaAbility { get; set; }
    public int? Power { get; set; }
    public int? Toughness { get; set; }
    public CardType CardTypes { get; set; } = CardType.None;

    // Backward-compatible: check both CardTypes flags and TypeLine
    public bool IsLand =>
        CardTypes.HasFlag(CardType.Land) ||
        TypeLine.Contains("Land", StringComparison.OrdinalIgnoreCase);

    public bool IsCreature =>
        CardTypes.HasFlag(CardType.Creature) ||
        TypeLine.Contains("Creature", StringComparison.OrdinalIgnoreCase);

    public static GameCard Create(string name, string typeLine = "", string? imageUrl = null)
    {
        var card = new GameCard { Name = name, TypeLine = typeLine, ImageUrl = imageUrl };
        if (CardDefinitions.TryGet(name, out var def))
        {
            card.ManaCost = def.ManaCost;
            card.ManaAbility = def.ManaAbility;
            card.Power = def.Power;
            card.Toughness = def.Toughness;
            card.CardTypes = def.CardTypes;
        }
        return card;
    }
}
