using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

/// <summary>
/// Tests for the rewritten FactOrFictionEffect that uses SplitCards + ChoosePile
/// instead of the old ChooseCard-in-a-loop approach.
/// </summary>
public class FactOrFictionTests
{
    private (GameState state, TestDecisionHandler casterHandler, TestDecisionHandler opponentHandler)
        CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Caster", h1);
        var p2 = new Player(Guid.NewGuid(), "Opponent", h2);
        var state = new GameState(p1, p2);
        return (state, h1, h2);
    }

    private StackObject CreateSpell(Guid controllerId)
    {
        var card = new GameCard { Name = "Fact or Fiction" };
        return new StackObject(card, controllerId,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);
    }

    /// <summary>
    /// Add N cards named "LibCard1" .. "LibCardN" to a player's library.
    /// Cards are added so that LibCard1 is on top and LibCardN on bottom.
    /// Returns the list in order [LibCard1, LibCard2, ..., LibCardN].
    /// </summary>
    private List<GameCard> StockLibrary(Player player, int count)
    {
        player.Library.Clear();
        var cards = new List<GameCard>();
        for (int i = 1; i <= count; i++)
            cards.Add(new GameCard { Name = $"LibCard{i}" });

        // Add in reverse so LibCard1 ends up on top (last element in the Zone list)
        for (int i = cards.Count - 1; i >= 0; i--)
            player.Library.Add(cards[i]);

        return cards;
    }

    [Fact]
    public async Task Resolve_OpponentSplits_CasterPicksPile1()
    {
        // Arrange: 5 cards in library. Opponent puts first 2 in pile 1.
        // Caster picks pile 1. Hand = 2, Graveyard = 3.
        var (state, casterHandler, opponentHandler) = CreateSetup();
        var cards = StockLibrary(state.Player1, 5);
        // PeekTop(5) returns [LibCard1, LibCard2, LibCard3, LibCard4, LibCard5]

        // Opponent splits: pile 1 = LibCard1, LibCard2
        opponentHandler.EnqueueSplitChoice(revealed =>
            revealed.Where(c => c.Name is "LibCard1" or "LibCard2").ToList());

        // Caster chooses pile 1
        casterHandler.EnqueuePileChoice(1);

        var spell = CreateSpell(state.Player1.Id);
        var effect = new FactOrFictionEffect();

        // Act
        await effect.ResolveAsync(state, spell, casterHandler);

        // Assert: hand = pile 1 (2 cards), graveyard = pile 2 (3 cards)
        state.Player1.Hand.Count.Should().Be(2);
        state.Player1.Graveyard.Count.Should().Be(3);
        state.Player1.Library.Count.Should().Be(0);
    }

    [Fact]
    public async Task Resolve_OpponentSplits_CasterPicksPile2()
    {
        // Arrange: 5 cards. Opponent puts first 2 in pile 1. Caster picks pile 2.
        // Hand = 3, Graveyard = 2.
        var (state, casterHandler, opponentHandler) = CreateSetup();
        var cards = StockLibrary(state.Player1, 5);

        // Opponent splits: pile 1 = LibCard1, LibCard2
        opponentHandler.EnqueueSplitChoice(revealed =>
            revealed.Where(c => c.Name is "LibCard1" or "LibCard2").ToList());

        // Caster chooses pile 2
        casterHandler.EnqueuePileChoice(2);

        var spell = CreateSpell(state.Player1.Id);
        var effect = new FactOrFictionEffect();

        // Act
        await effect.ResolveAsync(state, spell, casterHandler);

        // Assert: hand = pile 2 (3 cards), graveyard = pile 1 (2 cards)
        state.Player1.Hand.Count.Should().Be(3);
        state.Player1.Graveyard.Count.Should().Be(2);
        state.Player1.Library.Count.Should().Be(0);
    }

    [Fact]
    public async Task Resolve_EmptyPile1_CasterGetsPile2()
    {
        // Arrange: Opponent puts nothing in pile 1. Caster auto-gets all 5 (no choice needed).
        var (state, casterHandler, opponentHandler) = CreateSetup();
        var cards = StockLibrary(state.Player1, 5);

        // Opponent splits: pile 1 = empty
        opponentHandler.EnqueueSplitChoice(revealed => new List<GameCard>());

        // No pile choice needed -- caster auto-gets pile 2 when pile 1 is empty

        var spell = CreateSpell(state.Player1.Id);
        var effect = new FactOrFictionEffect();

        // Act
        await effect.ResolveAsync(state, spell, casterHandler);

        // Assert: hand = all 5 cards, graveyard = 0
        state.Player1.Hand.Count.Should().Be(5);
        state.Player1.Graveyard.Count.Should().Be(0);
        state.Player1.Library.Count.Should().Be(0);
    }

    [Fact]
    public async Task Resolve_FewerThan5Cards_WorksWithPartialReveal()
    {
        // Arrange: 3 cards in library. Opponent puts 1 in pile 1.
        // Caster picks pile 2. Hand = 2, Graveyard = 1, Library = 0.
        var (state, casterHandler, opponentHandler) = CreateSetup();
        var cards = StockLibrary(state.Player1, 3);

        // Opponent splits: pile 1 = LibCard1 only
        opponentHandler.EnqueueSplitChoice(revealed =>
            revealed.Where(c => c.Name == "LibCard1").ToList());

        // Caster picks pile 2
        casterHandler.EnqueuePileChoice(2);

        var spell = CreateSpell(state.Player1.Id);
        var effect = new FactOrFictionEffect();

        // Act
        await effect.ResolveAsync(state, spell, casterHandler);

        // Assert: hand = pile 2 (2 cards), graveyard = pile 1 (1 card), library = 0
        state.Player1.Hand.Count.Should().Be(2);
        state.Player1.Graveyard.Count.Should().Be(1);
        state.Player1.Library.Count.Should().Be(0);
    }

    [Fact]
    public async Task Resolve_EmptyLibrary_NoEffect()
    {
        // Arrange: 0 cards in library. No hand/graveyard changes.
        var (state, casterHandler, opponentHandler) = CreateSetup();
        state.Player1.Library.Clear();

        var spell = CreateSpell(state.Player1.Id);
        var effect = new FactOrFictionEffect();

        // Act
        await effect.ResolveAsync(state, spell, casterHandler);

        // Assert: nothing changed
        state.Player1.Hand.Count.Should().Be(0);
        state.Player1.Graveyard.Count.Should().Be(0);
        state.Player1.Library.Count.Should().Be(0);
    }

    [Fact]
    public async Task Resolve_SpecificCardsInCorrectPiles()
    {
        // Arrange: Opponent puts LibCard1 and LibCard3 in pile 1.
        // Caster picks pile 1. Verify hand has exactly those 2, graveyard has the other 3.
        var (state, casterHandler, opponentHandler) = CreateSetup();
        var cards = StockLibrary(state.Player1, 5);

        // Opponent splits: pile 1 = LibCard1, LibCard3
        opponentHandler.EnqueueSplitChoice(revealed =>
            revealed.Where(c => c.Name is "LibCard1" or "LibCard3").ToList());

        // Caster picks pile 1
        casterHandler.EnqueuePileChoice(1);

        var spell = CreateSpell(state.Player1.Id);
        var effect = new FactOrFictionEffect();

        // Act
        await effect.ResolveAsync(state, spell, casterHandler);

        // Assert: hand has exactly LibCard1 and LibCard3
        state.Player1.Hand.Count.Should().Be(2);
        state.Player1.Hand.Cards.Select(c => c.Name).Should()
            .BeEquivalentTo(new[] { "LibCard1", "LibCard3" });

        // Graveyard has LibCard2, LibCard4, LibCard5
        state.Player1.Graveyard.Count.Should().Be(3);
        state.Player1.Graveyard.Cards.Select(c => c.Name).Should()
            .BeEquivalentTo(new[] { "LibCard2", "LibCard4", "LibCard5" });

        state.Player1.Library.Count.Should().Be(0);
    }
}
