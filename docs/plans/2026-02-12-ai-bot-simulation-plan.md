# AI Bot Simulation Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Enable two AI bots to play full games against each other server-side with heuristic decision-making, game logging, and batch statistics.

**Architecture:** New `AiBotDecisionHandler` implements `IPlayerDecisionHandler` with heuristic board evaluation. `SimulationRunner` orchestrates bot-vs-bot games using the existing `GameEngine`. Prerequisites fix engine gaps: deck-out loss, life-check state-based actions, and winner tracking on `GameState`.

**Tech Stack:** .NET 10, C# 14, xUnit + FluentAssertions (TDD)

---

### Task 1: Deck-Out Loss Rule

The engine silently skips draws from an empty library. MTG rule 104.3c says this should lose the game. We also need `GameState` to track `Winner` so the engine (not just `GameSession`) can record who won.

**Files:**
- Modify: `src/MtgDecker.Engine/GameState.cs`
- Modify: `src/MtgDecker.Engine/GameEngine.cs:79-86` (ExecuteTurnBasedAction Draw)
- Modify: `src/MtgDecker.Engine/GameEngine.cs:622-630` (DrawCards)
- Test: `tests/MtgDecker.Engine.Tests/DeckOutTests.cs`

**Step 1: Add Winner property to GameState**

In `src/MtgDecker.Engine/GameState.cs`, add after `IsGameOver`:

```csharp
public string? Winner { get; set; }
```

**Step 2: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/DeckOutTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class DeckOutTests
{
    private static (GameEngine engine, GameState state) CreateGame(int p1LibrarySize, int p2LibrarySize)
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Player 1", handler1);
        var p2 = new Player(Guid.NewGuid(), "Player 2", handler2);

        for (int i = 0; i < p1LibrarySize; i++)
            p1.Library.Add(new GameCard { Name = $"Card {i + 1}" });
        for (int i = 0; i < p2LibrarySize; i++)
            p2.Library.Add(new GameCard { Name = $"Card {i + 1}" });

        var state = new GameState(p1, p2);
        return (new GameEngine(state), state);
    }

    [Fact]
    public void DrawPhase_EmptyLibrary_SetsGameOverAndWinner()
    {
        var (engine, state) = CreateGame(0, 5);

        engine.ExecuteTurnBasedAction(Phase.Draw);

        state.IsGameOver.Should().BeTrue();
        state.Winner.Should().Be("Player 2");
        state.GameLog.Should().Contain(l => l.Contains("loses") && l.Contains("draw from an empty library"));
    }

    [Fact]
    public void DrawPhase_NonEmptyLibrary_DoesNotEndGame()
    {
        var (engine, state) = CreateGame(3, 5);

        engine.ExecuteTurnBasedAction(Phase.Draw);

        state.IsGameOver.Should().BeFalse();
        state.Winner.Should().BeNull();
        state.Player1.Hand.Count.Should().Be(1);
    }

    [Fact]
    public void DrawCards_EmptyLibrary_MidDraw_SetsGameOver()
    {
        // Player has 2 cards but tries to draw 5
        var (engine, state) = CreateGame(2, 5);

        engine.DrawCards(state.Player1, 5);

        state.IsGameOver.Should().BeTrue();
        state.Winner.Should().Be("Player 2");
        state.Player1.Hand.Count.Should().Be(2); // drew 2 before running out
    }

    [Fact]
    public void DrawCards_SufficientLibrary_DoesNotEndGame()
    {
        var (engine, state) = CreateGame(5, 5);

        engine.DrawCards(state.Player1, 3);

        state.IsGameOver.Should().BeFalse();
        state.Player1.Hand.Count.Should().Be(3);
    }
}
```

**Step 3: Run tests to verify they fail**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "DeckOutTests" -v quiet`
Expected: FAIL — `ExecuteTurnBasedAction` is `internal`, `DrawCards` is `private`, and neither sets `IsGameOver`.

**Step 4: Make methods accessible and implement**

First, change `DrawCards` from `private` to `internal` in `GameEngine.cs:622`:

```csharp
internal void DrawCards(Player player, int count)
```

Then update `ExecuteTurnBasedAction` (line 79-86):

```csharp
case Phase.Draw:
    var drawn = _state.ActivePlayer.Library.DrawFromTop();
    if (drawn != null)
    {
        _state.ActivePlayer.Hand.Add(drawn);
        _state.Log($"{_state.ActivePlayer.Name} draws a card.");
    }
    else
    {
        var loser = _state.ActivePlayer;
        var winner = _state.GetOpponent(loser);
        _state.IsGameOver = true;
        _state.Winner = winner.Name;
        _state.Log($"{loser.Name} loses — cannot draw from an empty library.");
    }
    break;
```

Update `DrawCards` (line 622-630):

```csharp
internal void DrawCards(Player player, int count)
{
    for (int i = 0; i < count; i++)
    {
        var card = player.Library.DrawFromTop();
        if (card != null)
        {
            player.Hand.Add(card);
        }
        else
        {
            var winner = _state.GetOpponent(player);
            _state.IsGameOver = true;
            _state.Winner = winner.Name;
            _state.Log($"{player.Name} loses — cannot draw from an empty library.");
            return;
        }
    }
}
```

**Step 5: Run tests to verify they pass**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "DeckOutTests" -v quiet`
Expected: PASS (4/4)

**Step 6: Run full test suite**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ -v quiet`
Expected: All pass (existing tests still work since they always have cards in library)

**Step 7: Commit**

```bash
git add src/MtgDecker.Engine/GameState.cs src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/DeckOutTests.cs
git commit -m "feat(engine): add deck-out loss rule and Winner tracking on GameState"
```

---

### Task 2: State-Based Life Check in Engine

