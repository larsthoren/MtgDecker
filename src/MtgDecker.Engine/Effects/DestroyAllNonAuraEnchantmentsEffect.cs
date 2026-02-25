using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Effects;

public class DestroyAllNonAuraEnchantmentsEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        foreach (var player in new[] { state.Player1, state.Player2 })
        {
            var enchantments = player.Battlefield.Cards
                .Where(c => c.CardTypes.HasFlag(CardType.Enchantment)
                    && !c.Subtypes.Contains("Aura"))
                .ToList();

            foreach (var enchantment in enchantments)
            {
                player.Battlefield.Remove(enchantment);
                player.Graveyard.Add(enchantment);
            }
        }
        state.Log($"{spell.Card.Name} destroys all non-Aura enchantments.");
    }
}
