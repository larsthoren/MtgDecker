using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class MidCastStateTests
{
    [Fact]
    public void RemainingCost_InitialState_IsNull()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        state.PendingCastCard.Should().BeNull();
        state.PendingCastPlayerId.Should().BeNull();
        state.RemainingGenericCost.Should().Be(0);
        state.RemainingPhyrexianCost.Should().BeEmpty();
        state.IsMidCast.Should().BeFalse();
    }

    [Fact]
    public void BeginMidCast_SetsState()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var card = new GameCard { Name = "Test" };

        state.BeginMidCast(p1.Id, card, genericCost: 2, phyrexianCost: new Dictionary<ManaColor, int> { { ManaColor.Black, 1 } });

        state.IsMidCast.Should().BeTrue();
        state.PendingCastCard.Should().Be(card);
        state.PendingCastPlayerId.Should().Be(p1.Id);
        state.RemainingGenericCost.Should().Be(2);
        state.RemainingPhyrexianCost.Should().ContainKey(ManaColor.Black).WhoseValue.Should().Be(1);
    }

    [Fact]
    public void ClearMidCast_ResetsState()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var card = new GameCard { Name = "Test" };

        state.BeginMidCast(p1.Id, card, genericCost: 1, phyrexianCost: new Dictionary<ManaColor, int>());
        state.ClearMidCast();

        state.IsMidCast.Should().BeFalse();
        state.PendingCastCard.Should().BeNull();
        state.RemainingGenericCost.Should().Be(0);
    }

    [Fact]
    public void ApplyManaPayment_ReducesGenericCost()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var card = new GameCard { Name = "Test" };

        state.BeginMidCast(p1.Id, card, genericCost: 2, phyrexianCost: new Dictionary<ManaColor, int>());
        state.ApplyManaPayment(ManaColor.Red);

        state.RemainingGenericCost.Should().Be(1);
    }

    [Fact]
    public void ApplyManaPayment_ReducesPhyrexianFirst_WhenColorMatches()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var card = new GameCard { Name = "Test" };

        state.BeginMidCast(p1.Id, card, genericCost: 1,
            phyrexianCost: new Dictionary<ManaColor, int> { { ManaColor.Black, 2 } });
        state.ApplyManaPayment(ManaColor.Black);

        state.RemainingPhyrexianCost[ManaColor.Black].Should().Be(1);
        state.RemainingGenericCost.Should().Be(1);
    }

    [Fact]
    public void ApplyLifePayment_ReducesPhyrexianCost()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var card = new GameCard { Name = "Test" };

        state.BeginMidCast(p1.Id, card, genericCost: 0,
            phyrexianCost: new Dictionary<ManaColor, int> { { ManaColor.Black, 1 } });
        var reduced = state.ApplyLifePayment();

        reduced.Should().BeTrue();
        state.TotalRemainingPhyrexian.Should().Be(0);
    }

    [Fact]
    public void ApplyLifePayment_ReturnsFalse_WhenNoPhyrexianRemaining()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var card = new GameCard { Name = "Test" };

        state.BeginMidCast(p1.Id, card, genericCost: 1, phyrexianCost: new Dictionary<ManaColor, int>());
        var reduced = state.ApplyLifePayment();

        reduced.Should().BeFalse();
    }

    [Fact]
    public void IsFullyPaid_True_WhenAllCostsZero()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var card = new GameCard { Name = "Test" };

        state.BeginMidCast(p1.Id, card, genericCost: 0, phyrexianCost: new Dictionary<ManaColor, int>());

        state.IsFullyPaid.Should().BeTrue();
    }
}
