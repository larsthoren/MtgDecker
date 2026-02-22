# Bugfixes and Delve Design

## Bug 1: PlayLand on Opponent's Turn

**Root cause**: `PlayLandHandler` has no active player, phase, or stack validation.

**Fix**: Add three guards at the top of `PlayLandHandler.ExecuteAsync`:
- Active player check: `state.ActivePlayer.Id != action.PlayerId` → reject
- Phase check: must be `MainPhase1` or `MainPhase2` → reject
- Stack empty check: `state.StackCount > 0` → reject

**Tests**: 3 new engine tests, one per validation case.

## Bug 2: Daze Alternate Cost Forced Selection

**Root cause**: UI's `HandlePlay` checks mana pool (empty because lands not tapped yet), sees alternate cost exists, dispatches `CastSpell` immediately. Engine sees canPayMana=false + canPayAlternate=true, auto-selects alternate with no player choice.

**Fix**: Add "Cast (alt cost)" option to `ActionMenu`.

- `ActionMenu`: new `HasAlternateCost` bool parameter, new `OnPlayAlternate` EventCallback. Shows "Cast (alt cost)" button when true.
- `GameBoard.HandlePlay`: unchanged — always does normal mana casting (pending cast mode → tap lands → auto-complete).
- `GameBoard.HandlePlayAlternate`: new method — skips mana checks, dispatches `CastSpell` with `UseAlternateCost=true`.
- `GameAction`: add `UseAlternateCost` bool field.
- `CastSpellHandler`: when `action.UseAlternateCost` is true, skip the mana/alternate choice logic, go straight to alternate payment. Remove the early-dispatch for alternate cost cards in `HandlePlay` (lines 957-963).

**Tests**: Engine test verifying `UseAlternateCost` flag bypasses choice logic.

## Feature: Delve (Murktide Regent)

### Part A: Graveyard Viewer

Add expandable graveyard zone to player/opponent info bars. Click "Grave: N" chip to toggle a panel showing all graveyard cards with `CardDisplay` + hover preview.

### Part B: Delve Mechanic

- `CardDefinition.HasDelve` bool flag.
- New decision handler method: `ChooseCardsFromGraveyard(cards, maxCount, prompt)` — multi-select with confirm.
- `CastSpellHandler`: when casting a Delve card, prompt player to select graveyard cards to exile (up to generic cost amount). Each exiled card reduces generic cost by 1. Remaining cost paid from mana pool.
- `InteractiveDecisionHandler` + `TestDecisionHandler` + `AiBotDecisionHandler`: implement new method.
- UI: reuse graveyard viewer panel with selectable cards during Delve prompt.
- Register Murktide Regent with `HasDelve = true`.

**Tests**: Engine tests for Delve cost reduction, partial exile, zero exile, full exile.
