using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class StackUndoTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler h1) CreateSetup()
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
        return (engine, state, h1);
    }

    [Fact]
    public async Task UndoCastSpell_RemovesFromStack_RefundsMana_ReturnsToHand()
    {
        var (engine, state, h1) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var swords = GameCard.Create("Swords to Plowshares");
        state.Player1.Hand.Add(swords);
        state.Player1.ManaPool.Add(ManaColor.White, 1);

        var creature = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        state.Player2.Battlefield.Add(creature);
        h1.EnqueueTarget(new TargetInfo(creature.Id, state.Player2.Id, ZoneType.Battlefield));

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, swords.Id));
        state.Stack.Should().HaveCount(1);

        var result = engine.UndoLastAction(state.Player1.Id);

        result.Should().BeTrue();
        state.Stack.Should().BeEmpty();
        state.Player1.Hand.Cards.Should().Contain(c => c.Id == swords.Id);
        state.Player1.ManaPool[ManaColor.White].Should().Be(1);
    }

    [Fact]
    public async Task UndoCastCreature_RemovesFromStack_RefundsMana()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var goblin = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        state.Player1.Hand.Add(goblin);
        state.Player1.ManaPool.Add(ManaColor.Red, 1);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, goblin.Id));
        state.Stack.Should().HaveCount(1);

        var result = engine.UndoLastAction(state.Player1.Id);

        result.Should().BeTrue();
        state.Stack.Should().BeEmpty();
        state.Player1.Hand.Cards.Should().Contain(c => c.Id == goblin.Id);
        state.Player1.ManaPool[ManaColor.Red].Should().Be(1);
    }
}
