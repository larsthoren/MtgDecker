using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Effects;

public class ReplenishEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        var controller = spell.ControllerId == state.Player1.Id ? state.Player1 : state.Player2;

        var enchantments = controller.Graveyard.Cards
            .Where(c => c.CardTypes.HasFlag(CardType.Enchantment))
            .ToList();

        foreach (var card in enchantments)
        {
            controller.Graveyard.RemoveById(card.Id);

            // Auras without valid targets stay in graveyard
            if (card.Subtypes.Contains("Aura"))
            {
                controller.Graveyard.Add(card);
                state.Log($"{card.Name} stays in graveyard (no valid target for aura).");
                continue;
            }

            controller.Battlefield.Add(card);
            card.TurnEnteredBattlefield = state.TurnNumber;
            state.Log($"{card.Name} returns to the battlefield.");
        }
    }
}
