using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Actions;

internal class NinjutsuHandler : IActionHandler
{
    public async Task ExecuteAsync(GameAction action, GameEngine engine, GameState state, CancellationToken ct)
    {
        var ninjutsuPlayer = state.GetPlayer(action.PlayerId);
        var ninjaCard = ninjutsuPlayer.Hand.Cards.FirstOrDefault(c => c.Id == action.CardId);
        if (ninjaCard == null)
        {
            state.Log("Ninjutsu card not found in hand.");
            return;
        }

        if (!CardDefinitions.TryGet(ninjaCard.Name, out var ninjutsuDef) || ninjutsuDef.NinjutsuCost == null)
        {
            state.Log($"{ninjaCard.Name} does not have ninjutsu.");
            return;
        }

        if (state.CurrentPhase != Phase.Combat || state.Combat == null
            || state.CombatStep < CombatStep.DeclareBlockers)
        {
            state.Log("Ninjutsu can only be activated during combat after blockers are declared.");
            return;
        }

        var returnCreatureId = action.ReturnCardId;
        if (!returnCreatureId.HasValue)
        {
            state.Log("No creature specified to return.");
            return;
        }

        var returnCreature = ninjutsuPlayer.Battlefield.Cards.FirstOrDefault(c => c.Id == returnCreatureId.Value);
        if (returnCreature == null)
        {
            state.Log("Return creature not found on battlefield.");
            return;
        }

        if (!state.Combat.Attackers.Contains(returnCreature.Id))
        {
            state.Log($"{returnCreature.Name} is not attacking.");
            return;
        }

        if (state.Combat.IsBlocked(returnCreature.Id))
        {
            state.Log($"{returnCreature.Name} is blocked — cannot use ninjutsu.");
            return;
        }

        var ninjutsuCost = ninjutsuDef.NinjutsuCost;
        if (!ninjutsuPlayer.ManaPool.CanPay(ninjutsuCost))
        {
            state.Log($"Not enough mana to activate ninjutsu for {ninjaCard.Name}.");
            return;
        }

        await engine.PayManaCostAsync(ninjutsuCost, ninjutsuPlayer, ct);
        ninjutsuPlayer.PendingManaTaps.Clear();

        ninjutsuPlayer.Battlefield.RemoveById(returnCreature.Id);
        returnCreature.IsTapped = false;
        returnCreature.DamageMarked = 0;
        ninjutsuPlayer.Hand.Add(returnCreature);
        state.Combat.RemoveAttacker(returnCreature.Id);

        ninjutsuPlayer.Hand.RemoveById(ninjaCard.Id);
        ninjaCard.IsTapped = true;
        ninjaCard.TurnEnteredBattlefield = state.TurnNumber;
        ninjutsuPlayer.Battlefield.Add(ninjaCard);

        engine.ApplyEntersWithCounters(ninjaCard);

        state.Combat.DeclareAttacker(ninjaCard.Id);

        await engine.QueueSelfTriggersOnStackAsync(GameEvent.EnterBattlefield, ninjaCard, ninjutsuPlayer, ct);
        await engine.OnBoardChangedAsync(ct);

        state.Log($"{ninjutsuPlayer.Name} activates ninjutsu: {returnCreature.Name} returns to hand, {ninjaCard.Name} enters attacking.");
    }
}
