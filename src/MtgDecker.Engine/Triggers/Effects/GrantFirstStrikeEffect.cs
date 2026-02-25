namespace MtgDecker.Engine.Triggers.Effects;

public class GrantFirstStrikeEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        if (context.Target == null) return Task.CompletedTask;

        var targetId = context.Target.Id;
        var effect = new ContinuousEffect(
            context.Source.Id,
            ContinuousEffectType.GrantKeyword,
            (card, _) => card.Id == targetId,
            GrantedKeyword: Enums.Keyword.FirstStrike,
            UntilEndOfTurn: true);
        context.State.ActiveEffects.Add(effect);
        context.State.Log($"{context.Target.Name} gains first strike until end of turn.");
        return Task.CompletedTask;
    }
}
