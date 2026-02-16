using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests.Effects;

public class SkeletalScryingEffectTests
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
        var card = new GameCard { Name = "Skeletal Scrying" };
        return new StackObject(card, controllerId,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);
    }

    [Fact]
    public async Task SkeletalScrying_Exile3_Draw3_Lose3Life()
    {
        // Arrange: graveyard has 4 cards, library has 5
        var (state, handler) = CreateSetup();
        var player = state.Player1;

        player.Graveyard.Add(new GameCard { Name = "GY Card 1" });
        player.Graveyard.Add(new GameCard { Name = "GY Card 2" });
        player.Graveyard.Add(new GameCard { Name = "GY Card 3" });
        player.Graveyard.Add(new GameCard { Name = "GY Card 4" });

        player.Library.Clear();
        for (int i = 5; i >= 1; i--)
            player.Library.Add(new GameCard { Name = $"Lib Card {i}" });

        // Get graveyard card IDs after adding them
        var gyCards = player.Graveyard.Cards;
        handler.EnqueueCardChoice(gyCards[0].Id); // exile GY Card 1
        handler.EnqueueCardChoice(gyCards[1].Id); // exile GY Card 2
        handler.EnqueueCardChoice(gyCards[2].Id); // exile GY Card 3
        handler.EnqueueCardChoice(null);           // done choosing

        var spell = CreateSpell(state, player.Id);
        var effect = new SkeletalScryingEffect();

        // Act
        await effect.ResolveAsync(state, spell, handler);

        // Assert
        player.Exile.Count.Should().Be(3, "3 cards were exiled from graveyard");
        player.Graveyard.Count.Should().Be(1, "1 card remains in graveyard");
        player.Hand.Cards.Should().HaveCount(3, "drew X=3 cards");
        player.Life.Should().Be(17, "lost X=3 life (20 - 3 = 17)");
    }

    [Fact]
    public async Task SkeletalScrying_Exile1_Draw1_Lose1Life()
    {
        // Arrange: graveyard has 4 cards, library has 5
        var (state, handler) = CreateSetup();
        var player = state.Player1;

        player.Graveyard.Add(new GameCard { Name = "GY Card 1" });
        player.Graveyard.Add(new GameCard { Name = "GY Card 2" });
        player.Graveyard.Add(new GameCard { Name = "GY Card 3" });
        player.Graveyard.Add(new GameCard { Name = "GY Card 4" });

        player.Library.Clear();
        for (int i = 5; i >= 1; i--)
            player.Library.Add(new GameCard { Name = $"Lib Card {i}" });

        var gyCards = player.Graveyard.Cards;
        handler.EnqueueCardChoice(gyCards[0].Id); // exile GY Card 1
        handler.EnqueueCardChoice(null);           // done choosing

        var spell = CreateSpell(state, player.Id);
        var effect = new SkeletalScryingEffect();

        // Act
        await effect.ResolveAsync(state, spell, handler);

        // Assert
        player.Exile.Count.Should().Be(1, "1 card was exiled from graveyard");
        player.Graveyard.Count.Should().Be(3, "3 cards remain in graveyard");
        player.Hand.Cards.Should().HaveCount(1, "drew X=1 card");
        player.Life.Should().Be(19, "lost X=1 life (20 - 1 = 19)");
    }

    [Fact]
    public async Task SkeletalScrying_ExileNone_DrawsNothingLosesNothing()
    {
        // Arrange: graveyard has 4 cards, library has 5
        var (state, handler) = CreateSetup();
        var player = state.Player1;

        player.Graveyard.Add(new GameCard { Name = "GY Card 1" });
        player.Graveyard.Add(new GameCard { Name = "GY Card 2" });
        player.Graveyard.Add(new GameCard { Name = "GY Card 3" });
        player.Graveyard.Add(new GameCard { Name = "GY Card 4" });

        player.Library.Clear();
        for (int i = 5; i >= 1; i--)
            player.Library.Add(new GameCard { Name = $"Lib Card {i}" });

        handler.EnqueueCardChoice(null); // immediately skip

        var spell = CreateSpell(state, player.Id);
        var effect = new SkeletalScryingEffect();

        // Act
        await effect.ResolveAsync(state, spell, handler);

        // Assert
        player.Exile.Count.Should().Be(0, "no cards were exiled");
        player.Graveyard.Count.Should().Be(4, "all 4 graveyard cards remain");
        player.Hand.Cards.Should().BeEmpty("no cards drawn when X=0");
        player.Life.Should().Be(20, "no life lost when X=0");
    }

    [Fact]
    public async Task SkeletalScrying_EmptyGraveyard_DoesNothing()
    {
        // Arrange: no graveyard cards
        var (state, handler) = CreateSetup();
        var player = state.Player1;

        player.Library.Clear();
        for (int i = 5; i >= 1; i--)
            player.Library.Add(new GameCard { Name = $"Lib Card {i}" });

        var spell = CreateSpell(state, player.Id);
        var effect = new SkeletalScryingEffect();

        // Act
        await effect.ResolveAsync(state, spell, handler);

        // Assert
        player.Exile.Count.Should().Be(0, "no cards to exile");
        player.Hand.Cards.Should().BeEmpty("no cards drawn");
        player.Life.Should().Be(20, "no life lost");
    }

    [Fact]
    public async Task SkeletalScrying_NotEnoughLibrary_DrawsWhatIsAvailable()
    {
        // Arrange: 3 cards in graveyard, only 1 in library
        var (state, handler) = CreateSetup();
        var player = state.Player1;

        player.Graveyard.Add(new GameCard { Name = "GY Card 1" });
        player.Graveyard.Add(new GameCard { Name = "GY Card 2" });
        player.Graveyard.Add(new GameCard { Name = "GY Card 3" });

        player.Library.Clear();
        player.Library.Add(new GameCard { Name = "Lib Card 1" });

        var gyCards = player.Graveyard.Cards;
        handler.EnqueueCardChoice(gyCards[0].Id); // exile GY Card 1
        handler.EnqueueCardChoice(gyCards[1].Id); // exile GY Card 2
        handler.EnqueueCardChoice(gyCards[2].Id); // exile GY Card 3
        handler.EnqueueCardChoice(null);           // done (would stop anyway, graveyard empty)

        var spell = CreateSpell(state, player.Id);
        var effect = new SkeletalScryingEffect();

        // Act
        await effect.ResolveAsync(state, spell, handler);

        // Assert
        player.Exile.Count.Should().Be(3, "all 3 graveyard cards exiled");
        player.Graveyard.Count.Should().Be(0, "graveyard is empty");
        player.Hand.Cards.Should().HaveCount(1, "only 1 card available to draw");
        player.Life.Should().Be(17, "still loses X=3 life even though drew only 1");
    }
}
