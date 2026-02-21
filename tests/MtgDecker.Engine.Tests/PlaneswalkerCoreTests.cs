using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class PlaneswalkerCoreTests
{
    [Fact]
    public void CardType_Planeswalker_HasCorrectFlagValue()
    {
        ((int)CardType.Planeswalker).Should().Be(64);
    }

    [Fact]
    public void GameCard_IsPlaneswalker_TrueWhenPlaneswalkerType()
    {
        var card = new GameCard { CardTypes = CardType.Planeswalker };
        card.IsPlaneswalker.Should().BeTrue();
    }

    [Fact]
    public void GameCard_IsPlaneswalker_FalseForCreature()
    {
        var card = new GameCard { CardTypes = CardType.Creature };
        card.IsPlaneswalker.Should().BeFalse();
    }

    [Fact]
    public void GameCard_IsPlaneswalker_TrueWhenCombinedWithCreature()
    {
        var card = new GameCard { CardTypes = CardType.Creature | CardType.Planeswalker };
        card.IsPlaneswalker.Should().BeTrue();
        card.IsCreature.Should().BeTrue();
    }

    [Fact]
    public void GameCard_Loyalty_ReadsFromLoyaltyCounters()
    {
        var card = new GameCard { CardTypes = CardType.Planeswalker };
        card.AddCounters(CounterType.Loyalty, 4);
        card.Loyalty.Should().Be(4);
    }

    [Fact]
    public void GameCard_Loyalty_ZeroWhenNoCounters()
    {
        var card = new GameCard { CardTypes = CardType.Planeswalker };
        card.Loyalty.Should().Be(0);
    }

    [Fact]
    public void CardDefinition_StartingLoyalty_CanBeSet()
    {
        var def = new CardDefinition(null, null, null, null, CardType.Planeswalker)
        {
            StartingLoyalty = 4,
        };
        def.StartingLoyalty.Should().Be(4);
    }

    [Fact]
    public async Task SBA_Planeswalker_ZeroLoyalty_MovesToGraveyard()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var pw = new GameCard
        {
            Name = "Dying Planeswalker",
            CardTypes = CardType.Planeswalker,
        };
        // No loyalty counters = 0 loyalty
        p1.Battlefield.Add(pw);

        await engine.CheckStateBasedActionsAsync();

        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Dying Planeswalker");
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Dying Planeswalker");
    }

    [Fact]
    public async Task SBA_Planeswalker_PositiveLoyalty_StaysOnBattlefield()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var pw = new GameCard
        {
            Name = "Healthy Planeswalker",
            CardTypes = CardType.Planeswalker,
        };
        pw.AddCounters(CounterType.Loyalty, 3);
        p1.Battlefield.Add(pw);

        await engine.CheckStateBasedActionsAsync();

        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Healthy Planeswalker");
    }

    [Fact]
    public async Task Planeswalker_EntersWithLoyaltyCounters_WhenCast()
    {
        // Register a test planeswalker in CardDefinitions for this test
        var testPwName = "Test Planeswalker ETB";
        CardDefinitions.Register(new CardDefinition(
            ManaCost.Parse("{2}{U}{U}"), null, null, null, CardType.Planeswalker)
        {
            Name = testPwName,
            StartingLoyalty = 4,
        });

        try
        {
            var handler = new TestDecisionHandler();
            var state = new GameState(
                new Player(Guid.NewGuid(), "P1", handler),
                new Player(Guid.NewGuid(), "P2", handler));
            state.CurrentPhase = Phase.MainPhase1;

            var engine = new GameEngine(state);

            // Give player enough mana to cast (2UU)
            state.Player1.ManaPool.Add(ManaColor.Blue, 2);
            state.Player1.ManaPool.Add(ManaColor.Colorless, 2);

            var pw = GameCard.Create(testPwName, "Legendary Planeswalker — Test");
            state.Player1.Hand.Add(pw);

            // Cast the planeswalker
            await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, pw.Id));
            await engine.ResolveAllTriggersAsync();

            // The planeswalker should be on the battlefield with loyalty counters
            state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == pw.Id);
            pw.GetCounters(CounterType.Loyalty).Should().Be(4,
                "Planeswalker should enter with starting loyalty as loyalty counters");
            pw.Loyalty.Should().Be(4);
        }
        finally
        {
            CardDefinitions.Unregister(testPwName);
        }
    }

    [Fact]
    public void LoyaltyAbility_Record_StoresCorrectValues()
    {
        var effect = new DealDamageEffect(1);
        var ability = new LoyaltyAbility(-2, effect, "Deal 1 damage");
        ability.LoyaltyCost.Should().Be(-2);
        ability.Effect.Should().Be(effect);
        ability.Description.Should().Be("Deal 1 damage");
    }

    [Fact]
    public void CardDefinition_LoyaltyAbilities_CanBeSet()
    {
        var def = new CardDefinition(null, null, null, null, CardType.Planeswalker)
        {
            LoyaltyAbilities =
            [
                new LoyaltyAbility(1, new DealDamageEffect(1), "+1: Deal 1"),
                new LoyaltyAbility(-2, new DealDamageEffect(2), "-2: Deal 2"),
            ],
        };
        def.LoyaltyAbilities.Should().HaveCount(2);
    }

    [Fact]
    public void GameAction_ActivateLoyaltyAbility_HasCorrectProperties()
    {
        var action = GameAction.ActivateLoyaltyAbility(Guid.NewGuid(), Guid.NewGuid(), 1);
        action.Type.Should().Be(ActionType.ActivateLoyaltyAbility);
        action.AbilityIndex.Should().Be(1);
    }
}
