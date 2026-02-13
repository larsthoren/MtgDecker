# UI Enforcement Design

## Goal

Strip all sandbox/free actions from the game UI so that every player action goes through engine-enforced rules. Replace removed features with properly enforced alternatives where needed.

## Actions to Remove

### MoveCard (free zone movement)
Delete entirely: `ActionType.MoveCard`, `GameAction.MoveCard()` factory, the engine's MoveCard case in `ExecuteAction`, MoveCard undo handling, and the "Move to [Zone]" submenu in `ActionMenu.razor`. Cards only change zones through engine-enforced actions (play, cast, combat, effects).

### Draw Card button
Delete the `+` draw button from `GameBoard.razor`'s player info bar, the `OnDrawCard` event callback, and `GameSession.DrawCard`. Drawing only happens through the engine's draw step and card effects.

### Life +1/-1 buttons
Delete the HP adjustment buttons from `GameBoard.razor`, the `OnLifeAdjust` event callback, and `GameSession.AdjustLife`. Life changes only through engine-controlled damage and effects.

### Manual Untap button
Delete the "Untap" option from `ActionMenu.razor`. Cards only untap during the untap step (already handled by the engine).

### Sandbox play fallback
In `GameEngine.ExecuteAction` for PlayCard, remove the path that allows cards without a `CardDefinition` to enter the battlefield for free. Reject the action with a log message: "Card not supported in engine."

### Blanket Undo button
Remove the current global undo system. Replace with scoped undo (see below).

## Changes to Add

### Summoning sickness enforcement
When a player taps a non-land permanent, the engine checks if the card has summoning sickness. If it does, reject the tap with a log message: "{Card} has summoning sickness."

- Lands are exempt (can always tap for mana).
- Mana abilities on lands remain instant-speed during priority.
- Creatures with activated tap-cost abilities are gated by summoning sickness.
- Track via `GameCard` — compare the turn the creature entered the battlefield to the current turn. Clear summoning sickness at the start of the controlling player's untap step.

### Summoning sickness visual indicator
Cards with summoning sickness appear at 60% opacity with a dashed yellow border. CSS class `summoning-sick` applied to battlefield cards that have the condition. Indicator disappears when sickness clears at the start of the player's next turn.

### Scoped undo: pending mana taps
Instead of a global undo stack, track "pending mana taps" — lands tapped for mana whose mana is still unspent in the pool. An untap option appears only on these lands. When mana is spent (paying a spell cost), all pending taps lock in and can no longer be undone.

### Cancel for multi-step sequences
During multi-step engine decisions (targeting, card choices), a "Cancel" button lets the player abort and revert to before the sequence started. This is a misclick safety net for interactive effects like Brainstorm or Ponder.

## ActionMenu Simplification

**Card in Hand:**
- "Play" — the only action. Engine decides land drop vs spell cast.

**Creature on Battlefield:**
- Activated abilities (if any, engine-enforced, some with tap costs).
- No generic "tap" button — attacking is through the combat phase UI.

**Land on Battlefield:**
- Click to tap for mana (auto-tap behavior). No menu needed.
- If already tapped, no actions available.

**Cards in Graveyard/Exile/Library:**
- No actions (future mechanics like flashback would add context-specific actions).

No "Move to [Zone]" submenu anywhere.

## What Stays Unchanged

- Cast Spell flow (mana, targeting, stack, sorcery-speed)
- Combat declarations (toggle attackers/blockers before confirm)
- Pass Priority / PhaseBar
- Surrender
- Targeting overlays
- Mana color choice
- Card choice / tutor overlays
- Reveal acknowledgment

## Summary Table

| Change | Type | Scope |
|--------|------|-------|
| Remove MoveCard (action, UI menu, undo) | Delete | Engine + UI |
| Remove Draw Card button | Delete | UI + GameSession |
| Remove Life +1/-1 buttons | Delete | UI + GameSession |
| Remove manual Untap button | Delete | UI ActionMenu |
| Remove sandbox play fallback | Delete | Engine PlayCard path |
| Remove blanket Undo button | Delete | UI + Engine |
| Add summoning sickness check on tap | Add | Engine TapCard |
| Add summoning sickness visual (60% opacity + dashed yellow border) | Add | UI CSS |
| Add pending mana tap undo (untap unspent lands) | Add | Engine + UI |
| Add Cancel button for multi-step sequences | Add | UI decision overlays |
| Simplify ActionMenu per zone | Modify | UI ActionMenu |
