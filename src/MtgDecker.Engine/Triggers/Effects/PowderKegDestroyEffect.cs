using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Powder Keg activated ability — Tap, Sacrifice Powder Keg: Destroy each artifact
/// and each creature with mana value equal to the number of fuse counters on Powder Keg.
/// </summary>
public class PowderKegDestroyEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var fuseCount = context.Source.GetCounters(CounterType.Fuse);

        foreach (var player in context.State.Players)
        {
            var toDestroy = player.Battlefield.Cards
                .Where(c => (c.IsCreature || c.CardTypes.HasFlag(CardType.Artifact))
                    && (c.ManaCost?.ConvertedManaCost ?? 0) == fuseCount)
                .ToList();

            foreach (var card in toDestroy)
            {
                player.Battlefield.RemoveById(card.Id);
                player.Graveyard.Add(card);
                context.State.Log($"Powder Keg destroys {card.Name} (CMC {fuseCount}).");
            }
        }
        return Task.CompletedTask;
    }
}
