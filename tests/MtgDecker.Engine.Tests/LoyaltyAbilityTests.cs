using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class LoyaltyAbilityTests : IDisposable
{
    private const string TestPwName = "Test Loyalty PW";

    public LoyaltyAbilityTests()
    {
        CardDefinitions.Register(new CardDefinition(
            ManaCost.Parse("{2}{U}{B}"), null, null, null, CardType.Planeswalker)
        {
            Name = TestPwName,
            StartingLoyalty = 4,
            LoyaltyAbilities =
            [
                new LoyaltyAbility(1, new DealDamageEffect(1), "+1: Deal 1"),
                new LoyaltyAbility(0, new DealDamageEffect(2), "0: Deal 2"),
                new LoyaltyAbility(-2, new DealDamageEffect(3), "-2: Deal 3"),
            ],
        });
    }

    public void Dispose() => CardDefinitions.Unregister(TestPwName);

    private (GameEngine engine, GameState state, Player p1, Player p2,
        TestDecisionHandler h1, TestDecisionHandler h2) SetupWithPW()
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
    public async Task ActivateLoyaltyAbility_Plus1_IncreasesLoyaltyAndPushesOnStack()
    {
        var (engine, state, p1, p2, h1, h2) = SetupWithPW();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var pw = GameCard.Create(TestPwName);
        pw.AddCounters(CounterType.Loyalty, 4);
        pw.TurnEnteredBattlefield = state.TurnNumber - 1;
        p1.Battlefield.Add(pw);

        await engine.ExecuteAction(GameAction.ActivateLoyaltyAbility(p1.Id, pw.Id, 0));

        pw.Loyalty.Should().Be(5); // 4 + 1
        state.StackCount.Should().Be(1);
    }

    [Fact]
    public async Task ActivateLoyaltyAbility_Minus2_DecreasesLoyalty()
    {
        var (engine, state, p1, p2, h1, h2) = SetupWithPW();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var pw = GameCard.Create(TestPwName);
        pw.AddCounters(CounterType.Loyalty, 4);
        pw.TurnEnteredBattlefield = state.TurnNumber - 1;
        p1.Battlefield.Add(pw);

        await engine.ExecuteAction(GameAction.ActivateLoyaltyAbility(p1.Id, pw.Id, 2));

        pw.Loyalty.Should().Be(2); // 4 - 2
        state.StackCount.Should().Be(1);
    }

    [Fact]
    public async Task ActivateLoyaltyAbility_RejectedIfNotSorcerySpeed()
    {
        var (engine, state, p1, p2, h1, h2) = SetupWithPW();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.Combat; // Not sorcery speed

        var pw = GameCard.Create(TestPwName);
        pw.AddCounters(CounterType.Loyalty, 4);
        pw.TurnEnteredBattlefield = state.TurnNumber - 1;
        p1.Battlefield.Add(pw);

        await engine.ExecuteAction(GameAction.ActivateLoyaltyAbility(p1.Id, pw.Id, 0));

        state.StackCount.Should().Be(0);
        pw.Loyalty.Should().Be(4);
    }

    [Fact]
    public async Task ActivateLoyaltyAbility_RejectedIfAlreadyUsedThisTurn()
    {
        var (engine, state, p1, p2, h1, h2) = SetupWithPW();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var pw = GameCard.Create(TestPwName);
        pw.AddCounters(CounterType.Loyalty, 4);
        pw.TurnEnteredBattlefield = state.TurnNumber - 1;
        p1.Battlefield.Add(pw);

        // First activation succeeds
        await engine.ExecuteAction(GameAction.ActivateLoyaltyAbility(p1.Id, pw.Id, 0));
        state.StackCount.Should().Be(1);

        // Second activation rejected
        await engine.ExecuteAction(GameAction.ActivateLoyaltyAbility(p1.Id, pw.Id, 1));
        state.StackCount.Should().Be(1); // Still 1
    }

    [Fact]
    public async Task ActivateLoyaltyAbility_RejectedIfLoyaltyWouldGoBelowZero()
    {
        var (engine, state, p1, p2, h1, h2) = SetupWithPW();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var pw = GameCard.Create(TestPwName);
        pw.AddCounters(CounterType.Loyalty, 1); // Only 1 loyalty
        pw.TurnEnteredBattlefield = state.TurnNumber - 1;
        p1.Battlefield.Add(pw);

        // -2 ability would go below 0
        await engine.ExecuteAction(GameAction.ActivateLoyaltyAbility(p1.Id, pw.Id, 2));

        state.StackCount.Should().Be(0);
        pw.Loyalty.Should().Be(1);
    }
}
