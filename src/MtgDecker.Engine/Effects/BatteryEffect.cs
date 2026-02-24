using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Effects;

public class BatteryEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        var controller = state.GetPlayer(spell.ControllerId);
        var token = new GameCard
        {
            Name = "Elephant",
            Power = 3,
            Toughness = 3,
            CardTypes = CardType.Creature,
            Subtypes = ["Elephant"],
            IsToken = true,
            TurnEnteredBattlefield = state.TurnNumber,
            Colors = { ManaColor.Green },
        };
        controller.Battlefield.Add(token);
        state.Log($"{controller.Name} creates a 3/3 Elephant token.");
    }
}
