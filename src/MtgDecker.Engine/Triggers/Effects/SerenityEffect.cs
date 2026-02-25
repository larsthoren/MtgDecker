using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Serenity: At the beginning of your upkeep, destroy all artifacts and enchantments.
/// They can't be regenerated.
/// </summary>
public class SerenityEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var state = context.State;

        foreach (var player in new[] { state.Player1, state.Player2 })
        {
            var targets = player.Battlefield.Cards
                .Where(c => c.CardTypes.HasFlag(CardType.Artifact) || c.CardTypes.HasFlag(CardType.Enchantment))
                .ToList();

            foreach (var card in targets)
            {
                if (context.FireLeaveBattlefieldTriggers != null)
                    await context.FireLeaveBattlefieldTriggers(card);
                player.Battlefield.RemoveById(card.Id);
                if (!card.IsToken)
                    player.Graveyard.Add(card);
                state.Log($"{card.Name} is destroyed by Serenity.");
            }
        }
    }
}
