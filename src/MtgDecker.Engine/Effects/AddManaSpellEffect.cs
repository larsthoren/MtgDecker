using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Effects;

public class AddManaSpellEffect : SpellEffect
{
    public ManaColor Color { get; }
    public int Amount { get; }

    public AddManaSpellEffect(ManaColor color, int amount)
    {
        Color = color;
        Amount = amount;
    }

    public override void Resolve(GameState state, StackObject spell)
    {
        var caster = state.GetPlayer(spell.ControllerId);
        for (int i = 0; i < Amount; i++)
            caster.ManaPool.Add(Color);
        state.Log($"{spell.Card.Name} adds {Amount} {Color} mana.");
    }
}
