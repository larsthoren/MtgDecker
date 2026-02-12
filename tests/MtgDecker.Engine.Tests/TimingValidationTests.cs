using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class TimingValidationTests
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
    public void CanCastSorcery_InMainPhaseActivePlayerEmptyStack_True()
    {
        var (engine, state, _, _) = CreateSetup();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        engine.CanCastSorcery(state.Player1.Id).Should().BeTrue();
    }

    [Fact]
    public void CanCastSorcery_InCombatPhase_False()
    {
        var (engine, state, _, _) = CreateSetup();
        state.CurrentPhase = Phase.Combat;
        state.ActivePlayer = state.Player1;

        engine.CanCastSorcery(state.Player1.Id).Should().BeFalse();
    }

    [Fact]
    public void CanCastSorcery_NonActivePlayer_False()
    {
        var (engine, state, _, _) = CreateSetup();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        engine.CanCastSorcery(state.Player2.Id).Should().BeFalse();
    }

    [Fact]
    public void CanCastSorcery_StackNotEmpty_False()
    {
        var (engine, state, _, _) = CreateSetup();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;
        state.Stack.Add(new StackObject(
            new GameCard { Name = "Dummy" }, Guid.NewGuid(),
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0));

        engine.CanCastSorcery(state.Player1.Id).Should().BeFalse();
    }
}
