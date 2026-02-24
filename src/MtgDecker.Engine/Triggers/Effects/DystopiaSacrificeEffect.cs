using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Triggers.Effects;

public class DystopiaSacrificeEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        // This fires at each player's upkeep — the active player must sacrifice
        var activePlayer = context.State.ActivePlayer;

        // Find green or white permanents the active player controls
        var eligible = activePlayer.Battlefield.Cards
            .Where(IsGreenOrWhite)
            .ToList();

        if (eligible.Count == 0)
        {
            context.State.Log($"{activePlayer.Name} has no green or white permanents to sacrifice.");
            return;
        }

        var chosenId = await context.State.ActivePlayer.DecisionHandler.ChooseCard(
            eligible, "Sacrifice a green or white permanent (Dystopia)", optional: false, ct);

        if (chosenId.HasValue)
        {
            var chosen = eligible.FirstOrDefault(c => c.Id == chosenId.Value);
            if (chosen != null)
            {
                if (context.FireLeaveBattlefieldTriggers != null)
                    await context.FireLeaveBattlefieldTriggers(chosen);
                activePlayer.Battlefield.RemoveById(chosen.Id);
                activePlayer.Graveyard.Add(chosen);
                if (chosen.IsToken)
                    activePlayer.Graveyard.RemoveById(chosen.Id);
                context.State.Log($"{activePlayer.Name} sacrifices {chosen.Name} to Dystopia.");
            }
        }
        else
        {
            // Must sacrifice — pick first
            var first = eligible[0];
            if (context.FireLeaveBattlefieldTriggers != null)
                await context.FireLeaveBattlefieldTriggers(first);
            activePlayer.Battlefield.RemoveById(first.Id);
            activePlayer.Graveyard.Add(first);
            if (first.IsToken)
                activePlayer.Graveyard.RemoveById(first.Id);
            context.State.Log($"{activePlayer.Name} sacrifices {first.Name} to Dystopia.");
        }
    }

    private static bool IsGreenOrWhite(GameCard card)
    {
        if (card.ManaCost == null) return false;
        return card.ManaCost.ColorRequirements.ContainsKey(ManaColor.Green)
            || card.ManaCost.ColorRequirements.ContainsKey(ManaColor.White);
    }
}
