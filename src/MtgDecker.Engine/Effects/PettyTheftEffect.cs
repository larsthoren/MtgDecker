namespace MtgDecker.Engine.Effects;

/// <summary>
/// Petty Theft (Brazen Borrower adventure) — Return target nonland permanent
/// an opponent controls to its owner's hand.
/// </summary>
public class PettyTheftEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        if (spell.Targets.Count == 0) return;
        var target = spell.Targets[0];
        var owner = state.GetPlayer(target.PlayerId);
        var permanent = owner.Battlefield.Cards.FirstOrDefault(c => c.Id == target.CardId);
        if (permanent == null) return;

        // Cannot bounce lands
        if (permanent.IsLand)
        {
            state.Log($"{permanent.Name} is a land and cannot be targeted by Petty Theft.");
            return;
        }

        // Cannot bounce own permanents (must be opponent's)
        if (target.PlayerId == spell.ControllerId)
        {
            state.Log($"Petty Theft can only target permanents an opponent controls.");
            return;
        }

        owner.Battlefield.RemoveById(target.CardId);
        owner.Hand.Add(permanent);
        state.Log($"{permanent.Name} is returned to {owner.Name}'s hand.");
    }
}
