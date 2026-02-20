using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class AncientTombManaTests
{
    [Fact]
    public async Task TapCard_FixedMultiple_ProducesMultipleMana()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);

        var tomb = new GameCard
        {
            Name = "Ancient Tomb",
            CardTypes = CardType.Land,
            ManaAbility = ManaAbility.FixedMultiple(ManaColor.Colorless, 2, selfDamage: 2),
        };
        state.Player1.Battlefield.Add(tomb);

        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, tomb.Id));

        state.Player1.ManaPool[ManaColor.Colorless].Should().Be(2);
    }

    [Fact]
    public async Task TapCard_FixedMultipleWithDamage_DealsDamageToController()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);

        var tomb = new GameCard
        {
            Name = "Ancient Tomb",
            CardTypes = CardType.Land,
            ManaAbility = ManaAbility.FixedMultiple(ManaColor.Colorless, 2, selfDamage: 2),
        };
        state.Player1.Battlefield.Add(tomb);

        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, tomb.Id));

        state.Player1.Life.Should().Be(18); // 20 - 2
    }

    [Fact]
    public async Task TapCard_FixedMultipleNoDamage_DoesNotDealDamage()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);

        var city = new GameCard
        {
            Name = "City of Traitors",
            CardTypes = CardType.Land,
            ManaAbility = ManaAbility.FixedMultiple(ManaColor.Colorless, 2),
        };
        state.Player1.Battlefield.Add(city);

        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, city.Id));

        state.Player1.ManaPool[ManaColor.Colorless].Should().Be(2);
        state.Player1.Life.Should().Be(20); // no damage
    }

    [Fact]
    public async Task TapCard_NormalFixed_StillProducesOneMana()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);

        var mountain = new GameCard
        {
            Name = "Mountain",
            CardTypes = CardType.Land,
            ManaAbility = ManaAbility.Fixed(ManaColor.Red),
        };
        state.Player1.Battlefield.Add(mountain);

        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, mountain.Id));

        state.Player1.ManaPool[ManaColor.Red].Should().Be(1);
        state.Player1.Life.Should().Be(20);
    }
}
