# Premodern Card Implementation Review — Issues Found

> Strict card-by-card Oracle accuracy review of 88 cards implemented in PR #80.
> Review date: 2026-02-24

## Summary

- **88 cards reviewed** against Oracle text
- **52 clean** (no issues)
- **1 missing** card
- **9 critical** issues (wrong game behavior)
- **10 medium** issues (missing abilities, wrong triggers)
- **16 minor** issues (simplifications, acceptable for engine scope)

---

## Missing Card

| Card | Oracle | Notes |
|------|--------|-------|
| **Dauthi Slayer** | {B}{B}, Creature — Dauthi Soldier, 2/2, Shadow. Dauthi Slayer attacks each combat if able. | Completely absent from CardDefinitions.cs. Needs Shadow keyword + "must attack" enforcement. |

---

## Critical Issues (wrong game behavior)

### 1. Red Elemental Blast, Blue Elemental Blast, Hydroblast — missing permanent-destruction mode

**Cards:** Red Elemental Blast, Blue Elemental Blast, Hydroblast (3 cards)

**Problem:** All three use `TargetFilter.Spell()` which only targets spells on the stack. Oracle text for all three has a second mode: "Destroy target [color] permanent" which targets permanents on the battlefield. The permanent-destruction mode is completely missing.

**Oracle text:**
- REB: "Choose one — Counter target blue spell; or Destroy target blue permanent."
- BEB: "Choose one — Counter target red spell; or Destroy target red permanent."
- Hydroblast: "Choose one — Counter target spell if it's red; or Destroy target permanent if it's red."

**Fix:** These are modal spells. The effects (PyroblastEffect/BlueElementalBlastEffect) already handle both modes (counter spell OR destroy permanent), but the TargetFilter prevents targeting permanents. Need to change TargetFilter to allow targeting either spells on the stack OR permanents on the battlefield of the appropriate color.

---

### 2. Pyrokinesis — single target instead of divided damage

**Problem:** Uses `DamageEffect(4)` targeting a single creature. Oracle says "4 damage divided as you choose among any number of target creatures."

**Oracle text:** "Pyrokinesis deals 4 damage divided as you choose among any number of target creatures."

**Fix:** Need a DividedDamageEffect that lets the player choose multiple creature targets and divide 4 damage among them. This is complex new engine work. **Acceptable simplification:** keep single-target 4 damage and note it in a comment.

---

### 3. Ensnaring Bridge — checks wrong player's hand

