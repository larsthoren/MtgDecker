# Engine Full-Play Design: Goblins vs Enchantress

**Goal:** Bring both starter decks to near-full playability by implementing 7 missing engine systems.

**Deferred cards:** Parallax Wave, Solitary Confinement, Opalescence (too many new subsystems for this pass).

**Architecture:** Extends existing engine systems — continuous effects, triggers, mana abilities, combat, stack. The largest change is refactoring all triggered abilities to use the stack with APNAP ordering.

---

## 1. Stack-Based Triggers + APNAP Ordering

**Problem:** All triggered abilities currently fire immediately via `ProcessTriggersAsync`. Per MTG rules, triggered abilities go on the stack and can be responded to.

**Design:**

### New Type: TriggeredAbilityStackObject

```csharp
public record TriggeredAbilityStackObject(
    GameCard Source,
    Guid ControllerId,
    IEffect Effect,
    GameCard? Target = null,
    Guid? TargetPlayerId = null);
```

Added to `GameState.Stack` alongside existing spell stack objects. Resolved LIFO through normal stack resolution.

### Trigger Flow

1. An event occurs (ETB, Dies, SpellCast, Cycle, etc.)
2. Engine collects all triggered abilities that match the event
3. **APNAP ordering**: Active player's triggers first, then non-active player's
4. Within each player's triggers, that player chooses the order (via `DecisionHandler`)
5. For triggers that need targets (e.g., Gempalm Incinerator), prompt for targets as they go on the stack
6. All triggers added to stack
7. Priority passes — players can respond with instants/activated abilities
8. Stack resolves LIFO — each trigger's effect executes on resolution

### Refactor Scope

- `ProcessTriggersAsync` → collects triggers, builds stack objects, adds to stack
- `ProcessBoardTriggersAsync` → same pattern
- `ProcessAttackTriggersAsync` → same pattern
- `ProcessDelayedTriggersAsync` → same pattern
- New `DecisionHandler.ChooseTriggerOrder(List<TriggeredAbilityStackObject>)` for APNAP ordering
- Existing ETB effects (tokens, tutor, reveal) now resolve from the stack instead of inline

### Stack Object Polymorphism

The stack currently holds one type. Introduce a base type or interface:

```csharp
public interface IStackObject
{
    Guid ControllerId { get; }
}

public record SpellStackObject(...) : IStackObject;
public record TriggeredAbilityStackObject(...) : IStackObject;
```

Stack resolution dispatches based on type.

---

## 2. Aura Mechanics

**Problem:** Wild Growth needs aura attachment to a land permanent.

**Design:**

### GameCard Changes

```csharp
public Guid? AttachedTo { get; set; }  // The permanent this aura is attached to
```

### CardDefinition Changes

```csharp
public AuraTarget? AuraTarget { get; init; }  // null = not an aura
```

```csharp
public enum AuraTarget { Land, Creature, Permanent }
```

### Casting Flow

When casting a spell with `CardType.Enchantment` and `AuraTarget != null`:
1. Filter battlefield for valid targets matching `AuraTarget`
2. Prompt `ChooseCard` for attachment target
3. On resolution, set `aura.AttachedTo = target.Id` and place on battlefield

### State-Based Action: Aura Detachment

In SBA check: if an aura's `AttachedTo` target is no longer on any battlefield, move the aura to its owner's graveyard.

### Wild Growth: Mana Trigger

New `GameEvent.TapForMana` — fired when any land is tapped for mana in `GameEngine`.

Wild Growth's trigger:
```
Event: TapForMana
Condition: new TriggerCondition — AttachedPermanentTapped
Effect: AddBonusManaEffect(ManaColor.Green)
```

