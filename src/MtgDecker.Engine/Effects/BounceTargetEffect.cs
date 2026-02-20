namespace MtgDecker.Engine.Effects;

public class BounceTargetEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        if (spell.Targets.Count == 0) return;
        var target = spell.Targets[0];

        var owner = state.GetPlayer(target.PlayerId);
        var card = owner.Battlefield.Cards.FirstOrDefault(c => c.Id == target.CardId);
        if (card == null)
        {
            state.Log($"{spell.Card.Name} fizzles (target no longer on battlefield).");
            return;
        }

        owner.Battlefield.RemoveById(card.Id);
        owner.Hand.Add(card);
        card.IsTapped = false;
        state.Log($"{card.Name} is returned to {owner.Name}'s hand.");
    }
}
