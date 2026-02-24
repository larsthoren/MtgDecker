namespace MtgDecker.Engine.Effects;

public class CrumbleEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        if (spell.Targets.Count == 0) return;
        var target = spell.Targets[0];
        var owner = state.GetPlayer(target.PlayerId);
        var artifact = owner.Battlefield.RemoveById(target.CardId);
        if (artifact == null) return;

        artifact.RegenerationShields = 0; // Can't be regenerated

        var manaValue = artifact.ManaCost?.ConvertedManaCost ?? 0;
        owner.Graveyard.Add(artifact);
        owner.AdjustLife(manaValue);
        state.Log($"{artifact.Name} is destroyed by {spell.Card.Name}. {owner.Name} gains {manaValue} life ({owner.Life}).");
    }
}
