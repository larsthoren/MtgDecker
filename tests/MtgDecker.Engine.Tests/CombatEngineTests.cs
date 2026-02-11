using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class CombatEngineTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler p1Handler, TestDecisionHandler p2Handler) CreateSetup()
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
    public async Task NoAttackers_SkipsCombat()
    {
        var (engine, state, p1Handler, _) = CreateSetup();
        await engine.StartGameAsync();

        // Place a creature (no summoning sickness — set turn before current)
        var creature = new GameCard { Name = "Bear", TypeLine = "Creature — Bear", Power = 2, Toughness = 2, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(creature);

        // Enqueue: no attackers (empty list)
        p1Handler.EnqueueAttackers(Array.Empty<Guid>());

        await engine.RunCombatAsync(CancellationToken.None);

        state.Player2.Life.Should().Be(20, "no attackers means no damage");
    }

    [Fact]
    public async Task UnblockedAttacker_DealsDamageToDefender()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var creature = new GameCard { Name = "Bear", TypeLine = "Creature — Bear", Power = 2, Toughness = 2, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(creature);

        p1Handler.EnqueueAttackers(new List<Guid> { creature.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid>()); // no blockers

        await engine.RunCombatAsync(CancellationToken.None);

        state.Player2.Life.Should().Be(18, "2/2 creature should deal 2 damage");
        creature.IsTapped.Should().BeTrue("attacker should be tapped");
    }

    [Fact]
    public async Task BlockedAttacker_DealsNoDamageToDefender()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var attacker = new GameCard { Name = "Bear", TypeLine = "Creature — Bear", Power = 2, Toughness = 2, TurnEnteredBattlefield = 0 };
        var blocker = new GameCard { Name = "Wall", TypeLine = "Creature — Wall", Power = 0, Toughness = 4, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(attacker);
        state.Player2.Battlefield.Add(blocker);

        p1Handler.EnqueueAttackers(new List<Guid> { attacker.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid> { { blocker.Id, attacker.Id } });

        await engine.RunCombatAsync(CancellationToken.None);

        state.Player2.Life.Should().Be(20, "blocked attacker deals no damage to player");
    }

    [Fact]
    public async Task CombatDamage_KillsCreatureWithLethalDamage()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var attacker = new GameCard { Name = "Big Bear", TypeLine = "Creature", Power = 5, Toughness = 5, TurnEnteredBattlefield = 0 };
        var blocker = new GameCard { Name = "Small Bear", TypeLine = "Creature", Power = 2, Toughness = 2, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(attacker);
        state.Player2.Battlefield.Add(blocker);

        p1Handler.EnqueueAttackers(new List<Guid> { attacker.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid> { { blocker.Id, attacker.Id } });

        await engine.RunCombatAsync(CancellationToken.None);

        state.Player2.Battlefield.Cards.Should().NotContain(c => c.Id == blocker.Id, "blocker should die to 5 damage");
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Id == blocker.Id, "dead blocker goes to graveyard");
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == attacker.Id, "5/5 survives 2 damage");
        attacker.DamageMarked.Should().Be(2, "attacker took 2 damage from blocker");
    }

    [Fact]
    public async Task CombatDamage_BothCreaturesDie()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var attacker = new GameCard { Name = "Bear", TypeLine = "Creature", Power = 2, Toughness = 2, TurnEnteredBattlefield = 0 };
        var blocker = new GameCard { Name = "Bear2", TypeLine = "Creature", Power = 2, Toughness = 2, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(attacker);
        state.Player2.Battlefield.Add(blocker);

        p1Handler.EnqueueAttackers(new List<Guid> { attacker.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid> { { blocker.Id, attacker.Id } });

        await engine.RunCombatAsync(CancellationToken.None);

        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Id == attacker.Id);
        state.Player2.Battlefield.Cards.Should().NotContain(c => c.Id == blocker.Id);
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Id == attacker.Id);
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Id == blocker.Id);
    }

    [Fact]
    public async Task SummoningSickness_PreventsAttacking()
    {
        var (engine, state, p1Handler, _) = CreateSetup();
        await engine.StartGameAsync();

        // Creature entered this turn (has summoning sickness)
        var creature = new GameCard { Name = "Bear", TypeLine = "Creature", Power = 2, Toughness = 2, TurnEnteredBattlefield = state.TurnNumber };
        state.Player1.Battlefield.Add(creature);

        // Try to declare it as attacker — engine should filter it out
        p1Handler.EnqueueAttackers(new List<Guid> { creature.Id });

        await engine.RunCombatAsync(CancellationToken.None);

        state.Player2.Life.Should().Be(20, "creature with summoning sickness cannot attack");
    }

    [Fact]
    public async Task TappedCreature_CannotAttack()
    {
        var (engine, state, p1Handler, _) = CreateSetup();
        await engine.StartGameAsync();

        var creature = new GameCard { Name = "Bear", TypeLine = "Creature", Power = 2, Toughness = 2, TurnEnteredBattlefield = 0 };
        creature.IsTapped = true;
        state.Player1.Battlefield.Add(creature);

        p1Handler.EnqueueAttackers(new List<Guid> { creature.Id });

        await engine.RunCombatAsync(CancellationToken.None);

        state.Player2.Life.Should().Be(20, "tapped creature cannot attack");
    }

    [Fact]
    public async Task MultipleAttackers_DealCombinedDamage()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var bear1 = new GameCard { Name = "Bear1", TypeLine = "Creature", Power = 2, Toughness = 2, TurnEnteredBattlefield = 0 };
        var bear2 = new GameCard { Name = "Bear2", TypeLine = "Creature", Power = 3, Toughness = 3, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(bear1);
        state.Player1.Battlefield.Add(bear2);

        p1Handler.EnqueueAttackers(new List<Guid> { bear1.Id, bear2.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid>());

        await engine.RunCombatAsync(CancellationToken.None);

        state.Player2.Life.Should().Be(15, "2 + 3 = 5 damage");
    }

    [Fact]
    public async Task MultiBlock_AttackerDamageAssignedInOrder()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var attacker = new GameCard { Name = "Dragon", TypeLine = "Creature", Power = 5, Toughness = 5, TurnEnteredBattlefield = 0 };
        var blocker1 = new GameCard { Name = "Bear1", TypeLine = "Creature", Power = 2, Toughness = 2, TurnEnteredBattlefield = 0 };
        var blocker2 = new GameCard { Name = "Bear2", TypeLine = "Creature", Power = 2, Toughness = 3, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(attacker);
        state.Player2.Battlefield.Add(blocker1);
        state.Player2.Battlefield.Add(blocker2);

        p1Handler.EnqueueAttackers(new List<Guid> { attacker.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid>
        {
            { blocker1.Id, attacker.Id },
            { blocker2.Id, attacker.Id }
        });
        // Attacker orders: blocker1 first, blocker2 second
        p1Handler.EnqueueBlockerOrder(new List<Guid> { blocker1.Id, blocker2.Id });

        await engine.RunCombatAsync(CancellationToken.None);

        // 5 power: 2 lethal to blocker1, 3 lethal to blocker2 — both die
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Id == blocker1.Id);
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Id == blocker2.Id);
        // Attacker takes 2+2 = 4 damage, survives (5 toughness)
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == attacker.Id);
        attacker.DamageMarked.Should().Be(4);
    }

    [Fact]
    public async Task EndOfTurn_ClearsDamage()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var attacker = new GameCard { Name = "Bear", TypeLine = "Creature", Power = 2, Toughness = 5, TurnEnteredBattlefield = 0 };
        var blocker = new GameCard { Name = "Wall", TypeLine = "Creature", Power = 1, Toughness = 4, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(attacker);
        state.Player2.Battlefield.Add(blocker);

        p1Handler.EnqueueAttackers(new List<Guid> { attacker.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid> { { blocker.Id, attacker.Id } });

        await engine.RunCombatAsync(CancellationToken.None);

        attacker.DamageMarked.Should().Be(1, "took 1 damage from wall");
        blocker.DamageMarked.Should().Be(2, "took 2 damage from bear");

        // Simulate end of turn cleanup
        engine.ClearDamage();

        attacker.DamageMarked.Should().Be(0);
        blocker.DamageMarked.Should().Be(0);
    }

    [Fact]
    public async Task CombatStep_ProgresessCorrectly()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var creature = new GameCard { Name = "Bear", TypeLine = "Creature", Power = 2, Toughness = 2, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(creature);

        p1Handler.EnqueueAttackers(new List<Guid> { creature.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid>());

        await engine.RunCombatAsync(CancellationToken.None);

        state.CombatStep.Should().Be(CombatStep.None, "combat should be cleaned up after resolution");
        state.Combat.Should().BeNull("combat state should be cleared after resolution");
    }

    [Fact]
    public async Task NonCreatureCard_CannotAttack()
    {
        var (engine, state, p1Handler, _) = CreateSetup();
        await engine.StartGameAsync();

        var enchantment = new GameCard { Name = "Pacifism", TypeLine = "Enchantment — Aura", CardTypes = CardType.Enchantment, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(enchantment);

        p1Handler.EnqueueAttackers(new List<Guid> { enchantment.Id });

        await engine.RunCombatAsync(CancellationToken.None);

        state.Player2.Life.Should().Be(20, "non-creature cannot attack");
    }
}
