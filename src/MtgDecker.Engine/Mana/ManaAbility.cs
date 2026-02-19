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
    public int SelfDamage { get; }
    public int ProduceCount { get; }

    private ManaAbility(ManaAbilityType type, ManaColor? fixedColor, IReadOnlyList<ManaColor>? choiceColors,
        ManaColor? dynamicColor = null, Func<Player, int>? countFunc = null,
        IReadOnlySet<ManaColor>? painColors = null, CounterType? removesCounterOnTap = null,
        int selfDamage = 0, int produceCount = 1)
    {
        Type = type;
        FixedColor = fixedColor;
        ChoiceColors = choiceColors;
        DynamicColor = dynamicColor;
        CountFunc = countFunc;
        PainColors = painColors;
        RemovesCounterOnTap = removesCounterOnTap;
        SelfDamage = selfDamage;
        ProduceCount = produceCount;
    }

    public static ManaAbility Fixed(ManaColor color) =>
        new(ManaAbilityType.Fixed, color, null, produceCount: 1);

    public static ManaAbility FixedMultiple(ManaColor color, int count, int selfDamage = 0) =>
        new(ManaAbilityType.Fixed, color, null, selfDamage: selfDamage, produceCount: count);

    public static ManaAbility Choice(params ManaColor[] colors) =>
        new(ManaAbilityType.Choice, null, colors.ToList().AsReadOnly());

    public static ManaAbility PainChoice(ManaColor[] colors, ManaColor[] painColors) =>
        new(ManaAbilityType.Choice, null, colors.ToList().AsReadOnly(),
            painColors: painColors.ToHashSet().AsReadOnly());

    public static ManaAbility Dynamic(ManaColor color, Func<Player, int> countFunc) =>
        new(ManaAbilityType.Dynamic, null, null, color, countFunc);

    public static ManaAbility DepletionChoice(CounterType counterType, params ManaColor[] colors) =>
        new(ManaAbilityType.Choice, null, colors.ToList().AsReadOnly(), removesCounterOnTap: counterType);
}

public enum ManaAbilityType
{
    Fixed,
    Choice,
    Dynamic
}
