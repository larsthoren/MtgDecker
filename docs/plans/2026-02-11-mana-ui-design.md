# Mana System UI Integration Design

## Goal

Wire the engine's mana system into the game UI so that cards use their registered definitions (ManaCost, ManaAbility, CardTypes), mana pools are visible, and tapping lands for mana works interactively.

## Changes

### 1. GameCard.Create() Fix

**File:** `GameLobby.razor`

Change `new GameCard { Name, TypeLine, ImageUrl }` to `GameCard.Create(name, typeLine, imageUrl)` so cards loaded from the database get their ManaCost, ManaAbility, Power, Toughness, and CardTypes from the `CardDefinitions` registry. Without this, all cards are sandbox (free to play, no mana abilities).

### 2. Mana Pool Display

**File:** `PlayerZone.razor`, `PlayerZone.razor.css`

Both player zones show current mana pool in the zone header, after the library count / draw button. Mana is displayed using Scryfall SVG mana symbols (`https://svgs.scryfall.io/card-symbols/{letter}.svg` where letter is W, U, B, R, G, C) at 20x20px with a count number beside each.

- Only colors with amount > 0 are shown.
- Order follows WUBRG+C convention: White, Blue, Black, Red, Green, Colorless.
- Empty pool shows nothing.
- Both players' pools are visible (mana pools are public information in MTG).

`GameBoard.razor` passes the `ManaPool` object to each `PlayerZone`.

### 3. Card Click Behavior on Battlefield

**File:** `PlayerZone.razor`

Clicking a card on the battlefield has context-dependent behavior:

| Card State | Click Result |
|---|---|
| Untapped land, fixed mana ability (basic lands) | Instantly dispatch `TapCard` action — no action menu |
| Untapped land, choice mana ability (dual lands) | Show inline mana color picker |
| Everything else (tapped cards, creatures, etc.) | Normal action menu |

**Inline color picker:** When an untapped dual land is clicked, instead of the action menu, show clickable Scryfall SVG mana symbol buttons for the available colors plus a Cancel button. Clicking a color submits the choice via `InteractiveDecisionHandler.SubmitManaColor()`, which unblocks the engine, taps the land, and produces the chosen mana.

**Flow for choice lands:**
1. Player clicks untapped dual land on battlefield.
2. `PlayerZone` dispatches `GameAction.TapCard` via `OnAction`.
3. Engine calls `ChooseManaColor` on the handler, blocking until resolved.
4. `OnWaitingForInput` fires, UI detects `Handler.IsWaitingForManaColor == true`.
5. Inline color picker appears showing available colors.
6. Player clicks a color. UI calls `Handler.SubmitManaColor(color)`.
7. Engine unblocks, mana added, UI refreshes.

### 4. Auto-Pay Generic Costs

**File:** `InteractiveDecisionHandler.cs`

`ChooseGenericPayment` is changed to auto-resolve immediately (no `TaskCompletionSource`, no UI blocking). It returns an auto-calculated payment paying from the largest pool first, matching the engine's existing unambiguous auto-pay logic.

This eliminates the need for any generic payment UI. The `IsWaitingForGenericPayment` and `SubmitGenericPayment` methods become unused but are left in place for potential future use.

### 5. InteractiveDecisionHandler Changes

**File:** `InteractiveDecisionHandler.cs`

- Add `ManaColorOptions` property (`IReadOnlyList<ManaColor>?`) to expose the available colors when `IsWaitingForManaColor` is true. Set in `ChooseManaColor`, cleared after submission.
- Change `ChooseGenericPayment` to return immediately with auto-pay result.

## Files Summary

| File | Change |
|---|---|
| `GameLobby.razor` | `new GameCard` → `GameCard.Create()` |
| `PlayerZone.razor` | Mana pool display, smart click behavior, inline color picker |
| `PlayerZone.razor.css` | Styles for mana symbols and color picker |
| `GameBoard.razor` | Pass `ManaPool` to PlayerZones, detect mana choice state |
| `InteractiveDecisionHandler.cs` | `ManaColorOptions` property, auto-pay generic costs |

## Visual Assets

Mana symbols from Scryfall CDN (same source as card images):
- `https://svgs.scryfall.io/card-symbols/W.svg` (White)
- `https://svgs.scryfall.io/card-symbols/U.svg` (Blue)
- `https://svgs.scryfall.io/card-symbols/B.svg` (Black)
- `https://svgs.scryfall.io/card-symbols/R.svg` (Red)
- `https://svgs.scryfall.io/card-symbols/G.svg` (Green)
- `https://svgs.scryfall.io/card-symbols/C.svg` (Colorless)
