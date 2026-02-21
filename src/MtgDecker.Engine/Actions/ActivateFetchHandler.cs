using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Actions;

internal class ActivateFetchHandler : IActionHandler
{
    public async Task ExecuteAsync(GameAction action, GameEngine engine, GameState state, CancellationToken ct)
    {
        var player = state.GetPlayer(action.PlayerId);
        var fetchLand = player.Battlefield.Cards.FirstOrDefault(c => c.Id == action.CardId);
        if (fetchLand == null || fetchLand.IsTapped) return;

        var fetchDef = CardDefinitions.TryGet(fetchLand.Name, out var fd) ? fd : null;
        var fetchAbility = fetchDef?.FetchAbility ?? fetchLand.FetchAbility;
        if (fetchAbility == null) return;

        player.AdjustLife(-1);
        await engine.FireLeaveBattlefieldTriggersAsync(fetchLand, player, ct);
        player.Battlefield.RemoveById(fetchLand.Id);
        player.Graveyard.Add(fetchLand);
        state.Log($"{player.Name} sacrifices {fetchLand.Name}, pays 1 life ({player.Life}).");

        var searchTypes = fetchAbility.SearchTypes;
        var eligible = player.Library.Cards
            .Where(c => c.IsLand && searchTypes.Any(t =>
                c.Subtypes.Contains(t) || c.Name.Equals(t, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (eligible.Count > 0)
        {
            var chosenId = await player.DecisionHandler.ChooseCard(
                eligible, $"Search for a land ({string.Join(" or ", searchTypes)})",
                optional: true, ct);

            if (chosenId != null)
            {
                var land = player.Library.RemoveById(chosenId.Value);
                if (land != null)
                {
                    player.Battlefield.Add(land);
                    land.TurnEnteredBattlefield = state.TurnNumber;
                    if (land.EntersTapped) land.IsTapped = true;
                    state.Log($"{player.Name} fetches {land.Name}.");
                    engine.ApplyEntersWithCounters(land);
                    await engine.QueueSelfTriggersOnStackAsync(GameEvent.EnterBattlefield, land, player, ct);
                    await engine.OnBoardChangedAsync(ct);
                }
            }
        }
        else
        {
            state.Log($"{player.Name} finds no matching land.");
        }

        player.Library.Shuffle();
        player.ActionHistory.Push(action);
    }
}