Currently `GameEngine.ResolveCombatDamage` calls `Player.AdjustLife(-damage)` but never checks if life <= 0. Only `GameSession.AdjustLife` checks this. The engine needs its own state-based action check so bot simulations (which don't use `GameSession`) can end games from combat damage.

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (add CheckStateBasedActions, call after combat damage)
- Test: `tests/MtgDecker.Engine.Tests/StateBasedActionTests.cs`

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/StateBasedActionTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class StateBasedActionTests
{
    [Fact]
    public void CheckStateBasedActions_LifeAtZero_SetsGameOver()
    {
        var p1 = new Player(Guid.NewGuid(), "Player 1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        p1.AdjustLife(-20); // life = 0

        engine.CheckStateBasedActions();

        state.IsGameOver.Should().BeTrue();
        state.Winner.Should().Be("Player 2");
    }

    [Fact]
    public void CheckStateBasedActions_LifeBelowZero_SetsGameOver()
    {
        var p1 = new Player(Guid.NewGuid(), "Player 1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        p2.AdjustLife(-25); // life = -5

        engine.CheckStateBasedActions();

        state.IsGameOver.Should().BeTrue();
        state.Winner.Should().Be("Player 1");
    }

    [Fact]
    public void CheckStateBasedActions_BothAlive_DoesNotEndGame()
    {
        var p1 = new Player(Guid.NewGuid(), "Player 1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        engine.CheckStateBasedActions();

        state.IsGameOver.Should().BeFalse();
        state.Winner.Should().BeNull();
    }

    [Fact]
    public void CheckStateBasedActions_BothAtZero_FirstPlayerCheckedLoses()
    {
        var p1 = new Player(Guid.NewGuid(), "Player 1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        p1.AdjustLife(-20);
        p2.AdjustLife(-20);

        engine.CheckStateBasedActions();

        // Both at 0 = draw (both lose simultaneously in MTG rules)
        state.IsGameOver.Should().BeTrue();
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "StateBasedActionTests" -v quiet`
Expected: FAIL — `CheckStateBasedActions` doesn't exist.

**Step 3: Implement CheckStateBasedActions**

Add to `GameEngine.cs` (after `ProcessTriggersAsync`):

```csharp
internal void CheckStateBasedActions()
{
    if (_state.IsGameOver) return;

    bool p1Dead = _state.Player1.Life <= 0;
    bool p2Dead = _state.Player2.Life <= 0;

    if (p1Dead && p2Dead)
    {
        _state.IsGameOver = true;
        _state.Winner = null; // draw
        _state.Log($"Both players lose — {_state.Player1.Name} ({_state.Player1.Life} life) and {_state.Player2.Name} ({_state.Player2.Life} life).");
    }
    else if (p1Dead)
    {
        _state.IsGameOver = true;
        _state.Winner = _state.Player2.Name;
        _state.Log($"{_state.Player1.Name} loses — life reached {_state.Player1.Life}.");
    }
    else if (p2Dead)
    {
        _state.IsGameOver = true;
        _state.Winner = _state.Player1.Name;
        _state.Log($"{_state.Player2.Name} loses — life reached {_state.Player2.Life}.");
    }
}
```

Wire it into `RunCombatAsync` — after `ProcessCombatDeaths`, add:

```csharp
CheckStateBasedActions();
```

Also wire into `RunTurnAsync` — after `ExecuteTurnBasedAction`, add:

```csharp
if (_state.IsGameOver) return;
```

And after `RunCombatAsync`/`RunPriorityAsync`:

```csharp
if (_state.IsGameOver) return;
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "StateBasedActionTests" -v quiet`
Expected: PASS (4/4)

**Step 5: Run full test suite**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ -v quiet`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/GameEngine.cs tests/MtgDecker.Engine.Tests/StateBasedActionTests.cs
git commit -m "feat(engine): add state-based life check after combat damage"
```

---

### Task 3: BoardEvaluator

Static scoring function that evaluates board position from one player's perspective. Used by the AI bot to compare candidate actions.

**Files:**
- Create: `src/MtgDecker.Engine/AI/BoardEvaluator.cs`
- Test: `tests/MtgDecker.Engine.Tests/AI/BoardEvaluatorTests.cs`

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/AI/BoardEvaluatorTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine.AI;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests.AI;

public class BoardEvaluatorTests
{
    private static (GameState state, Player player, Player opponent) CreateGame()
    {
        var p1 = new Player(Guid.NewGuid(), "Player 1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        return (state, p1, p2);
    }

    [Fact]
    public void EmptyBoard_EqualLife_ReturnsZero()
    {
        var (state, player, _) = CreateGame();

        var score = BoardEvaluator.Evaluate(state, player);

        score.Should().Be(0.0);
    }

    [Fact]
    public void LifeAdvantage_IncreasesScore()
    {
        var (state, player, opponent) = CreateGame();
        opponent.AdjustLife(-10); // opponent at 10, player at 20

        var score = BoardEvaluator.Evaluate(state, player);

        score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CreatureOnBoard_IncreasesScore()
    {
        var (state, player, _) = CreateGame();
        player.Battlefield.Add(new GameCard
        {
            Name = "Bear",
            Power = 2,
            Toughness = 2,
            CardTypes = CardType.Creature
        });

        var score = BoardEvaluator.Evaluate(state, player);

        score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CardsInHand_IncreasesScore()
    {
        var (state, player, _) = CreateGame();
        player.Hand.Add(new GameCard { Name = "Card 1" });
        player.Hand.Add(new GameCard { Name = "Card 2" });

        var score = BoardEvaluator.Evaluate(state, player);

        score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void UntappedLands_IncreasesScore()
    {
        var (state, player, _) = CreateGame();
        player.Battlefield.Add(new GameCard
        {
            Name = "Mountain",
            CardTypes = CardType.Land,
            IsTapped = false
        });

        var score = BoardEvaluator.Evaluate(state, player);

        score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void OpponentCreatures_DecreasesScore()
    {
        var (state, player, opponent) = CreateGame();
        opponent.Battlefield.Add(new GameCard
        {
            Name = "Bear",
            Power = 2,
            Toughness = 2,
            CardTypes = CardType.Creature
        });

        var score = BoardEvaluator.Evaluate(state, player);

        score.Should().BeLessThan(0);
    }

    [Fact]
    public void Evaluate_IsSymmetric()
    {
        var (state, p1, p2) = CreateGame();
        p1.Battlefield.Add(new GameCard { Name = "Bear", Power = 2, Toughness = 2, CardTypes = CardType.Creature });

        var p1Score = BoardEvaluator.Evaluate(state, p1);
        var p2Score = BoardEvaluator.Evaluate(state, p2);

        // p1 has a creature, p2 doesn't — scores should be opposite
        p1Score.Should().BeGreaterThan(0);
        p2Score.Should().BeLessThan(0);
        p1Score.Should().BeApproximately(-p2Score, 0.001);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "BoardEvaluatorTests" -v quiet`
Expected: FAIL — `BoardEvaluator` doesn't exist.

**Step 3: Implement BoardEvaluator**

Create `src/MtgDecker.Engine/AI/BoardEvaluator.cs`:

```csharp
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.AI;

public static class BoardEvaluator
{
    private const double LifeWeight = 1.0;
    private const double CreaturePowerWeight = 2.0;
    private const double CreatureToughnessWeight = 0.5;
    private const double CardInHandWeight = 1.5;
    private const double UntappedLandWeight = 0.3;
    private const double CreatureCountWeight = 0.5;

    public static double Evaluate(GameState state, Player player)
    {
        var opponent = state.GetOpponent(player);
        return ScorePlayer(player) - ScorePlayer(opponent);
    }

    private static double ScorePlayer(Player player)
    {
        double score = 0;

        score += player.Life * LifeWeight;

        var creatures = player.Battlefield.Cards.Where(c => c.IsCreature).ToList();
        score += creatures.Sum(c => (c.Power ?? 0) * CreaturePowerWeight);
        score += creatures.Sum(c => (c.Toughness ?? 0) * CreatureToughnessWeight);
        score += creatures.Count * CreatureCountWeight;

        score += player.Hand.Count * CardInHandWeight;

        var untappedLands = player.Battlefield.Cards.Count(c => c.IsLand && !c.IsTapped);
        score += untappedLands * UntappedLandWeight;

        return score;
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "BoardEvaluatorTests" -v quiet`
Expected: PASS (7/7)

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/AI/BoardEvaluator.cs tests/MtgDecker.Engine.Tests/AI/BoardEvaluatorTests.cs
git commit -m "feat(engine): add BoardEvaluator for heuristic board scoring"
```

---

### Task 4: AiBotDecisionHandler — Mulligan and Mana Decisions

The simplest decision methods: mulligan logic (land-count heuristic), mana color selection, and generic payment.

**Files:**
- Create: `src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs`
- Test: `tests/MtgDecker.Engine.Tests/AI/AiBotMulliganTests.cs`

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/AI/AiBotMulliganTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine.AI;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests.AI;

public class AiBotMulliganTests
{
    private readonly AiBotDecisionHandler _bot = new();

    private IReadOnlyList<GameCard> MakeHand(int lands, int spells)
    {
        var hand = new List<GameCard>();
        for (int i = 0; i < lands; i++)
            hand.Add(new GameCard { Name = "Mountain", CardTypes = CardType.Land });
        for (int i = 0; i < spells; i++)
            hand.Add(new GameCard { Name = "Goblin", CardTypes = CardType.Creature });
        return hand;
    }

    [Theory]
    [InlineData(2, 5, 0, MulliganDecision.Keep)]  // 2 lands in 7 = keep
    [InlineData(3, 4, 0, MulliganDecision.Keep)]  // 3 lands in 7 = keep
    [InlineData(5, 2, 0, MulliganDecision.Keep)]  // 5 lands in 7 = keep
    [InlineData(0, 7, 0, MulliganDecision.Mulligan)] // 0 lands = mull
    [InlineData(1, 6, 0, MulliganDecision.Mulligan)] // 1 land in 7 = mull
    [InlineData(6, 1, 0, MulliganDecision.Mulligan)] // 6 lands in 7 = mull
    [InlineData(7, 0, 0, MulliganDecision.Mulligan)] // all lands = mull
    public async Task MulliganDecision_SevenCardHand(int lands, int spells, int mulliganCount, MulliganDecision expected)
    {
        var hand = MakeHand(lands, spells);
        var result = await _bot.GetMulliganDecision(hand, mulliganCount);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(2, 4, 1, MulliganDecision.Keep)]  // 2 lands in 6 = keep
    [InlineData(0, 6, 1, MulliganDecision.Mulligan)] // 0 lands in 6 = mull
    [InlineData(5, 1, 1, MulliganDecision.Mulligan)] // 5 lands in 6 = mull
    public async Task MulliganDecision_SixCardHand(int lands, int spells, int mulliganCount, MulliganDecision expected)
    {
        var hand = MakeHand(lands, spells);
        var result = await _bot.GetMulliganDecision(hand, mulliganCount);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task MulliganDecision_FourOrFewer_AlwaysKeeps()
    {
        var hand = MakeHand(0, 4); // 0 lands, 4 spells — terrible but keep at 4
        var result = await _bot.GetMulliganDecision(hand, 3);
        result.Should().Be(MulliganDecision.Keep);
    }

    [Fact]
    public async Task ChooseCardsToBottom_ReturnsCorrectCount()
    {
        var hand = MakeHand(4, 3);
        var result = await _bot.ChooseCardsToBottom(hand, 2);
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ChooseManaColor_ReturnsValidOption()
    {
        var options = new List<ManaColor> { ManaColor.Red, ManaColor.Green };
        var result = await _bot.ChooseManaColor(options);
        options.Should().Contain(result);
    }

    [Fact]
    public async Task ChooseGenericPayment_PaysCorrectAmount()
    {
        var available = new Dictionary<ManaColor, int>
        {
            [ManaColor.Red] = 3,
            [ManaColor.Green] = 2
        };
        var result = await _bot.ChooseGenericPayment(4, available);
        result.Values.Sum().Should().Be(4);
    }

    [Fact]
    public async Task RevealCards_AutoAcknowledges()
    {
        // Should complete without blocking
        await _bot.RevealCards([], [], "test");
    }

    [Fact]
    public async Task ChooseCard_ReturnsFromOptions()
    {
        var cards = new List<GameCard>
        {
            new() { Name = "Goblin Matron", CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{2}{R}") },
            new() { Name = "Goblin Lackey", CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{R}") }
        };
        var result = await _bot.ChooseCard(cards, "Choose a Goblin");
        result.Should().NotBeNull();
        cards.Select(c => c.Id).Should().Contain(result!.Value);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "AiBotMulliganTests" -v quiet`
Expected: FAIL — `AiBotDecisionHandler` doesn't exist.

**Step 3: Implement AiBotDecisionHandler (core methods)**

Create `src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs`:

```csharp
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.AI;

public class AiBotDecisionHandler : IPlayerDecisionHandler
{
    public Task<MulliganDecision> GetMulliganDecision(IReadOnlyList<GameCard> hand, int mulliganCount,
        CancellationToken ct = default)
    {
        // Always keep at 4 or fewer cards
        if (hand.Count <= 4)
            return Task.FromResult(MulliganDecision.Keep);

        var landCount = hand.Count(c => c.IsLand);
        var (minLands, maxLands) = hand.Count switch
        {
            7 => (2, 5),
            6 => (2, 4),
            5 => (1, 4),
            _ => (0, hand.Count)
        };

        var decision = landCount >= minLands && landCount <= maxLands
            ? MulliganDecision.Keep
            : MulliganDecision.Mulligan;

        return Task.FromResult(decision);
    }

    public Task<IReadOnlyList<GameCard>> ChooseCardsToBottom(IReadOnlyList<GameCard> hand, int count,
        CancellationToken ct = default)
    {
        // Bottom excess lands first, then cheapest spells
        var sorted = hand
            .OrderByDescending(c => c.IsLand) // lands first (to bottom)
            .ThenBy(c => c.ManaCost?.ConvertedManaCost ?? 0) // cheapest next
            .Take(count)
            .ToList();
        return Task.FromResult<IReadOnlyList<GameCard>>(sorted);
    }

    public Task<ManaColor> ChooseManaColor(IReadOnlyList<ManaColor> options,
        CancellationToken ct = default)
    {
        // Prefer colored mana over colorless
        var preferred = options.FirstOrDefault(c => c != ManaColor.Colorless);
        return Task.FromResult(preferred != default ? preferred : options[0]);
    }

    public Task<Dictionary<ManaColor, int>> ChooseGenericPayment(int genericAmount,
        Dictionary<ManaColor, int> available, CancellationToken ct = default)
    {
        // Pay with colorless first, then largest pools
        var payment = new Dictionary<ManaColor, int>();
        var remaining = genericAmount;

        // Colorless first
        if (available.TryGetValue(ManaColor.Colorless, out var colorless) && colorless > 0)
        {
            var take = Math.Min(remaining, colorless);
            payment[ManaColor.Colorless] = take;
            remaining -= take;
        }

        // Then by pool size descending
        foreach (var (color, amount) in available
            .Where(kv => kv.Key != ManaColor.Colorless)
            .OrderByDescending(kv => kv.Value))
        {
            if (remaining <= 0) break;
            var take = Math.Min(remaining, amount);
            payment[color] = take;
            remaining -= take;
        }

        return Task.FromResult(payment);
    }

    public Task<Guid?> ChooseCard(IReadOnlyList<GameCard> options, string prompt,
        bool optional = false, CancellationToken ct = default)
    {
        if (options.Count == 0)
            return Task.FromResult<Guid?>(null);

        // Pick the creature with highest CMC (most impactful)
        var best = options
            .OrderByDescending(c => c.ManaCost?.ConvertedManaCost ?? 0)
            .First();
        return Task.FromResult<Guid?>(best.Id);
    }

    public Task RevealCards(IReadOnlyList<GameCard> cards, IReadOnlyList<GameCard> kept,
        string prompt, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    // Stubs for Task 5 (action) and Task 6 (combat) — pass by default for now
    public Task<GameAction> GetAction(GameState gameState, Guid playerId,
        CancellationToken ct = default)
    {
        return Task.FromResult(GameAction.Pass(playerId));
    }

    public Task<IReadOnlyList<Guid>> ChooseAttackers(IReadOnlyList<GameCard> eligibleAttackers,
        CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<Guid>>([]);
    }

    public Task<Dictionary<Guid, Guid>> ChooseBlockers(IReadOnlyList<GameCard> eligibleBlockers,
        IReadOnlyList<GameCard> attackers, CancellationToken ct = default)
    {
        return Task.FromResult(new Dictionary<Guid, Guid>());
    }

    public Task<IReadOnlyList<Guid>> OrderBlockers(Guid attackerId, IReadOnlyList<GameCard> blockers,
        CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<Guid>>(blockers.Select(b => b.Id).OrderBy(id => id).ToList());
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "AiBotMulliganTests" -v quiet`
Expected: PASS (all tests)

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs tests/MtgDecker.Engine.Tests/AI/AiBotMulliganTests.cs
git commit -m "feat(engine): add AiBotDecisionHandler with mulligan and mana heuristics"
```

---

### Task 5: AiBotDecisionHandler — Action Selection

The core `GetAction` method: play lands, cast spells, or pass. The bot needs access to the game state to evaluate the board. It receives `GameState` and `playerId` from the engine.

**Files:**
- Modify: `src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs`
- Test: `tests/MtgDecker.Engine.Tests/AI/AiBotActionTests.cs`

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/AI/AiBotActionTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine.AI;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests.AI;

public class AiBotActionTests
{
    private static (GameState state, Player player) CreateGameWithBot()
    {
        var bot = new AiBotDecisionHandler();
        var opponent = new Player(Guid.NewGuid(), "Opponent", new AiBotDecisionHandler());
        var player = new Player(Guid.NewGuid(), "Bot", bot);
        var state = new GameState(player, opponent);
        state.CurrentPhase = Phase.MainPhase1;
        return (state, player);
    }

    [Fact]
    public async Task GetAction_WithLandInHand_PlaysLand()
    {
        var (state, player) = CreateGameWithBot();
        var mountain = new GameCard { Name = "Mountain", CardTypes = CardType.Land };
        player.Hand.Add(mountain);

        var action = await ((AiBotDecisionHandler)player.DecisionHandler)
            .GetAction(state, player.Id);

        action.Type.Should().Be(ActionType.PlayCard);
        action.CardId.Should().Be(mountain.Id);
    }

    [Fact]
    public async Task GetAction_LandAlreadyPlayed_DoesNotPlayAnotherLand()
    {
        var (state, player) = CreateGameWithBot();
        player.LandsPlayedThisTurn = 1;
        var mountain = new GameCard { Name = "Mountain", CardTypes = CardType.Land };
        player.Hand.Add(mountain);

        var action = await ((AiBotDecisionHandler)player.DecisionHandler)
            .GetAction(state, player.Id);

        // Should pass or cast a spell, not play another land
        action.Type.Should().NotBe(ActionType.PlayCard, "land drop already used");
    }

    [Fact]
    public async Task GetAction_WithAffordableSpell_CastsIt()
    {
        var (state, player) = CreateGameWithBot();
        var goblin = new GameCard
        {
            Name = "Goblin Lackey",
            CardTypes = CardType.Creature,
            ManaCost = ManaCost.Parse("{R}")
        };
        player.Hand.Add(goblin);
        player.ManaPool.Add(ManaColor.Red);

        var action = await ((AiBotDecisionHandler)player.DecisionHandler)
            .GetAction(state, player.Id);

        action.Type.Should().Be(ActionType.PlayCard);
        action.CardId.Should().Be(goblin.Id);
    }

    [Fact]
    public async Task GetAction_CastsExpensiveSpellFirst()
    {
        var (state, player) = CreateGameWithBot();
        var cheap = new GameCard
        {
            Name = "Mogg Fanatic",
            CardTypes = CardType.Creature,
            ManaCost = ManaCost.Parse("{R}")
        };
        var expensive = new GameCard
        {
            Name = "Goblin Warchief",
            CardTypes = CardType.Creature,
            ManaCost = ManaCost.Parse("{1}{R}{R}")
        };
        player.Hand.Add(cheap);
        player.Hand.Add(expensive);
        player.ManaPool.Add(ManaColor.Red, 3);

        var action = await ((AiBotDecisionHandler)player.DecisionHandler)
            .GetAction(state, player.Id);

        action.CardId.Should().Be(expensive.Id);
    }

    [Fact]
    public async Task GetAction_NotEnoughMana_Passes()
    {
        var (state, player) = CreateGameWithBot();
        var expensive = new GameCard
        {
            Name = "Siege-Gang Commander",
            CardTypes = CardType.Creature,
            ManaCost = ManaCost.Parse("{3}{R}{R}")
        };
        player.Hand.Add(expensive);
        player.ManaPool.Add(ManaColor.Red, 1);

        var action = await ((AiBotDecisionHandler)player.DecisionHandler)
            .GetAction(state, player.Id);

        action.Type.Should().Be(ActionType.PassPriority);
    }

    [Fact]
    public async Task GetAction_EmptyHand_Passes()
    {
        var (state, player) = CreateGameWithBot();

        var action = await ((AiBotDecisionHandler)player.DecisionHandler)
            .GetAction(state, player.Id);

        action.Type.Should().Be(ActionType.PassPriority);
    }

    [Fact]
    public async Task GetAction_PlaysLandBeforeSpell()
    {
        var (state, player) = CreateGameWithBot();
        var mountain = new GameCard { Name = "Mountain", CardTypes = CardType.Land };
        var goblin = new GameCard
        {
            Name = "Mogg Fanatic",
            CardTypes = CardType.Creature,
            ManaCost = ManaCost.Parse("{R}")
        };
        player.Hand.Add(mountain);
        player.Hand.Add(goblin);

        var action = await ((AiBotDecisionHandler)player.DecisionHandler)
            .GetAction(state, player.Id);

        // Land first, spell after
        action.CardId.Should().Be(mountain.Id);
    }

    [Fact]
    public async Task GetAction_NonMainPhase_Passes()
    {
        var (state, player) = CreateGameWithBot();
        state.CurrentPhase = Phase.Upkeep;
        player.Hand.Add(new GameCard { Name = "Mountain", CardTypes = CardType.Land });

        var action = await ((AiBotDecisionHandler)player.DecisionHandler)
            .GetAction(state, player.Id);

        action.Type.Should().Be(ActionType.PassPriority);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "AiBotActionTests" -v quiet`
Expected: FAIL — `GetAction` currently always returns Pass.

**Step 3: Implement GetAction**

Replace the stub `GetAction` in `AiBotDecisionHandler.cs`:

```csharp
public Task<GameAction> GetAction(GameState gameState, Guid playerId,
    CancellationToken ct = default)
{
    // Only act during main phases
    if (gameState.CurrentPhase != Phase.MainPhase1 && gameState.CurrentPhase != Phase.MainPhase2)
        return Task.FromResult(GameAction.Pass(playerId));

    var player = gameState.Player1.Id == playerId ? gameState.Player1 : gameState.Player2;
    var hand = player.Hand.Cards;

    if (hand.Count == 0)
        return Task.FromResult(GameAction.Pass(playerId));

    // Priority 1: Play a land if we haven't used our land drop
    if (player.LandsPlayedThisTurn == 0)
    {
        var land = hand.FirstOrDefault(c => c.IsLand);
        if (land != null)
            return Task.FromResult(GameAction.PlayCard(playerId, land.Id));
    }

    // Priority 2: Cast the most expensive affordable spell
    var castable = hand
        .Where(c => !c.IsLand && c.ManaCost != null && player.ManaPool.CanPay(c.ManaCost))
        .OrderByDescending(c => c.ManaCost!.ConvertedManaCost)
        .FirstOrDefault();

    if (castable != null)
        return Task.FromResult(GameAction.PlayCard(playerId, castable.Id));

    // Priority 3: Pass
    return Task.FromResult(GameAction.Pass(playerId));
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "AiBotActionTests" -v quiet`
Expected: PASS (all tests)

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs tests/MtgDecker.Engine.Tests/AI/AiBotActionTests.cs
git commit -m "feat(engine): add AI bot action selection with land-first, greedy-cast heuristic"
```

---

### Task 6: AiBotDecisionHandler — Combat Decisions

Attack and block heuristics. Attack when profitable, block to trade favorably or chump when lethal.

**Files:**
- Modify: `src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs`
- Test: `tests/MtgDecker.Engine.Tests/AI/AiBotCombatTests.cs`

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/AI/AiBotCombatTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine.AI;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests.AI;

public class AiBotCombatTests
{
    private readonly AiBotDecisionHandler _bot = new();

    private static GameCard MakeCreature(string name, int power, int toughness) => new()
    {
        Name = name,
        Power = power,
        Toughness = toughness,
        CardTypes = CardType.Creature,
    };

    [Fact]
    public async Task ChooseAttackers_NoBlockers_AttacksWithAll()
    {
        // Opponent has no creatures — attack with everything
        var attackers = new List<GameCard>
        {
            MakeCreature("Bear", 2, 2),
            MakeCreature("Goblin", 1, 1),
        };

        var result = await _bot.ChooseAttackers(attackers);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ChooseBlockers_NoAttackers_ReturnsEmpty()
    {
        var blockers = new List<GameCard> { MakeCreature("Bear", 2, 2) };
        var attackers = new List<GameCard>(); // no attackers

        var result = await _bot.ChooseBlockers(blockers, attackers);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ChooseBlockers_FavorableTrade_Blocks()
    {
        var myBlocker = MakeCreature("Bear", 2, 2);
        var theirAttacker = MakeCreature("Big Creature", 3, 2);

        var blockers = new List<GameCard> { myBlocker };
        var attackers = new List<GameCard> { theirAttacker };

        var result = await _bot.ChooseBlockers(blockers, attackers);

        // Bear (2/2) can kill the 3/2 — favorable trade
        result.Should().ContainKey(myBlocker.Id);
    }

    [Fact]
    public async Task OrderBlockers_OrdersByToughnessAscending()
    {
        var small = MakeCreature("Goblin", 1, 1);
        var big = MakeCreature("Bear", 2, 2);
        var blockers = new List<GameCard> { big, small };

        var result = await _bot.OrderBlockers(Guid.NewGuid(), blockers);

        result[0].Should().Be(small.Id); // kill smallest first
        result[1].Should().Be(big.Id);
    }

    [Fact]
    public async Task ChooseBlockers_UnfavorableTrade_DoesNotBlock()
    {
        var myBlocker = MakeCreature("Goblin", 1, 1);
        var theirAttacker = MakeCreature("Small Attacker", 1, 3);

        var blockers = new List<GameCard> { myBlocker };
        var attackers = new List<GameCard> { theirAttacker };

        var result = await _bot.ChooseBlockers(blockers, attackers);

        // Goblin 1/1 can't kill the 1/3, would die for nothing — don't block
        result.Should().BeEmpty();
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "AiBotCombatTests" -v quiet`
Expected: FAIL — combat methods return empty defaults.

**Step 3: Implement combat methods**

Replace the stubs in `AiBotDecisionHandler.cs`:

```csharp
public Task<IReadOnlyList<Guid>> ChooseAttackers(IReadOnlyList<GameCard> eligibleAttackers,
    CancellationToken ct = default)
{
    // Simple heuristic: attack with all eligible creatures
    // (The engine already filters for summoning sickness)
    var attackerIds = eligibleAttackers.Select(c => c.Id).ToList();
    return Task.FromResult<IReadOnlyList<Guid>>(attackerIds);
}

public Task<Dictionary<Guid, Guid>> ChooseBlockers(IReadOnlyList<GameCard> eligibleBlockers,
    IReadOnlyList<GameCard> attackers, CancellationToken ct = default)
{
    var assignments = new Dictionary<Guid, Guid>();
    var usedBlockers = new HashSet<Guid>();

    foreach (var attacker in attackers.OrderByDescending(a => a.Power ?? 0))
    {
        // Find best blocker that can kill this attacker (favorable trade)
        var bestBlocker = eligibleBlockers
            .Where(b => !usedBlockers.Contains(b.Id))
            .Where(b => (b.Power ?? 0) >= (attacker.Toughness ?? 0)) // can kill attacker
            .OrderBy(b => b.Power ?? 0) // use smallest sufficient blocker
            .FirstOrDefault();

        if (bestBlocker != null)
        {
            assignments[bestBlocker.Id] = attacker.Id;
            usedBlockers.Add(bestBlocker.Id);
        }
    }

    return Task.FromResult(assignments);
}

public Task<IReadOnlyList<Guid>> OrderBlockers(Guid attackerId, IReadOnlyList<GameCard> blockers,
    CancellationToken ct = default)
{
    // Order by toughness ascending — kill smallest first
    var ordered = blockers
        .OrderBy(b => b.Toughness ?? 0)
        .Select(b => b.Id)
        .ToList();
    return Task.FromResult<IReadOnlyList<Guid>>(ordered);
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "AiBotCombatTests" -v quiet`
Expected: PASS (all tests)

**Step 5: Run full test suite**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ -v quiet`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs tests/MtgDecker.Engine.Tests/AI/AiBotCombatTests.cs
git commit -m "feat(engine): add AI bot combat heuristics for attack and block decisions"
```

---

### Task 7: SimulationResult, BatchResult, and SimulationRunner

The simulation infrastructure: data models and the runner that orchestrates bot-vs-bot games.

**Files:**
- Create: `src/MtgDecker.Engine/Simulation/SimulationResult.cs`
- Create: `src/MtgDecker.Engine/Simulation/BatchResult.cs`
- Create: `src/MtgDecker.Engine/Simulation/SimulationRunner.cs`
- Test: `tests/MtgDecker.Engine.Tests/Simulation/SimulationRunnerTests.cs`

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/Simulation/SimulationRunnerTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Simulation;

namespace MtgDecker.Engine.Tests.Simulation;

public class SimulationRunnerTests
{
    private static List<GameCard> CreateSimpleDeck(int creatures, int lands)
    {
        var deck = new List<GameCard>();
        for (int i = 0; i < creatures; i++)
            deck.Add(GameCard.Create("Mogg Fanatic"));
        for (int i = 0; i < lands; i++)
            deck.Add(GameCard.Create("Mountain"));
        return deck;
    }

    [Fact]
    public async Task RunGameAsync_CompletesWithoutException()
    {
        var runner = new SimulationRunner();
        var deck1 = CreateSimpleDeck(20, 20);
        var deck2 = CreateSimpleDeck(20, 20);

        var result = await runner.RunGameAsync(deck1, deck2);

        result.Should().NotBeNull();
        result.TotalTurns.Should().BeGreaterThan(0);
        result.GameLog.Should().NotBeEmpty();
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task RunGameAsync_HasWinner()
    {
        var runner = new SimulationRunner();
        var deck1 = CreateSimpleDeck(20, 20);
        var deck2 = CreateSimpleDeck(20, 20);

        var result = await runner.RunGameAsync(deck1, deck2);

        // Game should end with a winner (combat or deckout)
        (result.WinnerName != null || result.IsDraw).Should().BeTrue();
    }

    [Fact]
    public async Task RunGameAsync_GameLogContainsTurns()
    {
        var runner = new SimulationRunner();
        var deck1 = CreateSimpleDeck(20, 20);
        var deck2 = CreateSimpleDeck(20, 20);

        var result = await runner.RunGameAsync(deck1, deck2);

        result.GameLog.Should().Contain(l => l.Contains("Turn 1"));
    }

    [Fact]
    public async Task RunBatchAsync_ReturnsCorrectGameCount()
    {
        var runner = new SimulationRunner();
        var deck1 = CreateSimpleDeck(20, 20);
        var deck2 = CreateSimpleDeck(20, 20);

        var batch = await runner.RunBatchAsync(deck1, deck2, 3);

        batch.TotalGames.Should().Be(3);
        batch.Games.Should().HaveCount(3);
        batch.Player1Wins.Should().BeGreaterThanOrEqualTo(0);
        batch.Player2Wins.Should().BeGreaterThanOrEqualTo(0);
        (batch.Player1Wins + batch.Player2Wins + batch.Draws).Should().Be(3);
    }

    [Fact]
    public async Task RunBatchAsync_WinRateIsBetweenZeroAndOne()
    {
        var runner = new SimulationRunner();
        var deck1 = CreateSimpleDeck(20, 20);
        var deck2 = CreateSimpleDeck(20, 20);

        var batch = await runner.RunBatchAsync(deck1, deck2, 5);

        batch.Player1WinRate.Should().BeInRange(0.0, 1.0);
        batch.AverageGameLength.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RunGameAsync_CustomNames()
    {
        var runner = new SimulationRunner();
        var deck1 = CreateSimpleDeck(20, 20);
        var deck2 = CreateSimpleDeck(20, 20);

        var result = await runner.RunGameAsync(deck1, deck2, "Goblins", "Enchantress");

        result.GameLog.Should().Contain(l => l.Contains("Goblins") || l.Contains("Enchantress"));
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "SimulationRunnerTests" -v quiet`
Expected: FAIL — classes don't exist.

**Step 3: Create SimulationResult**

Create `src/MtgDecker.Engine/Simulation/SimulationResult.cs`:

```csharp
namespace MtgDecker.Engine.Simulation;

public record SimulationResult(
    string? WinnerName,
    string? LoserName,
    bool IsDraw,
    int TotalTurns,
    int Player1FinalLife,
    int Player2FinalLife,
    IReadOnlyList<string> GameLog,
    TimeSpan Duration);
```

**Step 4: Create BatchResult**

Create `src/MtgDecker.Engine/Simulation/BatchResult.cs`:

```csharp
namespace MtgDecker.Engine.Simulation;

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

**Step 5: Create SimulationRunner**

Create `src/MtgDecker.Engine/Simulation/SimulationRunner.cs`:

```csharp
using System.Diagnostics;
using MtgDecker.Engine.AI;

namespace MtgDecker.Engine.Simulation;

public class SimulationRunner
{
    private const int MaxTurns = 100; // Safety valve

    public async Task<SimulationResult> RunGameAsync(
        IReadOnlyList<GameCard> deck1,
        IReadOnlyList<GameCard> deck2,
        string player1Name = "Bot A",
        string player2Name = "Bot B",
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var handler1 = new AiBotDecisionHandler();
        var handler2 = new AiBotDecisionHandler();

        var p1 = new Player(Guid.NewGuid(), player1Name, handler1);
        var p2 = new Player(Guid.NewGuid(), player2Name, handler2);

        // Copy cards into libraries (each game needs fresh copies)
        foreach (var card in deck1)
            p1.Library.Add(CloneCard(card));
        foreach (var card in deck2)
            p2.Library.Add(CloneCard(card));

        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        await engine.StartGameAsync(ct);
        state.IsFirstTurn = true;

        while (!state.IsGameOver && state.TurnNumber <= MaxTurns)
        {
            ct.ThrowIfCancellationRequested();
            await engine.RunTurnAsync(ct);
        }

        if (!state.IsGameOver)
        {
            // Max turns reached — draw
            state.IsGameOver = true;
            state.Log($"Game ended in a draw after {MaxTurns} turns.");
        }

        sw.Stop();

        var winnerName = state.Winner;
        var loserName = winnerName == null ? null
            : winnerName == player1Name ? player2Name : player1Name;

        return new SimulationResult(
            WinnerName: winnerName,
            LoserName: loserName,
            IsDraw: winnerName == null,
            TotalTurns: state.TurnNumber,
            Player1FinalLife: p1.Life,
            Player2FinalLife: p2.Life,
            GameLog: state.GameLog.ToList(),
            Duration: sw.Elapsed);
    }

    public async Task<BatchResult> RunBatchAsync(
        IReadOnlyList<GameCard> deck1,
        IReadOnlyList<GameCard> deck2,
        int gameCount,
        string player1Name = "Bot A",
        string player2Name = "Bot B",
        CancellationToken ct = default)
    {
        var results = new List<SimulationResult>();

        for (int i = 0; i < gameCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            var result = await RunGameAsync(deck1, deck2, player1Name, player2Name, ct);
            results.Add(result);
        }

        var p1Wins = results.Count(r => r.WinnerName == player1Name);
        var p2Wins = results.Count(r => r.WinnerName == player2Name);
        var draws = results.Count(r => r.IsDraw);

        return new BatchResult(
            TotalGames: gameCount,
            Player1Wins: p1Wins,
            Player2Wins: p2Wins,
            Draws: draws,
            Player1WinRate: gameCount > 0 ? (double)p1Wins / gameCount : 0,
            AverageGameLength: results.Average(r => r.TotalTurns),
            AverageLifeDifferential: results.Average(r =>
                Math.Abs(r.Player1FinalLife - r.Player2FinalLife)),
            Games: results);
    }

    private static GameCard CloneCard(GameCard original)
    {
        return new GameCard
        {
            Name = original.Name,
            TypeLine = original.TypeLine,
            ImageUrl = original.ImageUrl,
            ManaCost = original.ManaCost,
            ManaAbility = original.ManaAbility,
            Power = original.Power,
            Toughness = original.Toughness,
            CardTypes = original.CardTypes,
            Subtypes = original.Subtypes,
            Triggers = original.Triggers,
            IsToken = original.IsToken,
        };
    }
}
```

**Note:** `CloneCard` creates a fresh `GameCard` with a new `Id` (auto-generated `Guid.NewGuid()`) so each game has independent card instances.

**Step 6: Run tests to verify they pass**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "SimulationRunnerTests" -v quiet`
Expected: PASS (all tests)

**Step 7: Run full test suite**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ -v quiet`
Expected: All pass

**Step 8: Commit**

```bash
git add src/MtgDecker.Engine/Simulation/ tests/MtgDecker.Engine.Tests/Simulation/
git commit -m "feat(engine): add SimulationRunner for bot-vs-bot games with batch statistics"
```

---

### Task 8: Integration Smoke Test

Run a full simulation with realistic decks (Goblins vs Goblins using CardDefinitions) to verify the complete pipeline works end-to-end. Also verify the game actually produces combat and meaningful play.

**Files:**
- Test: `tests/MtgDecker.Engine.Tests/Simulation/SimulationIntegrationTests.cs`

**Step 1: Write integration tests**

Create `tests/MtgDecker.Engine.Tests/Simulation/SimulationIntegrationTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine.Simulation;

namespace MtgDecker.Engine.Tests.Simulation;

public class SimulationIntegrationTests
{
    private static List<GameCard> CreateGoblinDeck()
    {
        var deck = new List<GameCard>();

        // 24 Mountains
        for (int i = 0; i < 24; i++)
            deck.Add(GameCard.Create("Mountain", "Basic Land — Mountain"));

        // 4x Goblin Lackey, Mogg Fanatic, Goblin Piledriver, Goblin Warchief
        for (int i = 0; i < 4; i++)
        {
            deck.Add(GameCard.Create("Goblin Lackey"));
            deck.Add(GameCard.Create("Mogg Fanatic"));
            deck.Add(GameCard.Create("Goblin Piledriver"));
            deck.Add(GameCard.Create("Goblin Warchief"));
        }

        // 4x Goblin Matron (ETB: search for Goblin)
        for (int i = 0; i < 4; i++)
            deck.Add(GameCard.Create("Goblin Matron"));

        // 4x Goblin Ringleader (ETB: reveal top 4)
        for (int i = 0; i < 4; i++)
            deck.Add(GameCard.Create("Goblin Ringleader"));

        // 2x Siege-Gang Commander (ETB: create 3 tokens)
        for (int i = 0; i < 2; i++)
            deck.Add(GameCard.Create("Siege-Gang Commander"));

        // Pad to 60 with Skirk Prospector
        while (deck.Count < 60)
            deck.Add(GameCard.Create("Skirk Prospector"));

        return deck;
    }

    [Fact]
    public async Task FullGame_GoblinsVsGoblins_CompletesWithWinner()
    {
        var runner = new SimulationRunner();
        var deck1 = CreateGoblinDeck();
        var deck2 = CreateGoblinDeck();

        var result = await runner.RunGameAsync(deck1, deck2, "Goblins A", "Goblins B");

        result.TotalTurns.Should().BeGreaterThan(1);
        result.GameLog.Should().NotBeEmpty();
        (result.WinnerName != null || result.IsDraw).Should().BeTrue();
    }

    [Fact]
    public async Task FullGame_ProducesCombat()
    {
        var runner = new SimulationRunner();
        var deck1 = CreateGoblinDeck();
        var deck2 = CreateGoblinDeck();

        var result = await runner.RunGameAsync(deck1, deck2, "Goblins A", "Goblins B");

        // Should have at least some combat actions logged
        result.GameLog.Should().Contain(l => l.Contains("attacks") || l.Contains("damage") || l.Contains("deals"));
    }

    [Fact]
    public async Task FullGame_TriggersFireDuringPlay()
    {
        var runner = new SimulationRunner();
        var deck1 = CreateGoblinDeck();
        var deck2 = CreateGoblinDeck();

        var result = await runner.RunGameAsync(deck1, deck2, "Goblins A", "Goblins B");

        // Goblin Matron, Ringleader, or Siege-Gang should trigger
        result.GameLog.Should().Contain(l =>
            l.Contains("triggers") ||
            l.Contains("searches library") ||
            l.Contains("creates a") ||
            l.Contains("Revealed"));
    }

    [Fact]
    public async Task BatchRun_FiveGames_AllComplete()
    {
        var runner = new SimulationRunner();
        var deck1 = CreateGoblinDeck();
        var deck2 = CreateGoblinDeck();

        var batch = await runner.RunBatchAsync(deck1, deck2, 5, "Goblins A", "Goblins B");

        batch.TotalGames.Should().Be(5);
        batch.Games.Should().OnlyContain(g => g.TotalTurns > 0);
        batch.AverageGameLength.Should().BeGreaterThan(0);
    }
}
```

**Step 2: Run tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "SimulationIntegrationTests" -v quiet`
Expected: PASS (all 4 tests). If any fail, debug and fix the underlying issue.

**Step 3: Run full test suite**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ -v quiet`
Expected: All pass

**Step 4: Commit**

```bash
git add tests/MtgDecker.Engine.Tests/Simulation/SimulationIntegrationTests.cs
git commit -m "test(engine): add integration tests for bot-vs-bot simulation with Goblin decks"
```

---

### Task 9: Final Verification

Run the complete test suite across all projects, build the web project, and verify nothing is broken.

**Step 1: Run all test projects**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ -v quiet
dotnet test tests/MtgDecker.Domain.Tests/ -v quiet
dotnet test tests/MtgDecker.Application.Tests/ -v quiet
dotnet test tests/MtgDecker.Infrastructure.Tests/ -v quiet
```

Expected: All pass with 0 failures.

**Step 2: Build web project**

```bash
dotnet build src/MtgDecker.Web/ --verbosity quiet
```

Expected: Build succeeded, 0 errors.

**Step 3: Report final counts**

Report total test count across all projects and confirm all green.
