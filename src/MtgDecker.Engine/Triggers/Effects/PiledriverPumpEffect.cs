namespace MtgDecker.Engine.Triggers.Effects;

public class PiledriverPumpEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        if (context.State.Combat == null) return Task.CompletedTask;

        var otherAttackingGoblins = context.State.Combat.Attackers
            .Where(id => id != context.Source.Id)
            .Count(id =>
            {
                var card = context.Controller.Battlefield.Cards.FirstOrDefault(c => c.Id == id);
                return card != null && card.Subtypes.Contains("Goblin");
            });

        if (otherAttackingGoblins > 0)
        {
            var pump = otherAttackingGoblins * 2;
            var effect = new ContinuousEffect(
                context.Source.Id,
                ContinuousEffectType.ModifyPowerToughness,
                (card, _) => card.Id == context.Source.Id,
                PowerMod: pump,
                ToughnessMod: 0,
                UntilEndOfTurn: true);
            context.State.ActiveEffects.Add(effect);
            context.State.Log($"{context.Source.Name} gets +{pump}/+0 ({otherAttackingGoblins} other attacking Goblins).");
        }

        return Task.CompletedTask;
    }
}
