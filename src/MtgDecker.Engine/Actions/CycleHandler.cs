using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Actions;

internal class CycleHandler : IActionHandler
{
    public Task ExecuteAsync(GameAction action, GameEngine engine, GameState state, CancellationToken ct)
    {
        var player = state.GetPlayer(action.PlayerId);
        var cycleCard = player.Hand.Cards.FirstOrDefault(c => c.Id == action.CardId);
        if (cycleCard == null) return Task.CompletedTask;

        if (!CardDefinitions.TryGet(cycleCard.Name, out var cycleDef) || cycleDef.CyclingCost == null)
        {
            state.Log($"{cycleCard.Name} cannot be cycled.");
            return Task.CompletedTask;
        }

        var cyclingCost = cycleDef.CyclingCost;
        if (!player.ManaPool.CanPay(cyclingCost))
        {
            state.Log($"Cannot cycle {cycleCard.Name} — not enough mana.");
            return Task.CompletedTask;
        }

        player.ManaPool.Pay(cyclingCost);
        player.PendingManaTaps.Clear();

        player.Hand.RemoveById(cycleCard.Id);
        player.Graveyard.Add(cycleCard);

        engine.DrawCards(player, 1);
        state.Log($"{player.Name} cycles {cycleCard.Name}.");

        foreach (var trigger in cycleDef.CyclingTriggers)
        {
            state.Log($"{cycleCard.Name} triggers: {trigger.Effect.GetType().Name.Replace("Effect", "")}");
            state.StackPush(new TriggeredAbilityStackObject(cycleCard, player.Id, trigger.Effect));
        }

        player.ActionHistory.Push(action);
        return Task.CompletedTask;
    }
}
