using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class CombatIntegrationTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler p1Handler, TestDecisionHandler p2Handler) CreateGame()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);

        for (int i = 0; i < 40; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}", TypeLine = "Creature" });
            p2.Library.Add(new GameCard { Name = $"Card{i}", TypeLine = "Creature" });
        }

        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, h1, h2);
    }

    [Fact]
    public async Task FullTurn_CombatPhaseRunsDuringTurn()
    {
        var (engine, state, p1Handler, p2Handler) = CreateGame();
        await engine.StartGameAsync();
        state.IsFirstTurn = true;

        // Place a creature that was already there (no summoning sickness)
        var creature = new GameCard { Name = "Bear", TypeLine = "Creature", Power = 3, Toughness = 3, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(creature);

        // Program: attack with creature, no blockers
        p1Handler.EnqueueAttackers(new List<Guid> { creature.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid>());

        await engine.RunTurnAsync();

        state.Player2.Life.Should().Be(17, "3/3 creature should have dealt 3 damage during combat");
    }

    [Fact]
    public async Task DamageClears_AtEndOfTurn()
    {
        var (engine, state, p1Handler, p2Handler) = CreateGame();
        await engine.StartGameAsync();
        state.IsFirstTurn = true;

        var attacker = new GameCard { Name = "Bear", TypeLine = "Creature", Power = 2, Toughness = 5, TurnEnteredBattlefield = 0 };
        var blocker = new GameCard { Name = "Wall", TypeLine = "Creature", Power = 1, Toughness = 5, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(attacker);
        state.Player2.Battlefield.Add(blocker);

        p1Handler.EnqueueAttackers(new List<Guid> { attacker.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid> { { blocker.Id, attacker.Id } });

        await engine.RunTurnAsync();

        attacker.DamageMarked.Should().Be(0, "damage clears at end of turn");
        blocker.DamageMarked.Should().Be(0, "damage clears at end of turn");
    }

    [Fact]
    public async Task CombatKill_ReducesLifeToZero_EndsGame()
    {
        var (engine, state, p1Handler, p2Handler) = CreateGame();
        await engine.StartGameAsync();
        state.IsFirstTurn = true;

        state.Player2.AdjustLife(-18); // Reduce to 2 life

        var creature = new GameCard { Name = "Bear", TypeLine = "Creature", Power = 3, Toughness = 3, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(creature);

        p1Handler.EnqueueAttackers(new List<Guid> { creature.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid>());

        await engine.RunTurnAsync();

        state.Player2.Life.Should().BeLessThanOrEqualTo(0);
    }
}
