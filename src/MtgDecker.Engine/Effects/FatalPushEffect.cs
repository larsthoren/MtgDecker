namespace MtgDecker.Engine.Effects;

/// <summary>
/// Fatal Push — Destroy target creature if it has converted mana cost 2 or less.
/// Revolt — If a permanent you controlled left the battlefield this turn,
/// destroy that creature if it has converted mana cost 4 or less instead.
/// </summary>
public class FatalPushEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        if (spell.Targets.Count == 0) return;
        var target = spell.Targets[0];
        var owner = state.GetPlayer(target.PlayerId);
        var creature = owner.Battlefield.Cards.FirstOrDefault(c => c.Id == target.CardId);
        if (creature == null) return;

        // Calculate CMC of the creature
        var caster = state.GetPlayer(spell.ControllerId);
        bool revolt = caster.PermanentLeftBattlefieldThisTurn;
        int maxCmc = revolt ? 4 : 2;

        // Get CMC from CardDefinitions registry
        int cmc = 0;
        if (CardDefinitions.TryGet(creature.FrontName, out var def) && def.ManaCost != null)
            cmc = def.ManaCost.ConvertedManaCost;

        if (cmc > maxCmc)
        {
            state.Log($"Fatal Push fizzles — {creature.Name} has CMC {cmc} (max {maxCmc}).");
            return;
        }

        owner.Battlefield.RemoveById(creature.Id);
        if (!creature.IsToken)
            owner.Graveyard.Add(creature);
        state.Log($"Fatal Push destroys {creature.Name}." + (revolt ? " (Revolt)" : ""));
    }
}
