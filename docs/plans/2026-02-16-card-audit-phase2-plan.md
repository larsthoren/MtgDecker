# Card Audit Phase 2: Core Card Selection Spells

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace three incorrect `DrawCardsEffect` placeholders with proper card selection mechanics following the established Brainstorm/Preordain pattern.

**Architecture:** Each card gets a new `SpellEffect` subclass that overrides `ResolveAsync()` for interactive player decisions. Uses existing `ChooseCard()` and `RevealCards()` on `IPlayerDecisionHandler` — no new handler methods needed. Fact or Fiction requires accessing the opponent's decision handler via `Player.DecisionHandler`.

**Tech Stack:** C# 14, xUnit, FluentAssertions, existing TestDecisionHandler queue pattern

**Note:** Skeletal Scrying's real mana cost is {X}{B} but X-costs are not supported by the engine yet. This phase fixes the *effect* only (exile from graveyard, draw, lose life). Full X-cost mana support is deferred.

---

### Task 1: ImpulseEffect — Look at top 4, pick 1, rest to bottom

**Files:**
- Create: `src/MtgDecker.Engine/Effects/ImpulseEffect.cs`
- Test: `tests/MtgDecker.Engine.Tests/Effects/ImpulseEffectTests.cs`

**Context:** Impulse is currently `DrawCardsEffect(1)`. It should look at the top 4 cards, let the player pick 1, and put the other 3 on the bottom of their library in any order.

Follow the PreordainEffect pattern: use `PeekTop(4)` for non-destructive view, `ChooseCard()` for selection, then move cards to hand/bottom.

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/Effects/ImpulseEffectTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests.Effects;

public class ImpulseEffectTests
{
    private (GameState state, Player player, TestDecisionHandler handler, StackObject spell) Setup()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        // Stock library with known cards (top of library = last added)
        p1.Library.Clear();
        var card1 = new GameCard { Name = "Card A" };
        var card2 = new GameCard { Name = "Card B" };
        var card3 = new GameCard { Name = "Card C" };
        var card4 = new GameCard { Name = "Card D" };
        var card5 = new GameCard { Name = "Card E" };
        // Add bottom-to-top: E is bottom, A is top
        p1.Library.Add(card5);
        p1.Library.Add(card4);
        p1.Library.Add(card3);
        p1.Library.Add(card2);
        p1.Library.Add(card1);

        var spell = new StackObject(
            new GameCard { Name = "Impulse" },
            p1.Id,
            new Dictionary<ManaColor, int>(),
            [],
            0);

        return (state, p1, handler, spell);
    }

    [Fact]
    public async Task Impulse_PlayerPicksOne_PutsRestOnBottom()
    {
        var (state, player, handler, spell) = Setup();
        var effect = new ImpulseEffect();

        // Library top 4: A, B, C, D (from top). Player picks B.
        var topCards = player.Library.PeekTop(4);
        var cardB = topCards.First(c => c.Name == "Card B");
        handler.EnqueueCardChoice(cardB.Id);

        await effect.ResolveAsync(state, spell, handler);

        player.Hand.Cards.Should().ContainSingle(c => c.Name == "Card B");
        player.Library.Count.Should().Be(4, "started with 5, took 1 to hand, 3 went to bottom");
        // Card E should still be in library (it was 5th, not in top 4)
        player.Library.Cards.Should().Contain(c => c.Name == "Card E");
    }

    [Fact]
    public async Task Impulse_RemainingCardsGoToBottom()
    {
        var (state, player, handler, spell) = Setup();
        var effect = new ImpulseEffect();

        var topCards = player.Library.PeekTop(4);
        var cardA = topCards.First(c => c.Name == "Card A");
        handler.EnqueueCardChoice(cardA.Id);

        await effect.ResolveAsync(state, spell, handler);

        // After Impulse: hand has A, library top should still be E (5th card, now top)
        // The 3 remaining cards (B, C, D) went to bottom
        player.Hand.Cards.Should().ContainSingle(c => c.Name == "Card A");
        player.Library.Count.Should().Be(4);
    }

    [Fact]
    public async Task Impulse_LibraryHasFewerThan4_WorksWithAvailable()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        p1.Library.Clear();
        var card1 = new GameCard { Name = "Only Card" };
        var card2 = new GameCard { Name = "Second Card" };
        p1.Library.Add(card2);
        p1.Library.Add(card1);

        handler.EnqueueCardChoice(card1.Id);

        var spell = new StackObject(
            new GameCard { Name = "Impulse" },
            p1.Id, new Dictionary<ManaColor, int>(), [], 0);
        var effect = new ImpulseEffect();

        await effect.ResolveAsync(state, spell, handler);

        p1.Hand.Cards.Should().ContainSingle(c => c.Name == "Only Card");
        p1.Library.Count.Should().Be(1, "second card went to bottom");
    }

    [Fact]
    public async Task Impulse_EmptyLibrary_DoesNothing()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        p1.Library.Clear();

        var spell = new StackObject(
            new GameCard { Name = "Impulse" },
            p1.Id, new Dictionary<ManaColor, int>(), [], 0);
        var effect = new ImpulseEffect();

        await effect.ResolveAsync(state, spell, handler);

        p1.Hand.Count.Should().Be(0);
        p1.Library.Count.Should().Be(0);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "ImpulseEffectTests" -v n`
Expected: FAIL — `ImpulseEffect` class doesn't exist

**Step 3: Implement ImpulseEffect**

Create `src/MtgDecker.Engine/Effects/ImpulseEffect.cs`:

```csharp
namespace MtgDecker.Engine.Effects;

