# Mana System Design

**Goal:** Add a mana system to the game engine — mana pool, land tapping for mana, casting costs, and mana payment flow. Hardcode card definitions for the two starter decks (Legacy Goblins + Legacy Enchantress).

**Scope:** Engine only. Cards not in the registry continue working in sandbox mode (no mana required).

---

## 1. Mana Pool and Colors

ManaPool lives on Player, tracks available mana by color.

**ManaColor enum:** W (White), U (Blue), B (Black), R (Red), G (Green), Colorless.

**ManaPool:**
- `Dictionary<ManaColor, int>` tracking available mana per color.
- `Add(ManaColor, int amount)` — adds mana to pool.
- `CanPay(ManaCost cost)` — checks if pool can cover a cost.
- `Pay(ManaCost cost)` — deducts mana (for unambiguous payments).
- `Clear()` — empties pool. Called at phase transitions.
- `Total` — sum of all mana in pool.
- Pool resets (empties) at the end of each phase.

## 2. Mana Cost

Represents what a card costs to cast. Parsed from Scryfall-style strings like `{1}{R}{R}`.

**ManaCost:**
- `Dictionary<ManaColor, int>` for colored requirements (e.g., R=2).
- `int GenericCost` for the generic portion (payable with any color).
- `int ConvertedManaCost` — total (generic + all colored).
- `static Parse(string manaCostString)` — parses `{1}{R}{R}` format.

## 3. Mana Abilities

What a card produces when tapped for mana.

