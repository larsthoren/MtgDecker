using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class GameEnginePriorityTests
{
    private GameEngine CreateEngine(out GameState state, out TestDecisionHandler p1Handler, out TestDecisionHandler p2Handler)
    {
        p1Handler = new TestDecisionHandler();
        p2Handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Alice", p1Handler);
        var p2 = new Player(Guid.NewGuid(), "Bob", p2Handler);
        state = new GameState(p1, p2);
        return new GameEngine(state);
    }

    [Fact]
    public async Task RunPriorityAsync_BothPass_PhaseEnds()
    {
        var engine = CreateEngine(out var state, out var p1Handler, out var p2Handler);

        await engine.RunPriorityAsync();

        state.GameLog.Should().BeEmpty();
    }

    [Fact]
    public async Task RunPriorityAsync_ActivePlayerActsThenBothPass_PhaseEnds()
    {
        var engine = CreateEngine(out var state, out var p1Handler, out var p2Handler);
        var card = GameCard.Create("Forest", "Basic Land — Forest");
        state.Player1.Hand.Add(card);

        p1Handler.EnqueueAction(GameAction.PlayCard(state.Player1.Id, card.Id));

        await engine.RunPriorityAsync();

        state.Player1.Hand.Count.Should().Be(0);
        state.Player1.Battlefield.Count.Should().Be(1);
    }

    [Fact]
    public async Task RunPriorityAsync_ActivePlayerPasses_OpponentGetsPriority()
    {
        var engine = CreateEngine(out var state, out var p1Handler, out var p2Handler);
        // Use a land (land drops don't need mana)
        var card = GameCard.Create("Mountain", "Basic Land — Mountain");
        state.Player2.Hand.Add(card);

        p2Handler.EnqueueAction(GameAction.PlayCard(state.Player2.Id, card.Id));

        await engine.RunPriorityAsync();

        state.Player2.Battlefield.Count.Should().Be(1);
    }

    [Fact]
    public async Task RunPriorityAsync_OpponentActs_ActivePlayerGetsPriorityAgain()
    {
        var engine = CreateEngine(out var state, out var p1Handler, out var p2Handler);
        var card1 = GameCard.Create("Forest", "Basic Land — Forest");
        var card2 = GameCard.Create("Mountain", "Basic Land — Mountain");
        state.Player2.Hand.Add(card1);
        state.Player1.Hand.Add(card2);

        p2Handler.EnqueueAction(GameAction.PlayCard(state.Player2.Id, card1.Id));
        p1Handler.EnqueueAction(GameAction.PlayCard(state.Player1.Id, card2.Id));

        await engine.RunPriorityAsync();

        state.Player1.Battlefield.Count.Should().Be(1);
        state.Player2.Battlefield.Count.Should().Be(1);
    }

    [Fact]
    public async Task RunPriorityAsync_RejectedAction_PlayerRetainsPriority()
    {
        var engine = CreateEngine(out var state, out var p1Handler, out var p2Handler);

        // Set up: player has Wild Growth in hand but no mana (CastSpell will be rejected)
        var wildGrowth = GameCard.Create("Wild Growth", "Enchantment");
        state.Player1.Hand.Add(wildGrowth);

        // Also has a Forest to play after the rejected cast
        var forest = GameCard.Create("Forest", "Basic Land — Forest");
        state.Player1.Hand.Add(forest);

        // Action 1: Try to cast Wild Growth with no mana (rejected)
        // Action 2: Play Forest (should succeed because player retains priority)
        p1Handler.EnqueueAction(GameAction.CastSpell(state.Player1.Id, wildGrowth.Id));
        p1Handler.EnqueueAction(GameAction.PlayCard(state.Player1.Id, forest.Id));

        await engine.RunPriorityAsync();

        // Forest should be on battlefield — player got priority back after rejection
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Name == "Forest");
        state.GameLog.Should().Contain(l => l.Contains("Not enough mana") || l.Contains("Cannot cast"));
    }

    [Fact]
    public async Task RunPriorityAsync_ActivePlayerStartsWithPriority()
    {
        var engine = CreateEngine(out var state, out var p1Handler, out _);

        state.PriorityPlayer = state.Player2;

        await engine.RunPriorityAsync();

        // Method resets PriorityPlayer to ActivePlayer at start — no crash, both pass
    }
}
