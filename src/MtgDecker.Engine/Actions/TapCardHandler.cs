using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Actions;

internal class TapCardHandler : IActionHandler
{
    public async Task ExecuteAsync(GameAction action, GameEngine engine, GameState state, CancellationToken ct)
    {
        var player = state.GetPlayer(action.PlayerId);
        var tapTarget = player.Battlefield.Cards.FirstOrDefault(c => c.Id == action.CardId);
        if (tapTarget == null || tapTarget.IsTapped) return;

        // Summoning sickness: creatures that entered this turn can't be tapped (lands/artifacts exempt)
        if (tapTarget.IsCreature && tapTarget.HasSummoningSickness(state.TurnNumber))
        {
            state.Log($"{tapTarget.Name} has summoning sickness.");
            return;
        }

        // Check if activated abilities are prevented (e.g. Null Rod for artifacts, Cursed Totem for creatures)
        // Per MTG rules, mana abilities are activated abilities — Null Rod prevents artifact mana abilities
        if (tapTarget.ManaAbility != null && state.ActiveEffects.Any(e =>
            e.Type == ContinuousEffectType.PreventActivatedAbilities
            && e.Applies(tapTarget, player)))
        {
            state.Log($"{tapTarget.Name}'s activated abilities can't be activated.");
            return;
        }

        tapTarget.IsTapped = true;
        player.ActionHistory.Push(action);

        // Suppression: creatures that lost abilities can't produce mana
        if (tapTarget.IsCreature && tapTarget.AbilitiesRemoved)
        {
            state.Log($"{tapTarget.Name} has lost its abilities — no mana produced.");
            return;
        }

        if (tapTarget.ManaAbility != null)
        {
            action.IsManaAbility = true;
            var ability = tapTarget.ManaAbility;
            if (ability.Type == ManaAbilityType.Fixed)
            {
                var count = ability.ProduceCount;
                player.ManaPool.Add(ability.FixedColor!.Value, count);
                action.ManaProduced = ability.FixedColor!.Value;
                action.ManaProducedAmount = count;
                if (ability.SelfDamage > 0)
                {
                    player.AdjustLife(-ability.SelfDamage);
                    state.Log($"{tapTarget.Name} deals {ability.SelfDamage} damage to {player.Name}.");
                }
                var manaDesc = count > 1 ? $"{count} {ability.FixedColor}" : $"{ability.FixedColor}";
                state.Log($"{player.Name} taps {tapTarget.Name} for {manaDesc}.");
            }
            else if (ability.Type == ManaAbilityType.Choice)
            {
                var chosen = await player.DecisionHandler.ChooseManaColor(
                    ability.ChoiceColors!, ct);
                player.ManaPool.Add(chosen);
                action.ManaProduced = chosen;
                // Painland: deal 1 damage for colored mana choices
                if (ability.PainColors != null && ability.PainColors.Contains(chosen))
                {
                    player.AdjustLife(-1);
                    action.PainDamageDealt = true;
                    state.Log($"{tapTarget.Name} deals 1 damage to {player.Name}.");
                }
                state.Log($"{player.Name} taps {tapTarget.Name} for {chosen}.");
            }
            else if (ability.Type == ManaAbilityType.Dynamic)
            {
                var amount = ability.CountFunc!(player);
                if (amount > 0)
                {
                    player.ManaPool.Add(ability.DynamicColor!.Value, amount);
                    action.ManaProduced = ability.DynamicColor!.Value;
                    action.ManaProducedAmount = amount;
                    state.Log($"{player.Name} taps {tapTarget.Name} for {amount} {ability.DynamicColor}.");
                }
                else
                {
                    state.Log($"{player.Name} taps {tapTarget.Name} (produces no mana).");
                }
            }
            else if (ability.Type == ManaAbilityType.Filter)
            {
                // Filter lands require paying an activation cost to produce multiple colors
                if (ability.ActivationCost != null && !player.ManaPool.CanPay(ability.ActivationCost))
                {
                    // Can't pay — reject the tap
                    tapTarget.IsTapped = false;
                    player.ActionHistory.Pop();
                    state.Log($"{player.Name} cannot pay {ability.ActivationCost} to activate {tapTarget.Name}.");
                    return;
                }

                if (ability.ActivationCost != null)
                {
                    // Snapshot pool before paying to record what was actually spent (for undo)
                    var poolBefore = player.ManaPool.Available.ToDictionary(kv => kv.Key, kv => kv.Value);
                    player.ManaPool.Pay(ability.ActivationCost);
                    var poolAfter = player.ManaPool.Available;
                    action.ActualManaPaid = new Dictionary<ManaColor, int>();
                    foreach (var (color, before) in poolBefore)
                    {
                        var after = poolAfter.GetValueOrDefault(color, 0);
                        if (before > after)
                            action.ActualManaPaid[color] = before - after;
                    }
                }

                // Produce all colors — use BonusManaProduced for undo tracking
                action.BonusManaProduced = ability.ProducedColors!.ToList();
                foreach (var color in ability.ProducedColors!)
                    player.ManaPool.Add(color);

                state.Log($"{player.Name} taps {tapTarget.Name} (pays {ability.ActivationCost}) for {string.Join(", ", ability.ProducedColors)}.");
            }
        }
        else
        {
            state.Log($"{player.Name} taps {tapTarget.Name}.");
        }

        // Depletion counter: remove a counter when tapped for mana, sacrifice if empty
        if (tapTarget.ManaAbility?.RemovesCounterOnTap is { } depletionType)
        {
            tapTarget.RemoveCounter(depletionType);
            if (tapTarget.GetCounters(depletionType) <= 0)
            {
                player.Battlefield.RemoveById(tapTarget.Id);
                player.Graveyard.Add(tapTarget);
                state.Log($"{tapTarget.Name} is sacrificed (no {depletionType} counters remaining).");
            }
        }

        // Fire mana triggers from auras attached to this permanent (immediate — mana abilities don't use stack)
        foreach (var aura in player.Battlefield.Cards.Where(c => c.AttachedTo == tapTarget.Id).ToList())
        {
            var auraTriggers = aura.EffectiveTriggers;

            foreach (var trigger in auraTriggers)
            {
                if (trigger.Condition == TriggerCondition.AttachedPermanentTapped)
                {
                    // Track bonus mana for undo
                    if (trigger.Effect is AddBonusManaEffect bonusMana)
                    {
                        action.BonusManaProduced ??= [];
                        action.BonusManaProduced.Add(bonusMana.Color);
                    }

                    var ctx = new EffectContext(state, player, aura, player.DecisionHandler)
                    {
                        FireLeaveBattlefieldTriggers = async card =>
                        {
                            var ctrl = state.GetCardController(card.Id);
                            if (ctrl != null) await engine.FireLeaveBattlefieldTriggersAsync(card, ctrl, ct);
                        },
                    };
                    await trigger.Effect.Execute(ctx);
                }
            }
        }

        // Track pending tap for scoped undo
        player.PendingManaTaps.Add(tapTarget.Id);
    }
}
