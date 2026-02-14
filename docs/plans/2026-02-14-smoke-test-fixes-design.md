# Smoke Test Fixes — Design Document

**Date:** 2026-02-14
**Scope:** 11 issues from gameplay smoke test — engine bugs, lobby UX, and game board improvements.

---

## Already Fixed (this session)

### Fix A: Mana abilities don't pass priority (Issue #11)
- `GameAction.IsManaAbility` flag set during `TapCard` execution when card has `ManaAbility`
- `RunPriorityAsync` skips pass-flag reset for mana ability actions — player retains priority
- Implements MTG rule 605: mana abilities don't use the stack and don't pass priority

### Fix B: Argothian Enchantress card type (Issues #9, #10)
- Changed from `CardType.Creature | CardType.Enchantment` to `CardType.Creature`
- Argothian Enchantress is "Creature — Human Druid", not an enchantment creature
- This caused incorrect triggers when casting a second Enchantress

---

## Remaining Changes

### 1. Format-filtered deck selection in lobby (Issues #1, #4)

**Create Game flow:**
- Add `MudSelect` for Format (required) above the deck selector
- Deck dropdown filters to decks matching the selected format
- Game stores the chosen format in `GameSession`

**Join Game flow:**
- When navigating to join URL, the page shows the game's format
- Deck dropdown is pre-filtered to that format
- Player can still see the format but cannot change it (it's set by the creator)

**Data change:** `GameSession` gets a `Format` property (string) set during creation.

### 2. AI opponent with deck picker (Issue #2)

**Lobby changes:**
- Add "Play vs AI" `MudCheckBox` below the deck selector in Create Game panel
- When checked, show a second deck dropdown: "AI Deck" (filtered to same format)
- "Create Game" button changes label to "Start Game" when AI is checked
- Skip the waiting-for-opponent screen entirely

**Engine integration:**
- `GameSession.StartAsync` creates `AiBotDecisionHandler` for Player 2 instead of `InteractiveDecisionHandler`
- AI player name: "AI Bot"
- Game navigates directly to `/game/{id}?seat=1` after creation

### 3. Clickable game URL (Issue #3)

**Change:** Replace the plain-text game URL with a `MudLink` component:
```razor
<MudLink Href="@gameUrl" Target="_blank">@gameUrl</MudLink>
```
Opens in new tab by default so the creator's tab stays on the waiting screen.

### 4. Delete deck with confirmation (Issue #5)

**My Decks page:**
- Add a delete `MudIconButton` (trash icon) to each deck row
- Click opens a `MudMessageBox` confirmation dialog: "Delete {deckName}? This cannot be undone."
- On confirm, dispatch `DeleteDeckCommand` via MediatR

**Application layer:**
- New `DeleteDeckCommand` record with `DeckId` and `UserId`
- Handler: load deck, verify ownership, call `context.Remove(deck)`, save
- Validator: DeckId required, non-empty

**After implementation:** Delete "Test Burn Deck (Modern)" and "Testdeck (Modern)" through the new UI.

### 5. Randomize starting player (Issue #6)

**Change in `GameState` constructor:**
```csharp
var rng = Random.Shared;
var startsFirst = rng.Next(2) == 0 ? player1 : player2;
ActivePlayer = startsFirst;
PriorityPlayer = startsFirst;
```

**Game log:** Add entry "Coin flip: {playerName} goes first." at game start.

**MTG rule:** The player who wins the coin flip chooses to play or draw. For simplicity, the winner always plays first (no choice prompt).

### 6. Board-based targeting instead of popup (Issue #7)

**Remove** the target picker popup overlay entirely from `GameBoard.razor`.

**During targeting mode (`Handler.IsWaitingForTarget == true`):**

- **Battlefield cards:** Eligible target cards get an `eligible-target` CSS class (orange glow/pulsing outline). Non-eligible cards are dimmed. Clicking an eligible card submits the target (existing click handlers already do this).
- **Player targets:** Info bars (opponent-info-bar, player-info-bar) get a `targetable` CSS class (orange border glow). Clicking the info bar submits a player target (`TargetInfo` with `Guid.Empty`).
- **Stack targets:** Stack items in `StackDisplay` get the `eligible-target` class. Clicking submits stack target (existing handler).
- **Cancel:** Show a "Cancel targeting" `MudButton` in the phase-actions area of `PhaseBar`.
- **Prompt text:** Show "Choose target for {spellName}" as a small banner above the player battlefield area.

**CSS additions:**
```css
.eligible-target { box-shadow: 0 0 12px rgba(255, 165, 0, 0.7); cursor: pointer; }
.card-display:not(.eligible-target).targeting-mode { opacity: 0.4; }
.opponent-info-bar.targetable, .player-info-bar.targetable {
    outline: 2px solid orange; cursor: pointer;
}
```

### 7. Hide tap option for creatures without abilities (Issue #8)

**ActionMenu change:** Only show the "Tap" option when:
- Card is a land (always show — lands tap for mana), OR
- Card has a `ManaAbility`, OR
- Card has an activated ability that requires tapping

For Argothian Enchantress and similar creatures with no tap abilities, the tap option is hidden from the action menu.

**Implementation:** Pass `HasTapAbility` boolean parameter to `ActionMenu`. Compute from the card: `card.IsLand || card.ManaAbility != null`.

---

## Implementation Order

| Priority | Item | Complexity | Type |
|----------|------|-----------|------|
| 1 | #5 Randomize starting player | Small | Engine |
| 2 | #7 Hide tap for non-ability creatures | Small | UI |
| 3 | #3 Clickable game URL | Trivial | UI |
| 4 | #1/#4 Format-filtered decks in lobby | Medium | UI + GameSession |
| 5 | #4 Delete deck with confirmation | Medium | Full stack |
| 6 | #6 Board-based targeting | Medium | UI rework |
| 7 | #2 AI opponent with deck picker | Medium | UI + Engine integration |

Items 1-3 are quick wins (< 30 min each). Items 4-7 are medium effort.

---

## Out of Scope

- Stack display visual ordering (trigger appears below spell) — noted but not in this batch
- Impulse card implementation (needs `RevealAndChoose` effect) — separate feature
- Commander format rules — deferred per existing decision
