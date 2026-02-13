namespace MtgDecker.Engine.Triggers.Effects;

public class BecomeCreatureEffect : IEffect
{
    public int Power { get; }
    public int Toughness { get; }
    public string[] Subtypes { get; }

    public BecomeCreatureEffect(int power, int toughness, params string[] subtypes)
    {
        Power = power;
        Toughness = toughness;
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
        context.State.Log($"{context.Source.Name} becomes a {Power}/{Toughness} creature until end of turn.");
        return Task.CompletedTask;
    }
}