public class ImpulseEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var player = state.GetPlayer(spell.ControllerId);

        var topCards = player.Library.PeekTop(4).ToList();
        if (topCards.Count == 0)
        {
            state.Log($"{player.Name} has no cards in library (Impulse).");
            return;
        }

        // Show cards and let player pick one
        var chosenId = await handler.ChooseCard(topCards,
            "Impulse: Choose one card to put in your hand", optional: false, ct);

        GameCard? chosen = null;
        if (chosenId.HasValue)
            chosen = topCards.FirstOrDefault(c => c.Id == chosenId.Value);

        // Fallback: if handler returned invalid ID, take first card
        chosen ??= topCards[0];

        // Remove all peeked cards from library
        foreach (var card in topCards)
            player.Library.RemoveById(card.Id);

        // Put chosen card in hand
        player.Hand.Add(chosen);
        state.Log($"{player.Name} picks {chosen.Name} (Impulse).");

        // Put the rest on bottom of library
        foreach (var card in topCards.Where(c => c.Id != chosen.Id))
            player.Library.AddToBottom(card);

        state.Log($"{player.Name} puts {topCards.Count - 1} card(s) on the bottom of their library.");
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "ImpulseEffectTests" -v n`
Expected: PASS

**Step 5: Run all engine tests for regressions**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --nologo -v q`
Expected: All pass (ignore known flaky GameEngineIntegrationTests)

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/Effects/ImpulseEffect.cs tests/MtgDecker.Engine.Tests/Effects/ImpulseEffectTests.cs
git commit -m "feat(engine): add ImpulseEffect — look at top 4, pick 1, rest to bottom"
```

---

### Task 2: FactOrFictionEffect — Reveal 5, opponent splits, caster picks pile

**Files:**
- Create: `src/MtgDecker.Engine/Effects/FactOrFictionEffect.cs`
- Test: `tests/MtgDecker.Engine.Tests/Effects/FactOrFictionEffectTests.cs`

**Context:** Fact or Fiction is currently `DrawCardsEffect(3)`. The real card reveals the top 5, the *opponent* separates them into two piles, and the *caster* chooses which pile to put in their hand (the other pile goes to the graveyard).

Key design decisions:
- Access opponent via `state.Player1`/`state.Player2` comparison with `spell.ControllerId`
- Opponent splits by choosing cards for pile 1 one at a time (using `ChooseCard` with `optional: true`). When they skip, pile 1 is done and remaining cards form pile 2.
- Caster picks a pile using `ChooseCard` on pile 1 (`optional: true`). If they choose from pile 1, they get pile 1; if they skip, they get pile 2.
- The pile not chosen goes to the graveyard.

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/Effects/FactOrFictionEffectTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests.Effects;

public class FactOrFictionEffectTests
{
    private (GameState state, Player caster, Player opponent,
        TestDecisionHandler casterHandler, TestDecisionHandler opponentHandler,
        StackObject spell) Setup()
    {
        var casterHandler = new TestDecisionHandler();
        var opponentHandler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Caster", casterHandler);
        var p2 = new Player(Guid.NewGuid(), "Opponent", opponentHandler);
        var state = new GameState(p1, p2);

        // Stock library with 7 known cards (top = last added)
        p1.Library.Clear();
        for (int i = 7; i >= 1; i--)
            p1.Library.Add(new GameCard { Name = $"Card {i}" });
        // Top of library: Card 1, Card 2, ..., Card 7 is bottom

        var spell = new StackObject(
            new GameCard { Name = "Fact or Fiction" },
            p1.Id,
            new Dictionary<ManaColor, int>(),
            [],
            0);

        return (state, p1, p2, casterHandler, opponentHandler, spell);
    }

    [Fact]
    public async Task FoF_OpponentSplits3and2_CasterChoosesPile1()
    {
        var (state, caster, opponent, casterHandler, opponentHandler, spell) = Setup();
        var effect = new FactOrFictionEffect();

        // Top 5 cards: Card 1, Card 2, Card 3, Card 4, Card 5
        var top5 = caster.Library.PeekTop(5);

        // Opponent puts Card 1, Card 2, Card 3 into pile 1 (choose 3, then skip)
        opponentHandler.EnqueueCardChoice(top5[0].Id); // Card 1
        opponentHandler.EnqueueCardChoice(top5[1].Id); // Card 2
        opponentHandler.EnqueueCardChoice(top5[2].Id); // Card 3
        opponentHandler.EnqueueCardChoice(null);         // done with pile 1

        // Caster chooses pile 1 (picks any card from pile 1)
        casterHandler.EnqueueCardChoice(top5[0].Id);

        await effect.ResolveAsync(state, spell, casterHandler);

        // Caster hand has pile 1 (3 cards)
        caster.Hand.Count.Should().Be(3);
        caster.Hand.Cards.Should().Contain(c => c.Name == "Card 1");
        caster.Hand.Cards.Should().Contain(c => c.Name == "Card 2");
        caster.Hand.Cards.Should().Contain(c => c.Name == "Card 3");

        // Pile 2 (Card 4, Card 5) goes to graveyard
        caster.Graveyard.Cards.Should().Contain(c => c.Name == "Card 4");
        caster.Graveyard.Cards.Should().Contain(c => c.Name == "Card 5");
    }

    [Fact]
    public async Task FoF_OpponentSplits3and2_CasterChoosesPile2()
    {
        var (state, caster, opponent, casterHandler, opponentHandler, spell) = Setup();
        var effect = new FactOrFictionEffect();

        var top5 = caster.Library.PeekTop(5);

        // Opponent puts Card 1, Card 2, Card 3 into pile 1
        opponentHandler.EnqueueCardChoice(top5[0].Id);
        opponentHandler.EnqueueCardChoice(top5[1].Id);
        opponentHandler.EnqueueCardChoice(top5[2].Id);
        opponentHandler.EnqueueCardChoice(null);

        // Caster skips pile 1 → gets pile 2
        casterHandler.EnqueueCardChoice(null);

        await effect.ResolveAsync(state, spell, casterHandler);

        // Caster hand has pile 2 (2 cards)
        caster.Hand.Count.Should().Be(2);
        caster.Hand.Cards.Should().Contain(c => c.Name == "Card 4");
        caster.Hand.Cards.Should().Contain(c => c.Name == "Card 5");

        // Pile 1 (Card 1, Card 2, Card 3) goes to graveyard
        caster.Graveyard.Cards.Should().Contain(c => c.Name == "Card 1");
        caster.Graveyard.Cards.Should().Contain(c => c.Name == "Card 2");
        caster.Graveyard.Cards.Should().Contain(c => c.Name == "Card 3");
    }

    [Fact]
    public async Task FoF_OpponentPutsAllInOnePile_CasterGetsAll()
    {
        var (state, caster, opponent, casterHandler, opponentHandler, spell) = Setup();
        var effect = new FactOrFictionEffect();

        var top5 = caster.Library.PeekTop(5);

        // Opponent puts all 5 into pile 1
        foreach (var card in top5)
            opponentHandler.EnqueueCardChoice(card.Id);
        opponentHandler.EnqueueCardChoice(null);

        // Caster chooses pile 1
        casterHandler.EnqueueCardChoice(top5[0].Id);

        await effect.ResolveAsync(state, spell, casterHandler);

        caster.Hand.Count.Should().Be(5);
        caster.Graveyard.Count.Should().Be(0);
    }

    [Fact]
    public async Task FoF_OpponentPutsNoneInPile1_CasterGetsAll()
    {
        var (state, caster, opponent, casterHandler, opponentHandler, spell) = Setup();
        var effect = new FactOrFictionEffect();

        // Opponent immediately skips → pile 1 is empty, pile 2 is all 5
        opponentHandler.EnqueueCardChoice(null);

        // Caster skips pile 1 (empty) → gets pile 2 (all 5)
        casterHandler.EnqueueCardChoice(null);

        await effect.ResolveAsync(state, spell, casterHandler);

        caster.Hand.Count.Should().Be(5);
        caster.Graveyard.Count.Should().Be(0);
    }

    [Fact]
    public async Task FoF_LibraryHasFewerThan5_WorksWithAvailable()
    {
        var casterHandler = new TestDecisionHandler();
        var opponentHandler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Caster", casterHandler);
        var p2 = new Player(Guid.NewGuid(), "Opponent", opponentHandler);
        var state = new GameState(p1, p2);

        p1.Library.Clear();
        var cardA = new GameCard { Name = "Card A" };
        var cardB = new GameCard { Name = "Card B" };
        p1.Library.Add(cardB);
        p1.Library.Add(cardA);

        // Opponent puts Card A in pile 1
        opponentHandler.EnqueueCardChoice(cardA.Id);
        opponentHandler.EnqueueCardChoice(null);

        // Caster picks pile 1
        casterHandler.EnqueueCardChoice(cardA.Id);

        var spell = new StackObject(
            new GameCard { Name = "Fact or Fiction" },
            p1.Id, new Dictionary<ManaColor, int>(), [], 0);
        var effect = new FactOrFictionEffect();

        await effect.ResolveAsync(state, spell, casterHandler);

        p1.Hand.Count.Should().Be(1);
        p1.Hand.Cards.Should().Contain(c => c.Name == "Card A");
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Card B");
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "FactOrFictionEffectTests" -v n`
Expected: FAIL — `FactOrFictionEffect` class doesn't exist

