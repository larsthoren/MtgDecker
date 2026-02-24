# Ninjutsu UI Design

**Goal:** Let players activate Ninjutsu from hand during combat via the existing action menu and targeting patterns.

**Context:** The backend is fully implemented — `NinjutsuHandler`, `GameAction.Ninjutsu()`, and tests all exist. Only the UI layer is missing.

## Activation Flow

1. Player clicks a ninja card in hand during combat (after blockers declared).
2. Action menu shows a "Ninjutsu" button (alongside "Play" and "Cancel").
3. Button only appears when ALL conditions are met:
   - Combat phase, `CombatStep >= DeclareBlockers`
   - Selected card has `NinjutsuCost` in CardDefinitions
   - Player controls at least one unblocked attacking creature
4. Clicking "Ninjutsu" enters targeting mode — unblocked attackers highlighted, other cards dimmed.
5. Banner: "Choose unblocked attacker to return to hand."
6. Player clicks an unblocked attacker to complete activation.
7. Cancel button exits targeting mode.
8. UI submits `GameAction.Ninjutsu(playerId, ninjaCardId, returnCreatureId)`.
9. Engine pays mana (same `PayManaCostAsync` flow), returns attacker to hand, puts ninja on battlefield tapped and attacking. No stack involvement — Ninjutsu resolves immediately.

## File Changes

### ActionMenu.razor
- Add `bool HasNinjutsuCost` parameter
- Add `EventCallback OnNinjutsu` parameter
- Add "Ninjutsu" button visible when `CurrentZone == Hand && HasNinjutsuCost`

### GameBoard.razor
- Add `GameCard? _ninjutsuCard` field for ninjutsu targeting state
- Add `HasNinjutsuAvailable(GameCard card)` method:
  - `CardDefinitions.TryGet(card.Name, out var def) && def.NinjutsuCost != null`
  - `State.CurrentPhase == Phase.Combat && State.CombatStep >= CombatStep.DeclareBlockers`
  - Player has at least one unblocked attacker on battlefield
- Wire `OnNinjutsu="HandleNinjutsu"` and `HasNinjutsuCost="HasNinjutsuAvailable(_selectedCard)"` on ActionMenu
- `HandleNinjutsu()`: sets `_ninjutsuCard = _selectedCard`, clears selection
- When `_ninjutsuCard != null`:
  - Unblocked attackers get `Eligible="true"`
  - Clicking one calls `HandleNinjutsuTargetClick(card)` → submits `GameAction.Ninjutsu(LocalPlayer.Id, _ninjutsuCard.Id, card.Id)` → clears `_ninjutsuCard`
  - Show banner "Choose unblocked attacker to return to hand" + Cancel button
- Cancel clears `_ninjutsuCard`

### No changes needed
- GameEngine, NinjutsuHandler, InteractiveDecisionHandler, GamePage — all existing infrastructure works as-is.
