# Game UI Redesign: MTGO-Inspired Layout

## Goal

Redesign the game UI to maximize screen real estate, mimic MTGO's proven layout patterns, and add phase stop settings for faster gameplay. Replace prompt-based combat with direct battlefield click interaction.

## Current Problems

1. **Wasted vertical space** — opponent zone shows face-down hand cards (useless information)
2. **Small cards** (100px) — hard to read card details
3. **Game log always visible** — 300px right sidebar permanently consumes horizontal space
4. **Prompt-based combat** — separate panels for declaring attackers/blockers instead of clicking creatures
5. **No phase stops** — must manually pass priority through every phase
6. **No land/creature separation** — all permanents mixed in one flex-wrap area
7. **Phase indicator is a single chip** — no visibility into the full turn structure

---

## Layout Structure

Full viewport grid, no scrolling needed for normal board states:

```
┌──────────────────────────────────────────────────────────────┐
│ Opponent Name │ ♥ 20 │ Hand: 5 │ Grave: 2 │ Exile: 0 │ Lib: 45 │
├──────────────────────────────────────────────────────────────┤
│ Opponent Lands (~90px cards, row)                             │
│ Opponent Creatures/Enchantments (~90px cards, row)            │
├──────────────────────────────────────────────────────────────┤
│ Phase Bar (flat strip, all steps):                            │
│ [Untap][Upkeep][Draw][Main][BeginCbt][Atk][Blk][Dmg][EndCbt][Main][End] │
│   ○      ○      ○    ●       ○       ●    ●    ○     ○      ●    ○   │
│ + Stack items (if any) + [Pass] [Undo] [⚑ Surrender]         │
├──────────────────────────────────────────────────────────────┤
│ Your Creatures/Enchantments (130px cards, row)                │
│ Your Lands (130px cards, row)                                 │
├──────────────────────────────────────────────────────────────┤
│ Your Name │ ♥ 20 │ Mana: ⚪⚪🟢🟢 │ Grave: 1 │ Exile: 0     │
├──────────────────────────────────────────────────────────────┤
│ Your Hand (130px cards, centered along bottom)                │
└──────────────────────────────────────────────────────────────┘
                         + [📜] toggle → game log slide-over from right
```

### Grid Rows (CSS Grid)

```
.game-board {
    display: grid;
    grid-template-rows:
        auto          /* opponent info bar */
        1fr           /* opponent battlefield (lands + creatures) */
        auto          /* phase bar + stack */
        2fr           /* your battlefield (creatures + lands) */
        auto          /* your info bar */
        auto;         /* your hand */
    height: 100vh;
    padding: 4px;
}
```

- Opponent battlefield gets `1fr`, yours gets `2fr` — your zone is bigger and more important
- Info bars are `auto` height (single line)
- Phase bar is `auto` height
- Hand is `auto` height (one row of cards)

---

## Phase Bar

### Visual Design

Horizontal strip showing all 11 turn steps in order. Each step is a clickable toggle for phase stops.

```
[Untap] [Upkeep] [Draw] [Main₁] [Begin Combat] [Attackers] [Blockers] [Damage] [End Combat] [Main₂] [End]
```

**Visual states per step:**
- **Inactive + no stop**: Dimmed text, empty dot below (○)
- **Inactive + stop enabled**: Normal text, filled dot below (●)
- **Active phase**: Highlighted background (amber/gold), bold text
- **Past phase (this turn)**: Slightly brighter than inactive to show progression

**Click behavior:** Click any phase label to toggle its stop on/off.

### Mapping Phase + CombatStep to UI

The engine uses two separate enums. The UI maps them to a flat strip:

| UI Label | Engine Source | Default Stop |
|----------|-------------|--------------|
| Untap | Phase.Untap | No |
| Upkeep | Phase.Upkeep | No |
| Draw | Phase.Draw | No |
| Main | Phase.MainPhase1 | **Yes** |
| Begin Combat | CombatStep.BeginCombat | No |
| Attackers | CombatStep.DeclareAttackers | **Yes** |
| Blockers | CombatStep.DeclareBlockers | **Yes** |
| Damage | CombatStep.CombatDamage | No |
| End Combat | CombatStep.EndCombat | No |
| Main | Phase.MainPhase2 | **Yes** |
| End | Phase.End | No |

### Right Side of Phase Bar

- **Pass Priority** button (or "Done" / "Confirm" during combat)
- **Undo** button (outlined, warning color when available)
- **Surrender** button (small flag icon)

---

## Phase Stops (Engine Integration)

### New Type: PhaseStopSettings

```csharp
public class PhaseStopSettings
{
    public HashSet<Phase> PhaseStops { get; } = new() { Phase.MainPhase1, Phase.MainPhase2 };
    public HashSet<CombatStep> CombatStops { get; } = new() { CombatStep.DeclareAttackers, CombatStep.DeclareBlockers };

    public bool ShouldStop(Phase phase) => PhaseStops.Contains(phase);
    public bool ShouldStop(CombatStep step) => CombatStops.Contains(step);
}
```

### Engine Changes

Where the engine currently calls `ChooseAction` for priority, add a stop check:

