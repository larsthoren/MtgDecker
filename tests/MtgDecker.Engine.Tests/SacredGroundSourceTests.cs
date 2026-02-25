using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;

// ReSharper disable InconsistentNaming

namespace MtgDecker.Engine.Tests;

/// <summary>
/// Tests for Sacred Ground: only triggers on opponent-caused land destruction,
/// NOT on self-caused (fetchlands, self-sacrifice).
/// </summary>
public class SacredGroundSourceTests
{
    [Fact]
    public async Task SacredGround_DoesNotTrigger_OnSelfSacrifice()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P1 controls Sacred Ground
        var sacredGround = GameCard.Create("Sacred Ground");
        p1.Battlefield.Add(sacredGround);

        // A land goes to graveyard via self-sacrifice (e.g., fetchland)
        var land = new GameCard
        {
            Name = "Plains",
            CardTypes = CardType.Land,
            Subtypes = ["Plains"],
        };
        p1.Graveyard.Add(land);

        // Self-caused land destruction
        state.LastLandDestroyedByPlayerId = p1.Id;
        state.ActivePlayer = p1;

        await engine.QueueBoardTriggersOnStackAsync(GameEvent.LeavesBattlefield, land);

        // No trigger should fire — self-sacrifice
        state.StackCount.Should().Be(0);
    }

    [Fact]
    public async Task SacredGround_Triggers_OnOpponentCausedDestruction()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P1 controls Sacred Ground
        var sacredGround = GameCard.Create("Sacred Ground");
        p1.Battlefield.Add(sacredGround);

        // A land goes to graveyard via opponent's Wasteland
        var land = new GameCard
        {
            Name = "Plains",
            CardTypes = CardType.Land,
            Subtypes = ["Plains"],
        };
        p1.Graveyard.Add(land);

        // Opponent caused the land destruction
        state.LastLandDestroyedByPlayerId = p2.Id;
        state.ActivePlayer = p1;

        await engine.QueueBoardTriggersOnStackAsync(GameEvent.LeavesBattlefield, land);

        // Trigger should fire — opponent caused destruction
        state.StackCount.Should().Be(1);
    }

    [Fact]
    public async Task SacredGround_DoesNotTrigger_WhenNoSourceTracked()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var sacredGround = GameCard.Create("Sacred Ground");
        p1.Battlefield.Add(sacredGround);

        var land = new GameCard
        {
            Name = "Plains",
            CardTypes = CardType.Land,
            Subtypes = ["Plains"],
        };
        p1.Graveyard.Add(land);

        // No source tracked
        state.LastLandDestroyedByPlayerId = null;
        state.ActivePlayer = p1;

        await engine.QueueBoardTriggersOnStackAsync(GameEvent.LeavesBattlefield, land);

        state.StackCount.Should().Be(0);
    }

    [Fact]
    public void SacredGround_UsesOpponentCausesControllerLandToGraveyard_TriggerCondition()
    {
        CardDefinitions.TryGet("Sacred Ground", out var def).Should().BeTrue();
        def!.Triggers.Should().HaveCount(1);
        def.Triggers[0].Condition.Should().Be(TriggerCondition.OpponentCausesControllerLandToGraveyard);
    }
}
