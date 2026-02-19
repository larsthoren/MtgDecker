using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class SneakAttackPutEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var creatures = context.Controller.Hand.Cards
            .Where(c => c.IsCreature)
            .ToList();

        if (creatures.Count == 0)
        {
            context.State.Log("No creature cards in hand.");
            return;
        }

        var chosenId = await context.DecisionHandler.ChooseCard(
            creatures, "Put a creature card onto the battlefield", optional: true, ct);

        if (!chosenId.HasValue) return;

        var chosen = context.Controller.Hand.RemoveById(chosenId.Value);
        if (chosen == null) return;

        context.Controller.Battlefield.Add(chosen);
        chosen.TurnEnteredBattlefield = context.State.TurnNumber;
        context.State.Log($"{context.Controller.Name} puts {chosen.Name} onto the battlefield with Sneak Attack.");

        // Grant haste via continuous effect
        context.State.ActiveEffects.Add(new ContinuousEffect(
            chosen.Id,
            ContinuousEffectType.GrantKeyword,
            (card, _) => card.Id == chosen.Id,
            GrantedKeyword: Keyword.Haste,
            Layer: EffectLayer.Layer6_AbilityAddRemove));

        // Register end-of-turn sacrifice
        context.State.DelayedTriggers.Add(new DelayedTrigger(
            GameEvent.EndStep,
            new SacrificeSpecificCardEffect(chosen.Id),
            context.Controller.Id));
    }
}