**Problem:** `GameEngine.cs` line 938 uses `attacker.Hand.Cards.Count` (the attacking player's hand). Oracle says "your hand" = the Bridge controller's hand.

**Oracle text:** "Creatures with power greater than the number of cards in your hand can't attack."

**Fix:** For each Ensnaring Bridge found, check that specific Bridge controller's hand size, not the attacker's hand size. Need to track which player controls each Bridge.

---

### 4. Stifle — no targeting, only triggers

**Problem:** Three issues:
1. No TargetFilter — does not target at all, auto-picks first triggered ability
2. Only counters `TriggeredAbilityStackObject`, misses activated abilities on the stack
3. Player has no choice of which ability to counter

**Oracle text:** "Counter target activated or triggered ability. (Mana abilities can't be targeted.)"

**Fix:** Add targeting for stack objects that are TriggeredAbilityStackObject. For activated abilities — the engine doesn't put activated abilities on the stack as separate objects (they resolve immediately), so this part may need to remain simplified. At minimum, let the player choose which triggered ability to counter if multiple exist.

---

### 5. Teferi's Response — wrong targeting, missing effects

**Problem:** Three issues:
1. `TargetFilter.Spell()` targets ANY spell. Oracle: "target spell or ability that targets a land you control"
2. Cannot target abilities (only spells)
3. Missing "If a permanent's ability is countered this way, destroy that permanent"

**Oracle text:** "Counter target spell or ability that targets a land you control. If a permanent's ability is countered this way, destroy that permanent. Draw two cards."

**Fix:** This card is extremely complex to implement correctly (requires tracking what each spell targets). **Acceptable simplification:** Keep current implementation (counter any spell + draw 2) with a code comment noting the simplification.

---

### 6. Circle of Protection: Red / Circle of Protection: Black — prevents all damage, not color-specific

**Problem:** Two issues:
1. `CoPPreventDamageEffect` condition is `(_, _) => true` — prevents ALL damage to the player, not just damage from red/black sources
2. Not single-use — Oracle says "the next time" (one prevention per activation), but implementation prevents all damage for the rest of the turn

**Oracle text (CoP: Red):** "{1}: The next time a red source of your choice would deal damage to you this turn, prevent that damage."

**Fix:** The continuous effect needs to:
1. Only prevent damage from sources of the matching color
2. Be consumed after preventing damage once (single-use shield, not persistent)

This requires source-color tracking in the damage system, which is complex. **Minimum fix:** At least make the prevention single-use (consume after first damage prevention).

---

## Medium Issues (missing abilities, wrong triggers)

### 7. Gloom — missing activated ability cost increase

**Problem:** Only the spell cost increase ({3} more for white spells) is implemented. Missing: "Activated abilities of white enchantments cost {3} more to activate."

**Fix:** Extend the cost modification system to also apply to activated abilities of white enchantments. May need `PreventActivatedAbilities`-style check or new cost modification path.

---

### 8. Wild Mongrel — missing color change

**Problem:** Only +1/+1 pump implemented. Missing: "becomes the color of your choice until end of turn."

**Oracle text:** "Discard a card: Wild Mongrel gets +1/+1 and becomes the color of your choice until end of turn."

**Fix:** The engine doesn't have a card color system (cards derive color from ManaCost). Implementing color change requires adding a `Color` property to GameCard. **Acceptable simplification:** Keep +1/+1 only, note color change is deferred.

---

### 9. Spiritual Focus — fires on any discard

**Problem:** Trigger fires on ANY controller discard (self-initiated like Wild Mongrel, end-of-turn discard, etc.). Oracle: only when "a spell or ability an opponent controls" causes the discard.

**Oracle text:** "Whenever a spell or ability an opponent controls causes you to discard a card, you gain 2 life and you may draw a card."

**Fix:** Would need source tracking on discard events — track whether the discard was caused by an opponent's effect. The engine's `HandleDiscardAsync` doesn't track discard source. **Acceptable simplification:** Keep as-is with code comment.

---

### 10. Sacred Ground — fires on any land-to-graveyard

**Problem:** Trigger fires when ANY of controller's lands go to graveyard from battlefield (including self-sacrifice like fetchlands). Oracle: only when "an opponent's spell or ability" causes it.

**Oracle text:** "Whenever a spell or ability an opponent controls causes a land to be put into your graveyard from the battlefield, return that card to the battlefield."

**Fix:** Same source-tracking issue as Spiritual Focus. **Acceptable simplification:** Keep as-is with code comment.

---

### 11. Spinning Darkness — player chooses exile cards

**Problem:** Alternate cost lets player choose any 3 black cards from graveyard. Oracle requires exiling "the top three black cards" (graveyard order matters in some formats).

**Oracle text:** "You may exile the top three black cards of your graveyard rather than pay this spell's mana cost."

**Fix:** The graveyard in the engine is a list where order is maintained. Change the alternate cost handler to take the top 3 black cards automatically instead of letting the player choose.

---

### 12. Gaea's Blessing — wrong target, wrong mill trigger

**Problem:** Two issues:
1. Effect always targets self (controller). Oracle says "Target player" (should allow targeting opponent for graveyard hosing)
2. `ShuffleGraveyardOnDeath` triggers on discard (hand → graveyard). Oracle: "put into your graveyard **from your library**" (mill only, not discard)

**Oracle text:** "Target player shuffles up to three target cards from their graveyard into their library. You draw a card. When Gaea's Blessing is put into your graveyard from your library, shuffle your graveyard into your library."

**Fix:**
1. Add TargetFilter.Player() and modify GaeasBlessingEffect to use target player's graveyard
2. ShuffleGraveyardOnDeath needs to trigger only from library-to-graveyard (mill), not from discard

---

### 13. Flash of Insight — flashback X hardcoded to 1

**Problem:** `FlashbackCost(ExileBlueCardsFromGraveyard: 1)` hardcodes exiling 1 blue card, making X always 1 when flashbacked.

**Oracle text:** "Flashback—{1}{U}, Exile X blue cards from your graveyard."

**Fix:** The flashback cost should let the player choose how many blue cards to exile (X), and that X determines how many cards to look at. This needs the FlashbackHandler to support dynamic exile count.

---

### 14. Tsabo's Web — second ability not implemented

**Problem:** ETB draw works, but the static ability "Each land with an activated ability that isn't a mana ability doesn't untap during its controller's untap step" is not implemented.

**Fix:** Add a ContinuousEffect that prevents untapping for lands that have non-mana activated abilities (like FetchAbility, or other ActivatedAbilities on lands).

---

### 15. Dystopia — color detection misses tokens/lands

**Problem:** `IsGreenOrWhite` checks ManaCost color requirements. Permanents with null ManaCost (tokens, lands) are never eligible even if they are green/white.

**Fix:** Same systemic issue as Crusade/Anarchy — no card color system. For Dystopia specifically, could add explicit checks for known green/white permanents or check both ManaCost and specific card names.

---

### 16. Perish, Crumble — "can't be regenerated" not enforced

**Problem:** Both cards say the target "can't be regenerated." The engine now has a regeneration shield system (added for River Boa), so this clause should be enforced. Without it, a River Boa with a regeneration shield survives Perish.

**Fix:** Add a `CannotRegenerate` flag to the destroy logic, or bypass regeneration shields for these effects.

---

## Minor Issues (simplifications, acceptable for engine scope)

These are documented for completeness but may not need fixing:

| # | Card(s) | Issue |
|---|---------|-------|
| M1 | Anarchy, Crusade | Color detection via ManaCost misses tokens without mana costs |
| M2 | Simoon | No targeting step for opponent (functionally identical in 2-player) |
| M3 | Portent | Delayed trigger may fire on wrong player's upkeep |
| M4 | Enlightened Tutor | Search marked optional (Oracle mandatory, but "fail to find" is legal in hidden zones) |
| M5 | Earthquake | X determined by leftover mana pool rather than explicit X choice during casting |
| M6 | Carpet of Flowers | Always adds exactly X mana (not "up to X"), no "you may" decline option |
| M7 | Engineered Plague, Meddling Mage | "As enters" (replacement effect) modeled as ETB trigger (can be responded to) |
| M8 | Circular Logic | Auto-pays for opponent rather than giving them the choice |
| M9 | Brain Freeze | Storm resolved inline (multiplied mill) rather than creating stack copies. Also `HasStorm = true` set — verify no double-counting |
| M10 | Battery | Elephant token not marked green (engine-wide token color limitation) |
| M11 | Eternal Dragon | Graveyard return modeled as upkeep trigger, not activated ability |
| M12 | Decree of Silence cycling | No "you may" choice, no targeting for cycling trigger |
| M13 | Cleansing Meditation | Aura enchantments skipped when returning from graveyard to battlefield |

---

## Clean Cards (52 of 88)

These cards passed Oracle verification with no issues:

Yavimaya Coast, Savannah Lions, Annul, Erase, Tranquil Domain, Careful Study, Peek, Accumulated Knowledge, Frantic Search, Price of Progress, Absolute Law, Worship, Sphere of Resistance, Chill, Null Rod, Cursed Totem, True Believer, Nova Cleric, Thornscape Apprentice, Waterfront Bouncer, Aquamoeba, Flametongue Kavu, Warmth, Presence of the Master, Seal of Fire, Ivory Tower, Rejuvenation Chamber, Serenity, Zombie Infestation, Gush, Foil, Mogg Salvage, Roar of the Wurm, Krosan Reclamation, Radiant's Dragoons, Attunement, Soltari Foot Soldier, Soltari Monk, Soltari Priest, Soltari Champion, Xantid Swarm, Basking Rootwalla, Arrogant Wurm, Overload, Orim's Chant, Rancor, River Boa, Assault, Ramosian Sergeant, Phyrexian Dreadnought, Wonder, Kirtar's Desire
