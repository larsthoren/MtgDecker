using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class ManualPaymentTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler h1, TestDecisionHandler h2) CreateSetup()
    {
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
        return (engine, state, h1, h2);
    }

    [Fact]
    public async Task PayManaFromPool_ReducesGenericCost()
    {
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        var card = new GameCard { Name = "TestSpell", ManaCost = ManaCost.Parse("{2}{R}"), CardTypes = CardType.Instant };
        state.Player1.Hand.Add(card);
        state.Player1.ManaPool.Add(ManaColor.Red, 1);
        state.Player1.ManaPool.Add(ManaColor.Blue, 2);

        // Cast spell — R auto-deducted, enters mid-cast for {2} generic
        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, card.Id));
        state.IsMidCast.Should().BeTrue();
        state.RemainingGenericCost.Should().Be(2);

        // Pay 1 Blue toward generic
        await engine.ExecuteAction(GameAction.PayManaFromPool(state.Player1.Id, ManaColor.Blue));
        state.RemainingGenericCost.Should().Be(1);

        // Pay 1 more Blue — should auto-complete and put spell on stack
        await engine.ExecuteAction(GameAction.PayManaFromPool(state.Player1.Id, ManaColor.Blue));
        state.IsMidCast.Should().BeFalse();
        state.StackCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task PayManaFromPool_Fails_WhenNotMidCast()
    {
        var (engine, state, _, _) = CreateSetup();
        await engine.StartGameAsync();

        var act = () => engine.ExecuteAction(GameAction.PayManaFromPool(state.Player1.Id, ManaColor.Red));
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task PayManaFromPool_Fails_WhenPoolEmpty()
    {
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        var card = new GameCard { Name = "TestSpell", ManaCost = ManaCost.Parse("{1}"), CardTypes = CardType.Instant };
        state.Player1.Hand.Add(card);
        state.Player1.ManaPool.Add(ManaColor.Red, 1);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, card.Id));

        // Try to pay Blue when pool has no Blue
        var act = () => engine.ExecuteAction(GameAction.PayManaFromPool(state.Player1.Id, ManaColor.Blue));
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ColoredOnlyCost_AutoDeductsImmediately_NoMidCast()
    {
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        // {R}{R} = two colored, zero generic
        var card = new GameCard { Name = "TestSpell", ManaCost = ManaCost.Parse("{R}{R}"), CardTypes = CardType.Instant };
        state.Player1.Hand.Add(card);
        state.Player1.ManaPool.Add(ManaColor.Red, 2);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, card.Id));

        // Should NOT enter mid-cast — colored costs auto-deducted, goes straight to stack
        state.IsMidCast.Should().BeFalse();
        state.StackCount.Should().BeGreaterThanOrEqualTo(1);
    }
}
