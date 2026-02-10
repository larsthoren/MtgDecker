# Game UI Essentials Design

**Goal:** Add life counters, library card count, and exile zone to the game UI.

**Scope:** Engine changes (Player model, GameSession methods) + Blazor UI updates (PlayerZone header, exile display, action menu).

---

## 1. Life Counters

### Engine

- Add `int Life { get; set; } = 20` to `Player`.
- Add `AdjustLife(int playerSeat, int delta)` to `GameSession`. This modifies the player's Life, logs the change (`"Alice's life: 20 -> 17"`), and checks for death.
- When life reaches 0 or below: set `State.IsGameOver = true`, set `Winner` to the opponent's name, log `"Alice loses — life reached 0."`.
- Life adjustment is a side-channel operation (like undo), not a game action through the priority system.
- Both players can adjust either player's life total — buttons always enabled.

### UI

- PlayerZone header expands from `PlayerName [Active]` to: `PlayerName | [-] 20 [+] | Active | Library: 45`
- Default +/- buttons adjust by 1.
- Clicking the life number opens an inline MudTextField where the player types a delta (e.g., `-3`, `+5`) and presses Enter. The field auto-focuses and closes on Enter or Escape.
- Life changes trigger `OnStateChanged` via Log, so both tabs re-render.

## 2. Library Count

### Engine

No changes. `Player.Library.Count` already exists.

### UI

- Display `Library: N` in the PlayerZone header after the life counter.
- Updates automatically on every re-render (draw, mulligan, etc.).

## 3. Exile Zone

### Engine

- Add `Exile` value to `ZoneType` enum.
- Add `Zone Exile { get; }` property to `Player`, initialized as `new Zone(ZoneType.Exile)`.
- Add `ZoneType.Exile => Exile` case to `Player.GetZone()`.

### UI

- Exile zone appears next to graveyard in PlayerZone, same compact treatment: `Exile (N)` label with top card visible if non-empty.
- Action menu "Move to" options already iterate `Enum.GetValues<ZoneType>()`, so Exile appears automatically.
- Undo for MoveCard to/from Exile works automatically (existing undo reverses source/destination).

## Summary of Changes

| File | Change |
|------|--------|
| `Engine/Player.cs` | Add `Life` property (default 20), add `Exile` zone |
| `Engine/Enums/ZoneType.cs` | Add `Exile` value |
| `Engine/GameSession.cs` | Add `AdjustLife(int seat, int delta)` method |
| `Web/Game/PlayerZone.razor` | Expand header with life +/-, library count, exile zone display |
| `Web/Game/GameBoard.razor` | Pass life-related callbacks, pass exile data |
| `Web/GamePage.razor` | Wire `AdjustLife` callback |
