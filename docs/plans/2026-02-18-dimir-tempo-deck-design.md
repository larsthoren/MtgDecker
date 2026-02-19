# Dimir Tempo Legacy Deck — Design Document

**Goal:** Add the first of four 2026 Legacy pre-built decks (Dimir Tempo) to MtgDecker, with full engine support for all cards. This requires building four new engine subsystems: draw tracking, planeswalkers, transform cards, and adventure cards.

**Approach:** Build engine features as separate PRs, then register all cards and seed the deck. One deck at a time — Dimir Tempo first, then Izzet Delver, Show and Tell, and Beanstalk Control in future cycles.

---

## Decklist (2026 Legacy Dimir Tempo)

Source: MTGGoldfish, February 2026.

**Mainboard (60):**
- 4 Tamiyo, Inquisitive Student
- 4 Orcish Bowmasters
- 3 Murktide Regent
- 2 Kaito, Bane of Nightmares
- 4 Brainstorm
- 4 Ponder
- 4 Thoughtseize
- 4 Force of Will
- 4 Fatal Push
- 3 Daze
- 2 Brazen Borrower
- 1 Snuff Out
- 4 Polluted Delta
- 4 Underground Sea
- 4 Wasteland
- 2 Flooded Strand
- 1 Scalding Tarn
- 1 Misty Rainforest
- 1 Bloodstained Mire
- 1 Undercity Sewers
- 1 Island
- 1 Swamp

---

## Cards Already Registered (12)

Brainstorm, Ponder, Force of Will, Daze, Murktide Regent, Snuff Out, Wasteland, Flooded Strand, Scalding Tarn, Bloodstained Mire, Island, Swamp.

## New Cards Needed (10)

### Simple Cards (no new engine features)

| Card | Type | Mana | Implementation |
|------|------|------|---------------|
| Polluted Delta | Fetchland | — | FetchAbility for Island or Swamp |
| Underground Sea | Dual land | — | Taps for U or B, subtypes Island Swamp |
| Misty Rainforest | Fetchland | — | FetchAbility for Forest or Island |
| Undercity Sewers | Land | — | ETB tapped, surveil 1, taps for U or B |
| Thoughtseize | B Sorcery | {B} | Target reveals hand, choose nonland, discard, lose 2 life |
| Fatal Push | B Instant | {B} | Destroy creature CMC ≤ 2 (revolt: CMC ≤ 4) |

### Complex Cards (require engine features)

| Card | Mana | Engine Feature | Description |
|------|------|---------------|-------------|
| Orcish Bowmasters | {1}{B} | Draw tracking + Amass | Flash 1/1. ETB + opponent-draws-except-first: amass Orcs 1, deal 1 to any target |
| Kaito, Bane of Nightmares | {1}{U}{B} | Planeswalker system | Planeswalker with loyalty abilities (verify exact text) |
| Tamiyo, Inquisitive Student | {U} | Transform + Planeswalker | 0/3 creature, study counters, transforms into planeswalker |
| Brazen Borrower | {1}{U}{U} | Adventure cards | 3/1 Flash Flying; Adventure: Petty Theft {1}{U} bounce nonland |

---

## Engine Feature 1: Draw Tracking + Amass (PR 1)

### Draw Tracking

**Problem:** Orcish Bowmasters triggers "whenever an opponent draws a card except the first one they draw in each of their draw steps." Need per-player draw counting.

**Changes:**
- `Player.DrawsThisTurn` (int) — incremented every time a card is drawn
- Reset to 0 at turn start (in GameEngine turn-start logic)
- New `TriggerCondition.OpponentDrawsExceptFirst` — checked after each draw
- New `GameEvent.CardDrawn` if not already present — fires after each draw with the drawing player as context

### Amass Mechanic

**Problem:** "Amass Orcs 1" = if you control an Army creature, put a +1/+1 counter on it; otherwise create a 0/0 black Orc Army creature token, then put a +1/+1 counter on it.

**Changes:**
- New `AmassEffect` implementing `IEffect`
- Checks controller's battlefield for a creature with subtype "Army"
- If found: add +1/+1 counter to it
- If not found: create 0/0 Orc Army token, then add +1/+1 counter
- New `CounterType.PlusOnePlusOne` (if not already present)
- Token creation reuses existing token infrastructure

---

## Engine Feature 2: Planeswalker System (PR 2)

### Core Mechanics

**GameCard changes:**
- `int? Loyalty` — current loyalty counter value
- `int? StartingLoyalty` — from CardDefinition
- On ETB for planeswalkers: `Loyalty = StartingLoyalty`

**CardDefinition changes:**
- `int? StartingLoyalty` — initial loyalty
- `IReadOnlyList<LoyaltyAbility>? LoyaltyAbilities` — loyalty ability list
- `record LoyaltyAbility(int LoyaltyChange, IEffect Effect, TargetFilter? TargetFilter = null)`

### Activation Rules

