using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Actions;

internal class ActivateLoyaltyAbilityHandler : IActionHandler
{
    public Task ExecuteAsync(GameAction action, GameEngine engine, GameState state, CancellationToken ct)
    {
        var player = state.GetPlayer(action.PlayerId);
        var pwCard = player.Battlefield.Cards.FirstOrDefault(c => c.Id == action.CardId);
        if (pwCard == null || !pwCard.IsPlaneswalker) return Task.CompletedTask;

        if (!engine.CanCastSorcery(player.Id))
        {
            state.Log($"Cannot activate {pwCard.Name} — not at sorcery speed.");
            return Task.CompletedTask;
        }

        if (player.PlaneswalkerAbilitiesUsedThisTurn.Contains(pwCard.Id))
        {
            state.Log($"{pwCard.Name} has already activated a loyalty ability this turn.");
            return Task.CompletedTask;
        }

        CardDefinition? pwDef = null;
        if (pwCard.IsTransformed && pwCard.BackFaceDefinition?.LoyaltyAbilities != null)
            pwDef = pwCard.BackFaceDefinition;
        else if (CardDefinitions.TryGet(pwCard.Name, out var registeredDef))
            pwDef = registeredDef;

        if (pwDef?.LoyaltyAbilities == null)
        {
            state.Log($"{pwCard.Name} has no loyalty abilities.");
            return Task.CompletedTask;
        }

        var abilityIdx = action.AbilityIndex ?? -1;
        if (abilityIdx < 0 || abilityIdx >= pwDef.LoyaltyAbilities.Count)
        {
            state.Log($"Invalid loyalty ability index for {pwCard.Name}.");
            return Task.CompletedTask;
        }

        var loyaltyAbility = pwDef.LoyaltyAbilities[abilityIdx];

        if (loyaltyAbility.LoyaltyCost < 0 && pwCard.Loyalty + loyaltyAbility.LoyaltyCost < 0)
        {
            state.Log($"Not enough loyalty to activate {pwCard.Name} ({loyaltyAbility.Description}).");
            return Task.CompletedTask;
        }

        if (loyaltyAbility.LoyaltyCost > 0)
            pwCard.AddCounters(CounterType.Loyalty, loyaltyAbility.LoyaltyCost);
        else if (loyaltyAbility.LoyaltyCost < 0)
            for (int i = 0; i < -loyaltyAbility.LoyaltyCost; i++)
                pwCard.RemoveCounter(CounterType.Loyalty);

        player.PlaneswalkerAbilitiesUsedThisTurn.Add(pwCard.Id);

        var opponent = state.GetOpponent(player);
        var loyaltyStackObj = new ActivatedLoyaltyAbilityStackObject(
            pwCard, player.Id, loyaltyAbility.Effect, loyaltyAbility.Description)
        {
            TargetPlayerId = opponent.Id,
        };
        state.StackPush(loyaltyStackObj);

        state.Log($"{player.Name} activates {pwCard.Name}: {loyaltyAbility.Description}");
        return Task.CompletedTask;
    }
}
