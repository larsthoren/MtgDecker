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

    [Fact]
    public async Task RunPriorityAsync_ManaAbility_DoesNotPassPriority()
    {
        var engine = CreateEngine(out var state, out var p1Handler, out var p2Handler);

        // Put a Forest on P1's battlefield
        var forest = GameCard.Create("Forest", "Basic Land — Forest");
        state.Player1.Battlefield.Add(forest);

        // Also put a Mountain in hand to play after tapping
        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
        state.Player1.Hand.Add(mountain);

        // P1: tap Forest for mana, then play Mountain, then pass
        // If mana abilities passed priority, P2 would get priority between the tap and the play.
        // P2 has NO actions queued, so if priority goes to P2, TestDecisionHandler returns Pass.
        // Then P1 would need to act again. The test verifies the action sequence works
        // with P1 tapping, then immediately playing a land without P2 intervening.
        p1Handler.EnqueueAction(GameAction.TapCard(state.Player1.Id, forest.Id));
        p1Handler.EnqueueAction(GameAction.PlayCard(state.Player1.Id, mountain.Id));

        // Track whether P2 ever gets prompted between P1's tap and play
        int p2ActionCallCount = 0;
        p2Handler.OnBeforeAction = () => p2ActionCallCount++;

        await engine.RunPriorityAsync();

        state.Player1.ManaPool[ManaColor.Green].Should().Be(1, "Forest should have produced green mana");
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Name == "Mountain",
            "Mountain should be played without P2 getting priority in between");
        // P2 gets prompted once for the final pass round after P1 passes, not between tap and play
        p2ActionCallCount.Should().Be(1, "P2 should only be prompted once (final pass), not after mana tap");
    }
}
