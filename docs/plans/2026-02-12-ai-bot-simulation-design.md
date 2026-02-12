# AI Bot Simulation Design

## Goal

Enable two AI bots to play full games against each other server-side, with game logging and batch statistics. Start with heuristic-based AI, with architecture ready for Monte Carlo simulation later.

## Architecture

The engine already supports this through `IPlayerDecisionHandler` — each `Player` receives a decision handler at construction. The `InteractiveDecisionHandler` waits for human input; the `TestDecisionHandler` returns queued responses. An `AiBotDecisionHandler` makes decisions based on board state evaluation.

All new code lives in `MtgDecker.Engine` — no web/UI dependency. Games run to completion synchronously on the server. The existing `GameState.GameLog` captures every action.

## Prerequisites

### Deck-out Loss Rule

Currently the engine silently skips draws from an empty library. MTG rule 104.3c: a player who attempts to draw from an empty library loses the game. This must be implemented in:

- `ExecuteTurnBasedAction(Phase.Draw)` — the mandatory turn-based draw
- `DrawCards(Player, int)` — any effect that draws multiple cards

When the library is empty and a draw is attempted, set `IsGameOver = true` and log the loss.

## Components

### 1. BoardEvaluator

A static scoring function that evaluates a board position from one player's perspective.

```csharp
public static class BoardEvaluator
{
    public static double Evaluate(GameState state, Player player)
}
```

**Scoring factors:**
- Life differential: `(player.Life - opponent.Life) * weight`
- Creature power on battlefield: sum of power of all creatures
- Creature toughness on battlefield: sum of toughness (defensive value)
- Card advantage: `(player.Hand.Count - opponent.Hand.Count) * weight`
- Mana advantage: count of untapped lands
- Board presence: number of creatures (quantity matters for go-wide)

**Weights** are constants, tunable later. Starting values:
- Life point: 1.0
- Creature power on board: 2.0
- Creature toughness on board: 0.5
- Card in hand: 1.5
- Untapped land: 0.3
- Creature count: 0.5

### 2. AiBotDecisionHandler

Implements `IPlayerDecisionHandler` with heuristic logic for each decision point.

```csharp
public class AiBotDecisionHandler : IPlayerDecisionHandler
{
    public AiBotDecisionHandler(BoardEvaluator? evaluator = null)
}
```

**Decision logic per method:**

#### GetMulliganDecision
- Keep 7-card hands with 2-5 lands
- Keep 6-card hands with 2-4 lands
- Keep 5-card hands with 1-4 lands
- Always keep at 4 or fewer cards

#### GetAction
Priority ordering:
1. Play a land if available (prefer lands that produce colors needed for cards in hand)
2. Cast the most expensive spell affordable (greedy mana usage)
3. Pass priority

#### ChooseManaColor
- Pick the color most needed for cards in hand
- Fallback: pick the color with lowest current pool count (diversify)

#### ChooseGenericPayment
- Pay with colorless first, then least-needed colors

#### ChooseAttackers
- Calculate "attack value" for each eligible creature:
  - If opponent has no blockers: attack with everything
  - If opponent has blockers: attack if attacker power > best available blocker toughness, or if life race favors attacking
- Never attack if it would leave you dead on the crack-back (basic defensive check)

#### ChooseBlockers
- For each attacker, find the most efficient block:
  - Trade: blocker toughness <= attacker power AND blocker power >= attacker toughness (mutual kill, favorable if blocker CMC < attacker CMC)
  - Chump block: only if the damage would be lethal

#### OrderBlockers
- Order by toughness ascending (kill the smallest first to maximize damage assignment)

#### ChooseCard (tutor/search effects)
- Pick the card with highest "need" score based on current board state
- For creature tutors: pick the most expensive creature affordable next turn

#### RevealCards
- Auto-acknowledge (no decision to make)

### 3. SimulationResult

```csharp
public record SimulationResult(
    string WinnerName,
    string LoserName,
    bool IsDraw,
    int TotalTurns,
    int Player1FinalLife,
    int Player2FinalLife,
    IReadOnlyList<string> GameLog,
    TimeSpan Duration);
```

### 4. SimulationRunner

```csharp
public class SimulationRunner
{
    public Task<SimulationResult> RunGameAsync(
        IReadOnlyList<GameCard> deck1,
        IReadOnlyList<GameCard> deck2,
        string player1Name = "Bot A",
        string player2Name = "Bot B",
        CancellationToken ct = default)

    public Task<BatchResult> RunBatchAsync(
        IReadOnlyList<GameCard> deck1,
        IReadOnlyList<GameCard> deck2,
        int gameCount,
        string player1Name = "Bot A",
        string player2Name = "Bot B",
        CancellationToken ct = default)
}
```

### 5. BatchResult

```csharp
public record BatchResult(
    int TotalGames,
    int Player1Wins,
    int Player2Wins,
    int Draws,
    double Player1WinRate,
    double AverageGameLength,
    double AverageLifeDifferential,
    IReadOnlyList<SimulationResult> Games);
```

## Data Flow

```
SimulationRunner
  → Creates Player1(AiBotDecisionHandler) + Player2(AiBotDecisionHandler)
  → Creates GameState(Player1, Player2)
  → Creates GameEngine(GameState)
  → Calls engine.StartGameAsync()
    → Engine runs full game loop
    → Bot handlers make decisions via BoardEvaluator
    → GameLog captures everything
    → Game ends on life <= 0 or deck-out
  → Returns SimulationResult with log and stats
```

## Future: MCTS Extension

The heuristic bot's `BoardEvaluator` becomes the leaf-node evaluator for MCTS. The extension would:
- Clone `GameState` (requires making all state cloneable)
- Simulate N random games from current position using simple random bots
- Use win rate from simulations to pick the best move
- Fall back to heuristic for decisions during simulation rollouts

This is a separate feature — the heuristic bot stands alone.

## Testing Strategy

- Unit tests for `BoardEvaluator` with known board positions
- Unit tests for each `AiBotDecisionHandler` decision method
- Integration test: run a full bot-vs-bot game, verify it completes without exceptions
- Integration test: run batch of 10 games, verify stats are populated
- Deck-out rule: test that drawing from empty library ends the game

## File Structure

```
src/MtgDecker.Engine/
  AI/
    BoardEvaluator.cs
    AiBotDecisionHandler.cs
  Simulation/
    SimulationRunner.cs
    SimulationResult.cs
    BatchResult.cs

tests/MtgDecker.Engine.Tests/
  AI/
    BoardEvaluatorTests.cs
    AiBotDecisionHandlerTests.cs
  Simulation/
    SimulationRunnerTests.cs
```
