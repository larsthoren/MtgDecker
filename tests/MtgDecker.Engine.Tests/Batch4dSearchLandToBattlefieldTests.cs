using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class SearchLandToBattlefieldEffectTests
{
    private static (GameState state, Player p1, TestDecisionHandler h1) CreateGameState()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Alice", h1);
        var p2 = new Player(Guid.NewGuid(), "Bob", h2);
        var state = new GameState(p1, p2);
        return (state, p1, h1);
    }

    [Fact]
    public async Task Execute_FindsBasicLand_PutsOnBattlefieldTapped()
    {
        // Arrange
        var (state, p1, h1) = CreateGameState();
        var forest = GameCard.Create("Forest", "Basic Land — Forest");
        p1.Library.Add(forest);
        var source = GameCard.Create("Yavimaya Granger");
        var effect = new SearchLandToBattlefieldEffect(c => c.IsBasicLand, true);
        var context = new EffectContext(state, p1, source, h1);

        h1.EnqueueCardChoice(forest.Id);

        // Act
        await effect.Execute(context);

        // Assert
        p1.Battlefield.Cards.Should().Contain(c => c.Id == forest.Id);
        forest.IsTapped.Should().BeTrue("land enters tapped");
        p1.Library.Cards.Should().NotContain(c => c.Id == forest.Id);
    }

    [Fact]
    public async Task Execute_FindsBasicLand_EntersUntappedWhenNotSpecified()
    {
        // Arrange
        var (state, p1, h1) = CreateGameState();
        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
        p1.Library.Add(mountain);
        var source = GameCard.Create("Test Searcher");
        var effect = new SearchLandToBattlefieldEffect(c => c.IsBasicLand, entersTapped: false);
        var context = new EffectContext(state, p1, source, h1);

        h1.EnqueueCardChoice(mountain.Id);

        // Act
        await effect.Execute(context);

        // Assert
        p1.Battlefield.Cards.Should().Contain(c => c.Id == mountain.Id);
        mountain.IsTapped.Should().BeFalse("land enters untapped when entersTapped is false");
    }

    [Fact]
    public async Task Execute_NoMatchingLand_ShufflesAndLogs()
    {
        // Arrange
        var (state, p1, h1) = CreateGameState();
        // Put only a non-basic land in the library
        var nonBasic = GameCard.Create("Wasteland", "Land");
        p1.Library.Add(nonBasic);
        var source = GameCard.Create("Yavimaya Granger");
        var effect = new SearchLandToBattlefieldEffect(c => c.IsBasicLand, true);
        var context = new EffectContext(state, p1, source, h1);

        // Act
        await effect.Execute(context);

        // Assert
        p1.Battlefield.Cards.Should().BeEmpty("no basic land was found");
        state.GameLog.Should().Contain(l => l.Contains("finds no matching land"));
    }

    [Fact]
    public async Task Execute_PlayerDeclinesSearch_NothingMoved()
    {
        // Arrange
        var (state, p1, h1) = CreateGameState();
        var forest = GameCard.Create("Forest", "Basic Land — Forest");
        p1.Library.Add(forest);
        var source = GameCard.Create("Yavimaya Granger");
        var effect = new SearchLandToBattlefieldEffect(c => c.IsBasicLand, true);
        var context = new EffectContext(state, p1, source, h1);

        h1.EnqueueCardChoice(null); // decline

        // Act
        await effect.Execute(context);

        // Assert
        p1.Battlefield.Cards.Should().BeEmpty("player declined the search");
        state.GameLog.Should().Contain(l => l.Contains("declines to search"));
    }

    [Fact]
    public async Task Execute_FindsAnyBasicLand_NotJustForest()
    {
        // Arrange
        var (state, p1, h1) = CreateGameState();
        var island = GameCard.Create("Island", "Basic Land — Island");
        var plains = GameCard.Create("Plains", "Basic Land — Plains");
        p1.Library.Add(island);
        p1.Library.Add(plains);
        var source = GameCard.Create("Yavimaya Granger");
        var effect = new SearchLandToBattlefieldEffect(c => c.IsBasicLand, true);
        var context = new EffectContext(state, p1, source, h1);

        h1.EnqueueCardChoice(island.Id);

        // Act
        await effect.Execute(context);

        // Assert
        p1.Battlefield.Cards.Should().Contain(c => c.Id == island.Id,
            "should be able to search for any basic land, not just Forest");
    }

    [Fact]
    public async Task Execute_SetsTurnEnteredBattlefield()
    {
        // Arrange
        var (state, p1, h1) = CreateGameState();
        state.TurnNumber = 3;
        var forest = GameCard.Create("Forest", "Basic Land — Forest");
        p1.Library.Add(forest);
        var source = GameCard.Create("Yavimaya Granger");
        var effect = new SearchLandToBattlefieldEffect(c => c.IsBasicLand, true);
        var context = new EffectContext(state, p1, source, h1);

        h1.EnqueueCardChoice(forest.Id);

        // Act
        await effect.Execute(context);

        // Assert
        forest.TurnEnteredBattlefield.Should().Be(3,
            "the land should record when it entered the battlefield");
    }
}