- One loyalty ability per planeswalker per turn
- Sorcery speed only (your main phase, stack empty)
- Loyalty change is a cost (add/remove counters before ability goes on stack)
- Cannot activate if loyalty would go below 0
- Goes on stack as activated ability (new `ActivatedLoyaltyAbilityStackObject`?)

### Combat

- During declare attackers, attacking player can assign each attacker to attack the defending player OR a specific planeswalker the defending player controls
- `CombatState` tracks per-attacker: `Guid? AttackingPlaneswalker`
- Blockers assigned normally by defending player
- Combat damage from unblocked attackers hitting a planeswalker removes loyalty instead of dealing player damage

### State-Based Actions

- Planeswalker with loyalty ≤ 0 → put into graveyard (add to `CheckStateBasedActions`)

---

## Engine Feature 3: Transform Cards (PR 3)

### Architecture

**CardDefinition changes:**
- `CardDefinition? TransformInto` — back face definition (null for non-transform cards)
- Back face has its own name, types, P/T, abilities, starting loyalty, etc.

**GameCard changes:**
- `bool IsTransformed` — showing back face
- `CardDefinition? BackFaceDefinition` — cached from registry
- When `IsTransformed = true`, characteristics read from back face:
  - Name, CardTypes, Power, Toughness, Loyalty, Triggers, ContinuousEffects, etc.

### Transform Trigger

- Tamiyo-specific: "When Tamiyo has 3+ study counters, exile her, return transformed"
- Implementation: `TransformExileReturnEffect` — exiles the card, sets `IsTransformed = true`, moves to battlefield
- Study counter placement via existing `TriggerCondition` + `AddCountersEffect`

### Transform in Zones

- Only the front face exists in zones other than the battlefield (hand, library, graveyard)
- On the battlefield, the currently-showing face determines all characteristics
- Mana cost is always the front face (for CMC calculations)

---

## Engine Feature 4: Adventure Cards (PR 4)

### Architecture

**CardDefinition changes:**
- `AdventurePart? Adventure` — the instant/sorcery half
- `record AdventurePart(string Name, ManaCost Cost, SpellEffect? Effect, TargetFilter? Filter)`

**GameCard changes:**
- `bool IsOnAdventure` — true when in exile after adventure half resolved
- When in exile with `IsOnAdventure = true`, can be cast as the creature

### Cast Flow

1. Player has adventure card in hand
2. When casting, player chooses: Adventure mode or Creature mode
3. **Adventure mode:** Cast as instant/sorcery with adventure's mana cost and effect
   - On resolution: card goes to exile (not graveyard), `IsOnAdventure = true`
4. **From exile (adventure):** Cast as creature with the main card's mana cost
   - On resolution: enters battlefield normally, `IsOnAdventure = false`

### Brazen Borrower Specifics

- Adventure: Petty Theft — {1}{U} instant, return target nonland permanent an opponent controls to owner's hand
- Creature: {1}{U}{U}, 3/1, Flash, Flying, can't block
- "Can't block" is a static ability (new restriction)

---

## Additional Mechanics

### Revolt (for Fatal Push)

- Track `bool PermanentLeftBattlefieldThisTurn` per player
- Set to true whenever a permanent the player controls leaves the battlefield
- Reset at turn start
- Fatal Push checks this for the CMC ≤ 4 mode

### Surveil (for Undercity Sewers)

- "Surveil 1" = look at top card of library, put it back or into graveyard
- Requires player decision (keep on top vs mill)
- New `SurveilEffect` implementing `IEffect`

---

## Deck Seeding (PR 5)

Extend the existing seed pattern in `Program.cs`:

```csharp
if (!existingDecks.Any(d => d.Name == "Legacy Dimir Tempo"))
{
    var dimirTempoDeck = """
        4 Tamiyo, Inquisitive Student
        4 Orcish Bowmasters
        3 Murktide Regent
        ...
        """;
    var result = await mediator.Send(new ImportDeckCommand(
        dimirTempoDeck, "MTGO", "Legacy Dimir Tempo", Format.Legacy, seedUserId));
}
```

Cards must exist in the database (from Scryfall bulk import) for `ImportDeckCommand` to resolve them by name.

---

## PR Sequence

| PR | Feature | Scope | Cards Enabled |
|----|---------|-------|---------------|
| 1 | Draw tracking + Amass | Engine feature | Orcish Bowmasters |
| 2 | Planeswalker system | Engine feature | Kaito |
| 3 | Transform cards | Engine feature | Tamiyo (needs PR 2 too) |
| 4 | Adventure cards | Engine feature | Brazen Borrower |
| 5 | Card registrations + deck seed | Cards + seed data | All 10 new cards + deck |

**Total estimated new cards:** 10 (6 simple + 4 complex)
**Total engine features:** 4 (draw tracking, planeswalkers, transform, adventure)
**Total PRs for Dimir Tempo:** 5

---

## Open Questions (verify during planning)

- Kaito, Bane of Nightmares: exact loyalty abilities (need web lookup)
- Tamiyo back face: exact planeswalker abilities (need web lookup)
- Fatal Push revolt: confirm implementation approach
- Undercity Sewers: confirm surveil implementation
