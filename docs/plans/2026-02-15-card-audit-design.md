# Card Implementation Audit & Fix Plan

**Date**: 2026-02-15
**Scope**: All ~140 cards in `CardDefinitions.cs` registry

## Problem

Many cards in the CardDefinitions registry are incorrectly implemented. Issues range from completely blank stubs (no abilities at all) to cards with fundamentally wrong effects (Fact or Fiction drawing 3 instead of the pile-splitting mechanic). Out of ~140 registered cards, only ~40 are correctly implemented.

## Audit Results

### Summary

| Category | Count | Description |
|----------|-------|-------------|
| Correct | ~40 | Working as intended |
| Completely unimplemented (stub) | ~20 | Has cost/type only, no abilities |
| Wrong effect | ~8 | Has an effect but it's fundamentally incorrect |
| Missing key abilities | ~45 | Partially implemented, missing important mechanics |
| Minor issues | ~20 | Mostly correct with small gaps |

### Completely Unimplemented Cards (Stubs)

These exist in the registry with just cost and type, zero functional abilities:

- **Decree of Justice** — should create X Angel tokens, cycling for X Soldier tokens
- **Standstill** — sacrifice when any player casts spell, opponents draw 3
- **Humility** — all creatures lose abilities, become 1/1
- **Oath of Druids** — upkeep: if opponent has more creatures, reveal until creature
- **Survival of the Fittest** — {G}, discard creature: search for creature
- **Mox Diamond** — discard land to keep; {T}: any color
- **Powder Keg** — upkeep fuse counters; sac: destroy matching CMC
- **Quiet Speculation** — search for 3 flashback cards, put in graveyard
- **Reckless Charge** — +3/+0 haste, flashback {2}{R}
- **Funeral Pyre** — exile graveyard card, owner gets 1/1 Spirit
- **Zuran Orb** — sacrifice land: gain 2 life
- **Masticore** — upkeep discard, {2}: ping creature, {2}: regenerate
- **Mother of Runes** — {T}: protection from chosen color
- **Quirion Ranger** — return Forest: untap creature
- **Wirewood Symbiote** — return Elf: untap creature
- **Wall of Roots** — -0/-1 counter: add {G}
- **Withered Wretch** — {1}: exile graveyard card
- **Nantuko Shade** — {B}: +1/+1 until EOT
- **Knight of Stromgald** — protection from white, pump, first strike
- **Phyrexian Furnace** — exile bottom of graveyard; sac: draw

### Cards with Wrong Effects

- **Fact or Fiction** — `DrawCardsEffect(3)` → should reveal 5, opponent splits piles, choose one
- **Impulse** — `DrawCardsEffect(1)` → should look at top 4, pick 1, rest to bottom
- **Cataclysm** — `DestroyAllCreaturesEffect` → should keep 1 of each type per player
- **Skeletal Scrying** — `DrawCardsEffect(2)` → should exile X from graveyard, draw X, lose X
- **Gemstone Mine** — `ManaAbility.Choice(all 5)` → should have 3 mining counters
- **Grim Lavamancer** — deals 1 damage → should deal 2, costs {R}+tap+exile 2 from GY
- **Mana Leak** — hard counter → should be "unless pays {3}"
- **Cabal Therapy** — discards 1 → should name a card, discard all copies

### Missing Key Abilities

#### Missing Haste/Keywords
- Goblin Guide (Haste + reveal trigger)
- Monastery Swiftspear (Haste + Prowess)
- Goblin Ringleader (Haste)
- Anger (Haste + graveyard haste-grant)
- Delver of Secrets (transform)
- Murktide Regent (Delve, Flying, +1/+1 counters)
- Dragon's Rage Channeler (Surveil, Delirium)
- Ball Lightning (end-of-turn sacrifice)
- Exalted Angel (Lifelink)
- Wall of Blossoms (Defender)

#### Missing Alternate Costs
- Force of Will (exile blue card + 1 life)
- Daze (return Island)
- Fireblast (sacrifice 2 Mountains)
- Snuff Out (pay 4 life if control Swamp)

#### Missing Triggered/Activated Abilities
- Eidolon of the Great Revel (2 damage on CMC ≤ 3 spell)
- Priest of Titania (tap for G per Elf, not just 1)
- Graveborn Muse (draw/lose X where X = Zombies)
- Goblin King (should be "other" + mountainwalk)
- Squee, Goblin Nabob (upkeep return from graveyard)
- Ravenous Baloth (sacrifice Beast: gain 4)
- Caller of the Claw (Flash + ETB bear tokens)
- Deranged Hermit (Echo + Squirrel lord)
- Masticore (upkeep discard, ping, regenerate)

#### Missing Flashback
- Call of the Herd ({3}{G})
- Deep Analysis ({1}{U}, pay 3 life)
- Cabal Therapy (sacrifice creature)
- Ray of Revelation ({G})
- Reckless Charge ({2}{R})

