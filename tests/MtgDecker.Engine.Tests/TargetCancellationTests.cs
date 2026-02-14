using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class TargetCancellationTests
{
    [Fact]
    public async Task CancelTarget_ReturnsSpellToHand_ManaUnspent()
    {
        // Arrange: set up a game where P1 can cast Lightning Bolt
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);

        // Need library cards for StartGameAsync mulligan
        for (int i = 0; i < 40; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}" });
            p2.Library.Add(new GameCard { Name = $"Card{i}" });
        }

        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        // Give P1 a Mountain and Lightning Bolt in hand
        var mountain = GameCard.Create("Mountain");
        p1.Battlefield.Add(mountain);

        var bolt = GameCard.Create("Lightning Bolt");
        p1.Hand.Add(bolt);

        // Put a target on opponent's battlefield so targeting is triggered
        var bear = GameCard.Create("Grizzly Bears");
        bear.TurnEnteredBattlefield = state.TurnNumber - 1;
        p2.Battlefield.Add(bear);

        // Enqueue null target to simulate cancellation
        h1.EnqueueTarget(null);

        // Actions: tap Mountain for mana, then cast Bolt (which will be cancelled at targeting)
        h1.EnqueueAction(GameAction.TapCard(p1.Id, mountain.Id));
        h1.EnqueueAction(GameAction.CastSpell(p1.Id, bolt.Id));

        // Act: run priority (P1 taps, casts, targeting is cancelled, both pass)
        await engine.RunPriorityAsync();

        // Assert: spell should still be in hand (cast was cancelled before mana payment)
        p1.Hand.Cards.Should().Contain(c => c.Name == "Lightning Bolt",
            "the spell should return to hand when targeting is cancelled");
        state.Stack.Should().BeEmpty("no spell should be on the stack after cancellation");
        // Mana should still be in pool (tapping happened before cast, but cast didn't consume it)
        p1.ManaPool[ManaColor.Red].Should().Be(1,
            "mana should not be spent when targeting is cancelled");
    }

    [Fact]
    public async Task CancelTarget_DoesNotPushActionHistory()
    {
        // Arrange
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);

        for (int i = 0; i < 40; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}" });
            p2.Library.Add(new GameCard { Name = $"Card{i}" });
        }

        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var mountain = GameCard.Create("Mountain");
        p1.Battlefield.Add(mountain);

        var bolt = GameCard.Create("Lightning Bolt");
        p1.Hand.Add(bolt);

        var bear = GameCard.Create("Grizzly Bears");
        bear.TurnEnteredBattlefield = state.TurnNumber - 1;
        p2.Battlefield.Add(bear);

        // Cancel targeting
        h1.EnqueueTarget(null);

        var historyBefore = p1.ActionHistory.Count;

        // Execute directly (no priority loop, just the action)
        await engine.ExecuteAction(GameAction.CastSpell(p1.Id, bolt.Id));

        // The CastSpell action should not be in history (it was cancelled)
        p1.ActionHistory.Count.Should().Be(historyBefore,
            "cancelled cast should not add to action history");
    }

    [Fact]
    public async Task CancelTarget_LogsCancellation()
    {
        // Arrange
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);

        for (int i = 0; i < 40; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}" });
            p2.Library.Add(new GameCard { Name = $"Card{i}" });
        }

        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var mountain = GameCard.Create("Mountain");
        p1.Battlefield.Add(mountain);

        var bolt = GameCard.Create("Lightning Bolt");
        p1.Hand.Add(bolt);

        var bear = GameCard.Create("Grizzly Bears");
        bear.TurnEnteredBattlefield = state.TurnNumber - 1;
        p2.Battlefield.Add(bear);

        h1.EnqueueTarget(null);

        // Need mana to get past the CanPay check
        p1.ManaPool.Add(ManaColor.Red);

        await engine.ExecuteAction(GameAction.CastSpell(p1.Id, bolt.Id));

        state.GameLog.Should().Contain(l => l.Contains("cancels casting Lightning Bolt"));
    }
}
