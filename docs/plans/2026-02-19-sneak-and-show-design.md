# Legacy Sneak and Show — Design Document

## Overview

Add a complete Legacy Sneak and Show deck to the MtgDecker engine. This requires implementing ~15 new cards in `CardDefinitions`, creating ~10 new effect classes, and adding several new engine mechanics (extra turns, annihilator, symmetrical "put into play" effects, graveyard replacement effects, Blood Moon land override).

## Decklist (75 cards)

### Main Deck (60)

**Creatures (6)**
- 3 Emrakul, the Aeons Torn
- 3 Griselbrand

**Spells (36)**
- 4 Show and Tell
- 4 Sneak Attack
- 4 Brainstorm *(already registered)*
- 4 Ponder *(already registered)*
- 1 Preordain *(already registered)*
- 4 Force of Will *(already registered)*
- 2 Spell Pierce
- 4 Lotus Petal
- 1 Intuition

**Lands (18)**
- 3 Ancient Tomb
- 3 City of Traitors
- 4 Scalding Tarn *(already registered)*
- 2 Flooded Strand *(already registered)*
- 2 Volcanic Island *(already registered)*
- 3 Island *(already registered)*
- 1 Mountain *(already registered)*

### Sideboard (15)
- 3 Flusterstorm
- 2 Pyroblast
- 2 Blood Moon
- 2 Pyroclasm
- 2 Surgical Extraction
- 2 Grafdigger's Cage
- 2 Wipe Away

## Cards Already Registered

These exist in `CardDefinitions.cs` and need no changes:
- Brainstorm, Ponder, Preordain (cantrips)
- Force of Will, Daze (countermagic — Daze available but not in this list)
- Scalding Tarn, Flooded Strand, Volcanic Island, Island, Mountain, City of Brass (lands)

## New Card Implementations

### 1. Show and Tell — `{1}{U}{U}` Sorcery

**MTG Rules:** Each player may put an artifact, creature, enchantment, or land card from their hand onto the battlefield.

**Implementation:**
- `ShowAndTellEffect : SpellEffect` (async)
- Caster chooses first (via `DecisionHandler.ChooseCard`), then opponent chooses
- Filter: only permanent cards (creature, artifact, enchantment, land — not instant/sorcery)
- Both enter the battlefield simultaneously
- Must fire ETB triggers for both cards via `GameEngine.QueueSelfTriggersOnStackAsync`
- Need to pass `GameEngine` reference through spell resolution (already available via `GameState.Engine` or add to resolution context)

**Key concern:** SpellEffect.ResolveAsync currently receives `GameState`, `StackObject`, `IPlayerDecisionHandler`. It needs access to the engine to fire ETB triggers. Options:
1. Add `GameEngine` to `ResolveAsync` signature (breaking change to all effects)
2. Add `GameEngine` property to `GameState` (circular reference but pragmatic)
3. Have the engine check for newly-entered permanents after spell resolution and fire ETB triggers

**Recommendation:** Option 3 — the engine already fires ETB triggers after spell resolution for permanents that go to the battlefield (see `ResolveStackAsync`). The `ShowAndTellEffect` just needs to move cards from hand to battlefield and set `TurnEnteredBattlefield`. The engine will detect new permanents and fire triggers in its existing flow. However, this only works for the caster's permanents currently. We need to also queue triggers for the opponent's card. **Best approach: add `Action<GameCard, Player> OnPermanentEntered` callback to SpellEffect resolution context or post-process in engine.**

### 2. Sneak Attack — `{3}{R}` Enchantment

**MTG Rules:** `{R}: You may put a creature card from your hand onto the battlefield. That creature gains haste. Sacrifice it at the beginning of the next end step.`

**Implementation:**
- `CardDefinition` with `ActivatedAbility`:
  - Cost: `ManaCost.Parse("{R}")` (no tap required)
  - Effect: `SneakAttackPutEffect : IEffect`
- `SneakAttackPutEffect`:
  1. Prompt controller to choose a creature from hand
  2. Put it onto the battlefield
  3. Grant haste via `UntilEndOfTurn` continuous effect (but actually until sacrificed)
  4. Register end-of-turn sacrifice via existing `RegisterEndOfTurnSacrificeEffect` pattern
  5. Set `TurnEnteredBattlefield` for summoning sickness (moot since haste)
  6. Fire ETB triggers

**Note:** The haste grant should persist even past end of turn since the creature is sacrificed at EOT anyway. Using `UntilEndOfTurn = true` on a continuous effect works fine.

### 3. Emrakul, the Aeons Torn — `{15}` Legendary Creature 15/15

