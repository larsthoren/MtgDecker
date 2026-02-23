# Card Definitions Audit Results

> **Date:** 2026-02-22
> **Scope:** All ~140 cards in `CardDefinitions.cs` verified against Scryfall API
> **Method:** Systematic comparison of mana costs, P/T, types, subtypes, keywords, abilities, alternate costs, flashback, echo, cycling

---

## Critical Bugs (Wrong Mana Costs)

| Card | Implementation | Correct | Impact |
|------|---------------|---------|--------|
| Gempalm Incinerator | `{1}{R}` | `{2}{R}` | 1 mana too cheap |
| Rift Bolt | `{1}{R}` | `{2}{R}` | 1 mana too cheap (suspend not implemented) |
| Show and Tell | `{1}{U}{U}` | `{2}{U}` | Wrong colors + wrong CMC |
| Skeletal Scrying | `{1}{B}` | `{X}{B}` | Should be X-cost spell |

## Wrong Power/Toughness

| Card | Implementation | Correct |
|------|---------------|---------|
| Goblin Tinkerer | 1/1 | 1/2 |

## Wrong Ability Implementations (Functional Bugs)

| Card | Issue |
|------|-------|
| Goblin King | +1/+1 missing `ExcludeSelf: true` -- buffs itself, oracle says "Other Goblins" |
| Goblin Pyromancer | Pump is +2/+0, should be +3/+0; erroneously grants mountainwalk |
| Goblin Tinkerer | Uses `SacrificeSelf` instead of `TapSelf`; missing self-damage clause |
| Daze | Hard counter (`CounterSpellEffect`), should be soft counter ("unless controller pays {1}") |
| Prohibit | Uses "pay {2} or counter" mechanic, should check target spell's CMC <= 2; kicker missing |
| Knight of Stromgald | First strike is static (should be activated for {B}); pump costs {B} (should be {B}{B}) |
| Sterling Grove | Search puts card into hand, should put on top of library |
| Yavimaya Granger | Searches for "Forest" only (should be any basic land); puts to hand instead of battlefield tapped |
| Ray of Revelation | Target filter includes artifacts, should be enchantment-only |
| Priest of Titania | Counts only controller's Elves, should count ALL Elves on battlefield |
| Gempalm Incinerator | Cycling effect counts only controller's Goblins, should count all |
| Dust Bowl | Sacrifices itself instead of any land you control |
| Volcanic Spray | Missing flashback {1}{R}; hits flying creatures (shouldn't); missing player damage |
| Bottomless Pit | Makes all players discard each trigger (should be upkeep player only); not random |

## Wrong Subtypes

| Card | Implementation | Correct |
|------|---------------|---------|
| Goblin Piledriver | ["Goblin"] | ["Goblin", "Warrior"] |
| Goblin Warchief | ["Goblin"] | ["Goblin", "Warrior"] |
| Goblin Pyromancer | ["Goblin"] | ["Goblin", "Wizard"] |
| Goblin Guide | ["Goblin"] | ["Goblin", "Scout"] |
| Quirion Ranger | ["Elf"] | ["Elf", "Ranger"] |
| Bane of the Living | ["Zombie"] | ["Insect"] |
| Plague Spitter | ["Zombie"] | ["Phyrexian", "Horror"] |
| Phyrexian Rager | ["Horror"] | ["Phyrexian", "Horror"] |
| Jackal Pup | ["Hound"] | ["Jackal"] |
| Masticore | (none) | ["Masticore"] |
| Nantuko Vigilante | ["Insect", "Druid"] | ["Insect", "Druid", "Mutant"] |

## Missing Keywords

| Card | Missing Keyword |
|------|----------------|
| Anger | Haste on creature itself (only has graveyard ability) |
| Terravore | Trample |
| Murktide Regent | Flying |
| Wall of Roots | Defender |
| Faerie Conclave | Flying on animated form |
| Treetop Village | Trample on animated form |
| Emrakul, the Aeons Torn | "Can't be countered" |

## Missing Significant Abilities

| Card | Missing Ability |
|------|----------------|
| Grim Lavamancer | Exile 2 cards from graveyard as activation cost |
| Goblin Sharpshooter | "Doesn't untap during your untap step" |
| Sulfuric Vortex | Life-gain prevention |
| Searing Blood | Delayed trigger (3 damage to controller when creature dies) |
| Parallax Wave | Fading upkeep mechanic (auto-remove counter / auto-sacrifice) |
| Cursed Scroll | Name-a-card conditional (always deals damage unconditionally) |
| Flusterstorm | Storm keyword |
| Barbarian Ring | 1 self-damage on mana ability |
| Cabal Pit | 1 self-damage on mana ability |
| Mystic Sanctuary | Conditional enters-tapped + ETB return instant/sorcery to top |
| Mother of Runes | Target should be "creature you control" not any creature |
| Withered Wretch | Should exile from any graveyard, not just opponent's |

## Unimplemented Cards (Registered as Shells)

| Card | Status |
|------|--------|
| Powder Keg | No abilities at all |
| Phyrexian Furnace | No abilities at all |
| Grafdigger's Cage | No abilities at all |
| Funeral Pyre | No effect at all |
| Surgical Extraction | No effect (only Phyrexian mana cost works) |

## Known Simplifications (Lower Priority)

| Card | Simplification |
|------|---------------|
| Delver of Secrets | No transform mechanic |
| Murktide Regent | No delve, no +1/+1 counters |
| Dragon's Rage Channeler | No surveil, no delirium |
| Chain Lightning | No copy-back mechanic |
| Bane of the Living | No morph mechanic |
| Exalted Angel | No morph; uses Lifelink keyword instead of triggered lifegain |
| Nantuko Vigilante | No morph |
| Replenish | Auras unconditionally stay in graveyard (should return Auras with valid targets) |
| Sylvan Library | Triggers on upkeep instead of draw step; only offers Sylvan-drawn cards as choices |
| Cabal Therapy | Shows hand before naming (should be blind naming) |
| Funeral Charm | Only discard mode implemented (missing +2/-1 pump and swampwalk modes) |
| Mox Diamond | ETB is trigger, not replacement effect |
| Jackal Pup | Missing self-damage trigger when dealt damage |
| Tainted Field | Missing colorless option + Swamp-control condition |
| Darigaaz's Caldera | Should bounce-a-land ETB, not enter tapped |
| Treva's Ruins | Should bounce-a-land ETB, not enter tapped |
| City of Brass | Damage is pain-land style, should be triggered ability on being tapped |
| Goblin Lackey | Filters to creatures only, oracle says "Goblin permanent" |
| The Rack | Triggers on controller's upkeep instead of opponent's |
| Hypnotic Specter | Discard is not random; trigger is combat-damage-only (real card says "deals damage") |
| Deep Analysis | Always draws for caster (real card targets a player) |
| Quiet Speculation | Searches controller's library (real card targets a player's library) |
| Lava Spike | Missing Arcane subtype |
| Undercity Sewers | Missing Island/Swamp subtypes |
| Spawning Pool | Missing regeneration on animated form |
| Mishra's Factory | Missing {T}: Assembly-Worker gets +1/+1 ability |
| Cursed Scroll | Always deals damage (real card has name-a-card conditional) |

## Summary

| Category | Count |
|----------|-------|
| Fully correct | ~85 |
| Wrong mana cost | 4 |
| Wrong P/T | 1 |
| Wrong ability implementation | 14 |
| Wrong subtypes | 11 |
| Missing keywords | 7 |
| Missing significant abilities | 12 |
| Unimplemented shells | 5 |
| Known simplifications | ~28 |
