using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class PlaneswalkerCombatTests
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
    public void CombatState_AttackerTargets_TracksPerAttacker()
    {
        var combat = new CombatState(Guid.NewGuid(), Guid.NewGuid());
        var attackerId = Guid.NewGuid();
        var pwId = Guid.NewGuid();

        combat.DeclareAttacker(attackerId);
        combat.SetAttackerTarget(attackerId, pwId);

        combat.GetAttackerTarget(attackerId).Should().Be(pwId);
    }

    [Fact]
    public void CombatState_AttackerTarget_DefaultsToNull()
    {
        var combat = new CombatState(Guid.NewGuid(), Guid.NewGuid());
        var attackerId = Guid.NewGuid();

        combat.DeclareAttacker(attackerId);

        combat.GetAttackerTarget(attackerId).Should().BeNull();
    }

    [Fact]
    public async Task Combat_UnblockedAttacker_DealsDamageToPlaneswalker()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        // P1 has an attacker ready (no summoning sickness)
        var attacker = new GameCard
        {
            Name = "Bear",
            BasePower = 2,
            BaseToughness = 2,
            CardTypes = CardType.Creature,
            TurnEnteredBattlefield = state.TurnNumber - 1,
        };
        state.Player1.Battlefield.Add(attacker);

        // P2 has a planeswalker with 4 loyalty
        var pw = new GameCard
        {
            Name = "Target PW",
            CardTypes = CardType.Planeswalker,
        };
        pw.AddCounters(CounterType.Loyalty, 4);
        state.Player2.Battlefield.Add(pw);

        // P1 attacks with bear targeting the planeswalker
        p1Handler.EnqueueAttackers(new List<Guid> { attacker.Id });
        p1Handler.EnqueueAttackerTargets(new Dictionary<Guid, Guid?> { { attacker.Id, pw.Id } });

        // P2 doesn't block
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid>());

        state.CurrentPhase = Phase.Combat;
        await engine.RunCombatAsync(CancellationToken.None);

        // Planeswalker lost 2 loyalty (bear's power)
        pw.Loyalty.Should().Be(2);
        // Player didn't take damage
        state.Player2.Life.Should().Be(20);
    }

    [Fact]
    public async Task Combat_UnblockedAttacker_DefaultsToPlayerDamage_WhenNoPlaneswalkers()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var attacker = new GameCard
        {
            Name = "Bear",
            BasePower = 2,
            BaseToughness = 2,
            CardTypes = CardType.Creature,
            TurnEnteredBattlefield = state.TurnNumber - 1,
        };
        state.Player1.Battlefield.Add(attacker);

        // No planeswalkers — ChooseAttackerTargets should not be called
        p1Handler.EnqueueAttackers(new List<Guid> { attacker.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid>());

        state.CurrentPhase = Phase.Combat;
        await engine.RunCombatAsync(CancellationToken.None);

        state.Player2.Life.Should().Be(18);
    }

    [Fact]
    public async Task Combat_AttackerTargetingPlayer_WhenPlaneswalkerPresent()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var attacker = new GameCard
        {
            Name = "Bear",
            BasePower = 3,
            BaseToughness = 3,
            CardTypes = CardType.Creature,
            TurnEnteredBattlefield = state.TurnNumber - 1,
        };
        state.Player1.Battlefield.Add(attacker);

        var pw = new GameCard
        {
            Name = "Target PW",
            CardTypes = CardType.Planeswalker,
        };
        pw.AddCounters(CounterType.Loyalty, 4);
        state.Player2.Battlefield.Add(pw);

        // Explicitly target the player (null target)
        p1Handler.EnqueueAttackers(new List<Guid> { attacker.Id });
        p1Handler.EnqueueAttackerTargets(new Dictionary<Guid, Guid?> { { attacker.Id, null } });

        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid>());

        state.CurrentPhase = Phase.Combat;
        await engine.RunCombatAsync(CancellationToken.None);

        // Player takes damage, PW untouched
        state.Player2.Life.Should().Be(17);
        pw.Loyalty.Should().Be(4);
    }

    [Fact]
    public async Task Combat_PlaneswalkerDies_WhenDamageExceedsLoyalty()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var attacker = new GameCard
        {
            Name = "Giant",
            BasePower = 5,
            BaseToughness = 5,
            CardTypes = CardType.Creature,
            TurnEnteredBattlefield = state.TurnNumber - 1,
        };
        state.Player1.Battlefield.Add(attacker);

        var pw = new GameCard
        {
            Name = "Fragile PW",
            CardTypes = CardType.Planeswalker,
        };
        pw.AddCounters(CounterType.Loyalty, 3);
        state.Player2.Battlefield.Add(pw);

        p1Handler.EnqueueAttackers(new List<Guid> { attacker.Id });
        p1Handler.EnqueueAttackerTargets(new Dictionary<Guid, Guid?> { { attacker.Id, pw.Id } });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid>());

        state.CurrentPhase = Phase.Combat;
        await engine.RunCombatAsync(CancellationToken.None);

        // PW loyalty dropped to 0 or below → SBA puts it in graveyard
        state.Player2.Battlefield.Cards.Should().NotContain(c => c.Id == pw.Id);
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Id == pw.Id);
        // Player life untouched
        state.Player2.Life.Should().Be(20);
    }

    [Fact]
    public async Task Combat_MultipleAttackers_SplitBetweenPlayerAndPlaneswalker()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var attacker1 = new GameCard
        {
            Name = "Bear1",
            BasePower = 2,
            BaseToughness = 2,
            CardTypes = CardType.Creature,
            TurnEnteredBattlefield = state.TurnNumber - 1,
        };
        var attacker2 = new GameCard
        {
            Name = "Bear2",
            BasePower = 3,
            BaseToughness = 3,
            CardTypes = CardType.Creature,
            TurnEnteredBattlefield = state.TurnNumber - 1,
        };
        state.Player1.Battlefield.Add(attacker1);
        state.Player1.Battlefield.Add(attacker2);

        var pw = new GameCard
        {
            Name = "Target PW",
            CardTypes = CardType.Planeswalker,
        };
        pw.AddCounters(CounterType.Loyalty, 5);
        state.Player2.Battlefield.Add(pw);

        // attacker1 targets PW, attacker2 targets player
        p1Handler.EnqueueAttackers(new List<Guid> { attacker1.Id, attacker2.Id });
        p1Handler.EnqueueAttackerTargets(new Dictionary<Guid, Guid?>
        {
            { attacker1.Id, pw.Id },
            { attacker2.Id, null },
        });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid>());

        state.CurrentPhase = Phase.Combat;
        await engine.RunCombatAsync(CancellationToken.None);

        pw.Loyalty.Should().Be(3, "PW should have lost 2 loyalty from attacker1");
        state.Player2.Life.Should().Be(17, "Player should have lost 3 life from attacker2");
    }

    [Fact]
    public async Task Combat_BlockedAttacker_TargetingPW_DamageGoesToBlocker()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var attacker = new GameCard
        {
            Name = "Bear",
            BasePower = 2,
            BaseToughness = 2,
            CardTypes = CardType.Creature,
            TurnEnteredBattlefield = state.TurnNumber - 1,
        };
        state.Player1.Battlefield.Add(attacker);

        var blocker = new GameCard
        {
            Name = "Wall",
            BasePower = 0,
            BaseToughness = 4,
            CardTypes = CardType.Creature,
            TurnEnteredBattlefield = state.TurnNumber - 1,
        };
        state.Player2.Battlefield.Add(blocker);

        var pw = new GameCard
        {
            Name = "Target PW",
            CardTypes = CardType.Planeswalker,
        };
        pw.AddCounters(CounterType.Loyalty, 4);
        state.Player2.Battlefield.Add(pw);

        // Attacker targets PW, but gets blocked
        p1Handler.EnqueueAttackers(new List<Guid> { attacker.Id });
        p1Handler.EnqueueAttackerTargets(new Dictionary<Guid, Guid?> { { attacker.Id, pw.Id } });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid> { { blocker.Id, attacker.Id } });

        state.CurrentPhase = Phase.Combat;
        await engine.RunCombatAsync(CancellationToken.None);

        // PW unharmed — blocker intercepted the damage
        pw.Loyalty.Should().Be(4);
        // Blocker took damage
        blocker.DamageMarked.Should().Be(2);
        // Player life unaffected
        state.Player2.Life.Should().Be(20);
    }
}