**MTG Rules:**
- Flying, protection from colored spells, annihilator 6
- When you cast Emrakul, take an extra turn after this one
- When Emrakul is put into a graveyard from anywhere, its owner shuffles their graveyard into their library

**Implementation:**

**a) Base stats:**
```
ManaCost.Parse("{15}"), null, 15, 15, CardType.Creature
IsLegendary = true
Subtypes = ["Eldrazi"]
```

**b) Keywords — Flying:**
- `ContinuousEffect` granting `Keyword.Flying`

**c) Protection from colored spells:**
- New `Keyword.ProtectionFromColoredSpells`
- Engine check: when targeting with a spell, if target has this keyword and spell is colored (has any color in ManaCost besides colorless), targeting is illegal
- Implementation in `GameEngine` targeting validation

**d) Annihilator 6:**
- New `AnnihilatorEffect(int count) : IEffect`
- Trigger: `GameEvent.BeginCombat`, `TriggerCondition.SelfAttacks`
- Effect: defending player must sacrifice 6 permanents
  - Use `DecisionHandler.ChooseCard` in a loop (6 times) for the defending player
  - Each iteration, the opponent chooses one permanent they control to sacrifice
  - Must handle case where opponent has fewer than 6 permanents (sacrifice all)

**e) Extra turn on cast:**
- New `ExtraTurnOnCastEffect : IEffect`
- Trigger: `GameEvent.SpellCast`, `TriggerCondition.Self` — but this needs to be a cast trigger, not ETB
- Need new `TriggerCondition.SelfIsCast` — fires when this card is cast as a spell (goes on stack), not when it enters the battlefield
- **Engine change:** `GameState.ExtraTurns` queue (list of player IDs). When a turn ends, check if there are extra turns queued before passing to the other player.

**f) Graveyard shuffle replacement:**
- New property: `CardDefinition.ShuffleGraveyardOnDeath = true` (or a more generic replacement effect system)
- **Engine change:** Whenever Emrakul would be put into a graveyard from anywhere (dies, discarded, milled), instead shuffle it and its owner's graveyard into the library
- Check in all zone transitions that move to graveyard: combat death, destroy effects, discard, sacrifice
- Implementation: `GameCard.HasGraveyardShuffleReplacement` flag, checked in `MoveToGraveyard` helper

### 4. Griselbrand — `{4}{B}{B}{B}{B}` Legendary Creature 7/7

**MTG Rules:** Flying, lifelink. Pay 7 life: Draw seven cards.

**Implementation:**
```
ManaCost.Parse("{4}{B}{B}{B}{B}"), null, 7, 7, CardType.Creature
IsLegendary = true
Subtypes = ["Demon"]
```

- Flying + Lifelink via `ContinuousEffect` granting keywords
- Activated ability: `ActivatedAbilityCost(PayLife: 7)` + `DrawCardsActivatedEffect(7)`
- **Engine change:** Add `PayLife` to `ActivatedAbilityCost` record
- New `DrawCardsActivatedEffect(int count) : IEffect` — simply draws N cards for controller

### 5. Lotus Petal — `{0}` Artifact

**MTG Rules:** `{T}, Sacrifice Lotus Petal: Add one mana of any color.`

**Implementation:**
- `ManaCost.Parse("{0}")`, `CardType.Artifact`
- Activated ability: `ActivatedAbilityCost(TapSelf: true, SacrificeSelf: true)` + `AddAnyManaEffect`
- New `AddAnyManaEffect : IEffect` — prompts controller to choose a color, adds that mana to pool
- Reuse the mana color chooser from existing `ManaAbility.Choice` pattern

### 6. Spell Pierce — `{U}` Instant

**MTG Rules:** Counter target noncreature spell unless its controller pays {2}.

**Implementation:**
- `ManaCost.Parse("{U}")`, `CardType.Instant`
- Target filter: noncreature spells — new `TargetFilter.NoncreatureSpell()`
- Effect: `ConditionalCounterEffect(2)` (already exists)

### 7. Ancient Tomb — Land

**MTG Rules:** `{T}: Add {C}{C}. Ancient Tomb deals 2 damage to you.`

**Implementation:**
- New `ManaAbility.PainFixed(ManaColor color, int amount, int damage)` or `ManaAbility.DoublePain(int damage)`
- Produces 2 colorless mana, deals 2 damage to controller on tap
- **Engine change:** Extend the `TapForMana` handler to apply self-damage after producing mana
- Could model as `ManaAbility` with a `SelfDamage` property: `ManaAbility.FixedWithDamage(ManaColor.Colorless, count: 2, damage: 2)`

### 8. City of Traitors — Land

**MTG Rules:** `{T}: Add {C}{C}. When you play another land, sacrifice City of Traitors.`

