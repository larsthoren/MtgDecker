using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests.Effects;

public class BrainstormEffectTests
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
        var card = GameCard.Create("Brainstorm");
        return new StackObject(card, controllerId,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);
    }

    [Fact]
    public async Task BrainstormEffect_Draws3ThenPuts2Back()
    {
        // Arrange: player with 5 cards in library named A,B,C,D,E (E on top)
        // and 0 cards in hand initially
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

        state.Player1.Hand.Count.Should().Be(0);
        state.Player1.Library.Count.Should().Be(5);

        // After drawing 3 (E, D, C), hand = [E, D, C]
        // Enqueue 2 card choices for put-back: put E back first, then D
        handler.EnqueueCardChoice(cardE.Id);
        handler.EnqueueCardChoice(cardD.Id);

        var spell = CreateSpell(state, state.Player1.Id);
        var effect = new BrainstormEffect();

        // Act
        await effect.ResolveAsync(state, spell, handler);

        // Assert: hand has 1 card (C), library has 4 cards (A, B + E, D on top)
        state.Player1.Hand.Count.Should().Be(1);
        state.Player1.Hand.Cards[0].Id.Should().Be(cardC.Id);
        state.Player1.Library.Count.Should().Be(4);
    }

    [Fact]
    public async Task BrainstormEffect_PutBackCardsAreOnTopOfLibrary()
    {
        // Arrange: verify order - last card put back should be on very top
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

        // After drawing 3 (E, D, C), hand = [E, D, C]
        // Put back E first, then D
        handler.EnqueueCardChoice(cardE.Id);
        handler.EnqueueCardChoice(cardD.Id);

        var spell = CreateSpell(state, state.Player1.Id);
        var effect = new BrainstormEffect();

        // Act
        await effect.ResolveAsync(state, spell, handler);

        // Assert: library top should be D (last put back), then E, then B, then A
        // DrawFromTop removes from end of list, AddToTop appends to end
        // Library list order: [A, B, E, D] where D is at index 3 (top)
        var topCard = state.Player1.Library.DrawFromTop();
        topCard!.Id.Should().Be(cardD.Id, "last card put back should be on top");

        var secondCard = state.Player1.Library.DrawFromTop();
        secondCard!.Id.Should().Be(cardE.Id, "first card put back should be second from top");
    }

    [Fact]
    public async Task BrainstormEffect_WithFewerThan3InLibrary_DrawsWhatIsAvailable()
    {
        // Arrange: only 1 card in library
        var (state, handler) = CreateSetup();
        var onlyCard = new GameCard { Name = "Only Card" };
        state.Player1.Library.Add(onlyCard);

        // After drawing 1, hand = [onlyCard], library empty
        // Put back min(2, 1) = 1 card
        handler.EnqueueCardChoice(onlyCard.Id);

        var spell = CreateSpell(state, state.Player1.Id);
        var effect = new BrainstormEffect();

        // Act
        await effect.ResolveAsync(state, spell, handler);

        // Assert: hand is empty (drew 1, put 1 back), library has 1 card
        state.Player1.Hand.Count.Should().Be(0);
        state.Player1.Library.Count.Should().Be(1);
        state.Player1.Library.Cards[0].Id.Should().Be(onlyCard.Id);
    }

    [Fact]
    public async Task BrainstormEffect_EmptyLibrary_DrawsNothing_PutsNothingBack()
    {
        // Arrange: empty library, empty hand
        var (state, handler) = CreateSetup();

        var spell = CreateSpell(state, state.Player1.Id);
        var effect = new BrainstormEffect();

        // Act
        await effect.ResolveAsync(state, spell, handler);

        // Assert: nothing happened
        state.Player1.Hand.Count.Should().Be(0);
        state.Player1.Library.Count.Should().Be(0);
    }

    [Fact]
    public async Task BrainstormEffect_WithExistingHandCards_PutsBackFromFullHand()
    {
        // Arrange: player already has 2 cards in hand, 3 in library
        var (state, handler) = CreateSetup();
        var handCard1 = new GameCard { Name = "Hand Card 1" };
        var handCard2 = new GameCard { Name = "Hand Card 2" };
        state.Player1.Hand.Add(handCard1);
        state.Player1.Hand.Add(handCard2);

        var libCard1 = new GameCard { Name = "Lib Card 1" };
        var libCard2 = new GameCard { Name = "Lib Card 2" };
        var libCard3 = new GameCard { Name = "Lib Card 3" };
        state.Player1.Library.Add(libCard1);
        state.Player1.Library.Add(libCard2);
        state.Player1.Library.Add(libCard3);

        // After drawing 3 (libCard3, libCard2, libCard1), hand = [handCard1, handCard2, libCard3, libCard2, libCard1]
        // Put back the original hand cards
        handler.EnqueueCardChoice(handCard1.Id);
        handler.EnqueueCardChoice(handCard2.Id);

        var spell = CreateSpell(state, state.Player1.Id);
        var effect = new BrainstormEffect();

        // Act
        await effect.ResolveAsync(state, spell, handler);

        // Assert: hand has 3 drawn library cards, library has the 2 original hand cards on top
        state.Player1.Hand.Count.Should().Be(3);
        state.Player1.Library.Count.Should().Be(2);

        // Top of library should be handCard2 (last put back)
        var top = state.Player1.Library.DrawFromTop();
        top!.Id.Should().Be(handCard2.Id);
    }

    [Fact]
    public async Task BrainstormEffect_LogsDrawAndPutBack()
    {
        // Arrange
        var (state, handler) = CreateSetup();
        for (int i = 0; i < 3; i++)
            state.Player1.Library.Add(new GameCard { Name = $"Card {i}" });

        // Let defaults handle the card choices (TestDecisionHandler picks first available)
        var spell = CreateSpell(state, state.Player1.Id);
        var effect = new BrainstormEffect();

        // Act
        await effect.ResolveAsync(state, spell, handler);

        // Assert: should log both draw and put-back
        state.GameLog.Should().Contain(msg => msg.Contains("P1") && msg.Contains("draws 3 card(s)") && msg.Contains("Brainstorm"));
        state.GameLog.Should().Contain(msg => msg.Contains("P1") && msg.Contains("puts 2 card(s) on top of library"));
    }

    [Fact]
    public async Task BrainstormEffect_Player2Controller_AffectsPlayer2()
    {
        // Arrange
        var (state, _) = CreateSetup();
        var h2 = (TestDecisionHandler)state.Player2.DecisionHandler;
        for (int i = 0; i < 5; i++)
            state.Player2.Library.Add(new GameCard { Name = $"P2 Card {i}" });

        var spell = CreateSpell(state, state.Player2.Id);
        var effect = new BrainstormEffect();

        // Act
        await effect.ResolveAsync(state, spell, h2);

        // Assert: player 2 drew 3, put 2 back
        state.Player2.Hand.Count.Should().Be(1);
        state.Player2.Library.Count.Should().Be(4);
        // Player 1 unaffected
        state.Player1.Hand.Count.Should().Be(0);
    }

    [Fact]
    public async Task BrainstormEffect_With2CardsInLibrary_Draws2PutsBack2()
    {
        // Arrange: 2 cards in library
        var (state, handler) = CreateSetup();
        var cardA = new GameCard { Name = "Card A" };
        var cardB = new GameCard { Name = "Card B" };
        state.Player1.Library.Add(cardA);
        state.Player1.Library.Add(cardB);

        // After drawing 2 (B, A), hand = [B, A]
        // Put back min(2, 2) = 2 cards
        handler.EnqueueCardChoice(cardA.Id);
        handler.EnqueueCardChoice(cardB.Id);

        var spell = CreateSpell(state, state.Player1.Id);
        var effect = new BrainstormEffect();

        // Act
        await effect.ResolveAsync(state, spell, handler);

        // Assert: hand empty, library has 2 cards
        state.Player1.Hand.Count.Should().Be(0);
        state.Player1.Library.Count.Should().Be(2);
    }
}