**Step 3: Implement FactOrFictionEffect**

Create `src/MtgDecker.Engine/Effects/FactOrFictionEffect.cs`:

```csharp
namespace MtgDecker.Engine.Effects;

public class FactOrFictionEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler casterHandler, CancellationToken ct = default)
    {
        var caster = state.GetPlayer(spell.ControllerId);
        var opponent = spell.ControllerId == state.Player1.Id ? state.Player2 : state.Player1;

        var topCards = caster.Library.PeekTop(5).ToList();
        if (topCards.Count == 0)
        {
            state.Log($"{caster.Name} has no cards in library (Fact or Fiction).");
            return;
        }

        // Remove revealed cards from library
        foreach (var card in topCards)
            caster.Library.RemoveById(card.Id);

        state.Log($"{caster.Name} reveals {topCards.Count} cards (Fact or Fiction).");

        // Opponent splits into two piles
        var pile1 = new List<GameCard>();
        var remaining = new List<GameCard>(topCards);

        while (remaining.Count > 0)
        {
            var chosenId = await opponent.DecisionHandler.ChooseCard(
                remaining,
                $"Fact or Fiction: Choose cards for Pile 1 ({pile1.Count} selected). Skip when done.",
                optional: true, ct);

            if (!chosenId.HasValue) break;

            var chosen = remaining.FirstOrDefault(c => c.Id == chosenId.Value);
            if (chosen != null)
            {
                pile1.Add(chosen);
                remaining.Remove(chosen);
            }
        }

        var pile2 = remaining;

        state.Log($"Piles split: Pile 1 ({pile1.Count} cards), Pile 2 ({pile2.Count} cards).");

        // Caster chooses a pile
        List<GameCard> chosenPile;
        List<GameCard> rejectedPile;

        if (pile1.Count > 0)
        {
            var casterChoice = await casterHandler.ChooseCard(
                pile1,
                $"Choose Pile 1 ({pile1.Count} cards)? Skip for Pile 2 ({pile2.Count} cards).",
                optional: true, ct);

            if (casterChoice.HasValue)
            {
                chosenPile = pile1;
                rejectedPile = pile2;
            }
            else
            {
                chosenPile = pile2;
                rejectedPile = pile1;
            }
        }
        else
        {
            // Pile 1 is empty, caster gets pile 2 automatically
            chosenPile = pile2;
            rejectedPile = pile1;
        }

        // Chosen pile goes to hand
        foreach (var card in chosenPile)
            caster.Hand.Add(card);

        // Rejected pile goes to graveyard
        foreach (var card in rejectedPile)
            caster.Graveyard.Add(card);

        state.Log($"{caster.Name} takes {chosenPile.Count} card(s), {rejectedPile.Count} card(s) to graveyard.");
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "FactOrFictionEffectTests" -v n`
Expected: PASS

