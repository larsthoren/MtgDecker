# Deferred Cards Design: Opalescence, Parallax Wave, Solitary Confinement

## Goal

Implement the 3 remaining deferred enchantress deck cards to reach 100% deck coverage for both the Goblins and Enchantress decks.

## Cards

### Solitary Confinement ({2}{W})
- **Upkeep cost**: At the beginning of your upkeep, sacrifice Solitary Confinement unless you discard a card.
- **Skip draw**: Skip your draw step.
- **Player shroud**: You can't be the target of spells or abilities. You can't be dealt damage.

### Parallax Wave ({2}{W}{W})
- **Fade counters**: Enters the battlefield with 5 fade counters.
- **Activated ability**: Remove a fade counter: Exile target creature.
- **LTB trigger**: When Parallax Wave leaves the battlefield, each creature exiled with it returns to play under its owner's control.

### Opalescence ({2}{W}{W})
- **Type-changing**: Each other non-Aura enchantment is a creature with power and toughness each equal to its converted mana cost.

## Implementation Order

1. Solitary Confinement (simplest — self-contained new effects)
2. Parallax Wave (medium — needs counter system + exile tracking + LTB)
3. Opalescence (hardest — type-changing continuous effects)

---

## System 1: Upkeep Cost — Sacrifice or Discard (Solitary Confinement)

### New Types
- `UpkeepCostEffect : IEffect` — Triggered on `GameEvent.Upkeep` / `TriggerCondition.Upkeep`
  - If controller has cards in hand: prompt `ChooseCard` for discard (optional)
  - If they choose a card: discard it (move to graveyard), log
  - If they decline or have no cards: sacrifice the source enchantment, log

### Engine Changes
- No new engine changes needed — upkeep triggers already fire via `QueueBoardTriggersOnStackAsync`
- The effect handles its own sacrifice/discard logic

### AI Support
- `AiBotDecisionHandler`: When asked to choose a card to discard for upkeep cost, discard lowest-value card if hand size > 2, otherwise decline (sacrifice)

---

## System 2: Skip Draw Step (Solitary Confinement)

### New Types
- `ContinuousEffectType.SkipDraw` — New enum value

### Engine Changes
- `GameEngine.ExecuteTurnBasedAction` at the draw step: Before drawing, check `_state.ActiveEffects` for any `SkipDraw` effect where the controller is the active player. If found, skip the draw and log.

### ContinuousEffect Registration
- Solitary Confinement's definition includes:
  ```
  new ContinuousEffect(Guid.Empty, ContinuousEffectType.SkipDraw,
      (_, _) => true)
  ```
- The `Applies` predicate is unused for this type; the engine checks controller match.

---

## System 3: Player Shroud / Damage Prevention (Solitary Confinement)

### New Types
- `ContinuousEffectType.GrantPlayerShroud` — New enum value
- `ContinuousEffectType.PreventDamageToPlayer` — New enum value (or combine with shroud)

### Design Decision: Combine or Separate?
**Separate** — Shroud and damage prevention are distinct MTG concepts. Other cards could grant one without the other. Two new enum values.

### Engine Changes
- **Player targeting check**: Before allowing a spell or ability to target a player, check `_state.ActiveEffects` for `GrantPlayerShroud` where the controller is that player. If found, that player is an illegal target.
  - Affects: `CanTargetPlayer` on activated abilities, spell targeting
- **Damage prevention**: In `DealDamageToPlayer` (or wherever player damage is applied), check for `PreventDamageToPlayer` effects. If the controller has one, prevent the damage and log.
  - Affects: combat damage to player, direct damage effects (Mogg Fanatic, Siege-Gang Commander)

### AI Support
- AI should recognize it can't target the protected player with damage abilities

---

## System 4: Counter System (Parallax Wave)

### New Types
- `CounterType` enum: `Fade` (extensible for future +1/+1, charge, etc.)
- `GameCard.Counters` property: `Dictionary<CounterType, int>` (default empty)
- Methods on GameCard: `AddCounters(CounterType, int)`, `RemoveCounter(CounterType)` (returns bool)

### Engine Changes
- `ActivatedAbilityCost` gets new field: `RemoveCounterType` (`CounterType?`)
  - When processing activated ability costs, if `RemoveCounterType` is set, call `RemoveCounter` on the source card. If it returns false (no counters), ability can't be activated.
- `GetAvailableActions`: When building activated ability actions, check counter availability

### ETB Counter Placement
- New `AddCountersEffect : IEffect` — Places N counters of a type on the source card
- Parallax Wave's definition includes ETB trigger: `new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new AddCountersEffect(CounterType.Fade, 5))`

---

## System 5: Per-Source Exile Tracking (Parallax Wave)

### New Types
- `ExileCreatureEffect : IEffect` — Exiles target creature, records the exile on the source card
- `GameCard.ExiledCardIds` property: `List<Guid>` — Tracks which cards this permanent has exiled

