using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Tamiyo, Seasoned Scholar +2 ability:
/// Until your next turn, creatures opponents control get -1/-0.
/// Simplified from the full MTG text which only targets attacking creatures.
/// </summary>
public class TamiyoDefenseEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var state = context.State;
        var controllerId = context.Controller.Id;

        var effect = new ContinuousEffect(
            context.Source.Id,
            ContinuousEffectType.ModifyPowerToughness,
            (card, player) => card.IsCreature && player.Id != controllerId,
            PowerMod: -1,
            ExpiresOnTurnNumber: state.TurnNumber + 2,
            Layer: EffectLayer.Layer7c_ModifyPT);
        state.ActiveEffects.Add(effect);
        state.Log("Until your next turn, creatures opponents control get -1/-0.");

        return Task.CompletedTask;
    }
}