**Step 5: Run all engine tests for regressions**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --nologo -v q`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/Effects/FactOrFictionEffect.cs tests/MtgDecker.Engine.Tests/Effects/FactOrFictionEffectTests.cs
git commit -m "feat(engine): add FactOrFictionEffect — reveal 5, opponent splits, caster picks pile"
```

---

### Task 3: SkeletalScryingEffect — Exile from graveyard, draw, lose life

**Files:**
- Create: `src/MtgDecker.Engine/Effects/SkeletalScryingEffect.cs`
- Test: `tests/MtgDecker.Engine.Tests/Effects/SkeletalScryingEffectTests.cs`

**Context:** Skeletal Scrying is currently `DrawCardsEffect(2)`. The real card: "As an additional cost, exile X cards from your graveyard. Draw X cards and lose X life." Since we don't have X-cost mana support, the effect lets the player choose how many graveyard cards to exile at resolution time. The count becomes X for drawing and life loss.

**Step 1: Write failing tests**

Create `tests/MtgDecker.Engine.Tests/Effects/SkeletalScryingEffectTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests.Effects;

public class SkeletalScryingEffectTests
{
    private (GameState state, Player player, TestDecisionHandler handler, StackObject spell) Setup()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        // Stock graveyard with cards
        p1.Graveyard.Add(new GameCard { Name = "GY Card 1" });
        p1.Graveyard.Add(new GameCard { Name = "GY Card 2" });
        p1.Graveyard.Add(new GameCard { Name = "GY Card 3" });
        p1.Graveyard.Add(new GameCard { Name = "GY Card 4" });

        // Stock library with cards to draw
        p1.Library.Clear();
        for (int i = 5; i >= 1; i--)
            p1.Library.Add(new GameCard { Name = $"Lib Card {i}" });

        var spell = new StackObject(
            new GameCard { Name = "Skeletal Scrying" },
            p1.Id,
            new Dictionary<ManaColor, int>(),
            [],
            0);

        return (state, p1, handler, spell);
    }

    [Fact]
    public async Task SkeletalScrying_Exile3_Draw3_Lose3Life()
    {
        var (state, player, handler, spell) = Setup();
        var effect = new SkeletalScryingEffect();

        // Player picks 3 graveyard cards to exile, then skips
        var gyCards = player.Graveyard.Cards;
        handler.EnqueueCardChoice(gyCards[0].Id);
        handler.EnqueueCardChoice(gyCards[1].Id);
        handler.EnqueueCardChoice(gyCards[2].Id);
        handler.EnqueueCardChoice(null); // done choosing

        await effect.ResolveAsync(state, spell, handler);

        player.Exile.Count.Should().Be(3);
        player.Graveyard.Count.Should().Be(1, "started with 4, exiled 3");
        player.Hand.Count.Should().Be(3, "draw X = 3");
        player.Life.Should().Be(17, "lose X = 3 life (20 - 3)");
    }

    [Fact]
    public async Task SkeletalScrying_Exile1_Draw1_Lose1Life()
    {
        var (state, player, handler, spell) = Setup();
        var effect = new SkeletalScryingEffect();

        var gyCards = player.Graveyard.Cards;
        handler.EnqueueCardChoice(gyCards[0].Id);
        handler.EnqueueCardChoice(null);

        await effect.ResolveAsync(state, spell, handler);

        player.Exile.Count.Should().Be(1);
        player.Graveyard.Count.Should().Be(3);
        player.Hand.Count.Should().Be(1);
        player.Life.Should().Be(19);
    }

    [Fact]
    public async Task SkeletalScrying_ExileNone_DrawsNothingLosesNothing()
    {
        var (state, player, handler, spell) = Setup();
        var effect = new SkeletalScryingEffect();

        // Player immediately skips — exiles 0
        handler.EnqueueCardChoice(null);

        await effect.ResolveAsync(state, spell, handler);

        player.Exile.Count.Should().Be(0);
        player.Graveyard.Count.Should().Be(4);
        player.Hand.Count.Should().Be(0);
        player.Life.Should().Be(20);
    }

    [Fact]
    public async Task SkeletalScrying_EmptyGraveyard_DoesNothing()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        // Empty graveyard, some library
        p1.Library.Add(new GameCard { Name = "Card" });

        var spell = new StackObject(
            new GameCard { Name = "Skeletal Scrying" },
            p1.Id, new Dictionary<ManaColor, int>(), [], 0);
        var effect = new SkeletalScryingEffect();

        await effect.ResolveAsync(state, spell, handler);

        p1.Exile.Count.Should().Be(0);
        p1.Hand.Count.Should().Be(0);
        p1.Life.Should().Be(20);
    }

    [Fact]
    public async Task SkeletalScrying_NotEnoughLibrary_DrawsWhatIsAvailable()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        // 3 cards in graveyard, only 1 in library
        p1.Graveyard.Add(new GameCard { Name = "GY 1" });
        p1.Graveyard.Add(new GameCard { Name = "GY 2" });
        p1.Graveyard.Add(new GameCard { Name = "GY 3" });
        p1.Library.Clear();
        p1.Library.Add(new GameCard { Name = "Only Card" });

        // Exile all 3
        var gyCards = p1.Graveyard.Cards;
        handler.EnqueueCardChoice(gyCards[0].Id);
        handler.EnqueueCardChoice(gyCards[1].Id);
        handler.EnqueueCardChoice(gyCards[2].Id);
        handler.EnqueueCardChoice(null);

        var spell = new StackObject(
            new GameCard { Name = "Skeletal Scrying" },
            p1.Id, new Dictionary<ManaColor, int>(), [], 0);
        var effect = new SkeletalScryingEffect();

        await effect.ResolveAsync(state, spell, handler);

        p1.Exile.Count.Should().Be(3);
        p1.Hand.Count.Should().Be(1, "only 1 card in library despite X=3");
        p1.Life.Should().Be(17, "still lose 3 life even if draw fewer");
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "SkeletalScryingEffectTests" -v n`
Expected: FAIL — `SkeletalScryingEffect` class doesn't exist

