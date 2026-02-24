namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Gives +P/+T until end of turn to all other creatures the controller controls.
/// Used by Soltari Champion's attack trigger.
/// </summary>
public class PumpAllOtherCreaturesEffect(int powerMod, int toughnessMod) : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var sourceId = context.Source.Id;
        var controllerId = context.Controller.Id;

        var otherCreatures = context.Controller.Battlefield.Cards
            .Where(c => c.IsCreature && c.Id != sourceId)
            .ToList();

        foreach (var creature in otherCreatures)
        {
            var creatureId = creature.Id;
            var effect = new ContinuousEffect(
                sourceId,
                ContinuousEffectType.ModifyPowerToughness,
                (card, _) => card.Id == creatureId,
                PowerMod: powerMod,
                ToughnessMod: toughnessMod,
                UntilEndOfTurn: true);
            context.State.ActiveEffects.Add(effect);
        }

        if (otherCreatures.Count > 0)
        {
            context.State.Log($"{context.Source.Name} gives other creatures +{powerMod}/+{toughnessMod} until end of turn ({otherCreatures.Count} creatures).");
        }

        return Task.CompletedTask;
    }
}
