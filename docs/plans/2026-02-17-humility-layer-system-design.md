# Humility & MTG Layer System Implementation Design

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement Humility ({2}{W}{W} enchantment — all creatures lose all abilities and have base P/T 1/1) by building a proper MTG layer system into RecalculateState.

**Architecture:** Refactor the continuous effect system to use explicit MTG layers (4, 6, 7a-7c) with timestamp ordering. Add ability-removal tracking that suppresses triggers, activated abilities, mana abilities, keywords, and continuous effects originating from affected creatures.

---

## Background: MTG Layer System (Rule 613)

Continuous effects in MTG are applied in a strict order:

| Layer | Name | Our Cards |
|-------|------|-----------|
| 1 | Copy effects | (none) |
| 2 | Control-changing | (none) |
| 3 | Text-changing | (none) |
| 4 | Type-changing | Opalescence, Faerie Conclave, Mishra's Factory |
| 5 | Color-changing | (none) |
| 6 | Ability add/remove | Humility (remove), Anger (grant haste), all keyword grants |
| 7a | CDA (characteristic-defining) | Terravore dynamic P/T |
| 7b | Set P/T | Humility "are 1/1" |
| 7c | Modify P/T | Lords (+1/+1), pump effects, Giant Growth |
| 7d | Counters | (none yet) |
| 7e | P/T switching | (none) |

Within each layer, effects apply in **timestamp order** (earlier effect applies first). When two Layer 7b effects conflict, the later timestamp wins.

### Humility's Interactions

Humility operates in two layers:
- **Layer 6:** Removes all abilities from all creatures
- **Layer 7b:** Sets all creatures' base P/T to 1/1

