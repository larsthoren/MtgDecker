using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class CastSpellStackTests
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
    public async Task CastInstant_GoesOnStack()
    {
        var (engine, state, h1, _) = CreateSetup();
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
        state.Stack[0].Card.Should().Be(swords);
        state.Stack[0].ControllerId.Should().Be(state.Player1.Id);
        state.Player1.Hand.Cards.Should().NotContain(c => c.Id == swords.Id);
        state.Player1.ManaPool.Total.Should().Be(0);
    }

    [Fact]
    public async Task CastInstant_TargetRecorded()
    {
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var swords = GameCard.Create("Swords to Plowshares");
        state.Player1.Hand.Add(swords);
        state.Player1.ManaPool.Add(ManaColor.White, 1);

        var creature = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        state.Player2.Battlefield.Add(creature);
        h1.EnqueueTarget(new TargetInfo(creature.Id, state.Player2.Id, ZoneType.Battlefield));

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, swords.Id));

        state.Stack[0].Targets.Should().ContainSingle()
            .Which.CardId.Should().Be(creature.Id);
    }

    [Fact]
    public async Task CastSorcerySpeed_InCombat_Rejected()
    {
        var (engine, state, _, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.Combat;

        var replenish = GameCard.Create("Replenish");
        state.Player1.Hand.Add(replenish);
        state.Player1.ManaPool.Add(ManaColor.White, 4);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, replenish.Id));

        state.Stack.Should().BeEmpty();
        state.Player1.Hand.Cards.Should().Contain(c => c.Id == replenish.Id);
    }

    [Fact]
    public async Task CastInstant_InCombat_Allowed()
    {
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.Combat;

        var swords = GameCard.Create("Swords to Plowshares");
        state.Player1.Hand.Add(swords);
        state.Player1.ManaPool.Add(ManaColor.White, 1);

        var creature = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        state.Player2.Battlefield.Add(creature);
        h1.EnqueueTarget(new TargetInfo(creature.Id, state.Player2.Id, ZoneType.Battlefield));

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, swords.Id));

        state.Stack.Should().HaveCount(1);
    }

    [Fact]
    public async Task CastSpell_InsufficientMana_Rejected()
    {
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var swords = GameCard.Create("Swords to Plowshares");
        state.Player1.Hand.Add(swords);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, swords.Id));

        state.Stack.Should().BeEmpty();
        state.Player1.Hand.Cards.Should().Contain(c => c.Id == swords.Id);
    }

    [Fact]
    public async Task CastCreatureSpell_GoesOnStack()
    {
        var (engine, state, _, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var goblin = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        state.Player1.Hand.Add(goblin);
        state.Player1.ManaPool.Add(ManaColor.Red, 1);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, goblin.Id));

        state.Stack.Should().HaveCount(1);
        state.Stack[0].Card.Name.Should().Be("Mogg Fanatic");
        state.Player1.Hand.Cards.Should().NotContain(c => c.Id == goblin.Id);
    }
}
