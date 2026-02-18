using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class AmassEffectTests
{
    private static (EffectContext context, Player player, GameState state) CreateContext()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var source = new GameCard { Name = "Source" };
        var context = new EffectContext(state, p1, source, h1);
        return (context, p1, state);
    }

    [Fact]
    public async Task AmassOrcs_NoArmyExists_CreatesOrcArmyTokenWithCounter()
    {
        var (context, player, state) = CreateContext();
        var effect = new AmassEffect("Orc", 1);

        await effect.Execute(context);

        player.Battlefield.Cards.Should().HaveCount(1);
        var token = player.Battlefield.Cards[0];
        token.IsToken.Should().BeTrue();
        token.Name.Should().Be("Orc Army");
        token.IsCreature.Should().BeTrue();
        token.Subtypes.Should().Contain("Orc");
        token.Subtypes.Should().Contain("Army");
        token.BasePower.Should().Be(0);
        token.BaseToughness.Should().Be(0);
        token.GetCounters(CounterType.PlusOnePlusOne).Should().Be(1);
    }

    [Fact]
    public async Task AmassOrcs_ArmyExists_AddsCounterToExisting()
    {
        var (context, player, state) = CreateContext();

        var army = new GameCard
        {
            Name = "Orc Army",
            BasePower = 0,
            BaseToughness = 0,
            CardTypes = CardType.Creature,
            Subtypes = ["Orc", "Army"],
            IsToken = true,
        };
        army.AddCounters(CounterType.PlusOnePlusOne, 2);
        player.Battlefield.Add(army);

        var effect = new AmassEffect("Orc", 1);
        await effect.Execute(context);

        player.Battlefield.Cards.Where(c => c.Subtypes.Contains("Army")).Should().HaveCount(1);
        army.GetCounters(CounterType.PlusOnePlusOne).Should().Be(3);
    }

    [Fact]
    public async Task AmassOrcs_HigherAmassValue_AddsMultipleCounters()
    {
        var (context, player, state) = CreateContext();
        var effect = new AmassEffect("Orc", 3);

        await effect.Execute(context);

        var token = player.Battlefield.Cards[0];
        token.GetCounters(CounterType.PlusOnePlusOne).Should().Be(3);
    }
}
