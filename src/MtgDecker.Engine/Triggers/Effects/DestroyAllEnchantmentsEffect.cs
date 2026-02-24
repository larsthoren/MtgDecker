using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class DestroyAllEnchantmentsEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var state = context.State;

        foreach (var player in new[] { state.Player1, state.Player2 })
        {
            var enchantments = player.Battlefield.Cards
                .Where(c => c.CardTypes.HasFlag(CardType.Enchantment))
                .ToList();

            foreach (var enchantment in enchantments)
            {
                if (context.FireLeaveBattlefieldTriggers != null)
                    await context.FireLeaveBattlefieldTriggers(enchantment);
                player.Battlefield.RemoveById(enchantment.Id);
                if (!enchantment.IsToken)
                    player.Graveyard.Add(enchantment);
                state.Log($"{enchantment.Name} is destroyed.");
            }
        }
    }
}