**Step 3: Implement SkeletalScryingEffect**

Create `src/MtgDecker.Engine/Effects/SkeletalScryingEffect.cs`:

```csharp
namespace MtgDecker.Engine.Effects;

public class SkeletalScryingEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var player = state.GetPlayer(spell.ControllerId);

        if (player.Graveyard.Count == 0)
        {
            state.Log($"{player.Name} has no cards in graveyard (Skeletal Scrying).");
            return;
        }

        // Player chooses cards from graveyard to exile
        var exiled = new List<GameCard>();
        while (player.Graveyard.Count > 0)
        {
            var remaining = player.Graveyard.Cards
                .Where(c => !exiled.Any(e => e.Id == c.Id))
                .ToList();

            if (remaining.Count == 0) break;

            var chosenId = await handler.ChooseCard(
                remaining,
                $"Skeletal Scrying: Choose a card to exile ({exiled.Count} exiled so far). Skip when done.",
                optional: true, ct);

            if (!chosenId.HasValue) break;

            var chosen = player.Graveyard.RemoveById(chosenId.Value);
            if (chosen != null)
            {
                player.Exile.Add(chosen);
                exiled.Add(chosen);
            }
        }

        var x = exiled.Count;
        if (x == 0)
        {
            state.Log($"{player.Name} exiles 0 cards (Skeletal Scrying).");
            return;
        }

        state.Log($"{player.Name} exiles {x} card(s) from graveyard (Skeletal Scrying).");

        // Draw X cards
        var drawn = 0;
        for (int i = 0; i < x; i++)
        {
            var card = player.Library.DrawFromTop();
            if (card == null) break;
            player.Hand.Add(card);
            drawn++;
        }
        state.Log($"{player.Name} draws {drawn} card(s).");

        // Lose X life
        player.AdjustLife(-x);
        state.Log($"{player.Name} loses {x} life ({player.Life} life).");
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "SkeletalScryingEffectTests" -v n`
Expected: PASS

