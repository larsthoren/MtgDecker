using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Effects;

public class CleansingMeditationEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        var controller = state.GetPlayer(spell.ControllerId);

        // Check threshold BEFORE destroying (controller's graveyard >= 7)
        var hasThreshold = controller.Graveyard.Cards.Count >= 7;

        // Collect all enchantments on battlefield (both players)
        var p1Enchantments = state.Player1.Battlefield.Cards
            .Where(c => c.CardTypes.HasFlag(CardType.Enchantment))
            .ToList();
        var p2Enchantments = state.Player2.Battlefield.Cards
            .Where(c => c.CardTypes.HasFlag(CardType.Enchantment))
            .ToList();

        // Track controller's enchantments for threshold return
        var controllerEnchantmentIds = new HashSet<Guid>();
        if (hasThreshold)
        {
            var controllerEnchantments = controller == state.Player1 ? p1Enchantments : p2Enchantments;
            foreach (var ench in controllerEnchantments)
                controllerEnchantmentIds.Add(ench.Id);
        }

        // Destroy all enchantments (move to graveyard)
        foreach (var ench in p1Enchantments)
        {
            state.Player1.Battlefield.RemoveById(ench.Id);
            state.Player1.Graveyard.Add(ench);
            state.Log($"{ench.Name} is destroyed.");
        }
        foreach (var ench in p2Enchantments)
        {
            state.Player2.Battlefield.RemoveById(ench.Id);
            state.Player2.Graveyard.Add(ench);
            state.Log($"{ench.Name} is destroyed.");
        }

        if (hasThreshold)
        {
            state.Log("Threshold active — returning controller's enchantments.");

            // Return all enchantment cards from controller's graveyard to battlefield
            // (includes the ones just destroyed plus any already there)
            var enchantmentsInGraveyard = controller.Graveyard.Cards
                .Where(c => c.CardTypes.HasFlag(CardType.Enchantment))
                .ToList();

            foreach (var ench in enchantmentsInGraveyard)
            {
                // Skip auras — they need valid targets
                if (ench.Subtypes.Contains("Aura"))
                {
                    state.Log($"{ench.Name} stays in graveyard (aura needs a target).");
                    continue;
                }

                controller.Graveyard.RemoveById(ench.Id);
                controller.Battlefield.Add(ench);
                ench.TurnEnteredBattlefield = state.TurnNumber;
                state.Log($"{ench.Name} returns to the battlefield.");
            }
        }
    }
}