When the enchanted land is tapped for mana, find all auras attached to it with `TapForMana` triggers, fire their effects (adding bonus mana to the controller's pool).

Since mana abilities don't use the stack in MTG, this trigger also resolves immediately (not stack-based). Mana abilities are the one exception to stack-based triggers.

---

## 3. Cycling

**Problem:** Gempalm Incinerator's primary mode is cycling, not casting.

**Design:**

### CardDefinition Changes

```csharp
public ManaCost? CyclingCost { get; init; }
public IReadOnlyList<Trigger> CyclingTriggers { get; init; } = [];
```

### New ActionType

```csharp
ActionType.Cycle  // Card ID identifies the card in hand to cycle
```

### Cycle Action Flow

1. Player chooses Cycle action with a card that has `CyclingCost`
2. Pay mana cost
3. Discard card from hand to graveyard
4. Draw a card
5. Fire `GameEvent.Cycle` — any `CyclingTriggers` on the cycled card go on the stack (stack-based)

### Gempalm Incinerator

```csharp
CyclingCost = ManaCost.Parse("{1}{R}"),
CyclingTriggers = [new Trigger(GameEvent.Cycle, TriggerCondition.Self, new GempalmIncineratorEffect())]
```

`GempalmIncineratorEffect`: Count Goblins on the cycling player's battlefield, deal that much damage to target creature. Target chosen when trigger goes on the stack.

### Decision Handler

`GetAction` must include cycling options: for each card in hand with a `CyclingCost` that the player can pay, offer `ActionType.Cycle`.

---

## 4. Dynamic Mana Abilities

**Problem:** Serra's Sanctum taps for {W} per enchantment controlled.

**Design:**

### ManaAbility Extension

```csharp
public static ManaAbility Dynamic(ManaColor color, Func<Player, int> countFunc)
```

Stores the color and count function. When the engine processes a tap-for-mana action on a land with a Dynamic ability, it calls `countFunc(player)` to determine how much mana to add.

### GameEngine Changes

In the tap-for-mana flow, handle `Dynamic` variant:
```csharp
case ManaAbilityType.Dynamic:
    var amount = ability.CountFunc(player);
    player.ManaPool.Add(ability.Color, amount);
    break;
```

### Serra's Sanctum Registration

```csharp
["Serra's Sanctum"] = new(null, ManaAbility.Dynamic(ManaColor.White,
    p => p.Battlefield.Cards.Count(c => c.CardTypes.HasFlag(CardType.Enchantment))),
    null, null, CardType.Land) { IsLegendary = true },
```

---

## 5. Opponent Cost Modification

**Problem:** Aura of Silence makes opponent's artifacts/enchantments cost {2} more.

**Design:**

### ContinuousEffect Extension

Add a `CostAppliesToOpponent` flag (bool):

```csharp
public record ContinuousEffect(
    ...existing params...,
    bool CostAppliesToOpponent = false);  // true = applies to opponent's spells only
```

### Spell Cost Calculation

When calculating the effective cost of a spell, iterate all active `ModifyCost` effects:
- If `CostAppliesToOpponent == false`: apply if the caster is the effect controller (existing behavior for Warchief)
- If `CostAppliesToOpponent == true`: apply if the caster is NOT the effect controller

### Aura of Silence Registration

```csharp
["Aura of Silence"] = new(ManaCost.Parse("{1}{W}{W}"), null, null, null, CardType.Enchantment)
{
    ContinuousEffects = [
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyCost,
            (_, _) => true, CostMod: 2,
            CostApplies: c => c.CardTypes.HasFlag(CardType.Artifact) || c.CardTypes.HasFlag(CardType.Enchantment),
            CostAppliesToOpponent: true),
    ],
    ActivatedAbility = new(new ActivatedAbilityCost(SacrificeSelf: true),
        new DestroyTargetEffect(),
        c => c.CardTypes.HasFlag(CardType.Artifact) || c.CardTypes.HasFlag(CardType.Enchantment)),
},
```

---

## 6. Mass Recursion (Replenish)

**Problem:** Replenish returns all enchantments from graveyard to battlefield.

**Design:**

### ReplenishEffect

```csharp
public class ReplenishEffect : SpellEffect
{
    public override Task Execute(EffectContext context, CancellationToken ct)
    {
        var enchantments = context.Controller.Graveyard.Cards
            .Where(c => c.CardTypes.HasFlag(CardType.Enchantment))
            .ToList();

        foreach (var card in enchantments)
        {
            context.Controller.Graveyard.RemoveById(card.Id);

            // Auras need valid targets; if none available, stay in graveyard
            if (card.Subtypes.Contains("Aura"))
            {
                // Find valid target based on AuraTarget
                // If no valid target exists, card stays in graveyard
                // (simplified: skip aura re-attachment for now)
                context.Controller.Graveyard.Add(card);
                continue;
            }

            context.Controller.Battlefield.Add(card);
            card.TurnEnteredBattlefield = context.State.TurnNumber;
        }

        // Fire ETB triggers for all cards that entered (batch)
        // These go on the stack via the new stack-based trigger system
        return Task.CompletedTask;
    }
}
```

Note: Replenish does NOT cast the enchantments — so enchantress triggers (`ControllerCastsEnchantment`) do NOT fire. Only ETB triggers fire.

Aura re-attachment during mass return is complex (MTG rule 303.4f). For this pass, auras returned by Replenish that need targets will stay in graveyard. Full aura-return targeting is deferred.

---

## 7. Evasion & Protection Keywords

### Mountainwalk

**Goblin Pyromancer** grants mountainwalk to all Goblins until end of turn (already has the pump + delayed destroy, just missing mountainwalk).

New keyword: `Keyword.Mountainwalk` (already exists in enum, just needs combat logic).

**Combat change:** In `DeclareBlockers` phase, when checking if a creature can be blocked:
- If attacker has `Mountainwalk` and the defending player controls a land with subtype "Mountain", the creature cannot be blocked.

Add to Goblin Pyromancer's ETB continuous effect:
```csharp
new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
    (card, _) => card.IsCreature && card.Subtypes.Contains("Goblin"),
    GrantedKeyword: Keyword.Mountainwalk, UntilEndOfTurn: true)
```

### Sterling Grove Shroud Grant

Sterling Grove: "Other enchantments you control have shroud."

```csharp
ContinuousEffects = [
    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
        (card, source) => card.CardTypes.HasFlag(CardType.Enchantment) && card.Id != source.Id,
        GrantedKeyword: Keyword.Shroud),
],
```

**Targeting change:** When choosing targets for spells/activated abilities, filter out permanents with `Keyword.Shroud` in their `ActiveKeywords`. This applies to both creature targeting and enchantment/artifact targeting.

---

## Implementation Order

Build in dependency order:

1. **Stack-based triggers + APNAP** — foundational refactor, everything depends on this
2. **Aura mechanics** — needs stack triggers for mana trigger design decisions
3. **Cycling** — needs stack triggers for "when you cycle" triggers
4. **Dynamic mana abilities** — independent, can be done anytime
5. **Opponent cost modification** — independent, extends existing continuous effects
6. **Evasion/protection keywords** — independent, extends combat + targeting
7. **Mass recursion** — needs auras + stack triggers in place

## Cards Unlocked

After implementation:
- **Wild Growth** — fully functional with aura attachment + mana trigger
- **Gempalm Incinerator** — cycling mode with Goblin-count damage
- **Serra's Sanctum** — dynamic mana based on enchantment count
- **Aura of Silence** — opponent tax + sacrifice to destroy
- **Replenish** — mass enchantment recursion (non-aura enchantments)
- **Goblin Pyromancer** — complete with mountainwalk grant
- **Sterling Grove** — complete with shroud grant to enchantments

## Deferred

- **Parallax Wave** — needs counter system, exile-per-source tracking, LTB triggers
- **Solitary Confinement** — needs upkeep costs, forced sacrifice, skip draw step, player protection
- **Opalescence** — needs type-changing effects (enchantment → creature)
- **Aura return targeting** — Replenish returning auras to valid targets (rule 303.4f)
