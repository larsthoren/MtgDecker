using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class PayLifeAbilityTests : IDisposable
{
    private const string TestCardName = "Test Griselbrand";
    private const string TestNormalAbilityCard = "Test Prodigal Sorcerer";

    public PayLifeAbilityTests()
    {
        // Register test cards for PayLife ability testing
        CardDefinitions.Register(new CardDefinition(
            ManaCost: ManaCost.Parse("{4}{B}{B}{B}{B}"),
            ManaAbility: null,
            Power: 7,
            Toughness: 7,
            CardTypes: CardType.Creature
        )
        {
            Name = TestCardName,
            ActivatedAbility = new ActivatedAbility(
                Cost: new ActivatedAbilityCost(PayLife: 7),
                Effect: new DrawCardsActivatedEffect(7))
        });

        CardDefinitions.Register(new CardDefinition(
            ManaCost: ManaCost.Parse("{2}{U}"),
            ManaAbility: null,
            Power: 1,
            Toughness: 1,
            CardTypes: CardType.Creature
        )
        {
            Name = TestNormalAbilityCard,
            ActivatedAbility = new ActivatedAbility(
                Cost: new ActivatedAbilityCost(TapSelf: true),
                Effect: new DealDamageEffect(1),
                CanTargetPlayer: true)
        });
    }

    public void Dispose()
    {
        CardDefinitions.Unregister(TestCardName);
        CardDefinitions.Unregister(TestNormalAbilityCard);
    }

    [Fact]
    public async Task ActivateAbility_PayLife7_DeductsLifeAndDraws7Cards()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);
        state.ActivePlayer = state.Player1;
        state.CurrentPhase = Phase.MainPhase1;

        var griselbrand = new GameCard
        {
            Name = TestCardName,
            CardTypes = CardType.Creature,
            TurnEnteredBattlefield = 0, // no summoning sickness
        };
        state.Player1.Battlefield.Add(griselbrand);

        // Add 7 cards to library so draw succeeds
        for (int i = 0; i < 7; i++)
            state.Player1.Library.Add(new GameCard { Name = $"Card {i}", CardTypes = CardType.Creature });

        var initialLife = state.Player1.Life; // 20
        var initialHandCount = state.Player1.Hand.Cards.Count;

        await engine.ExecuteAction(GameAction.ActivateAbility(state.Player1.Id, griselbrand.Id));

        // Ability goes on the stack
        state.StackCount.Should().Be(1);

        // Resolve the ability from the stack
        await engine.ResolveAllTriggersAsync();

        // Life should be reduced by 7
        state.Player1.Life.Should().Be(initialLife - 7);

        // Hand should have 7 more cards
        state.Player1.Hand.Cards.Count.Should().Be(initialHandCount + 7);
    }

    [Fact]
    public async Task ActivateAbility_PayLife_CannotActivateWhenLifeTooLow()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);
        state.ActivePlayer = state.Player1;
        state.CurrentPhase = Phase.MainPhase1;

        var griselbrand = new GameCard
        {
            Name = TestCardName,
            CardTypes = CardType.Creature,
            TurnEnteredBattlefield = 0,
        };
        state.Player1.Battlefield.Add(griselbrand);

        // Set life below the PayLife cost of 7
        state.Player1.AdjustLife(-15); // life = 5
        state.Player1.Life.Should().Be(5);

        await engine.ExecuteAction(GameAction.ActivateAbility(state.Player1.Id, griselbrand.Id));

        // Ability should NOT be on the stack
        state.StackCount.Should().Be(0);

        // Life should be unchanged
        state.Player1.Life.Should().Be(5);
    }

    [Fact]
    public async Task ActivateAbility_NormalTapAbility_StillWorksWithoutPayLife()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);
        state.ActivePlayer = state.Player1;
        state.CurrentPhase = Phase.MainPhase1;

        var sorcerer = new GameCard
        {
            Name = TestNormalAbilityCard,
            CardTypes = CardType.Creature,
            TurnEnteredBattlefield = 0,
        };
        state.Player1.Battlefield.Add(sorcerer);

        var initialLife = state.Player1.Life;

        // Target the opponent player
        await engine.ExecuteAction(GameAction.ActivateAbility(
            state.Player1.Id, sorcerer.Id, targetPlayerId: state.Player2.Id));

        // Ability should be on the stack
        state.StackCount.Should().Be(1);

        // Life should be unchanged (no PayLife cost)
        state.Player1.Life.Should().Be(initialLife);

        // Card should be tapped
        sorcerer.IsTapped.Should().BeTrue();
    }
}
