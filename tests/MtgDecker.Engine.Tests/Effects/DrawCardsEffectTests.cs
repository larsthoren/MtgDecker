using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests.Effects;

public class DrawCardsEffectTests
{
    private GameState CreateState()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        return new GameState(p1, p2);
    }

    private StackObject CreateSpell(GameState state, Guid controllerId)
    {
        var card = GameCard.Create("Divination");
        return new StackObject(card, controllerId,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);
    }

    [Fact]
    public void DrawCardsEffect_DrawsNCards_FromLibraryToHand()
    {
        // Arrange
        var state = CreateState();
        for (int i = 0; i < 5; i++)
            state.Player1.Library.Add(GameCard.Create($"Card {i}"));

        state.Player1.Hand.Count.Should().Be(0);
        state.Player1.Library.Count.Should().Be(5);

        var spell = CreateSpell(state, state.Player1.Id);
        var effect = new DrawCardsEffect(3);

        // Act
        effect.Resolve(state, spell);

        // Assert
        state.Player1.Hand.Count.Should().Be(3);
        state.Player1.Library.Count.Should().Be(2);
    }

    [Fact]
    public void DrawCardsEffect_StopsAtEmptyLibrary_DoesNotCrash()
    {
        // Arrange
        var state = CreateState();
        state.Player1.Library.Add(GameCard.Create("Only Card"));

        var spell = CreateSpell(state, state.Player1.Id);
        var effect = new DrawCardsEffect(3);

        // Act
        effect.Resolve(state, spell);

        // Assert
        state.Player1.Hand.Count.Should().Be(1);
        state.Player1.Library.Count.Should().Be(0);
    }

    [Fact]
    public void DrawCardsEffect_LogsAction()
    {
        // Arrange
        var state = CreateState();
        for (int i = 0; i < 3; i++)
            state.Player1.Library.Add(GameCard.Create($"Card {i}"));

        var spell = CreateSpell(state, state.Player1.Id);
        var effect = new DrawCardsEffect(2);

        // Act
        effect.Resolve(state, spell);

        // Assert
        state.GameLog.Should().ContainSingle()
            .Which.Should().Contain("P1").And.Contain("draws 2 card(s)");
    }

    [Fact]
    public void DrawCardsEffect_DrawsFromTopOfLibrary()
    {
        // Arrange: Library is a stack; top card is last added
        var state = CreateState();
        var bottomCard = GameCard.Create("Bottom Card");
        var topCard = GameCard.Create("Top Card");
        state.Player1.Library.Add(bottomCard);
        state.Player1.Library.Add(topCard);

        var spell = CreateSpell(state, state.Player1.Id);
        var effect = new DrawCardsEffect(1);

        // Act
        effect.Resolve(state, spell);

        // Assert: should draw the top card (last added)
        state.Player1.Hand.Cards.Should().ContainSingle()
            .Which.Id.Should().Be(topCard.Id);
        state.Player1.Library.Cards.Should().ContainSingle()
            .Which.Id.Should().Be(bottomCard.Id);
    }

    [Fact]
    public void DrawCardsEffect_Player2Controller_DrawsForPlayer2()
    {
        // Arrange
        var state = CreateState();
        for (int i = 0; i < 3; i++)
            state.Player2.Library.Add(GameCard.Create($"Card {i}"));

        var spell = CreateSpell(state, state.Player2.Id);
        var effect = new DrawCardsEffect(2);

        // Act
        effect.Resolve(state, spell);

        // Assert
        state.Player2.Hand.Count.Should().Be(2);
        state.Player2.Library.Count.Should().Be(1);
        // Player1 should be unaffected
        state.Player1.Hand.Count.Should().Be(0);
    }

    [Fact]
    public void DrawCardsEffect_EmptyLibrary_DrawsZero_LogsCorrectly()
    {
        // Arrange
        var state = CreateState();
        // Library is empty

        var spell = CreateSpell(state, state.Player1.Id);
        var effect = new DrawCardsEffect(3);

        // Act
        effect.Resolve(state, spell);

        // Assert
        state.Player1.Hand.Count.Should().Be(0);
        state.GameLog.Should().ContainSingle()
            .Which.Should().Contain("draws 0 card(s)");
    }

    [Fact]
    public void DrawCardsEffect_CountProperty_ReturnsConstructorValue()
    {
        var effect = new DrawCardsEffect(5);
        effect.Count.Should().Be(5);
    }
}