**ManaAbility types:**
- **Fixed:** produces exactly one color (basic lands). Tap Mountain = add {R}.
- **Choice:** player picks from options (pain lands). Tap Karplusan Forest = choose {C}, {R}, or {G}.
- **Special:** scripted (Serra's Sanctum, Skirk Prospector). Deferred to later.

## 4. Tapping Lands for Mana

Currently tapping just sets `IsTapped = true`. With the mana system:

- If the card has a `ManaAbility`:
  - **Fixed:** auto-add mana to pool, set tapped.
  - **Choice:** prompt player via `DecisionHandler.ChooseManaColor(options)`, add chosen color, set tapped.
- If the card has no `ManaAbility`: tap works as before (no mana produced).

### Land mana production (starter decks):

| Land | Produces |
|------|----------|
| Mountain | {R} |
| Forest | {G} |
| Plains | {W} |
| Karplusan Forest | choose {C}, {R}, or {G} |
| Brushland | choose {C}, {G}, or {W} |
| Wooded Foothills | Sacrifice, search library for Mountain or Forest, put into play (later) |
| Windswept Heath | Sacrifice, search library for Forest or Plains, put into play (later) |
| Serra's Sanctum | {W} per enchantment you control (later) |
| Rishadan Port | {C} (later) |
| Wasteland | {C} (later) |

**Implementation tiers:**
- Tier 1 (MVP): Basic lands + pain lands.
- Tier 2: Fetchlands (sacrifice, search library).
- Tier 3: Special lands (Sanctum, Port, Wasteland).

## 5. Casting Spells — Payment Flow

When a player casts a spell:

1. Player selects a card in hand and chooses "Cast."
2. Engine looks up `ManaCost` from CardDefinitions.
3. Engine checks `ManaPool`:
   - **Enough mana, unambiguous:** auto-deduct colored requirements first, then generic. Move card to battlefield (permanent) or graveyard (instant/sorcery). Done.
   - **Enough mana, ambiguous generic:** colored requirements are auto-deducted. Player is prompted to choose which color(s) to spend on generic cost via `DecisionHandler.ChooseGenericPayment(remaining, availableColors)`.
   - **Not enough mana:** show what's needed minus what's in pool. Player taps lands to fill pool, then confirms. Player can cancel (mana already tapped stays in pool).
4. Card resolves (no stack in MVP — instant resolution).

**Ambiguity rule:** Generic mana `{1}` can be paid with any color. If pool has multiple colors remaining after paying colored requirements, the player chooses which to spend on generic.

**Land drops:** Lands have no mana cost. Playing a land uses the existing PlayCard action but is gated by `Player.LandsPlayedThisTurn` (max 1 per turn, reset at turn start). Cards without a registry entry bypass this check (sandbox mode).

## 6. Card Definitions Registry

Static dictionary mapping card name to game properties. Cards not in the registry work in sandbox mode.

**Registry entry fields:**
- `ManaCost` — cost to cast (null for lands).
- `ManaAbility` — what tapping produces (null for non-mana sources).
- `Power`, `Toughness` — creature stats (null for non-creatures).
- `CardTypes` — flags: Land, Creature, Enchantment, Instant, Sorcery, Artifact.

### Starter deck registry (maindeck only):

**Goblins:**
```
Goblin Lackey        | {R}       | 1/1  | Creature
Goblin Matron        | {2}{R}    | 1/1  | Creature
Goblin Piledriver    | {1}{R}    | 1/2  | Creature
Goblin Ringleader    | {3}{R}    | 2/2  | Creature
Goblin Warchief      | {1}{R}{R} | 2/2  | Creature
Mogg Fanatic         | {R}       | 1/1  | Creature
Gempalm Incinerator  | {1}{R}    | 2/1  | Creature
Siege-Gang Commander | {3}{R}{R} | 2/2  | Creature
Goblin King          | {1}{R}{R} | 2/2  | Creature
Goblin Pyromancer    | {3}{R}    | 2/2  | Creature
Goblin Sharpshooter  | {2}{R}    | 1/1  | Creature
Goblin Tinkerer      | {1}{R}    | 1/1  | Creature
Skirk Prospector     | {R}       | 1/1  | Creature (mana ability later)
Naturalize           | {1}{G}    | —    | Instant
Mountain             | —         | —    | Land, produces {R}
Forest               | —         | —    | Land, produces {G}
Karplusan Forest     | —         | —    | Land, produces choose {C}/{R}/{G}
Wooded Foothills     | —         | —    | Land (fetchland, later)
Rishadan Port        | —         | —    | Land (special, later)
Wasteland            | —         | —    | Land (special, later)
```

**Enchantress:**
```
Argothian Enchantress   | {1}{G}    | 0/1  | Creature Enchantment
Swords to Plowshares    | {W}       | —    | Instant
Replenish               | {3}{W}    | —    | Sorcery
Enchantress's Presence  | {2}{G}    | —    | Enchantment
Wild Growth             | {G}       | —    | Enchantment (mana ability later)
Exploration             | {G}       | —    | Enchantment
Mirri's Guile           | {G}       | —    | Enchantment
Opalescence             | {2}{W}{W} | —    | Enchantment
Parallax Wave           | {2}{W}{W} | —    | Enchantment
Sterling Grove          | {G}{W}    | —    | Enchantment
Aura of Silence         | {1}{W}{W} | —    | Enchantment
Seal of Cleansing       | {1}{W}    | —    | Enchantment
Solitary Confinement    | {2}{W}    | —    | Enchantment
Sylvan Library          | {1}{G}    | —    | Enchantment
Forest                  | —         | —    | Land, produces {G}
Plains                  | —         | —    | Land, produces {W}
Brushland               | —         | —    | Land, produces choose {C}/{G}/{W}
Windswept Heath         | —         | —    | Land (fetchland, later)
Serra's Sanctum         | —         | —    | Land (special, later)
Wooded Foothills        | —         | —    | Land (fetchland, later)
```

## 7. Decision Handler Extensions

`IPlayerDecisionHandler` gets new methods:

- `ChooseManaColor(IReadOnlyList<ManaColor> options)` — for pain lands. Returns chosen color.
- `ChooseGenericPayment(int genericAmount, Dictionary<ManaColor, int> available)` — for ambiguous generic costs. Returns which colors to spend.

## 8. Architecture

All new types in `MtgDecker.Engine`:

```
Engine/
  Enums/
    ManaColor.cs
    CardType.cs
  Mana/
    ManaCost.cs
    ManaPool.cs
    ManaAbility.cs
  CardDefinitions.cs
  GameCard.cs         (extended)
  Player.cs           (add ManaPool, LandsPlayedThisTurn)
  GameState.cs        (clear pools at phase transitions)
  GameEngine.cs       (CastSpell flow, tap-for-mana, land drop tracking)
```

**Key behavioral changes:**
- TapCard splits: tap-for-mana (adds to pool) vs tap-without-mana.
- PlayCard becomes CastSpell for cards with ManaCost. Lands use PlayCard with land drop limit.
- Mana pools clear at end of each phase.
- Land drop counter resets at turn start.
- Cards without registry entries continue working in sandbox mode.

## 9. Summary of Changes

| File | Change |
|------|--------|
| `Engine/Enums/ManaColor.cs` | New enum |
| `Engine/Enums/CardType.cs` | New flags enum |
| `Engine/Mana/ManaCost.cs` | New class — cost representation + parsing |
| `Engine/Mana/ManaPool.cs` | New class — per-player mana tracking |
| `Engine/Mana/ManaAbility.cs` | New class — fixed/choice mana production |
| `Engine/CardDefinitions.cs` | New static registry |
| `Engine/GameCard.cs` | Add ManaCost?, ManaProduction?, Power?, Toughness?, CardTypes |
| `Engine/Player.cs` | Add ManaPool, LandsPlayedThisTurn |
| `Engine/GameState.cs` | Clear mana pools at phase transitions |
| `Engine/GameEngine.cs` | CastSpell flow, tap-for-mana, land drop enforcement |
| `Engine/IPlayerDecisionHandler.cs` | Add ChooseManaColor, ChooseGenericPayment |