**Step 5: Run all engine tests for regressions**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --nologo -v q`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/Effects/SkeletalScryingEffect.cs tests/MtgDecker.Engine.Tests/Effects/SkeletalScryingEffectTests.cs
git commit -m "feat(engine): add SkeletalScryingEffect — exile from graveyard, draw X, lose X life"
```

---

### Task 4: Wire up cards in CardDefinitions + final verification

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Test: `tests/MtgDecker.Engine.Tests/CardDefinitionsTests.cs`

**Context:** Replace the incorrect `DrawCardsEffect` with the new effect classes for Impulse, Fact or Fiction, and Skeletal Scrying.

**Step 1: Write failing tests**

Add to `tests/MtgDecker.Engine.Tests/CardDefinitionsTests.cs`:

```csharp
// === Card audit Phase 2: correct card selection effects ===

[Fact]
public void Impulse_HasImpulseEffect()
{
    CardDefinitions.TryGet("Impulse", out var def);

    def!.Effect.Should().BeOfType<ImpulseEffect>(
        because: "Impulse looks at top 4, picks 1, not just draw 1");
}

[Fact]
public void FactOrFiction_HasFactOrFictionEffect()
{
    CardDefinitions.TryGet("Fact or Fiction", out var def);

    def!.Effect.Should().BeOfType<FactOrFictionEffect>(
        because: "Fact or Fiction reveals 5, opponent splits, caster picks pile");
}

[Fact]
public void SkeletalScrying_HasSkeletalScryingEffect()
{
    CardDefinitions.TryGet("Skeletal Scrying", out var def);

    def!.Effect.Should().BeOfType<SkeletalScryingEffect>(
        because: "Skeletal Scrying exiles from graveyard, draws X, loses X life");
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "Impulse_HasImpulseEffect|FactOrFiction_HasFactOrFictionEffect|SkeletalScrying_HasSkeletalScryingEffect" -v n`
Expected: FAIL — still using `DrawCardsEffect`

