using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class CityOfTraitorsTests
{
    [Fact]
    public async Task CityOfTraitors_WhenAnotherLandPlayed_SacrificesSelf()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);
        state.ActivePlayer = state.Player1;
        state.TurnNumber = 1;
        state.CurrentPhase = Phase.MainPhase1;

        // City is on battlefield with the trigger
        var city = new GameCard
        {
            Name = "City of Traitors",
            CardTypes = CardType.Land,
            ManaAbility = ManaAbility.FixedMultiple(ManaColor.Colorless, 2),
            Triggers = [new Trigger(GameEvent.LandPlayed, TriggerCondition.ControllerPlaysAnotherLand, new SacrificeSelfOnLandEffect())],
        };
        state.Player1.Battlefield.Add(city);

        // Play another land from hand
        var island = new GameCard
        {
            Name = "Island",
            CardTypes = CardType.Land,
            ManaAbility = ManaAbility.Fixed(ManaColor.Blue),
        };
        state.Player1.Hand.Add(island);

        await engine.ExecuteAction(GameAction.PlayLand(state.Player1.Id, island.Id));
        await engine.ResolveAllTriggersAsync();

        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Name == "City of Traitors");
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Name == "City of Traitors");
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Name == "Island");
    }

    [Fact]
    public async Task CityOfTraitors_TapsForMana_DoesNotSacrifice()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);

        var city = new GameCard
        {
            Name = "City of Traitors",
            CardTypes = CardType.Land,
            ManaAbility = ManaAbility.FixedMultiple(ManaColor.Colorless, 2),
            Triggers = [new Trigger(GameEvent.LandPlayed, TriggerCondition.ControllerPlaysAnotherLand, new SacrificeSelfOnLandEffect())],
        };
        state.Player1.Battlefield.Add(city);

        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, city.Id));

        state.Player1.Battlefield.Cards.Should().Contain(c => c.Name == "City of Traitors");
        state.Player1.ManaPool[ManaColor.Colorless].Should().Be(2);
    }

    [Fact]
    public async Task CityOfTraitors_OpponentPlaysLand_DoesNotSacrifice()
    {
        var (state, h1, h2) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);
        state.ActivePlayer = state.Player2;
        state.TurnNumber = 1;
        state.CurrentPhase = Phase.MainPhase1;

        // City is P1's
        var city = new GameCard
        {
            Name = "City of Traitors",
            CardTypes = CardType.Land,
            ManaAbility = ManaAbility.FixedMultiple(ManaColor.Colorless, 2),
            Triggers = [new Trigger(GameEvent.LandPlayed, TriggerCondition.ControllerPlaysAnotherLand, new SacrificeSelfOnLandEffect())],
        };
        state.Player1.Battlefield.Add(city);

        // P2 plays a land
        var island = new GameCard
        {
            Name = "Island",
            CardTypes = CardType.Land,
            ManaAbility = ManaAbility.Fixed(ManaColor.Blue),
        };
        state.Player2.Hand.Add(island);

        await engine.ExecuteAction(GameAction.PlayLand(state.Player2.Id, island.Id));
        await engine.ResolveAllTriggersAsync();

        // City should still be on P1's battlefield
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Name == "City of Traitors");
    }
}
