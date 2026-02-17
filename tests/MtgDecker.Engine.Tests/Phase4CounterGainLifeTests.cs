using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class Phase4CounterGainLifeTests
{
    private static (GameState state, Player p1, Player p2) CreateGameState()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Alice", h1);
        var p2 = new Player(Guid.NewGuid(), "Bob", h2);
        var state = new GameState(p1, p2);
        return (state, p1, p2);
    }

    [Fact]
    public void CounterAndGainLife_CountersSpell_AndGainsLife()
    {
        // Arrange
        var (state, p1, p2) = CreateGameState();
        var targetCard = GameCard.Create("Lightning Bolt", "Instant");
        var targetSpell = new StackObject(targetCard, p2.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);
        state.StackPush(targetSpell);

        var counterCard = GameCard.Create("Absorb", "Instant");
        var target = new TargetInfo(targetCard.Id, p2.Id, ZoneType.Stack);
        var counterSpell = new StackObject(counterCard, p1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo> { target }, 0);

        var effect = new CounterAndGainLifeEffect(3);

        // Act
        effect.Resolve(state, counterSpell);

        // Assert: spell countered (removed from stack)
        state.Stack.OfType<StackObject>().Should().NotContain(s => s.Card.Id == targetCard.Id);
        // Assert: controller gains 3 life (20 + 3 = 23)
        p1.Life.Should().Be(23);
    }

    [Fact]
    public void CounterAndGainLife_Fizzles_NoLifeGain()
    {
        // Arrange: target spell not on the stack (already resolved)
        var (state, p1, p2) = CreateGameState();
        var targetCard = GameCard.Create("Lightning Bolt", "Instant");
        // Note: target spell is NOT on the stack

        var counterCard = GameCard.Create("Absorb", "Instant");
        var target = new TargetInfo(targetCard.Id, p2.Id, ZoneType.Stack);
        var counterSpell = new StackObject(counterCard, p1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo> { target }, 0);

        var effect = new CounterAndGainLifeEffect(3);

        // Act
        effect.Resolve(state, counterSpell);

        // Assert: fizzle - no life gain, life stays at 20
        p1.Life.Should().Be(20);
    }

    [Fact]
    public void CounterAndGainLife_SendsCounteredCardToGraveyard()
    {
        // Arrange
        var (state, p1, p2) = CreateGameState();
        var targetCard = GameCard.Create("Lightning Bolt", "Instant");
        var targetSpell = new StackObject(targetCard, p2.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);
        state.StackPush(targetSpell);

        var counterCard = GameCard.Create("Absorb", "Instant");
        var target = new TargetInfo(targetCard.Id, p2.Id, ZoneType.Stack);
        var counterSpell = new StackObject(counterCard, p1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo> { target }, 0);

        var effect = new CounterAndGainLifeEffect(3);

        // Act
        effect.Resolve(state, counterSpell);

        // Assert: countered card goes to its owner's (p2's) graveyard
        p2.Graveyard.Cards.Should().Contain(c => c.Id == targetCard.Id);
    }
}
