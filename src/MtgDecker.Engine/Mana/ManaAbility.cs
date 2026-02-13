using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Mana;

public class ManaAbility
{
    public ManaAbilityType Type { get; }
    public ManaColor? FixedColor { get; }
    public IReadOnlyList<ManaColor>? ChoiceColors { get; }
    public ManaColor? DynamicColor { get; }
    public Func<Player, int>? CountFunc { get; }

    private ManaAbility(ManaAbilityType type, ManaColor? fixedColor, IReadOnlyList<ManaColor>? choiceColors,
        ManaColor? dynamicColor = null, Func<Player, int>? countFunc = null)
    {
        Type = type;
        FixedColor = fixedColor;
        ChoiceColors = choiceColors;
        DynamicColor = dynamicColor;
        CountFunc = countFunc;
    }

    public static ManaAbility Fixed(ManaColor color) =>
        new(ManaAbilityType.Fixed, color, null);

    public static ManaAbility Choice(params ManaColor[] colors) =>
        new(ManaAbilityType.Choice, null, colors.ToList().AsReadOnly());

    public static ManaAbility Dynamic(ManaColor color, Func<Player, int> countFunc) =>
        new(ManaAbilityType.Dynamic, null, null, color, countFunc);
}

public enum ManaAbilityType
{
    Fixed,
    Choice,
    Dynamic
}
