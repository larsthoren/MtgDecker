# Premodern Decks Implementation Design

## Goal
Add CardDefinitions for 8 Premodern tournament decks, implementing new engine mechanics as needed.

## Selected Decks (user's order)
1. **Mono Black Control** (13) — Control: discard, removal, artifacts
2. **Oath of Druids** (11) — Control: Oath trigger, land destruction, flashback
3. **Landstill** (10) — Control: counterspells, board wipes, man-lands
4. **Deadguy Ale** (09) — Aggro: BW creatures, discard, removal
5. **Terrageddon** (06) — Aggro: land destruction, Threshold, man-lands
6. **Elves** (05) — Aggro: tribal mana dorks, toolbox
7. **Mono Black Aggro** (03) — Aggro: discard, creatures, man-lands
8. **Sligh/RDW** (01) — Aggro: burn, hasty creatures

## Approach: Mechanics-First Batches

### Batch 1: Foundation Mechanics (~25 new effects)
New mechanics that enable the most cards across all 8 decks:

1. **Swamp basic land** — needed by 4 decks
2. **Flying keyword** — Hypnotic Specter, Exalted Angel (3+ decks)
3. **Discard effects** — Duress, Cabal Therapy, Hymn-style (5 decks)
4. **Edict effects** — Diabolic Edict (3 decks)
5. **Mana-producing spells** — Dark Ritual (4 decks)
6. **Board wipes** — Wrath of God, Armageddon (3 decks)
7. **Destroy-with-condition** — Smother (CMC ≤ 3), Vindicate (any permanent)
8. **Man-land activation** — Mishra's Factory (5 decks), Treetop Village, Faerie Conclave
9. **Life-loss upkeep triggers** — The Rack, Phyrexian Arena
10. **Haste keyword grant** — Ball Lightning (already have Haste via Warchief, just need to mark cards)

### Batch 2: Medium Mechanics
11. **Morph** — Bane of the Living, Exalted Angel (simplified: just face-down 2/2 for {3})
12. **Threshold** — Nimble Mongoose, Barbarian Ring, Cabal Pit
13. **Flashback** — Cabal Therapy, Call of the Herd, Deep Analysis
14. **Trample keyword** — Terravore, Ball Lightning
15. **Protection from [color]** — simplify to "can't be blocked by [color]"

### Batch 3: Complex Mechanics (defer or simplify)
- Oath of Druids trigger (reveal until creature) — complex
- Survival of the Fittest (discard creature: search creature) — complex toolbox
- Mox Diamond (play cost: discard land) — alternative cost
- Cataclysm (each player keeps 1 of each type) — complex selection

## Simplification Strategy
Cards needing unsupported mechanics get simplified versions:
- **Morph creatures**: Register as regular creatures at morph cost (face-up stats)
- **Flashback cards**: Register for front-face only; flashback added in Batch 2
- **Modal spells** (Funeral Charm): Register most common mode only
- **Complex activated abilities** (Cursed Scroll): Register with simplified version
- Cards not in registry still work via sandbox mode (no mana required)

## New Engine Types Needed

### New SpellEffects
- `DestroyCreatureEffect` — targeted creature destruction (Smother, Snuff Out)
- `DestroyPermanentEffect` — destroy any permanent (Vindicate)
- `EdictEffect` — target player sacrifices a creature
- `DiscardEffect` — target player discards card(s), with optional filter
- `AddManaSpellEffect` — add mana to pool (Dark Ritual)
- `DestroyAllCreaturesEffect` — board wipe (Wrath of God)
- `DestroyAllLandsEffect` — Armageddon
- `DamageAllCreaturesEffect` — Pyroclasm-style
- `GainLifeEffect` — for Absorb, etc.

### New TriggerEffects
- `DealDamageToAllEffect` — Plague Spitter upkeep
- `EachPlayerDiscardsEffect` — Bottomless Pit upkeep
- `RackDamageEffect` — The Rack upkeep (3 minus hand size)
- `DrawAndLoseLifeEffect` — Phyrexian Arena upkeep
- `ManLandEffect` — become creature until EOT

### New Keywords
- `Flying` — evasion (can only be blocked by Flying/Reach)
- `Trample` — excess combat damage to player
- `FirstStrike` — deals damage first in combat

### New TargetFilter factories
- `NonBlackCreature()` — for Snuff Out
- `CreatureWithCMCAtMost(int)` — for Smother
- `AnyPermanent()` — for Vindicate

## Card Count by Deck (unique new cards needed)

| Deck | Total Cards | Already Registered | New Cards |
|------|-------------|-------------------|-----------|
| Sligh | 14 unique | 5 (Mountain, Mogg Fanatic, Lightning Bolt, Fireblast, Flame Rift, Wooded Foothills) | ~9 |
| MBC | 17 unique | 2 (Wasteland, Mishra's Factory) | ~15 |
| MBA | 16 unique | 1 (Wasteland) | ~15 |
| Deadguy Ale | 14 unique | 1 (Swords) | ~13 |
| Landstill | 18 unique | 4 (Counterspell, Swords, Island, Plains) | ~14 |
| Oath | 19 unique | 4 (Swords, Sylvan Library, Windswept Heath, Wasteland) | ~15 |
| Terrageddon | 17 unique | 5 (Swords, Naturalize, Sylvan Library, Windswept Heath, Wasteland) | ~12 |
| Elves | 19 unique | 3 (Forest, Mountain, Wooded Foothills, Naturalize) | ~15 |

**Total new unique cards: ~80** (many shared across decks like Dark Ritual, Duress, Swamp, Vindicate)
**Total unique new cards after dedup: ~60**

## Implementation Order
1. New effect classes + keywords (engine foundation)
2. New TargetFilter factories
3. Shared cards (Swamp, Dark Ritual, Duress, Disenchant, etc.)
4. Deck-specific cards in user's order: MBC → Oath → Landstill → Deadguy → Terrageddon → Elves → MBA → Sligh