### How It Works
1. Parallax Wave activates: remove fade counter, choose target creature
2. `ExileCreatureEffect` moves the target from battlefield to exile zone
3. The target's `Id` is added to the Wave's `ExiledCardIds` list
4. When Wave leaves the battlefield (destroyed, bounced, exiled itself), LTB trigger fires

### Zone Transfer
- Moving a card to exile: set `Zone = ZoneType.Exile`, add to `player.Exile.Cards`, remove from battlefield
- Already supported by the zone system — just need to call the right move logic

---

## System 6: Leave-the-Battlefield Triggers (Parallax Wave)

### New Types
- `GameEvent.LeavesBattlefield` — New event type
- `TriggerCondition.SelfLeavesBattlefield` — New trigger condition
- `ReturnExiledCardsEffect : IEffect` — Returns all cards in source's `ExiledCardIds` to battlefield

### Engine Changes
- Whenever a card moves FROM battlefield (to graveyard, exile, hand, library), fire `GameEvent.LeavesBattlefield` triggers on that card BEFORE the move completes
- `ReturnExiledCardsEffect`:
  1. Reads `context.Source.ExiledCardIds`
  2. For each ID, finds the card in exile zones (either player)
  3. Moves it to the battlefield under its owner's control
  4. Clears the ExiledCardIds list

### Important: Trigger Timing
- LTB triggers use "last known information" — the trigger checks the card's state as it last existed on the battlefield
- For our implementation: fire the trigger before removing the card from battlefield state, so the trigger can read ExiledCardIds

---

## System 7: Type-Changing Continuous Effect (Opalescence)

### New Types
- `ContinuousEffectType.BecomeCreature` — Makes non-creature permanents into creatures

### ContinuousEffect Properties
- Reuse existing `Applies` predicate: `(card, source) => card.CardTypes.HasFlag(CardType.Enchantment) && !card.Subtypes.Contains("Aura") && card.Id != source`
- New property: `SetPowerToughnessToCMC` (bool) — When true, sets base P/T to the card's ManaCost.ConvertedManaCost

### Engine Changes: ApplyContinuousEffects
- **Layer ordering** (simplified for our scope):
  1. Type-changing effects (`BecomeCreature`) — adds `CardType.Creature` to card's effective types
  2. P/T setting effects — sets base P/T to CMC
  3. P/T modification effects (`ModifyPowerToughness`) — adds +1/+1 from lords
  4. Keyword grants (`GrantKeyword`) — haste, shroud, etc.

- Current `ApplyContinuousEffects` applies all effects in a single pass. Need to split into ordered layers.

### GameCard Changes
- `EffectiveCardTypes` property (CardType) — Computed card types after continuous effects. Default: same as `CardTypes`.
- All `IsCreature`, `IsLand`, etc. checks use `EffectiveCardTypes` instead of `CardTypes`.

### Interactions
- Enchantment-creatures from Opalescence:
  - Can attack/block (subject to summoning sickness)
  - Can be targeted by creature-targeting effects (Swords to Plowshares, Gempalm)
  - Die from lethal damage / 0 toughness SBA
  - Still count as enchantments (trigger Argothian Enchantress, Enchantress's Presence)
  - Sterling Grove's shroud still applies (they're still enchantments)

### Summoning Sickness
- Already tracked via `TurnEnteredBattlefield`. If the enchantment entered the battlefield this turn, it has summoning sickness as a creature (can't attack or use tap abilities).

---

## Card Definitions

### Solitary Confinement
```
ManaCost: {2}{W}
CardType: Enchantment
Triggers: [Upkeep -> UpkeepCostEffect(sacrifice-or-discard)]
ContinuousEffects: [SkipDraw, GrantPlayerShroud, PreventDamageToPlayer]
```

### Parallax Wave
```
ManaCost: {2}{W}{W}
CardType: Enchantment
Triggers: [ETB -> AddCountersEffect(Fade, 5), LTB -> ReturnExiledCardsEffect]
ActivatedAbility: Cost(RemoveCounter: Fade) -> ExileCreatureEffect, TargetFilter: creature
```

### Opalescence
```
ManaCost: {2}{W}{W}
CardType: Enchantment
ContinuousEffects: [BecomeCreature with SetPowerToughnessToCMC, excludes Auras and self]
```

---

## Test Strategy

Each system gets dedicated test files:
- `UpkeepCostTests.cs` — sacrifice-or-discard behavior
- `SkipDrawTests.cs` — draw step skipping
- `PlayerProtectionTests.cs` — player shroud + damage prevention
- `CounterTests.cs` — counter add/remove, cost checking
- `ExileTrackingTests.cs` — per-source exile tracking
- `LeaveBattlefieldTests.cs` — LTB triggers
- `TypeChangingTests.cs` — Opalescence creature conversion
- Integration tests for card interactions (Opalescence + Sterling Grove, Wave + creatures, etc.)
