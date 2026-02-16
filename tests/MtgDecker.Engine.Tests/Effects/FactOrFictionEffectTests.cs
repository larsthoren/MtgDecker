using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests.Effects;

public class FactOrFictionEffectTests
{
    private (GameState state, TestDecisionHandler casterHandler, TestDecisionHandler opponentHandler)
        CreateSetup()
    {
        var casterHandler = new TestDecisionHandler();
        var opponentHandler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Caster", casterHandler);
        var p2 = new Player(Guid.NewGuid(), "Opponent", opponentHandler);
        var state = new GameState(p1, p2);
        return (state, casterHandler, opponentHandler);
    }

    private StackObject CreateSpell(GameState state, Guid controllerId)
    {
        var card = new GameCard { Name = "Fact or Fiction" };
        return new StackObject(card, controllerId,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);
    }

    /// <summary>
    /// Stock a player's library with Card 1 (top) through Card N (bottom).
    /// Add() puts on top, so we add in reverse order.
    /// </summary>
    private List<GameCard> StockLibrary(Player player, int count)
    {
        player.Library.Clear();
        var cards = new List<GameCard>();
        for (int i = 1; i <= count; i++)
            cards.Add(new GameCard { Name = $"Card {i}" });

        // Add in reverse so Card 1 ends up on top
        for (int i = cards.Count - 1; i >= 0; i--)
            player.Library.Add(cards[i]);

        return cards;
    }

    [Fact]
    public async Task FoF_OpponentSplits3and2_CasterChoosesPile1()
    {
        // Arrange: library has Card 1 (top) through Card 7 (bottom)
        var (state, casterHandler, opponentHandler) = CreateSetup();
        var cards = StockLibrary(state.Player1, 7);
        // cards[0]=Card 1, cards[1]=Card 2, ..., cards[6]=Card 7
        // PeekTop(5) returns [Card 1, Card 2, Card 3, Card 4, Card 5]

        // Opponent puts Card 1, Card 2, Card 3 into pile 1, then skips
        opponentHandler.EnqueueCardChoice(cards[0].Id); // Card 1
        opponentHandler.EnqueueCardChoice(cards[1].Id); // Card 2
        opponentHandler.EnqueueCardChoice(cards[2].Id); // Card 3
        opponentHandler.EnqueueCardChoice(null);         // done splitting

        // Caster chooses pile 1 (picks Card 1 from pile 1 options)
        casterHandler.EnqueueCardChoice(cards[0].Id);

        var spell = CreateSpell(state, state.Player1.Id);
        var effect = new FactOrFictionEffect();

        // Act
        await effect.ResolveAsync(state, spell, casterHandler);

        // Assert: hand has Card 1, 2, 3 (pile 1)
        state.Player1.Hand.Cards.Should().HaveCount(3);
        state.Player1.Hand.Cards.Select(c => c.Id).Should()
            .BeEquivalentTo(new[] { cards[0].Id, cards[1].Id, cards[2].Id });

        // Graveyard has Card 4, 5 (pile 2)
        state.Player1.Graveyard.Cards.Should().HaveCount(2);
        state.Player1.Graveyard.Cards.Select(c => c.Id).Should()
            .BeEquivalentTo(new[] { cards[3].Id, cards[4].Id });

        // Library should still have Card 6, 7
        state.Player1.Library.Count.Should().Be(2);
    }

    [Fact]
    public async Task FoF_OpponentSplits3and2_CasterChoosesPile2()
    {
        // Arrange
        var (state, casterHandler, opponentHandler) = CreateSetup();
        var cards = StockLibrary(state.Player1, 7);

        // Opponent puts Card 1, 2, 3 into pile 1, then skips
        opponentHandler.EnqueueCardChoice(cards[0].Id); // Card 1
        opponentHandler.EnqueueCardChoice(cards[1].Id); // Card 2
        opponentHandler.EnqueueCardChoice(cards[2].Id); // Card 3
        opponentHandler.EnqueueCardChoice(null);         // done

        // Caster skips (null) → gets pile 2
        casterHandler.EnqueueCardChoice(null);

        var spell = CreateSpell(state, state.Player1.Id);
        var effect = new FactOrFictionEffect();

        // Act
        await effect.ResolveAsync(state, spell, casterHandler);

        // Assert: hand has Card 4, 5 (pile 2)
        state.Player1.Hand.Cards.Should().HaveCount(2);
        state.Player1.Hand.Cards.Select(c => c.Id).Should()
            .BeEquivalentTo(new[] { cards[3].Id, cards[4].Id });

        // Graveyard has Card 1, 2, 3 (pile 1)
        state.Player1.Graveyard.Cards.Should().HaveCount(3);
        state.Player1.Graveyard.Cards.Select(c => c.Id).Should()
            .BeEquivalentTo(new[] { cards[0].Id, cards[1].Id, cards[2].Id });

        // Library still has Card 6, 7
        state.Player1.Library.Count.Should().Be(2);
    }

