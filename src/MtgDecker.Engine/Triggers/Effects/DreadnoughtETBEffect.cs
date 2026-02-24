namespace MtgDecker.Engine.Triggers.Effects;

public class DreadnoughtETBEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var player = context.Controller;
        var dreadnought = context.Source;

        // Not on battlefield anymore — fizzled
        if (!player.Battlefield.Contains(dreadnought.Id)) return;

        var otherCreatures = player.Battlefield.Cards
            .Where(c => c.IsCreature && c.Id != dreadnought.Id)
            .ToList();

        var totalAvailablePower = otherCreatures.Sum(c => c.Power ?? 0);

        if (totalAvailablePower < 12)
        {
            // Cannot meet the requirement — sacrifice Dreadnought
            if (context.FireLeaveBattlefieldTriggers != null)
                await context.FireLeaveBattlefieldTriggers(dreadnought);
            player.Battlefield.RemoveById(dreadnought.Id);
            player.Graveyard.Add(dreadnought);
            context.State.Log("Phyrexian Dreadnought is sacrificed (not enough creature power).");
            return;
        }

        // Let player choose creatures to sacrifice until total power >= 12
        var sacrificed = new List<GameCard>();
        var sacrificedPower = 0;

        while (sacrificedPower < 12)
        {
            var remaining = player.Battlefield.Cards
                .Where(c => c.IsCreature && c.Id != dreadnought.Id && !sacrificed.Contains(c))
                .ToList();

            if (remaining.Count == 0) break;

            var chosenId = await context.DecisionHandler.ChooseCard(
                remaining, $"Sacrifice creatures (total power {sacrificedPower}/12)", optional: true, ct);

            if (!chosenId.HasValue)
            {
                // Player declined — sacrifice Dreadnought instead
                if (context.FireLeaveBattlefieldTriggers != null)
                    await context.FireLeaveBattlefieldTriggers(dreadnought);
                player.Battlefield.RemoveById(dreadnought.Id);
                player.Graveyard.Add(dreadnought);
                context.State.Log("Phyrexian Dreadnought is sacrificed (player declined to sacrifice creatures).");
                return;
            }

            var chosen = remaining.FirstOrDefault(c => c.Id == chosenId.Value);
            if (chosen != null)
            {
                sacrificed.Add(chosen);
                sacrificedPower += chosen.Power ?? 0;
            }
        }

        // Sacrifice the chosen creatures
        foreach (var creature in sacrificed)
        {
            if (context.FireLeaveBattlefieldTriggers != null)
                await context.FireLeaveBattlefieldTriggers(creature);
            player.Battlefield.RemoveById(creature.Id);
            player.Graveyard.Add(creature);
            if (creature.IsToken)
                player.Graveyard.RemoveById(creature.Id);
            context.State.Log($"{creature.Name} is sacrificed for Phyrexian Dreadnought.");
        }
    }
}