1. Check if there's a stop set for the current phase/combat step
2. If no stop AND stack is empty AND no pending triggers: **auto-pass** (don't call `ChooseAction`)
3. If stop is set OR stack is non-empty: call `ChooseAction` as normal
4. Always stop when opponent takes an action (reactive priority)

### Where PhaseStopSettings Lives

- On `IPlayerDecisionHandler` as a property, or passed through `InteractiveDecisionHandler`
- `AiBotDecisionHandler` ignores stops (AI always gets full priority)
- `InteractiveDecisionHandler` checks stops before surfacing priority to the UI
- Persisted in browser `localStorage` so settings survive page refresh

---

## Combat UX (Click-to-Attack/Block)

### Declare Attackers

1. Phase bar highlights "Attackers" step
2. Eligible untapped creatures get a subtle green glow/outline
3. **Click an eligible creature** → taps it, adds red "ATK" indicator and red border glow
4. **Click an attacking creature** → untaps it, removes attacker status
5. Phase bar "Pass" button changes to "Confirm Attacks" / "Skip Combat"
6. Confirm → proceeds to Declare Blockers (or damage if no blockers possible)

### Declare Blockers

1. Phase bar highlights "Blockers" step
2. Opponent's attacking creatures shown with red "ATK" indicators
3. Your eligible untapped creatures get a subtle green glow
4. **Click your creature** → highlights as "selected blocker" (blue outline)
5. **Click an attacker** → assigns the block (visual line/arrow, blue "BLK" indicator)
6. Click your blocking creature again → removes block assignment
7. Phase bar shows "Confirm Blocks" / "No Blocks"

### Blocker Ordering (Multi-Block)

When multiple creatures block one attacker:
1. Small numbered overlay appears on each blocker
2. Click blockers in damage priority order (1st, 2nd, 3rd...)
3. "Confirm Order" button

### No Separate Prompt Panels

All combat interaction happens directly on the battlefield through clicking. The only UI addition is the phase bar buttons changing context (Confirm/Skip).

---

## Opponent Zone (Compact)

### Info Bar (Single Line)

```
[Opponent Name] [♥ 20] [Hand: 5] [Grave: 2] [Exile: 0] [Library: 45]
```

- Life total prominent
- Zone counts as small chips — clickable to peek at contents (graveyard/exile popup)
- Mana pool shown inline if non-empty (same Scryfall SVG symbols)
- No face-down hand cards displayed

### Battlefield

- Two rows: lands on top, non-lands below
- Cards are ~90px wide — readable but not dominant
- Tapped cards rotated 90° as now
- Combat indicators (ATK/BLK) shown as now

---

## Your Zone (Full Detail)

### Battlefield

- Two rows: non-land permanents on top (closer to combat zone), lands below
- Cards are 130px wide
- Tapped cards rotated 90°
- Eligible cards glow during combat phases

### Info Bar (Single Line)

```
[Your Name] [♥ 20] [Mana: ⚪⚪🟢🟢] [Grave: 1] [Exile: 0] [Library: 50]
```

- Mana pool with Scryfall SVG symbols (same as current)
- Graveyard/Exile clickable to peek

### Hand

- Full-width row at very bottom of screen
- 130px cards, centered
- Click a card for contextual action (Play Land / Cast Spell / Cycle)

---

## Card Interaction (Click Actions)

### Your Battlefield

| Card State | Click Action |
|-----------|-------------|
| Untapped land (single mana) | Tap for mana immediately |
| Untapped land (choice, e.g. Brushland) | Show inline mana color picker |
| Untapped creature (combat phase) | Toggle attack/block |
| Untapped creature (main phase, has ability) | Show action menu: Activate |
| Enchantment with activated ability | Show action menu: Activate |
| Tapped permanent | No action (or info popup) |
| Any permanent during targeting | Select as target |

### Your Hand

| Card Type | Click Action |
|-----------|-------------|
| Land (land drop available) | Play land |
| Spell (affordable) | Cast spell (auto-tap or mana picker) |
| Spell (not affordable) | Show "insufficient mana" indicator |
| Card with cycling | Show menu: Cast / Cycle |

### Opponent's Battlefield

| Context | Click Action |
|---------|-------------|
| During targeting | Select as target |
| Otherwise | Show card zoom/details |

---

## Game Log (Slide-Over)

### Toggle Button

Small icon button (📜 or document icon) in the top-right corner of the game board, always visible. Badge shows count of new entries since last opened.

### Panel

- Slides in from right edge, ~350px wide
- Semi-transparent dark overlay behind it
- Same monospace log as current implementation
- Auto-scrolls to bottom on new entries
- Click outside or toggle button to close
- Stays open during gameplay until manually closed

---

## Stack Display

When the stack has items:
- Show below the phase bar as a horizontal strip
- Each stack item is a compact chip/card showing: spell name, targets (if any)
- Top of stack (next to resolve) highlighted
- Stack clears visually as items resolve

When stack is empty: nothing shown (phase bar uses full width).

---

## Summary of Changes

### UI Components to Modify

| Component | Change |
|-----------|--------|
| GameBoard.razor | New CSS grid layout, remove right sidebar |
| PlayerZone.razor | Split into OpponentZone + PlayerZone, land/creature row separation |
| CardDisplay.razor | Support 90px and 130px sizes, combat eligible glow |
| GameLogPanel.razor | Convert to slide-over overlay |
| New: PhaseBar.razor | Full MTGO-style phase strip with stop toggles |
| New: StackStrip.razor | Horizontal inline stack display |
| Remove prompt panels | Combat prompts replaced by click interaction |

### Engine Changes

| Change | Scope |
|--------|-------|
| PhaseStopSettings type | New class |
| InteractiveDecisionHandler | Check stops before surfacing priority |
| Auto-pass logic | Skip ChooseAction when no stop + empty stack |

### No Engine Changes Needed For

- Click-to-attack (UI translates clicks into same `DeclareAttackers` list)
- Land/creature row separation (purely visual)
- Card sizing (purely CSS)
- Game log toggle (purely UI)