    [Fact]
    public async Task FoF_OpponentPutsAllInOnePile_CasterGetsAll()
    {
        // Arrange
        var (state, casterHandler, opponentHandler) = CreateSetup();
        var cards = StockLibrary(state.Player1, 7);

        // Opponent puts all 5 into pile 1
        opponentHandler.EnqueueCardChoice(cards[0].Id); // Card 1
        opponentHandler.EnqueueCardChoice(cards[1].Id); // Card 2
        opponentHandler.EnqueueCardChoice(cards[2].Id); // Card 3
        opponentHandler.EnqueueCardChoice(cards[3].Id); // Card 4
        opponentHandler.EnqueueCardChoice(cards[4].Id); // Card 5
        // No more remaining cards → loop ends automatically

        // Caster chooses pile 1 (picks Card 1)
        casterHandler.EnqueueCardChoice(cards[0].Id);

        var spell = CreateSpell(state, state.Player1.Id);
        var effect = new FactOrFictionEffect();

        // Act
        await effect.ResolveAsync(state, spell, casterHandler);

        // Assert: hand has all 5
        state.Player1.Hand.Cards.Should().HaveCount(5);

        // Graveyard is empty (pile 2 was empty)
        state.Player1.Graveyard.Cards.Should().BeEmpty();

        // Library has Card 6, 7
        state.Player1.Library.Count.Should().Be(2);
    }

    [Fact]
    public async Task FoF_OpponentPutsNoneInPile1_CasterGetsAll()
    {
        // Arrange
        var (state, casterHandler, opponentHandler) = CreateSetup();
        var cards = StockLibrary(state.Player1, 7);

        // Opponent immediately skips → pile 1 empty, pile 2 = all 5
        opponentHandler.EnqueueCardChoice(null);

        // Caster doesn't need to choose (pile 1 is empty, auto-gets pile 2)
        // No caster choice needed

        var spell = CreateSpell(state, state.Player1.Id);
        var effect = new FactOrFictionEffect();

        // Act
        await effect.ResolveAsync(state, spell, casterHandler);

        // Assert: hand has all 5
        state.Player1.Hand.Cards.Should().HaveCount(5);

        // Graveyard is empty
        state.Player1.Graveyard.Cards.Should().BeEmpty();

        // Library has Card 6, 7
        state.Player1.Library.Count.Should().Be(2);
    }

    [Fact]
    public async Task FoF_LibraryHasFewerThan5_WorksWithAvailable()
    {
        // Arrange: only 2 cards in library
        var (state, casterHandler, opponentHandler) = CreateSetup();
        state.Player1.Library.Clear();
        var cardA = new GameCard { Name = "Card A" };
        var cardB = new GameCard { Name = "Card B" };
        state.Player1.Library.Add(cardB); // bottom
        state.Player1.Library.Add(cardA); // top
        // PeekTop(5) returns [Card A, Card B]

        // Opponent puts Card A in pile 1, then skips
        opponentHandler.EnqueueCardChoice(cardA.Id);
        opponentHandler.EnqueueCardChoice(null); // done

        // Caster picks pile 1 (Card A)
        casterHandler.EnqueueCardChoice(cardA.Id);

        var spell = CreateSpell(state, state.Player1.Id);
        var effect = new FactOrFictionEffect();

        // Act
        await effect.ResolveAsync(state, spell, casterHandler);

        // Assert: hand has Card A (pile 1)
        state.Player1.Hand.Cards.Should().HaveCount(1);
        state.Player1.Hand.Cards[0].Id.Should().Be(cardA.Id);

        // Graveyard has Card B (pile 2)
        state.Player1.Graveyard.Cards.Should().HaveCount(1);
        state.Player1.Graveyard.Cards[0].Id.Should().Be(cardB.Id);

        // Library is empty
        state.Player1.Library.Count.Should().Be(0);
    }
}
