using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class DystopiaUpkeepEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var player = context.Controller;
        var card = context.Source;

        // Card no longer on battlefield — fizzled
        if (!player.Battlefield.Contains(card.Id)) return;

        // Add an age counter
        card.AddCounters(CounterType.Age, 1);
        var ageCounters = card.GetCounters(CounterType.Age);
        context.State.Log($"Dystopia now has {ageCounters} age counter(s).");

        // Cumulative upkeep: pay 1 life per age counter
        var lifeCost = ageCounters;

        if (player.Life > lifeCost)
        {
            var choice = await context.DecisionHandler.ChooseCard(
                [card], $"Pay {lifeCost} life for Dystopia's cumulative upkeep?", optional: true, ct);

            if (choice.HasValue)
            {
                player.AdjustLife(-lifeCost);
                context.State.Log($"{player.Name} pays {lifeCost} life for Dystopia ({player.Life} life remaining).");
                return;
            }
        }

        // Sacrifice Dystopia
        if (context.FireLeaveBattlefieldTriggers != null)
            await context.FireLeaveBattlefieldTriggers(card);
        player.Battlefield.RemoveById(card.Id);
        player.Graveyard.Add(card);
        context.State.Log("Dystopia is sacrificed (cumulative upkeep not paid).");
    }
}
