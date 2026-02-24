using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Sacred Ground (simplified): Whenever a land you control is put into your graveyard
/// from the battlefield, return that card to the battlefield.
/// The trigger fires with the land as Source in the EffectContext.
/// </summary>
public class SacredGroundEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        // The triggering land should be in the controller's graveyard
        // We look for the most recently added land in the graveyard
        var land = context.Controller.Graveyard.Cards
            .LastOrDefault(c => c.IsLand);

        if (land == null)
        {
            context.State.Log($"{context.Source.Name}: no land found in graveyard to return.");
            return Task.CompletedTask;
        }

        context.Controller.Graveyard.RemoveById(land.Id);
        context.Controller.Battlefield.Add(land);
        context.State.Log($"{context.Source.Name} returns {land.Name} to the battlefield.");

        return Task.CompletedTask;
    }
}