**Step 3: Update CardDefinitions.cs**

Change these three entries:

```csharp
// Impulse — was DrawCardsEffect(1)
["Impulse"] = new(ManaCost.Parse("{1}{U}"), null, null, null, CardType.Instant,
    Effect: new ImpulseEffect()),

// Fact or Fiction — was DrawCardsEffect(3)
["Fact or Fiction"] = new(ManaCost.Parse("{3}{U}"), null, null, null, CardType.Instant,
    Effect: new FactOrFictionEffect()),

// Skeletal Scrying — was DrawCardsEffect(2)
["Skeletal Scrying"] = new(ManaCost.Parse("{1}{B}"), null, null, null, CardType.Instant,
    Effect: new SkeletalScryingEffect()),
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "Impulse_HasImpulseEffect|FactOrFiction_HasFactOrFictionEffect|SkeletalScrying_HasSkeletalScryingEffect" -v n`
Expected: PASS

**Step 5: Run ALL test projects for final verification**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --nologo -v q
dotnet test tests/MtgDecker.Domain.Tests/ --nologo -v q
dotnet test tests/MtgDecker.Application.Tests/ --nologo -v q
dotnet test tests/MtgDecker.Infrastructure.Tests/ --nologo -v q
```

Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/CardDefinitions.cs tests/MtgDecker.Engine.Tests/CardDefinitionsTests.cs
git commit -m "fix(engine): wire Impulse, Fact or Fiction, Skeletal Scrying to correct effects"
```