**Implementation:**
- Taps for 2 colorless: same `ManaAbility` approach as Ancient Tomb but without damage
- Sacrifice trigger: need `GameEvent.LandPlayed` or extend `EnterBattlefield` handling
- New `TriggerCondition.ControllerPlaysAnotherLand` — fires when controller plays a land that is NOT this card
- Effect: `SacrificeSelfEffect : IEffect` — sacrifice the source card
- **Engine change:** Fire `GameEvent.LandPlayed` (or similar) after a land is played, so City of Traitors can detect when another land enters

### 9. Intuition — `{2}{U}` Instant

**MTG Rules:** Search your library for three cards and reveal them. Target opponent chooses one. Put that card into your hand and the rest into your graveyard.

**Implementation:**
- `IntuitionEffect : SpellEffect` (async)
- Steps:
  1. Controller searches library for 3 cards (need `DecisionHandler.ChooseCards(list, 3, prompt)`)
  2. Reveal them (log + UI notification)
  3. Opponent chooses 1 (via opponent's `DecisionHandler.ChooseCard`)
  4. Chosen card → controller's hand, other 2 → controller's graveyard
- This needs a way to let the opponent make a choice. Current `DecisionHandler` is per-player. Need to get the opponent's handler.
- **Engine approach:** `SpellEffect.ResolveAsync` can access `state.Player1`/`state.Player2` to find the opponent, and use their decision handler for the choice.

### 10. Blood Moon — `{2}{R}` Enchantment (Sideboard)

**MTG Rules:** Nonbasic lands are Mountains.

**Implementation:**
- Continuous effect that modifies all nonbasic lands:
  - Override their mana abilities to `ManaAbility.Fixed(ManaColor.Red)`
  - Remove other abilities (fetch, activated)
  - Add "Mountain" subtype
- New `ContinuousEffectType.OverrideLandType`
- Apply at `EffectLayer.Layer4_TypeChanging`
- **Engine change:** The continuous effect application system needs to handle land type override — replacing mana abilities on affected permanents

### 11. Pyroclasm — `{1}{R}` Sorcery (Sideboard)

**MTG Rules:** Pyroclasm deals 2 damage to each creature.

**Implementation:**
- `DamageAllCreaturesEffect(2)` already exists
- Simple registration: `new(ManaCost.Parse("{1}{R}"), null, null, null, CardType.Sorcery, Effect: new DamageAllCreaturesEffect(2))`

### 12. Flusterstorm — `{U}` Instant (Sideboard)

**MTG Rules:** Counter target instant or sorcery spell unless its controller pays {1}. Storm.

**Simplified implementation (stub — no storm):**
- Target filter: instant or sorcery spells
- Effect: `ConditionalCounterEffect(1)`
- Storm keyword deferred — add a comment noting the simplification

### 13. Pyroblast — `{R}` Instant (Sideboard)

**MTG Rules:** Choose one — Counter target spell if it's blue; or destroy target permanent if it's blue.

**Implementation:**
- Modal spell — new pattern needed
- Simplified: target blue spell and counter it, OR target blue permanent and destroy it
- New `PyroblastEffect : SpellEffect` — check if target is a spell on stack (counter) or permanent on battlefield (destroy)
- For now, implement as counter target blue spell (most common sideboard use case) with `TargetFilter` for blue spells

### 14. Surgical Extraction — `{B/P}` Instant (Sideboard)

**MTG Rules:** Pay 2 life or {B}. Choose target card in a graveyard other than a basic land. Search its owner's graveyard, hand, and library for all cards with the same name and exile them.

**Simplified implementation (stub):**
- Cost: `ManaCost.Parse("{B}")` with `AlternateCost(LifeCost: 2)` (Phyrexian mana approximation)
- Effect: `SurgicalExtractionEffect` — exile target card from graveyard, search for and exile all copies
- Target filter: card in opponent's graveyard

### 15. Grafdigger's Cage — `{1}` Artifact (Sideboard)

**MTG Rules:** Creature cards in graveyards and libraries can't enter the battlefield. Players can't cast spells from graveyards or libraries.

**Simplified implementation (stub):**
- Continuous effect preventing creatures from entering battlefield from graveyard/library
- For now, mark as a static effect that the engine checks — implement the check in relevant code paths

### 16. Wipe Away — `{1}{U}{U}` Instant (Sideboard)

**MTG Rules:** Split second. Return target permanent to its owner's hand.

**Simplified implementation (stub — no split second):**
- Effect: `BounceTargetEffect : SpellEffect` — return target permanent to owner's hand
- `TargetFilter.AnyPermanent()`
- Split second deferred

## Engine Changes Summary

### New Enums/Values

1. `Keyword.ProtectionFromColoredSpells` — Emrakul
2. `Keyword.Annihilator` — (optional, can just use trigger)
3. `GameEvent.LandPlayed` — City of Traitors trigger
4. `TriggerCondition.ControllerPlaysAnotherLand` — City of Traitors
5. `TriggerCondition.SelfIsCast` — Emrakul extra turn (cast trigger, not ETB)

### New Properties

6. `ActivatedAbilityCost.PayLife : int?` — Griselbrand
7. `ManaAbility.SelfDamage : int` — Ancient Tomb
8. `ManaAbility.ProduceCount : int` — Ancient Tomb / City of Traitors (produce 2 mana)
9. `CardDefinition.ShuffleGraveyardOnDeath : bool` — Emrakul replacement effect

### New GameState Properties

10. `GameState.ExtraTurns : Queue<Guid>` — extra turn queue (player IDs)

### New Engine Logic

11. **Extra turn processing** in turn loop — after a turn ends, check `ExtraTurns` queue
12. **Annihilator processing** — when annihilator trigger resolves, opponent sacrifices N permanents
13. **Protection from colored spells** targeting check — in spell targeting validation
14. **Graveyard shuffle replacement** — intercept all zone transitions to graveyard for cards with this flag
15. **Land played event firing** — fire `GameEvent.LandPlayed` when a land is played
16. **Pay life activated ability cost** — deduct life in activated ability resolution
17. **Self-damage on mana tap** — Ancient Tomb
18. **Double mana production** — Ancient Tomb / City of Traitors produce 2 colorless
19. **Blood Moon land override** — continuous effect application for land type changes

## New Effect Classes

### SpellEffects (`Effects/`)
1. `ShowAndTellEffect` — symmetrical put-into-play
2. `IntuitionEffect` — search 3, opponent picks 1
3. `BounceTargetEffect` — return permanent to hand
4. `PyroblastEffect` — counter blue spell or destroy blue permanent

### Trigger Effects (`Triggers/Effects/`)
5. `AnnihilatorEffect(int count)` — force opponent to sacrifice N permanents
6. `ExtraTurnEffect` — queue extra turn for controller
7. `SneakAttackPutEffect` — put creature from hand + haste + EOT sacrifice
8. `PayLifeDrawCardsEffect(int life, int cards)` — Griselbrand activated
9. `AddAnyManaEffect` — choose color, add to pool (Lotus Petal)
10. `SacrificeSelfEffect` — sacrifice source card (City of Traitors)
11. `SurgicalExtractionEffect` — exile from graveyard + search copies (stub)

### TargetFilter additions
12. `TargetFilter.NoncreatureSpell()` — for Spell Pierce
13. `TargetFilter.BlueSpell()` — for Pyroblast
14. `TargetFilter.InstantOrSorcerySpell()` — for Flusterstorm

## Testing Strategy

Each new card gets dedicated tests:
- **Show and Tell**: both players choose, only one chooses, neither chooses, ETB triggers fire
- **Sneak Attack**: put creature, verify haste, verify EOT sacrifice, multiple activations per turn
- **Emrakul**: annihilator forces sacrifices, extra turn on cast (not on Show and Tell/Sneak Attack), graveyard shuffle replacement, protection from colored targeting
- **Griselbrand**: pay 7 life draw 7, can't activate below 7 life, lifelink in combat
- **Lotus Petal**: tap + sacrifice for any color, can't reuse
- **Ancient Tomb**: produces 2 colorless, deals 2 to controller
- **City of Traitors**: produces 2 colorless, sacrificed when another land played
- **Spell Pierce**: counters noncreature, doesn't counter creature spells
- **Intuition**: search 3, opponent picks 1, rest to graveyard
- **Blood Moon**: nonbasic lands become Mountains, basics unaffected
- **Sideboard stubs**: basic functionality tests

## Implementation Order

1. **Engine infrastructure** — extra turns, new enums, ActivatedAbilityCost.PayLife, ManaAbility extensions
2. **Simple cards** — Lotus Petal, Spell Pierce, Pyroclasm, Ancient Tomb, City of Traitors
3. **Griselbrand** — activated ability with pay life
4. **Sneak Attack** — put creature + haste + EOT sacrifice
5. **Show and Tell** — symmetrical effect
6. **Emrakul** — annihilator, extra turn, graveyard shuffle, protection
7. **Intuition** — search + opponent choice
8. **Sideboard cards** — Blood Moon, Flusterstorm, Pyroblast, Surgical, Cage, Wipe Away
9. **Integration tests** — full deck game scenarios
