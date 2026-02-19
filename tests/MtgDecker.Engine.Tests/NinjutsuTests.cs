using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class NinjutsuTests : IDisposable
{
    private const string TestNinjaName = "Test Ninjutsu Creature";

    public NinjutsuTests()
    {
        CardDefinitions.Register(new CardDefinition(
            ManaCost.Parse("{2}{U}{B}"), null, 3, 2, CardType.Creature)
        {
            Name = TestNinjaName,
            NinjutsuCost = ManaCost.Parse("{1}{U}{B}"),
        });
    }

    public void Dispose() => CardDefinitions.Unregister(TestNinjaName);

    private (GameEngine engine, GameState state, Player p1, Player p2,
        TestDecisionHandler h1, TestDecisionHandler h2) CreateSetup()
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
        return (engine, state, p1, p2, h1, h2);
    }

    [Fact]
    public async Task Ninjutsu_ReturnsUnblockedAttacker_PutsNinjaOnBattlefield()
    {
        var (engine, state, p1, p2, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.Combat;

        var attacker = new GameCard
        {
            Name = "Unblocked Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
            TurnEnteredBattlefield = state.TurnNumber - 1,
        };
        p1.Battlefield.Add(attacker);

        var ninja = GameCard.Create(TestNinjaName);
        p1.Hand.Add(ninja);

        p1.ManaPool.Add(ManaColor.Blue, 1);
        p1.ManaPool.Add(ManaColor.Black, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 1);

        state.Combat = new CombatState(p1.Id, p2.Id);
        state.Combat.DeclareAttacker(attacker.Id);
        state.CombatStep = CombatStep.DeclareBlockers;

        await engine.ExecuteAction(
            GameAction.Ninjutsu(p1.Id, ninja.Id, attacker.Id));

        // Bear returned to hand
        p1.Hand.Cards.Should().Contain(c => c.Name == "Unblocked Bear");
        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Unblocked Bear");

        // Ninja on battlefield, tapped
        p1.Battlefield.Cards.Should().Contain(c => c.Name == TestNinjaName);
        var ninjaOnField = p1.Battlefield.Cards.First(c => c.Name == TestNinjaName);
        ninjaOnField.IsTapped.Should().BeTrue();
    }

    [Fact]
    public async Task Ninjutsu_NinjaIsAttacking()
    {
        var (engine, state, p1, p2, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.Combat;

        var attacker = new GameCard
        {
            Name = "Unblocked Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
            TurnEnteredBattlefield = state.TurnNumber - 1,
        };
        p1.Battlefield.Add(attacker);

        var ninja = GameCard.Create(TestNinjaName);
        p1.Hand.Add(ninja);

        p1.ManaPool.Add(ManaColor.Blue, 1);
        p1.ManaPool.Add(ManaColor.Black, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 1);

        state.Combat = new CombatState(p1.Id, p2.Id);
        state.Combat.DeclareAttacker(attacker.Id);
        state.CombatStep = CombatStep.DeclareBlockers;

        await engine.ExecuteAction(
            GameAction.Ninjutsu(p1.Id, ninja.Id, attacker.Id));

        // Ninja should be in the attackers list
        var ninjaOnField = p1.Battlefield.Cards.First(c => c.Name == TestNinjaName);
        state.Combat.Attackers.Should().Contain(ninjaOnField.Id);

        // Original attacker should no longer be in attackers list
        state.Combat.Attackers.Should().NotContain(attacker.Id);
    }

    [Fact]
    public async Task Ninjutsu_PaysMana()
    {
        var (engine, state, p1, p2, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.Combat;

        var attacker = new GameCard
        {
            Name = "Unblocked Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
            TurnEnteredBattlefield = state.TurnNumber - 1,
        };
        p1.Battlefield.Add(attacker);

        var ninja = GameCard.Create(TestNinjaName);
        p1.Hand.Add(ninja);

        p1.ManaPool.Add(ManaColor.Blue, 1);
        p1.ManaPool.Add(ManaColor.Black, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 1);

        state.Combat = new CombatState(p1.Id, p2.Id);
        state.Combat.DeclareAttacker(attacker.Id);
        state.CombatStep = CombatStep.DeclareBlockers;

        await engine.ExecuteAction(
            GameAction.Ninjutsu(p1.Id, ninja.Id, attacker.Id));

        // Mana should have been spent: {1}{U}{B} = 3 total
        p1.ManaPool.Total.Should().Be(0);
    }

    [Fact]
    public async Task Ninjutsu_RejectedIfBlockedAttacker()
    {
        var (engine, state, p1, p2, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.Combat;

        var attacker = new GameCard
        {
            Name = "Blocked Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
            TurnEnteredBattlefield = state.TurnNumber - 1,
        };
        p1.Battlefield.Add(attacker);

        var blocker = new GameCard
        {
            Name = "Wall",
            CardTypes = CardType.Creature,
            BasePower = 0,
            BaseToughness = 4,
        };
        p2.Battlefield.Add(blocker);

        var ninja = GameCard.Create(TestNinjaName);
        p1.Hand.Add(ninja);

        p1.ManaPool.Add(ManaColor.Blue, 1);
        p1.ManaPool.Add(ManaColor.Black, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 1);

        state.Combat = new CombatState(p1.Id, p2.Id);
        state.Combat.DeclareAttacker(attacker.Id);
        state.Combat.DeclareBlocker(blocker.Id, attacker.Id);
        state.CombatStep = CombatStep.DeclareBlockers;

        await engine.ExecuteAction(
            GameAction.Ninjutsu(p1.Id, ninja.Id, attacker.Id));

        // Ninjutsu rejected — bear still on battlefield
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Blocked Bear");
        p1.Hand.Cards.Should().Contain(c => c.Name == TestNinjaName);
    }

    [Fact]
    public async Task Ninjutsu_RejectedIfNotDuringCombat()
    {
        var (engine, state, p1, p2, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var attacker = new GameCard
        {
            Name = "Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        p1.Battlefield.Add(attacker);

        var ninja = GameCard.Create(TestNinjaName);
        p1.Hand.Add(ninja);

        p1.ManaPool.Add(ManaColor.Blue, 1);
        p1.ManaPool.Add(ManaColor.Black, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 1);

        await engine.ExecuteAction(
            GameAction.Ninjutsu(p1.Id, ninja.Id, attacker.Id));

        // Rejected — main phase
        p1.Hand.Cards.Should().Contain(c => c.Name == TestNinjaName);
    }

    [Fact]
    public async Task Ninjutsu_RejectedIfInsufficientMana()
    {
        var (engine, state, p1, p2, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.Combat;

        var attacker = new GameCard
        {
            Name = "Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
            TurnEnteredBattlefield = state.TurnNumber - 1,
        };
        p1.Battlefield.Add(attacker);

        var ninja = GameCard.Create(TestNinjaName);
        p1.Hand.Add(ninja);

        // No mana!
        state.Combat = new CombatState(p1.Id, p2.Id);
        state.Combat.DeclareAttacker(attacker.Id);
        state.CombatStep = CombatStep.DeclareBlockers;

        await engine.ExecuteAction(
            GameAction.Ninjutsu(p1.Id, ninja.Id, attacker.Id));

        // Rejected — no mana
        p1.Hand.Cards.Should().Contain(c => c.Name == TestNinjaName);
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Bear");
    }

    [Fact]
    public async Task Ninjutsu_RejectedIfNoCombatState()
    {
        var (engine, state, p1, p2, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.Combat;
        // No combat state set

        var attacker = new GameCard
        {
            Name = "Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
            TurnEnteredBattlefield = state.TurnNumber - 1,
        };
        p1.Battlefield.Add(attacker);

        var ninja = GameCard.Create(TestNinjaName);
        p1.Hand.Add(ninja);

        p1.ManaPool.Add(ManaColor.Blue, 1);
        p1.ManaPool.Add(ManaColor.Black, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 1);

        await engine.ExecuteAction(
            GameAction.Ninjutsu(p1.Id, ninja.Id, attacker.Id));

        // Rejected — no combat state
        p1.Hand.Cards.Should().Contain(c => c.Name == TestNinjaName);
    }

    [Fact]
    public async Task Ninjutsu_RejectedIfCardHasNoNinjutsuCost()
    {
        var (engine, state, p1, p2, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.Combat;

        var attacker = new GameCard
        {
            Name = "Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
            TurnEnteredBattlefield = state.TurnNumber - 1,
        };
        p1.Battlefield.Add(attacker);

        // Regular card with no ninjutsu cost
        var regularCard = new GameCard
        {
            Name = "Regular Creature",
            CardTypes = CardType.Creature,
        };
        p1.Hand.Add(regularCard);

        p1.ManaPool.Add(ManaColor.Blue, 1);
        p1.ManaPool.Add(ManaColor.Black, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 1);

        state.Combat = new CombatState(p1.Id, p2.Id);
        state.Combat.DeclareAttacker(attacker.Id);
        state.CombatStep = CombatStep.DeclareBlockers;

        await engine.ExecuteAction(
            GameAction.Ninjutsu(p1.Id, regularCard.Id, attacker.Id));

        // Rejected — no ninjutsu cost
        p1.Hand.Cards.Should().Contain(c => c.Name == "Regular Creature");
    }

    [Fact]
    public async Task Ninjutsu_RejectedIfReturnCreatureNotAttacking()
    {
        var (engine, state, p1, p2, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.Combat;

        var nonAttacker = new GameCard
        {
            Name = "Sitting Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
            TurnEnteredBattlefield = state.TurnNumber - 1,
        };
        p1.Battlefield.Add(nonAttacker);

        // Another creature that IS attacking (needed for valid combat state)
        var attacker = new GameCard
        {
            Name = "Attacking Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
            TurnEnteredBattlefield = state.TurnNumber - 1,
        };
        p1.Battlefield.Add(attacker);

        var ninja = GameCard.Create(TestNinjaName);
        p1.Hand.Add(ninja);

        p1.ManaPool.Add(ManaColor.Blue, 1);
        p1.ManaPool.Add(ManaColor.Black, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 1);

        state.Combat = new CombatState(p1.Id, p2.Id);
        state.Combat.DeclareAttacker(attacker.Id); // Only attacker is attacking
        state.CombatStep = CombatStep.DeclareBlockers;

        // Try to return the non-attacker
        await engine.ExecuteAction(
            GameAction.Ninjutsu(p1.Id, ninja.Id, nonAttacker.Id));

        // Rejected — nonAttacker is not attacking
        p1.Hand.Cards.Should().Contain(c => c.Name == TestNinjaName);
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Sitting Bear");
    }

    [Fact]
    public async Task Ninjutsu_TriggersETB()
    {
        // Register a ninja with an ETB trigger
        const string etbNinjaName = "ETB Test Ninja";
        CardDefinitions.Register(new CardDefinition(
            ManaCost.Parse("{2}{U}{B}"), null, 2, 2, CardType.Creature)
        {
            Name = etbNinjaName,
            NinjutsuCost = ManaCost.Parse("{U}{B}"),
        });

        try
        {
            var (engine, state, p1, p2, h1, h2) = CreateSetup();
            await engine.StartGameAsync();
            state.ActivePlayer = p1;
            state.CurrentPhase = Phase.Combat;

            var attacker = new GameCard
            {
                Name = "Unblocked Bear",
                CardTypes = CardType.Creature,
                BasePower = 2,
                BaseToughness = 2,
                TurnEnteredBattlefield = state.TurnNumber - 1,
            };
            p1.Battlefield.Add(attacker);

            var ninja = GameCard.Create(etbNinjaName);
            p1.Hand.Add(ninja);

            p1.ManaPool.Add(ManaColor.Blue, 1);
            p1.ManaPool.Add(ManaColor.Black, 1);

            state.Combat = new CombatState(p1.Id, p2.Id);
            state.Combat.DeclareAttacker(attacker.Id);
            state.CombatStep = CombatStep.DeclareBlockers;

            await engine.ExecuteAction(
                GameAction.Ninjutsu(p1.Id, ninja.Id, attacker.Id));

            // Ninja should have TurnEnteredBattlefield set
            var ninjaOnField = p1.Battlefield.Cards.First(c => c.Name == etbNinjaName);
            ninjaOnField.TurnEnteredBattlefield.Should().Be(state.TurnNumber);
        }
        finally
        {
            CardDefinitions.Unregister(etbNinjaName);
        }
    }
}
