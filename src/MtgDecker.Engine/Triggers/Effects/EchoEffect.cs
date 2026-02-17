using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Triggers.Effects;

public class EchoEffect(ManaCost echoCost) : IEffect
{
    public ManaCost EchoCost { get; } = echoCost;

    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var player = context.Controller;
        var card = context.Source;

        // Card no longer on battlefield — fizzled
        if (!player.Battlefield.Contains(card.Id)) return;

        // Already paid (e.g., re-triggered)
        if (card.EchoPaid) return;

        if (player.ManaPool.CanPay(EchoCost))
        {
            // Ask player if they want to pay
            var choice = await context.DecisionHandler.ChooseCard(
                [card], $"Pay echo {EchoCost} for {card.Name}?", optional: true, ct);

            if (choice.HasValue)
            {
                player.ManaPool.Pay(EchoCost);
                card.EchoPaid = true;
                context.State.Log($"{player.Name} pays echo {EchoCost} for {card.Name}.");
                return;
            }
        }

        // Sacrifice
        if (context.FireLeaveBattlefieldTriggers != null)
            await context.FireLeaveBattlefieldTriggers(card);
        player.Battlefield.RemoveById(card.Id);
        player.Graveyard.Add(card);
        if (card.IsToken)
            player.Graveyard.RemoveById(card.Id);
        context.State.Log($"{card.Name} is sacrificed (echo not paid).");
    }
}
