using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Effects;

public class DamageNonflyingCreaturesAndPlayersEffect : SpellEffect
{
    public int Amount { get; }

    public DamageNonflyingCreaturesAndPlayersEffect(int amount) => Amount = amount;

    public override void Resolve(GameState state, StackObject spell)
    {
        foreach (var player in state.Players)
        {
            foreach (var creature in player.Battlefield.Cards.Where(c => c.IsCreature))
            {
                if (!creature.ActiveKeywords.Contains(Keyword.Flying))
                    creature.DamageMarked += Amount;
            }
            player.AdjustLife(-Amount);
        }
        state.Log($"{spell.Card.Name} deals {Amount} damage to each creature without flying and each player.");
    }
}
