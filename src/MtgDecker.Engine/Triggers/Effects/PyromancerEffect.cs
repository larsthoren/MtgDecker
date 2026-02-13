using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class PyromancerEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var pump = new ContinuousEffect(
            context.Source.Id,
            ContinuousEffectType.ModifyPowerToughness,
            (card, _) => card.IsCreature && card.Subtypes.Contains("Goblin"),
            PowerMod: 2,
            ToughnessMod: 0,
            UntilEndOfTurn: true);
        context.State.ActiveEffects.Add(pump);

        var mountainwalk = new ContinuousEffect(
            context.Source.Id,
            ContinuousEffectType.GrantKeyword,
            (card, _) => card.IsCreature && card.Subtypes.Contains("Goblin"),
            GrantedKeyword: Keyword.Mountainwalk,
            UntilEndOfTurn: true);
        context.State.ActiveEffects.Add(mountainwalk);

        context.State.Log($"All Goblins get +2/+0 and mountainwalk until end of turn.");

        var delayed = new DelayedTrigger(
            GameEvent.EndStep,
            new DestroyAllSubtypeEffect("Goblin"),
            context.Controller.Id);
        context.State.DelayedTriggers.Add(delayed);

        return Task.CompletedTask;
    }
}