Because ability removal (Layer 6) comes before P/T modification (Layer 7c), a creature under Humility that gets Giant Growth becomes 4/4 (1/1 base + 3/3 from Growth). But a lord effect from a creature (like Goblin King's +1/+1) is suppressed because the King lost its abilities in Layer 6.

---

## New Types and Fields

### EffectLayer Enum

```csharp
// File: src/MtgDecker.Engine/Enums/EffectLayer.cs (new)
public enum EffectLayer
{
    Layer4_TypeChanging = 4,
    Layer6_AbilityAddRemove = 6,
    Layer7a_CDA = 70,
    Layer7b_SetPT = 71,
    Layer7c_ModifyPT = 72,
}
```

### ContinuousEffectType Additions

```csharp
// Add to existing enum:
SetBasePowerToughness,  // Layer 7b — overwrites base P/T
RemoveAbilities,        // Layer 6 — strips all abilities from matching permanents
```

### ContinuousEffect New Fields

```csharp
// Add to ContinuousEffect record:
EffectLayer? Layer = null,        // Which MTG layer this effect belongs to (null = non-layered)
long Timestamp = 0,               // Ordering within the same layer
int? SetPower = null,             // For SetBasePowerToughness — the value to set
int? SetToughness = null,         // For SetBasePowerToughness — the value to set
```

### GameCard New Field

```csharp
// Add to GameCard:
public bool AbilitiesRemoved { get; set; }
```

---

## Refactored RecalculateState

### Current Flow (Before)

```
1. Clear ActiveEffects
2. RebuildActiveEffects (both players' battlefields)
3. RebuildGraveyardAbilities (both players' graveyards)
4. Re-add temp effects
5. Reset effective values
6. Layer 0: CDA (DynamicBasePower/DynamicBaseToughness)
7. Layer 1: BecomeCreature
8. Layer 2: ModifyPowerToughness
9. Layer 3: GrantKeyword
10. Non-layered: ExtraLandDrop
```

### New Flow (After)

```
1. Clear ActiveEffects
2. RebuildActiveEffects (both players' battlefields)
3. RebuildGraveyardAbilities (both players' graveyards)
4. Re-add temp effects
5. Reset effective values (including AbilitiesRemoved = false)
6. Build AbilitiesRemovedFrom set (empty)
7. Process effects BY LAYER ORDER, within each layer by Timestamp:
   a. Layer 4 — Type-changing (BecomeCreature)
   b. Layer 6 — Ability add/remove:
      i.  First: process RemoveAbilities effects → populate AbilitiesRemovedFrom set
                 + set card.AbilitiesRemoved = true on matching creatures
      ii. Then: process GrantKeyword effects → skip if source is in AbilitiesRemovedFrom
   c. Layer 7a — CDA: apply DynamicBasePower/DynamicBaseToughness
      → Skip if the creature is in AbilitiesRemovedFrom
   d. Layer 7b — SetBasePowerToughness: overwrite BasePower/BaseToughness
   e. Layer 7c — ModifyPowerToughness: additive P/T changes
      → Skip if source creature is in AbilitiesRemovedFrom
8. Non-layered effects: ExtraLandDrop, ModifyCost, SkipDraw, etc.
```

### Key Design Decision: Suppression via AbilitiesRemovedFrom

A `HashSet<Guid> abilitiesRemovedFrom` is populated during Layer 6 processing. It contains the IDs of all creatures that had their abilities removed.

In later layers, any ContinuousEffect whose `SourceId` is in this set is **skipped**. This correctly handles:
- Goblin King loses its lord effect (source is the King, which lost abilities)
- An Aura's effect still applies (source is the Aura, not the creature)
- Anger's graveyard haste still applies (source is Anger in graveyard, not a creature on battlefield)

---

## Ability Suppression: All Runtime Locations

When `card.AbilitiesRemoved == true`, the following must be suppressed:

### Triggers (4 methods)

| Method | What It Does | Suppression |
|--------|-------------|-------------|
| `CollectBoardTriggers` | Board-wide triggers from permanents | Skip triggers from permanents with `AbilitiesRemoved` |
| `QueueAttackTriggersOnStackAsync` | Attack triggers (Goblin Guide) | Skip if attacker has `AbilitiesRemoved` |
| `QueueEchoTriggersOnStackAsync` | Echo upkeep triggers | Skip if creature has `AbilitiesRemoved` |
| `QueueSelfTriggersOnStackAsync` | Self ETB/dies triggers | Skip if source has `AbilitiesRemoved` |

### Activated Abilities (1 location)

| Location | Suppression |
|----------|-------------|
| `case ActionType.ActivateAbility` (~line 539) | Check `AbilitiesRemoved` on source permanent. If true, log and break. |

### Mana Abilities (1 location)

| Location | Suppression |
|----------|-------------|
| `case ActionType.TapCard` (~line 242) | Before checking `ManaAbility`, verify the card doesn't have `AbilitiesRemoved`. If removed, skip mana production (tap still succeeds, just no mana). |

### Continuous Effects (3 locations in layer processing)

| Layer | Suppression |
|-------|-------------|
| Layer 6 GrantKeyword | Skip if `SourceId` is in `AbilitiesRemovedFrom` |
| Layer 7a CDA | Skip if creature is in `AbilitiesRemovedFrom` |
| Layer 7c ModifyPT | Skip if `SourceId` is in `AbilitiesRemovedFrom` |

### NOT Suppressed

- Abilities on non-creature permanents (enchantments, artifacts, lands)
- Effects from external sources (auras, enchantments, graveyard abilities)
- Card characteristics (name, types, subtypes, mana cost, color)
- Non-layered effects from non-creature sources (ExtraLandDrop from Exploration)

---

## Timestamp Assignment

Timestamps are assigned during `RebuildActiveEffects` using an incrementing counter on GameState:

```csharp
// On GameState:
public long NextEffectTimestamp { get; set; } = 1;
```

Each time an effect is added to `ActiveEffects`, it gets `Timestamp = _state.NextEffectTimestamp++`. The counter resets to 1 at the start of each `RecalculateState` call (since we rebuild all effects from scratch).

Cards that entered the battlefield earlier have their effects added first during rebuild (since `RebuildActiveEffects` iterates the battlefield list in order), giving them lower timestamps. This is a correct approximation: battlefield order reflects entry order.

For temporary (UntilEndOfTurn) effects that are preserved across rebuilds, their timestamps are reassigned during re-addition — this is correct because temp effects from spells like Giant Growth always have a later timestamp than permanent-based effects.

---

## Migrating Existing Effects

Every ContinuousEffect in CardDefinitions.cs needs a `Layer` tag:

### Self-keyword grants (creature grants keyword to itself)
Examples: Goblin Guide (Haste), Hypnotic Specter (Flying), Ball Lightning (Haste+Trample)
**Layer:** `EffectLayer.Layer6_AbilityAddRemove`

### Lord keyword grants (creature grants keywords to others)
Examples: Goblin Warchief (Haste to Goblins), Goblin King (Mountainwalk to Goblins)
**Layer:** `EffectLayer.Layer6_AbilityAddRemove`

### Lord P/T buffs (creature gives +N/+N to others)
Examples: Goblin King (+1/+1 to Goblins), Deranged Hermit (+1/+1 to Squirrels)
**Layer:** `EffectLayer.Layer7c_ModifyPT`

### Non-creature keyword grants
Examples: Sterling Grove (Shroud to enchantments), Anger graveyard (Haste to creatures)
**Layer:** `EffectLayer.Layer6_AbilityAddRemove`

### Type-changing effects
Example: Opalescence (enchantments become creatures)
**Layer:** `EffectLayer.Layer4_TypeChanging`

### Non-creature P/T modifications
Example: Nimble Mongoose threshold (+2/+2 to self, from self — but it's a creature, so under Humility it IS suppressed)
**Layer:** `EffectLayer.Layer7c_ModifyPT`

### Non-layered effects (no Layer tag needed)
Examples: Exploration (ExtraLandDrop), Solitary Confinement (SkipDraw, GrantPlayerShroud, PreventDamageToPlayer), Aura of Silence (ModifyCost)
**Layer:** `null` (processed in the non-layered section)

---

## Humility Card Definition

```csharp
["Humility"] = new(ManaCost.Parse("{2}{W}{W}"), null, null, null, CardType.Enchantment)
{
    ContinuousEffects =
    [
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.RemoveAbilities,
            (card, _) => card.IsCreature,
            Layer: EffectLayer.Layer6_AbilityAddRemove),
        new ContinuousEffect(Guid.Empty, ContinuousEffectType.SetBasePowerToughness,
            (card, _) => card.IsCreature,
            SetPower: 1, SetToughness: 1,
            Layer: EffectLayer.Layer7b_SetPT),
    ],
},
```

Since Humility is an enchantment (not a creature), its effects are never suppressed by another Humility. Both effects target all creatures on the battlefield.

---

## Test Scenarios

### Core Humility Tests
1. Humility makes all creatures 1/1
2. Humility removes keywords (flying, haste, shroud)
3. Humility suppresses lord P/T buffs (Goblin King's +1/+1 no longer applies)
4. Humility suppresses lord keyword grants (Goblin King's mountainwalk no longer applies)
5. Humility suppresses activated abilities (creature abilities can't be activated)
6. Humility suppresses mana abilities (Llanowar Elves can't tap for mana)
7. Humility suppresses triggered abilities (ETB triggers don't fire)
8. Humility suppresses CDA (Terravore becomes 1/1, not dynamic)

### Layer Interaction Tests
9. Pump spell under Humility: creature is 1/1 base, Giant Growth makes it 4/4
10. Non-creature effects still work under Humility (Exploration still grants extra land drop)
11. Aura effects still apply under Humility (an aura granting +2/+2 makes creature 3/3)
12. Anger in graveyard still grants haste under Humility (external source)
13. Enchantment abilities not affected (Sterling Grove still grants shroud to enchantments)

### Layer 7b Ordering Tests
14. Multiple SetBasePowerToughness effects — later timestamp wins

### Regression Tests
15. RecalculateState without Humility still works identically (all existing tests pass)
16. Existing lord effects still apply correctly when no Humility present

---

## Edge Cases

### Opalescence + Humility
When both are on the battlefield:
- Opalescence (Layer 4): Makes all non-Aura enchantments into creatures with P/T = CMC
- Humility is now a creature (it's an enchantment made into a creature by Opalescence)
- Humility (Layer 6): Removes abilities from all creatures, including itself
- BUT: Humility removing its own abilities is a paradox. Per MTG rules (613.7), since both effects are in different layers, they both apply regardless. Humility still makes creatures 1/1 and removes abilities, even though it's also a creature.
- Our implementation handles this correctly: Humility's effects are added from its CardDefinition. Even if Humility-the-creature gets AbilitiesRemoved, the effects were already added to ActiveEffects during RebuildActiveEffects. The AbilitiesRemovedFrom check only suppresses effects in LATER layers from creatures that lost abilities. Since Humility's RemoveAbilities effect is the one doing the removing in Layer 6, it still executes.

The only remaining question: does Humility become a creature with its CMC P/T from Opalescence, or 1/1? Answer: Opalescence sets P/T in Layer 4 (as BecomeCreature with SetPowerToughnessToCMC). Humility overrides in Layer 7b to 1/1. So Humility itself becomes a 1/1 creature under Opalescence. This is correct per MTG rules.

### Double Humility
Two Humilities on the battlefield: both do the same thing. Creatures are still 1/1 with no abilities. No special handling needed — the effects are idempotent.

---

## Files Changed

### New Files
- `src/MtgDecker.Engine/Enums/EffectLayer.cs`
- `tests/MtgDecker.Engine.Tests/HumilityLayerSystemTests.cs`

### Modified Files
- `src/MtgDecker.Engine/ContinuousEffect.cs` — add Layer, Timestamp, SetPower, SetToughness, new enum values
- `src/MtgDecker.Engine/GameCard.cs` — add AbilitiesRemoved
- `src/MtgDecker.Engine/GameState.cs` — add NextEffectTimestamp
- `src/MtgDecker.Engine/GameEngine.cs` — refactor RecalculateState, add suppression checks at 8 locations
- `src/MtgDecker.Engine/CardDefinitions.cs` — add Layer tags to all ContinuousEffects, add Humility definition

---

## Risk Assessment

**High risk:** RecalculateState refactor touches the core of the effect system. Every existing test exercises this code.

**Mitigation:** All 1248 existing tests must pass after the refactor. The Layer tag additions to existing effects should be behavior-preserving — existing effects get explicit layers matching their current implicit processing order.

**Medium risk:** Ability suppression at 8 runtime locations. Missing one means a creature incorrectly keeps an ability under Humility.

**Mitigation:** Comprehensive test suite covering each suppression point individually.
