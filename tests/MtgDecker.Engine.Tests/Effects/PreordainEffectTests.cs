using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests.Effects;

public class PreordainEffectTests
{
    private (GameState state, TestDecisionHandler handler) CreateState()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        return (new GameState(p1, p2), h1);
    }

    private StackObject CreateSpell(GameState state, Guid controllerId)
    {
        var card = GameCard.Create("Preordain");
        return new StackObject(card, controllerId,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);
    }

    [Fact]
    public async Task PreordainEffect_KeepsBothOnTop_DrawsOne()
    {
        // Arrange
        // Library bottom-to-top: A B C D E (E is top)
        var (state, handler) = CreateState();
        var cardA = GameCard.Create("Card A");
        var cardB = GameCard.Create("Card B");
        var cardC = GameCard.Create("Card C");
        var cardD = GameCard.Create("Card D");
        var cardE = GameCard.Create("Card E");
        state.Player1.Library.Add(cardA); // bottom
        state.Player1.Library.Add(cardB);
        state.Player1.Library.Add(cardC);
        state.Player1.Library.Add(cardD);
        state.Player1.Library.Add(cardE); // top

        // PeekTop(2) returns [E, D] (top-first order)
        // Keep both: choose E (keep on top), choose D (keep on top)
        handler.EnqueueCardChoice(cardE.Id); // keep E on top
        handler.EnqueueCardChoice(cardD.Id); // keep D on top

        var spell = CreateSpell(state, state.Player1.Id);
        var effect = new PreordainEffect();

        // Act
        await effect.ResolveAsync(state, spell, handler);

        // Assert
        // After scry: D is added to top first, then... wait.
        // The foreach iterates [E, D]. Both kept.
        // keptOnTop = [E, D]. AddToTop(E) then AddToTop(D).
        // Library top-to-bottom after reinsert: D, E, C, B, A
        // Draw from top => draws D
        state.Player1.Hand.Cards.Should().HaveCount(1);
        state.Player1.Hand.Cards[0].Id.Should().Be(cardD.Id);

        // Library should have 4 cards remaining: E, C, B, A (top-to-bottom)
        state.Player1.Library.Count.Should().Be(4);
        // Top card should now be E
        state.Player1.Library.PeekTop(1)[0].Id.Should().Be(cardE.Id);
    }

    [Fact]
    public async Task PreordainEffect_BottomsBoth_DrawsOne()
    {
        // Arrange
        var (state, handler) = CreateState();
        var cardA = GameCard.Create("Card A");
        var cardB = GameCard.Create("Card B");
        var cardC = GameCard.Create("Card C");
        var cardD = GameCard.Create("Card D");
        var cardE = GameCard.Create("Card E");
        state.Player1.Library.Add(cardA);
        state.Player1.Library.Add(cardB);
        state.Player1.Library.Add(cardC);
        state.Player1.Library.Add(cardD);
        state.Player1.Library.Add(cardE);

        // PeekTop(2) returns [E, D]
        // Bottom both: null (skip E), null (skip D)
        handler.EnqueueCardChoice(null); // bottom E
        handler.EnqueueCardChoice(null); // bottom D

        var spell = CreateSpell(state, state.Player1.Id);
        var effect = new PreordainEffect();

        // Act
        await effect.ResolveAsync(state, spell, handler);

        // Assert
        // After scry: E and D on bottom, top is now C
        // Draw from top => draws C
        state.Player1.Hand.Cards.Should().HaveCount(1);
        state.Player1.Hand.Cards[0].Id.Should().Be(cardC.Id);

        // Library: 4 cards remaining
        state.Player1.Library.Count.Should().Be(4);

        // E and D should be on the bottom of the library
        // Bottom cards are index 0, 1 in the internal list
        var bottomTwo = state.Player1.Library.Cards.Take(2).Select(c => c.Id).ToList();
        bottomTwo.Should().Contain(cardE.Id);
        bottomTwo.Should().Contain(cardD.Id);
    }

    [Fact]
    public async Task PreordainEffect_KeepsOneBottomsOne_DrawsKeptCard()
    {
        // Arrange
        var (state, handler) = CreateState();
        var cardA = GameCard.Create("Card A");
        var cardB = GameCard.Create("Card B");
        var cardC = GameCard.Create("Card C");
        var cardD = GameCard.Create("Card D");
        var cardE = GameCard.Create("Card E");
        state.Player1.Library.Add(cardA);
        state.Player1.Library.Add(cardB);
        state.Player1.Library.Add(cardC);
        state.Player1.Library.Add(cardD);
        state.Player1.Library.Add(cardE);

        // PeekTop(2) returns [E, D]
        // Keep E on top, bottom D
        handler.EnqueueCardChoice(cardE.Id); // keep E
        handler.EnqueueCardChoice(null);     // bottom D

        var spell = CreateSpell(state, state.Player1.Id);
        var effect = new PreordainEffect();

        // Act
        await effect.ResolveAsync(state, spell, handler);

        // Assert
        // After scry: D on bottom, E added back to top
        // Draw from top => draws E
        state.Player1.Hand.Cards.Should().HaveCount(1);
        state.Player1.Hand.Cards[0].Id.Should().Be(cardE.Id);

        // Library: 4 cards remaining
        state.Player1.Library.Count.Should().Be(4);

        // D should be on the bottom
        state.Player1.Library.Cards[0].Id.Should().Be(cardD.Id);
    }

    [Fact]
    public async Task PreordainEffect_BottomsFirstKeepsSecond_DrawsKeptCard()
    {
        // Arrange
        var (state, handler) = CreateState();
        var cardA = GameCard.Create("Card A");
        var cardB = GameCard.Create("Card B");
        var cardC = GameCard.Create("Card C");
        var cardD = GameCard.Create("Card D");
        var cardE = GameCard.Create("Card E");
        state.Player1.Library.Add(cardA);
        state.Player1.Library.Add(cardB);
        state.Player1.Library.Add(cardC);
        state.Player1.Library.Add(cardD);
        state.Player1.Library.Add(cardE);

        // PeekTop(2) returns [E, D]
        // Bottom E, keep D on top
        handler.EnqueueCardChoice(null);     // bottom E
        handler.EnqueueCardChoice(cardD.Id); // keep D

        var spell = CreateSpell(state, state.Player1.Id);
        var effect = new PreordainEffect();

        // Act
        await effect.ResolveAsync(state, spell, handler);

        // Assert
        // After scry: E on bottom, D added to top
        // Draw from top => draws D
        state.Player1.Hand.Cards.Should().HaveCount(1);
        state.Player1.Hand.Cards[0].Id.Should().Be(cardD.Id);

        // Library: 4 cards remaining
        state.Player1.Library.Count.Should().Be(4);

        // E should be on the bottom
        state.Player1.Library.Cards[0].Id.Should().Be(cardE.Id);
    }

    [Fact]
    public async Task PreordainEffect_WithOnlyOneCardInLibrary()
    {
        // Arrange
        var (state, handler) = CreateState();
        var onlyCard = GameCard.Create("Lone Card");
        state.Player1.Library.Add(onlyCard);

        // PeekTop(2) returns just [onlyCard] since only 1 exists
        // Keep it on top
        handler.EnqueueCardChoice(onlyCard.Id);

        var spell = CreateSpell(state, state.Player1.Id);
        var effect = new PreordainEffect();

        // Act
        await effect.ResolveAsync(state, spell, handler);

        // Assert
        // Scry 1 (only 1 card), keep on top, then draw it
        state.Player1.Hand.Cards.Should().HaveCount(1);
        state.Player1.Hand.Cards[0].Id.Should().Be(onlyCard.Id);
        state.Player1.Library.Count.Should().Be(0);
    }

    [Fact]
    public async Task PreordainEffect_WithOnlyOneCard_BottomIt_DrawsNothing()
    {
        // Arrange
        var (state, handler) = CreateState();
        var onlyCard = GameCard.Create("Lone Card");
        state.Player1.Library.Add(onlyCard);

        // Bottom the only card
        handler.EnqueueCardChoice(null);

        var spell = CreateSpell(state, state.Player1.Id);
        var effect = new PreordainEffect();

        // Act
        await effect.ResolveAsync(state, spell, handler);

        // Assert
        // Scry 1, bottom it, then draw it (it's the only card left)
        state.Player1.Hand.Cards.Should().HaveCount(1);
        state.Player1.Hand.Cards[0].Id.Should().Be(onlyCard.Id);
        state.Player1.Library.Count.Should().Be(0);
    }

    [Fact]
    public async Task PreordainEffect_EmptyLibrary_DrawsNothing()
    {
        // Arrange
        var (state, handler) = CreateState();
        // Library is empty - nothing to scry or draw

        var spell = CreateSpell(state, state.Player1.Id);
        var effect = new PreordainEffect();

        // Act
        await effect.ResolveAsync(state, spell, handler);

        // Assert
        state.Player1.Hand.Cards.Should().BeEmpty();
        state.Player1.Library.Count.Should().Be(0);
    }

    [Fact]
    public async Task PreordainEffect_LogsScryAndDraw()
    {
        // Arrange
        var (state, handler) = CreateState();
        state.Player1.Library.Add(GameCard.Create("Card A"));
        state.Player1.Library.Add(GameCard.Create("Card B"));
        state.Player1.Library.Add(GameCard.Create("Card C"));

        // Keep both scryed cards
        handler.EnqueueCardChoice(state.Player1.Library.PeekTop(2)[0].Id);
        handler.EnqueueCardChoice(state.Player1.Library.PeekTop(2)[1].Id);

        var spell = CreateSpell(state, state.Player1.Id);
        var effect = new PreordainEffect();

        // Act
        await effect.ResolveAsync(state, spell, handler);

        // Assert
        state.GameLog.Should().Contain(l => l.Contains("draws a card") && l.Contains("Preordain"));
    }

    [Fact]
    public async Task PreordainEffect_Player2Controller_AffectsPlayer2()
    {
        // Arrange
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);

        var cardX = GameCard.Create("Card X");
        var cardY = GameCard.Create("Card Y");
        var cardZ = GameCard.Create("Card Z");
        state.Player2.Library.Add(cardX);
        state.Player2.Library.Add(cardY);
        state.Player2.Library.Add(cardZ);

        // PeekTop(2) returns [Z, Y]
        // Keep Z, bottom Y
        h2.EnqueueCardChoice(cardZ.Id);
        h2.EnqueueCardChoice(null);

        var spell = CreateSpell(state, state.Player2.Id);
        var effect = new PreordainEffect();

        // Act
        await effect.ResolveAsync(state, spell, h2);

        // Assert
        state.Player2.Hand.Cards.Should().HaveCount(1);
        state.Player2.Hand.Cards[0].Id.Should().Be(cardZ.Id);
        state.Player2.Library.Count.Should().Be(2);
        // Player1 unaffected
        state.Player1.Hand.Cards.Should().BeEmpty();
    }
}
