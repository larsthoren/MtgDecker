# MTG Engine UI Design

## Goal

Wire the MtgDecker.Engine into the existing Blazor web app as a two-player game interface. MTGO-inspired: information-dense, functional, no animations. Two browser tabs on localhost for v1, architecture ready for future network play.

## Decisions

- Two-player, same machine (two tabs), game code in URL
- MTGO-style layout: opponent top, you bottom, game log right
- Click card → context-sensitive action menu (no drag and drop)
- Small card images from Scryfall CDN, tapped = rotated 90 degrees
- Priority: clear phase indicator, waiting state when not your turn
- Always-visible game log panel (right side, ~25% width)
- Modal dialog for mulligan
- Surrender button ends the game
- Any card in any zone can be moved to any other zone (shared tabletop)

## Architecture

### GameSessionManager (Singleton)

Holds a `Dictionary<string, GameSession>` of active games.

- **Create game**: Player 1 picks a deck, gets a 6-char game ID. Deck loaded via `IDeckRepository`, mapped to `GameCard` objects with Scryfall image URLs.
- **Join game**: Player 2 navigates to `/game/{gameId}`, picks their deck. Both seated → game loop starts on background task.
- **Surrender**: Sets `IsGameOver = true`, cancels `CancellationToken`, ends game loop.
- **Cleanup**: Sessions removed after game ends. No database persistence — ephemeral.

### BlazorDecisionHandler

Implements `IPlayerDecisionHandler`. Bridges async engine loop with interactive UI using `TaskCompletionSource`:

- Engine calls `GetAction()` → awaits TCS
- Player clicks action in UI → `SubmitAction(GameAction)` completes the TCS
- Engine unblocks, executes action, fires `OnStateChanged`
- Both player components re-render

Uses `TaskCreationOptions.RunContinuationsAsynchronously` to avoid deadlocks. Same pattern for `GetMulliganDecision()` and `ChooseCardsToBottom()`.

### GameSession

- Holds `GameEngine`, `GameState`, two `BlazorDecisionHandler` instances
- Exposes `OnStateChanged` event — components call `InvokeAsync(StateHasChanged)`
- Game loop runs on background task: `StartGameAsync()` → mulligan → `RunTurnAsync()` loop
- Cancellation via `CancellationTokenSource` for surrender

### Deck Loading

Existing `IDeckRepository` loads `Deck` + `DeckEntry` + `Card` data. Mapped to `GameCard`:
- `Card.Name` → `GameCard.Name`
- `Card.TypeLine` → `GameCard.TypeLine`
- `Card.ImageUrl` → `GameCard.ImageUrl`
- One `GameCard` per copy (respecting `DeckEntry.Quantity`)

## Page Layout

### Routes

- `/game/new` — Game lobby (create/join)
- `/game/{gameId}` — Game board

### Game Board Layout (CSS Grid)

```
+------------------------------------------+------------------+
|           Opponent's Zones               |                  |
|  [Graveyard pile]  [Battlefield grid]    |   Game Log       |
|  [Hand: card backs showing count]        |   (scrolling,    |
+------------------------------------------+   monospace,     |
|  Turn Bar: Phase | Turn # | Whose Turn   |   color-coded)   |
|  [Pass Priority] [Surrender]             |                  |
+------------------------------------------+                  |
|           Your Zones                     |                  |
|  [Hand: face-up card images]             |                  |
|  [Battlefield grid]  [Graveyard pile]    |                  |
+------------------------------------------+------------------+
```

### Component Breakdown

- **GamePage.razor** — Top-level page, manages session connection and player seat
- **GameLobby.razor** — Pre-game: create game / join with code / pick deck
- **GameBoard.razor** — Main board layout with all zones
- **PlayerZone.razor** — Reusable: one player's battlefield + hand + graveyard (flipped for opponent)
- **CardDisplay.razor** — Single card thumbnail, tap rotation, click handler
- **ActionMenu.razor** — Context-sensitive popup on card click
- **GameLogPanel.razor** — Right-side scrolling log
- **MulliganDialog.razor** — Modal for keep/mulligan and bottom-card selection

## Action Menu (Context-Sensitive)

All cards in all zones show "Move to..." with available destination zones. Additional shortcuts per zone:

| Zone | Your Cards | Opponent's Cards |
|------|-----------|-----------------|
| Hand | "Play" + Move to (Battlefield, Graveyard, Library) | Move to (Battlefield, Graveyard, Library) |
| Battlefield | "Tap"/"Untap" + Move to (Hand, Graveyard, Library) | Move to (Hand, Graveyard, Library) |
| Graveyard | Move to (Hand, Battlefield, Library) | Move to (Hand, Battlefield, Library) |

Move targets always exclude the current zone. Actions on opponent's cards target the opponent's zones.

## Player Interaction Flow

1. **Phase indicator**: Turn bar shows current phase and "Your Priority" / "Waiting for opponent..."
2. **Play a card**: Click card in hand → Action menu → "Play" → card moves to battlefield
3. **Tap/Untap**: Click battlefield card → "Tap" or "Untap"
4. **Move card**: Click any card → "Move to..." → pick destination zone
5. **Pass priority**: Click "Pass Priority" button in turn bar
6. **Surrender**: Click "Surrender" → confirmation dialog → game over for both

## Mulligan Flow

1. Game starts → MulliganDialog opens as modal
2. Shows your 7 cards face-up with "Keep" / "Mulligan" buttons
3. If mulligan: cards returned, redraw, dialog shows again with updated count
4. If keep after mulligan: dialog shows cards with click-to-select for bottom cards
5. Both players finish mulligan → game board loads → turn 1 begins

## Opponent Hand Visibility

Opponent's hand shown as card backs (count visible). The `GameState` is shared server-side, but the UI filters: `PlayerZone` renders face-up cards only for the local player's seat. Opponent sees the same in reverse.

## Thread Safety

- Engine loop: background task
- Blazor components: circuit synchronization context
- `OnStateChanged` → `InvokeAsync(StateHasChanged)` to marshal correctly
- `TaskCompletionSource` with `RunContinuationsAsynchronously`
- `GameSessionManager` dictionary: `ConcurrentDictionary<string, GameSession>`

## Navigation

Add "Play Game" link to existing NavMenu drawer. Routes:
- `/game/new` → GameLobby
- `/game/{gameId}` → GamePage

## Future Considerations (Not in v1)

- Network play (v4-5): Replace localhost with hosted server, add auth
- Spectator mode
- Game replay from log
- Life totals, mana pool, combat damage
- Stack visualization
