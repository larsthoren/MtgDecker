using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class FetchLandTests
{
    [Fact]
    public async Task Fetch_Land_Sacrifices_And_Searches_Library()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        var fetch = GameCard.Create("Wooded Foothills", "Land");
        var mountain = GameCard.Create("Mountain", "Basic Land \u2014 Mountain");
        var forest = GameCard.Create("Forest", "Basic Land \u2014 Forest");

        p1.Battlefield.Add(fetch);
        p1.Library.Add(mountain);
        p1.Library.Add(forest);

        handler.EnqueueCardChoice(mountain.Id);

        await engine.ExecuteAction(GameAction.ActivateFetch(p1.Id, fetch.Id));

        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Wooded Foothills");
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Wooded Foothills");
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Mountain");
        p1.Library.Cards.Should().NotContain(c => c.Name == "Mountain");
        p1.Life.Should().Be(19);
    }

    [Fact]
    public async Task Fetch_Land_Shuffles_Library_After_Search()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        var fetch = GameCard.Create("Wooded Foothills", "Land");
        for (int i = 0; i < 10; i++)
            p1.Library.Add(new GameCard { Name = $"Card {i}" });
        var mountain = GameCard.Create("Mountain", "Basic Land \u2014 Mountain");
        p1.Library.Add(mountain);
        p1.Battlefield.Add(fetch);

        handler.EnqueueCardChoice(mountain.Id);

        await engine.ExecuteAction(GameAction.ActivateFetch(p1.Id, fetch.Id));

        p1.Library.Count.Should().Be(10);
    }

    [Fact]
    public async Task Fetch_Land_Only_Finds_Matching_Subtypes()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        var fetch = GameCard.Create("Windswept Heath", "Land");
        var plains = GameCard.Create("Plains", "Basic Land \u2014 Plains");
        var mountain = GameCard.Create("Mountain", "Basic Land \u2014 Mountain");

        p1.Battlefield.Add(fetch);
        p1.Library.Add(plains);
        p1.Library.Add(mountain);

        handler.EnqueueCardChoice(plains.Id);

        await engine.ExecuteAction(GameAction.ActivateFetch(p1.Id, fetch.Id));

        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Plains");
        p1.Library.Cards.Should().Contain(c => c.Name == "Mountain");
    }

    [Fact]
    public async Task Fetch_Land_No_Match_In_Library_Still_Sacrifices()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        var fetch = GameCard.Create("Wooded Foothills", "Land");
        p1.Battlefield.Add(fetch);

        await engine.ExecuteAction(GameAction.ActivateFetch(p1.Id, fetch.Id));

        p1.Battlefield.Cards.Should().BeEmpty();
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Wooded Foothills");
        p1.Life.Should().Be(19);
    }

    [Fact]
    public async Task Fetch_Land_Cannot_Activate_When_Tapped()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        var fetch = GameCard.Create("Wooded Foothills", "Land");
        fetch.IsTapped = true; // already tapped
        p1.Battlefield.Add(fetch);

        var mountain = GameCard.Create("Mountain", "Basic Land \u2014 Mountain");
        p1.Library.Add(mountain);

        await engine.ExecuteAction(GameAction.ActivateFetch(p1.Id, fetch.Id));

        // Should NOT have activated — fetch land is still on battlefield, no life lost
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Wooded Foothills");
        p1.Life.Should().Be(20);
    }

    [Fact]
    public void Basic_Lands_Have_Subtypes_In_CardDefinitions()
    {
        var mountain = GameCard.Create("Mountain", "Basic Land \u2014 Mountain");
        mountain.Subtypes.Should().Contain("Mountain");

        var forest = GameCard.Create("Forest", "Basic Land \u2014 Forest");
        forest.Subtypes.Should().Contain("Forest");

        var plains = GameCard.Create("Plains", "Basic Land \u2014 Plains");
        plains.Subtypes.Should().Contain("Plains");
    }
}
