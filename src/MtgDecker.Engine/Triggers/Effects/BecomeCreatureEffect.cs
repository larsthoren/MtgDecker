using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class BecomeCreatureEffect : IEffect
{
    public int Power { get; }
    public int Toughness { get; }
    public Keyword[]? Keywords { get; }
    public string[] Subtypes { get; }

    public BecomeCreatureEffect(int power, int toughness, params string[] subtypes)
        : this(power, toughness, null, subtypes) { }

    public BecomeCreatureEffect(int power, int toughness, Keyword[]? keywords, params string[] subtypes)
    {
        Power = power;
        Toughness = toughness;
        Keywords = keywords;
        Subtypes = subtypes;
    }

    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var effect = new ContinuousEffect(
            context.Source.Id,
            ContinuousEffectType.BecomeCreature,
            (card, _) => card.Id == context.Source.Id,
            PowerMod: Power,
            ToughnessMod: Toughness,
            UntilEndOfTurn: true);
        context.State.ActiveEffects.Add(effect);

        if (Keywords != null)
        {
            foreach (var keyword in Keywords)
            {
                var kwEffect = new ContinuousEffect(
                    context.Source.Id,
                    ContinuousEffectType.GrantKeyword,
                    (card, _) => card.Id == context.Source.Id,
                    GrantedKeyword: keyword,
                    UntilEndOfTurn: true,
                    Layer: EffectLayer.Layer6_AbilityAddRemove);
                context.State.ActiveEffects.Add(kwEffect);
            }
        }

        context.State.Log($"{context.Source.Name} becomes a {Power}/{Toughness} creature until end of turn.");
        return Task.CompletedTask;
    }
}
