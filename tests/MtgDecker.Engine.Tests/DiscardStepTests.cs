using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class DiscardStepTests
{
    private static (GameEngine engine, GameState state, Player p1, Player p2,
        TestDecisionHandler handler1, TestDecisionHandler handler2) Setup()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", handler2);
        var state = new GameState(p1, p2);
        state.IsFirstTurn = true; // Skip draw on first turn to keep hand size predictable
        var engine = new GameEngine(state);
        return (engine, state, p1, p2, handler1, handler2);
    }

    [Fact]
    public async Task RunTurnAsync_HandExceedsMaxSize_PromptsDiscard()
    {
        var (engine, state, p1, _, _, _) = Setup();

        // Put 9 cards in P1's hand
        for (int i = 0; i < 9; i++)
            p1.Hand.Add(GameCard.Create($"Card {i + 1}"));

        // Need at least 1 card in library to avoid deck-out issues
        p1.Library.Add(GameCard.Create("LibraryCard"));

        // IsFirstTurn is true, so draw step is skipped — P1 stays at 9 cards before discard
        await engine.RunTurnAsync();

        // After cleanup/discard step, P1 should have exactly 7 cards in hand
        p1.Hand.Count.Should().Be(7, "player should discard down to max hand size of 7");
        p1.Graveyard.Count.Should().Be(2, "2 discarded cards should be in graveyard");
    }

    [Fact]
    public async Task RunTurnAsync_HandAtMaxSize_NoDiscard()
    {
        var (engine, state, p1, _, _, _) = Setup();

        // Put exactly 7 cards in P1's hand
        for (int i = 0; i < 7; i++)
            p1.Hand.Add(GameCard.Create($"Card {i + 1}"));

        p1.Library.Add(GameCard.Create("LibraryCard"));

        await engine.RunTurnAsync();

        // Hand should remain at 7, no cards discarded
        p1.Hand.Count.Should().Be(7, "hand at max size should not trigger discard");
        p1.Graveyard.Count.Should().Be(0, "no cards should be discarded");
    }

    [Fact]
    public async Task RunTurnAsync_HandBelowMaxSize_NoDiscard()
    {
        var (engine, state, p1, _, _, _) = Setup();

        // Put only 3 cards in P1's hand
        for (int i = 0; i < 3; i++)
            p1.Hand.Add(GameCard.Create($"Card {i + 1}"));

        p1.Library.Add(GameCard.Create("LibraryCard"));

        await engine.RunTurnAsync();

        // Hand should remain at 3, no cards discarded
        p1.Hand.Count.Should().Be(3, "hand below max size should not trigger discard");
        p1.Graveyard.Count.Should().Be(0, "no cards should be discarded");
    }

    [Fact]
    public async Task RunTurnAsync_DiscardChoice_MovesChosenCardsToGraveyard()
    {
        var (engine, state, p1, _, handler1, _) = Setup();

        // Put 9 cards in P1's hand
        var cards = new List<GameCard>();
        for (int i = 0; i < 9; i++)
        {
            var card = GameCard.Create($"Card {i + 1}");
            cards.Add(card);
            p1.Hand.Add(card);
        }

        p1.Library.Add(GameCard.Create("LibraryCard"));

        // Enqueue a discard choice that picks the last 2 cards (Card 8, Card 9)
        handler1.EnqueueDiscardChoice((hand, count) =>
            hand.OrderBy(c => c.Name).TakeLast(count).ToList());

        await engine.RunTurnAsync();

        // Verify the specific chosen cards ended up in graveyard
        p1.Hand.Count.Should().Be(7);
        p1.Graveyard.Count.Should().Be(2);

        var graveyardNames = p1.Graveyard.Cards.Select(c => c.Name).ToList();
        graveyardNames.Should().Contain("Card 8");
        graveyardNames.Should().Contain("Card 9");

        // Verify those cards are no longer in hand
        var handNames = p1.Hand.Cards.Select(c => c.Name).ToList();
        handNames.Should().NotContain("Card 8");
        handNames.Should().NotContain("Card 9");
    }

    [Fact]
    public async Task RunTurnAsync_OnlyActivePlayerDiscards()
    {
        var (engine, state, p1, p2, _, _) = Setup();

        // P1 (active) has exactly 7 cards — no discard needed
        for (int i = 0; i < 7; i++)
            p1.Hand.Add(GameCard.Create($"P1 Card {i + 1}"));

        // P2 (non-active) has 9 cards — would need discard, but it's not their turn
        for (int i = 0; i < 9; i++)
            p2.Hand.Add(GameCard.Create($"P2 Card {i + 1}"));

        p1.Library.Add(GameCard.Create("LibraryCard"));

        await engine.RunTurnAsync();

        // P1 should still have 7 (no discard needed)
        p1.Hand.Count.Should().Be(7);
        p1.Graveyard.Count.Should().Be(0);

        // P2 should still have 9 — discard only applies to active player
        p2.Hand.Count.Should().Be(9, "non-active player should not be forced to discard during active player's cleanup step");
        p2.Graveyard.Count.Should().Be(0);
    }
}
