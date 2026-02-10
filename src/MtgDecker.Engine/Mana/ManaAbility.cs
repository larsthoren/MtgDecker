using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Mana;

public class ManaAbility
{
    public ManaAbilityType Type { get; }
    public ManaColor? FixedColor { get; }
    public IReadOnlyList<ManaColor>? ChoiceColors { get; }

    private ManaAbility(ManaAbilityType type, ManaColor? fixedColor, IReadOnlyList<ManaColor>? choiceColors)
    {
        Type = type;
        FixedColor = fixedColor;
        ChoiceColors = choiceColors;
    }

    public static ManaAbility Fixed(ManaColor color) =>
        new(ManaAbilityType.Fixed, color, null);

    public static ManaAbility Choice(params ManaColor[] colors) =>
        new(ManaAbilityType.Choice, null, colors.ToList().AsReadOnly());
}

public enum ManaAbilityType
{
    Fixed,
    Choice
}
