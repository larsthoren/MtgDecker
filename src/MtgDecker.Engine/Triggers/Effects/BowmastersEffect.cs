using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Orcish Bowmasters' combined effect: amass Orcs 1, then deal 1 damage to any target.
/// Target selection happens during resolution via ChooseTarget (board targeting UI).
/// </summary>
public class BowmastersEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        // Step 1: Amass Orcs 1
        var amass = new AmassEffect("Orc", 1);
        await amass.Execute(context, ct);

        // Step 2: Deal 1 damage to any target (creature or player)
        var opponent = context.State.GetOpponent(context.Controller);

        // Build eligible targets: all creatures (excluding shroud / opponent hexproof) + both players
        var eligibleTargets = context.State.Player1.Battlefield.Cards
            .Concat(context.State.Player2.Battlefield.Cards)
            .Where(c => c.IsCreature
                && !c.ActiveKeywords.Contains(Keyword.Shroud)
                && !(c.ActiveKeywords.Contains(Keyword.Hexproof)
                    && !context.Controller.Battlefield.Contains(c.Id)))
            .ToList();

        // Add player sentinel cards so the UI player info bars are clickable
        eligibleTargets.Add(new GameCard { Id = Guid.Empty, Name = context.Controller.Name });
        eligibleTargets.Add(new GameCard { Id = Guid.Empty, Name = opponent.Name });

        // Mandatory targeting — no optional parameter
        var target = await context.DecisionHandler.ChooseTarget(
            "Orcish Bowmasters", eligibleTargets, opponent.Id, ct);

        if (target != null && target.CardId != Guid.Empty)
        {
            // Creature target
            var creature = context.State.Player1.Battlefield.Cards
                .Concat(context.State.Player2.Battlefield.Cards)
                .FirstOrDefault(c => c.Id == target.CardId);

            if (creature != null)
            {
                creature.DamageMarked += 1;
                context.State.Log($"{context.Source.Name} deals 1 damage to {creature.Name}.");
            }
            else
            {
                // Creature no longer on battlefield — fall back to opponent
                DealDamageToPlayer(context, opponent);
            }
        }
        else if (target != null && target.CardId == Guid.Empty && target.Zone == ZoneType.None)
        {
            // Player target
            var targetPlayer = context.State.GetPlayer(target.PlayerId);
            DealDamageToPlayer(context, targetPlayer);
        }
        else
        {
            // Defensive fallback (shouldn't happen since targeting is mandatory)
            DealDamageToPlayer(context, opponent);
        }
    }

    private static void DealDamageToPlayer(EffectContext context, Player player)
    {
        var hasDamageProtection = context.State.ActiveEffects.Any(e =>
            e.Type == ContinuousEffectType.PreventDamageToPlayer
            && (context.State.Player1.Battlefield.Contains(e.SourceId)
                ? context.State.Player1 : context.State.Player2).Id == player.Id);

        // Check for color-specific shield (Circle of Protection)
        var colorShield = player.DamagePreventionShields
            .FirstOrDefault(s => context.Source.Colors.Contains(s.Color));

        if (hasDamageProtection)
        {
            context.State.Log($"Damage to {player.Name} is prevented (protection).");
        }
        else if (colorShield != null)
        {
            player.DamagePreventionShields.Remove(colorShield);
            context.State.Log($"Damage to {player.Name} is prevented (Circle of Protection).");
        }
        else
        {
            player.AdjustLife(-1);
            context.State.Log($"{context.Source.Name} deals 1 damage to {player.Name}. ({player.Life} life)");
        }
    }
}
