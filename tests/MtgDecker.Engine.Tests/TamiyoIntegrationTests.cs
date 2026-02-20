using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class TamiyoIntegrationTests
{
    private static (GameEngine engine, GameState state, Player p1, Player p2,
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

    #region Front face creation from registry

    [Fact]
    public void Tamiyo_CreatedFromRegistry_HasCorrectFrontFace()
    {
        var card = GameCard.Create("Tamiyo, Inquisitive Student");
        card.IsCreature.Should().BeTrue();
        card.BasePower.Should().Be(0);
        card.BaseToughness.Should().Be(3);
        card.IsTransformed.Should().BeFalse();
        card.BackFaceDefinition.Should().NotBeNull();
    }

    [Fact]
    public void Tamiyo_CreatedFromRegistry_HasBackFaceInfo()
    {
        var card = GameCard.Create("Tamiyo, Inquisitive Student");
        card.BackFaceDefinition!.Name.Should().Be("Tamiyo, Seasoned Scholar");
        card.BackFaceDefinition.CardTypes.Should().Be(CardType.Planeswalker);
        card.BackFaceDefinition.StartingLoyalty.Should().Be(2);
    }

    #endregion

    #region Flying keyword via RecalculateState

    [Fact]
    public void Tamiyo_FrontFace_HasFlyingViaRecalculateState()
    {
        var (engine, state, p1, _, _, _) = CreateSetup();
        state.ActivePlayer = p1;

        var tamiyo = GameCard.Create("Tamiyo, Inquisitive Student");
        p1.Battlefield.Add(tamiyo);

        engine.RecalculateState();

        tamiyo.ActiveKeywords.Should().Contain(Keyword.Flying,
            "Tamiyo front face has flying as a continuous effect");
    }

    #endregion

    #region Transform to planeswalker

    [Fact]
    public void Tamiyo_AfterTransform_IsPlaneswalkerWithLoyalty()
    {
        var (engine, state, p1, _, _, _) = CreateSetup();
        state.ActivePlayer = p1;

        var card = GameCard.Create("Tamiyo, Inquisitive Student");
        p1.Battlefield.Add(card);

        // Manually trigger transform
        card.IsTransformed = true;
        card.AddCounters(CounterType.Loyalty, card.BackFaceDefinition!.StartingLoyalty!.Value);

        card.Name.Should().Be("Tamiyo, Seasoned Scholar");
        card.IsPlaneswalker.Should().BeTrue();
        card.IsCreature.Should().BeFalse();
        card.Loyalty.Should().Be(2);
    }

    [Fact]
    public void Tamiyo_AfterTransform_FrontNameStillAccessible()
    {
        var card = GameCard.Create("Tamiyo, Inquisitive Student");
        card.IsTransformed = true;

        card.FrontName.Should().Be("Tamiyo, Inquisitive Student");
        card.Name.Should().Be("Tamiyo, Seasoned Scholar");
    }

    [Fact]
    public void Tamiyo_Transform_SetsLoyaltyCountersCorrectly()
    {
        var card = GameCard.Create("Tamiyo, Inquisitive Student");

        // Before transform: no loyalty
        card.Loyalty.Should().Be(0);

        // Transform and add starting loyalty (mirroring TransformExileReturnEffect behavior)
        card.IsTransformed = true;
        card.AddCounters(CounterType.Loyalty, card.BackFaceDefinition!.StartingLoyalty!.Value);

        // Should have exactly the starting loyalty (2)
        card.Loyalty.Should().Be(2);
        card.GetCounters(CounterType.Loyalty).Should().Be(2);
    }

    #endregion

    #region Loyalty ability: +2 defense

    [Fact]
    public async Task Tamiyo_PlusTwo_CreatesDefenseEffect()
    {
        var (engine, state, p1, p2, h1, _) = CreateSetup();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var tamiyo = GameCard.Create("Tamiyo, Inquisitive Student");
        tamiyo.IsTransformed = true;
        tamiyo.AddCounters(CounterType.Loyalty, 2);
        p1.Battlefield.Add(tamiyo);

        // Activate +2 ability (index 0)
        await engine.ExecuteAction(GameAction.ActivateLoyaltyAbility(p1.Id, tamiyo.Id, 0));

        // Loyalty: 2 + 2 = 4
        tamiyo.Loyalty.Should().Be(4);

        // Resolve the stack
        await engine.ResolveAllTriggersAsync();

        // Defense effect should be active — opponent creatures get -1/-0
        state.ActiveEffects.Should().Contain(e =>
            e.Type == ContinuousEffectType.ModifyPowerToughness && e.PowerMod == -1);
    }

    #endregion

    #region Loyalty ability: -3 recover

    [Fact]
    public async Task Tamiyo_Recover_ReturnsInstantFromGraveyard()
    {
        var (engine, state, p1, _, h1, _) = CreateSetup();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var tamiyo = GameCard.Create("Tamiyo, Inquisitive Student");
        tamiyo.IsTransformed = true;
        tamiyo.AddCounters(CounterType.Loyalty, 5);
        p1.Battlefield.Add(tamiyo);

        var instant = new GameCard { Name = "Lightning Bolt", CardTypes = CardType.Instant };
        p1.Graveyard.Add(instant);

        // Queue choose card response for the -3 ability targeting the instant
        h1.EnqueueCardChoice(instant.Id);

        // Activate -3 ability (index 1)
        await engine.ExecuteAction(GameAction.ActivateLoyaltyAbility(p1.Id, tamiyo.Id, 1));

        // Loyalty: 5 - 3 = 2
        tamiyo.Loyalty.Should().Be(2);

        // Resolve stack
        await engine.ResolveAllTriggersAsync();

        p1.Hand.Cards.Should().Contain(c => c.Name == "Lightning Bolt");
        p1.Graveyard.Cards.Should().NotContain(c => c.Name == "Lightning Bolt");
    }

    [Fact]
    public async Task Tamiyo_Recover_CannotActivateWithInsufficientLoyalty()
    {
        var (engine, state, p1, _, h1, _) = CreateSetup();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var tamiyo = GameCard.Create("Tamiyo, Inquisitive Student");
        tamiyo.IsTransformed = true;
        tamiyo.AddCounters(CounterType.Loyalty, 2); // Only 2, need 3
        p1.Battlefield.Add(tamiyo);

        var instant = new GameCard { Name = "Counterspell", CardTypes = CardType.Instant };
        p1.Graveyard.Add(instant);

        // Try -3 with only 2 loyalty
        await engine.ExecuteAction(GameAction.ActivateLoyaltyAbility(p1.Id, tamiyo.Id, 1));

        // Loyalty should not change (ability can't be activated)
        tamiyo.Loyalty.Should().Be(2);

        // Nothing on stack
        state.StackCount.Should().Be(0);

        // Card still in graveyard
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Counterspell");
    }

    #endregion

    #region Loyalty ability: -7 ultimate

    [Fact]
    public async Task Tamiyo_Ultimate_DrawsHalfLibraryAndCreatesEmblem()
    {
        var (engine, state, p1, _, h1, _) = CreateSetup();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var tamiyo = GameCard.Create("Tamiyo, Inquisitive Student");
        tamiyo.IsTransformed = true;
        tamiyo.AddCounters(CounterType.Loyalty, 7);
        p1.Battlefield.Add(tamiyo);

        // Clear hand and library to set up a known state
        p1.Hand.Clear();
        p1.Library.Clear();
        for (int i = 0; i < 10; i++)
            p1.Library.Add(new GameCard { Name = $"LibCard{i}" });

        // Activate -7 ability (index 2)
        await engine.ExecuteAction(GameAction.ActivateLoyaltyAbility(p1.Id, tamiyo.Id, 2));

        // Loyalty: 7 - 7 = 0
        tamiyo.Loyalty.Should().Be(0);

        // Resolve stack
        await engine.ResolveAllTriggersAsync();

        // Should draw ceil(10/2) = 5 cards
        p1.Hand.Cards.Should().HaveCount(5);
        p1.Library.Count.Should().Be(5);

        // Emblem created with no max hand size
        p1.Emblems.Should().HaveCount(1);
        p1.Emblems[0].Description.Should().Contain("no maximum hand size");
    }

    [Fact]
    public async Task Tamiyo_Ultimate_DrawsCorrectAmountForOddLibrary()
    {
        var (engine, state, p1, _, h1, _) = CreateSetup();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var tamiyo = GameCard.Create("Tamiyo, Inquisitive Student");
        tamiyo.IsTransformed = true;
        tamiyo.AddCounters(CounterType.Loyalty, 7);
        p1.Battlefield.Add(tamiyo);

        // Set up exactly 7 cards in library -> draws ceil(7/2) = 4
        p1.Hand.Clear();
        p1.Library.Clear();
        for (int i = 0; i < 7; i++)
            p1.Library.Add(new GameCard { Name = $"LibCard{i}" });

        await engine.ExecuteAction(GameAction.ActivateLoyaltyAbility(p1.Id, tamiyo.Id, 2));
        await engine.ResolveAllTriggersAsync();

        p1.Hand.Cards.Should().HaveCount(4, "ceil(7/2) = 4");
        p1.Library.Count.Should().Be(3);
        p1.Emblems.Should().HaveCount(1);
    }

    #endregion

    #region One loyalty ability per turn

    [Fact]
    public async Task Tamiyo_OnlyOneLoyaltyAbilityPerTurn()
    {
        var (engine, state, p1, _, h1, _) = CreateSetup();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var tamiyo = GameCard.Create("Tamiyo, Inquisitive Student");
        tamiyo.IsTransformed = true;
        tamiyo.AddCounters(CounterType.Loyalty, 4);
        p1.Battlefield.Add(tamiyo);

        // First activation: +2
        await engine.ExecuteAction(GameAction.ActivateLoyaltyAbility(p1.Id, tamiyo.Id, 0));
        await engine.ResolveAllTriggersAsync();

        tamiyo.Loyalty.Should().Be(6);

        // Second activation on same turn — should be blocked
        await engine.ExecuteAction(GameAction.ActivateLoyaltyAbility(p1.Id, tamiyo.Id, 0));

        // Loyalty should remain 6
        tamiyo.Loyalty.Should().Be(6);
    }

    #endregion
}