### Land Issues

#### Pain Lands Using `Choice` Instead of `PainChoice`
- Caves of Koilos, Llanowar Wastes, Battlefield Forge, Adarkar Wastes

#### Lands Missing Mana Ability
- Rishadan Port — missing `ManaAbility.Fixed(Colorless)`
- Wasteland — missing `ManaAbility.Fixed(Colorless)`
- Scalding Tarn — missing `FetchAbility(["Island", "Mountain"])`

#### Lands with Wrong Mechanics
- Skycloud Expanse — should cost {1} and add both W+U
- Tainted Field — should only work if you control Swamp
- Gemstone Mine — should use mining counters

#### Missing Subtypes
- Island — missing `["Island"]`
- Volcanic Island — missing `["Island", "Mountain"]`

#### Missing Secondary Abilities
- Barbarian Ring (Threshold: deal 2)
- Cabal Pit (Threshold: -2/-2)
- Dust Bowl (destroy nonbasic land)
- Darigaaz's Caldera / Treva's Ruins (bounce land ETB)
- Mystic Sanctuary (conditional ETB + recur spell)
- Faerie Conclave (flying on animated form)
- Treetop Village (trample on animated form)

### Correctly Implemented Cards (~40)

Lightning Bolt, Lava Spike, Flame Rift, Shock, Incinerate, Counterspell, Brainstorm, Ponder, Preordain, Naturalize, Disenchant, Vindicate, Smother, Diabolic Edict, Wrath of God, Armageddon, Dark Ritual, Swords to Plowshares, Replenish, Goblin Matron, Goblin Warchief, Mogg Fanatic, Gempalm Incinerator, Siege-Gang Commander, Goblin Sharpshooter, Goblin Pyromancer, Skirk Prospector, Argothian Enchantress, Hypnotic Specter, Ravenous Rats, Phyrexian Rager, Llanowar Elves, Fyndhorn Elves, Enchantress's Presence, Wild Growth, Exploration, Mirri's Guile, Parallax Wave, Sterling Grove, Aura of Silence, Seal of Cleansing, Solitary Confinement, Sylvan Library, Phyrexian Arena, Sulfuric Vortex, The Rack, all basic lands, pain lands (Karplusan Forest, Brushland, City of Brass), fetch lands, Serra's Sanctum, Gaea's Cradle.

## Implementation Plan

### Phase 1: Quick Wins (Tier 1 — Registry-Only Fixes)

All one-line changes in `CardDefinitions.cs`, no new effect classes:

**A. Pain lands — Change `Choice` to `PainChoice` (4 cards):**
- Caves of Koilos: `PainChoice([C, W, B], [W, B])`
- Llanowar Wastes: `PainChoice([C, B, G], [B, G])`
- Battlefield Forge: `PainChoice([C, R, W], [R, W])`
- Adarkar Wastes: `PainChoice([C, W, U], [W, U])`

**B. Missing land abilities (3 cards):**
- Rishadan Port: add `ManaAbility.Fixed(ManaColor.Colorless)`
- Wasteland: add `ManaAbility.Fixed(ManaColor.Colorless)`
- Scalding Tarn: add `FetchAbility(["Island", "Mountain"])`

**C. Missing subtypes (2 cards):**
- Island: add `Subtypes = ["Island"]`
- Volcanic Island: add `Subtypes = ["Island", "Mountain"]`

**D. Missing keywords (6 cards):**
- Goblin Guide: add Haste ContinuousEffect
- Goblin Ringleader: add Haste ContinuousEffect
- Monastery Swiftspear: add Haste ContinuousEffect
- Exalted Angel: add Flying (already has) + Lifelink (add to Keyword enum)
- Wall of Blossoms: add Defender (add to Keyword enum)
- Anger: add Haste ContinuousEffect

**E. Fix existing definitions (4 cards):**
- Goblin King: add ExcludeSelf + Mountainwalk grant
- Opalescence: add ExcludeSelf
- Grim Lavamancer: fix to 2 damage, add {R} mana cost to ability
- Goblin Tinkerer: add {R} mana cost to ability

New Keyword enum values needed: `Lifelink`, `Defender`

### Phase 2: Core Card Selection Spells

New effect classes following Brainstorm/Preordain pattern:

- **ImpulseEffect**: look at top 4, player picks 1, rest to bottom
- **FactOrFictionEffect**: reveal top 5, opponent splits into 2 piles, player chooses pile
- **ScryingEffect**: exile X from graveyard as cost, draw X, lose X life

### Phase 3: Missing Triggers & Abilities

New triggers and activated abilities following established patterns:

- **Eidolon of the Great Revel**: trigger on any spell cast CMC ≤ 3 → deal 2 to caster
- **Ball Lightning**: end step trigger → sacrifice self
- **Goblin Guide**: attack trigger → reveal opponent top card, if land they draw it
- **Squee, Goblin Nabob**: upkeep trigger → may return from graveyard to hand
- **Plague Spitter**: add dies trigger (1 damage all creatures + players)
- **Searing Blood**: delayed trigger on creature death → 3 damage to controller
- **Nantuko Shade**: activated ability {B}: +1/+1 until EOT
- **Zuran Orb**: activated ability sac land: gain 2 life
- **Ravenous Baloth**: activated ability sac Beast: gain 4 life
- **Withered Wretch**: activated ability {1}: exile graveyard card
- **Dust Bowl**: activated ability {3}, {T}, sac land: destroy nonbasic
- **Mother of Runes**: activated ability {T}: protection from chosen color

### Phase 4: Conditional Counters + Correct Discard

- **ConditionalCounterEffect**: counter unless controller pays {X} (for Mana Leak, Prohibit)
- **CounterAndGainLifeEffect**: counter + gain life (for Absorb)
- **NameAndDiscardEffect**: name a card, reveal hand, discard all copies (for Cabal Therapy)
- **GerrardVerdictEffect**: discard 2, gain 3 life per land discarded
- **DuressEffect**: caster chooses which non-creature non-land to discard

### Phase 5: Counter-Based Mechanics

- **Gemstone Mine**: ETB with 3 mining counters, remove to tap for any color, sacrifice when empty
- **Priest of Titania**: dynamic mana = {G} per Elf on battlefield
- **Graveborn Muse**: dynamic draw/life = X where X = Zombies you control

### Phase 6: Complex Single-Card Effects

- **CataclysmEffect**: each player chooses 1 artifact, 1 creature, 1 enchantment, 1 land — sacrifices rest (interactive selection)
- **Decree of Justice**: create X Angel tokens + cycling {2}{W} with pay {X} for X Soldier tokens

### Phase 7: Threshold

New engine mechanic: continuous effect conditioned on graveyard count ≥ 7

Cards: Nimble Mongoose (+2/+2), Barbarian Ring (sac: deal 2), Cabal Pit (sac: -2/-2)

### Phase 8: Echo

New engine mechanic: upkeep re-pay or sacrifice

Cards: Multani's Acolyte ({G}{G}), Deranged Hermit ({3}{G}{G}), Yavimaya Granger ({2}{G})

Also add Deranged Hermit's Squirrel lord (+1/+1) as ContinuousEffect.

### Phase 9: Alternate Costs

New engine framework: "you may [do X] instead of paying mana cost"

Cards: Force of Will (exile blue + 1 life), Daze (return Island), Fireblast (sac 2 Mountains), Snuff Out (4 life if Swamp), Mox Diamond (discard land or die)

### Phase 10: Flashback

New engine mechanic: cast from graveyard with alternate cost, exile after resolution

Cards: Call of the Herd, Deep Analysis, Cabal Therapy, Ray of Revelation, Reckless Charge, Quiet Speculation (searches for flashback cards)

### Phase 11: Remaining Complex Mechanics

Each is a separate engine feature:
- **Morph**: face-down 2/2 for {3}, flip action (Bane of Living, Nantuko Vigilante, Exalted Angel)
- **Transform**: Delver of Secrets flip to 3/2 flyer
- **Delve**: Murktide Regent exile instants/sorceries to reduce cost
- **Prowess**: Monastery Swiftspear +1/+1 on noncreature cast
- **Surveil + Delirium**: Dragon's Rage Channeler
- **Suspend**: Rift Bolt exile with time counters
- **Kicker**: Prohibit enhanced counter
- **Protection from color**: Goblin Piledriver (pro-blue), Knight of Stromgald (pro-white)

### Phase 12: Stretch Goals

- **Humility** — layer-based ability removal (extremely complex rules interactions)
- **Oath of Druids** — reveal-until-creature mill mechanic
- **Survival of the Fittest** — discard-to-tutor activated (needs new cost type)
- **Standstill** — trigger on any spell cast → sacrifice + opponents draw 3
- **Anger** graveyard ability — haste while in graveyard + control Mountain
- **Caller of the Claw** — Flash + count creatures died this turn (needs death tracking)
- **Terravore** — dynamic P/T = lands in all graveyards
- **Masticore** — upkeep discard + ping + regenerate

## Notes

- Phases 1-5 cover the most impactful fixes (~60 cards) without requiring new engine frameworks
- Each phase from 7 onward introduces a single new engine mechanic
- Flashback (Phase 10) has the most cards waiting on it (5+)
- Humility is intentionally last — it's notoriously complex even in official MTG rules engines
- New Keyword enum values needed across phases: Lifelink, Defender, Prowess, Flash, Protection
