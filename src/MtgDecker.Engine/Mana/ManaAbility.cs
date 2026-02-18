using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Mana;

public class ManaAbility
{
    public ManaAbilityType Type { get; }
    public ManaColor? FixedColor { get; }
    public IReadOnlyList<ManaColor>? ChoiceColors { get; }
    public ManaColor? DynamicColor { get; }
    public Func<Player, int>? CountFunc { get; }
    public IReadOnlySet<ManaColor>? PainColors { get; }
    public CounterType? RemovesCounterOnTap { get; }
    public ManaCost? ActivationCost { get; }
    public IReadOnlyList<ManaColor>? ProducedColors { get; }

    private ManaAbility(ManaAbilityType type, ManaColor? fixedColor, IReadOnlyList<ManaColor>? choiceColors,
        ManaColor? dynamicColor = null, Func<Player, int>? countFunc = null,
        IReadOnlySet<ManaColor>? painColors = null, CounterType? removesCounterOnTap = null,
        ManaCost? activationCost = null, IReadOnlyList<ManaColor>? producedColors = null)
    {
        Type = type;
        FixedColor = fixedColor;
        ChoiceColors = choiceColors;
        DynamicColor = dynamicColor;
        CountFunc = countFunc;
        PainColors = painColors;
        RemovesCounterOnTap = removesCounterOnTap;
        ActivationCost = activationCost;
        ProducedColors = producedColors;
    }

    public static ManaAbility Fixed(ManaColor color) =>
        new(ManaAbilityType.Fixed, color, null);

    public static ManaAbility Choice(params ManaColor[] colors) =>
        new(ManaAbilityType.Choice, null, colors.ToList().AsReadOnly());

    public static ManaAbility PainChoice(ManaColor[] colors, ManaColor[] painColors) =>
        new(ManaAbilityType.Choice, null, colors.ToList().AsReadOnly(),
            painColors: painColors.ToHashSet().AsReadOnly());

    public static ManaAbility Dynamic(ManaColor color, Func<Player, int> countFunc) =>
        new(ManaAbilityType.Dynamic, null, null, color, countFunc);

    public static ManaAbility DepletionChoice(CounterType counterType, params ManaColor[] colors) =>
        new(ManaAbilityType.Choice, null, colors.ToList().AsReadOnly(), removesCounterOnTap: counterType);

    public static ManaAbility Filter(ManaCost cost, params ManaColor[] producedColors) =>
        new(ManaAbilityType.Filter, null, null, activationCost: cost,
            producedColors: producedColors.ToList().AsReadOnly());
}

public enum ManaAbilityType
{
    Fixed,
    Choice,
    Dynamic,
    Filter
}
