using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests.Effects;

public class ImpulseEffectTests
{
    private (GameState state, TestDecisionHandler handler) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        return (state, h1);
    }

    private StackObject CreateSpell(GameState state, Guid controllerId)
    {
        var card = new GameCard { Name = "Impulse" };
        return new StackObject(card, controllerId,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);
    }

    [Fact]
    public async Task Impulse_PlayerPicksOne_PutsRestOnBottom()
    {
        // Arrange: library bottom-to-top: A B C D E (E is top)
        var (state, handler) = CreateSetup();
        var cardA = new GameCard { Name = "Card A" };
        var cardB = new GameCard { Name = "Card B" };
        var cardC = new GameCard { Name = "Card C" };
        var cardD = new GameCard { Name = "Card D" };
        var cardE = new GameCard { Name = "Card E" };
        state.Player1.Library.Add(cardA); // bottom
        state.Player1.Library.Add(cardB);
        state.Player1.Library.Add(cardC);
        state.Player1.Library.Add(cardD);
        state.Player1.Library.Add(cardE); // top

        // PeekTop(4) returns [E, D, C, B] (top-first order)
        // Player picks Card B
        handler.EnqueueCardChoice(cardB.Id);

        var spell = CreateSpell(state, state.Player1.Id);
        var effect = new ImpulseEffect();

        // Act
        await effect.ResolveAsync(state, spell, handler);

        // Assert: hand gets B
        state.Player1.Hand.Cards.Should().HaveCount(1);
        state.Player1.Hand.Cards[0].Id.Should().Be(cardB.Id);

        // Library should have 4 cards remaining (A + E, D, C on bottom)
        state.Player1.Library.Count.Should().Be(4);

        // E (the original card below the top 4) should still be in the library
        state.Player1.Library.Cards.Should().Contain(c => c.Id == cardE.Id);
    }

    [Fact]
    public async Task Impulse_RemainingCardsGoToBottom()
    {
        // Arrange: library bottom-to-top: A B C D E (E is top)
        var (state, handler) = CreateSetup();
        var cardA = new GameCard { Name = "Card A" };
        var cardB = new GameCard { Name = "Card B" };
        var cardC = new GameCard { Name = "Card C" };
        var cardD = new GameCard { Name = "Card D" };
        var cardE = new GameCard { Name = "Card E" };
        state.Player1.Library.Add(cardA); // bottom
        state.Player1.Library.Add(cardB);
        state.Player1.Library.Add(cardC);
        state.Player1.Library.Add(cardD);
        state.Player1.Library.Add(cardE); // top

        // Player picks Card E (the top card)
        handler.EnqueueCardChoice(cardE.Id);

        var spell = CreateSpell(state, state.Player1.Id);
        var effect = new ImpulseEffect();

        // Act
        await effect.ResolveAsync(state, spell, handler);

        // Assert: hand has E
        state.Player1.Hand.Cards.Should().HaveCount(1);
        state.Player1.Hand.Cards[0].Id.Should().Be(cardE.Id);

        // Library should have 4 cards
        state.Player1.Library.Count.Should().Be(4);

        // Card A was the only card not in the top 4. It should NOT be on the bottom.
        // The remaining 3 (D, C, B) went to bottom. A stays where it was.
        // Top of library should be A (it was below the top 4, now it's the highest original card)
        state.Player1.Library.PeekTop(1)[0].Id.Should().Be(cardA.Id);
    }

    [Fact]
    public async Task Impulse_LibraryHasFewerThan4_WorksWithAvailable()
    {
        // Arrange: only 2 cards in library
        var (state, handler) = CreateSetup();
        var cardX = new GameCard { Name = "Card X" };
        var cardY = new GameCard { Name = "Card Y" };
        state.Player1.Library.Add(cardX); // bottom
        state.Player1.Library.Add(cardY); // top

        // PeekTop(4) returns [Y, X] (only 2 available)
        // Player picks X
        handler.EnqueueCardChoice(cardX.Id);

        var spell = CreateSpell(state, state.Player1.Id);
        var effect = new ImpulseEffect();

        // Act
        await effect.ResolveAsync(state, spell, handler);

        // Assert: hand has X
        state.Player1.Hand.Cards.Should().HaveCount(1);
        state.Player1.Hand.Cards[0].Id.Should().Be(cardX.Id);

        // Library has 1 card (Y went to bottom, which is also top since it's the only card)
        state.Player1.Library.Count.Should().Be(1);
        state.Player1.Library.Cards[0].Id.Should().Be(cardY.Id);
    }

    [Fact]
    public async Task Impulse_EmptyLibrary_DoesNothing()
    {
        // Arrange: empty library
        var (state, handler) = CreateSetup();

        var spell = CreateSpell(state, state.Player1.Id);
        var effect = new ImpulseEffect();

        // Act
        await effect.ResolveAsync(state, spell, handler);

        // Assert: nothing happened
        state.Player1.Hand.Cards.Should().BeEmpty();
        state.Player1.Library.Count.Should().Be(0);
    }
}
