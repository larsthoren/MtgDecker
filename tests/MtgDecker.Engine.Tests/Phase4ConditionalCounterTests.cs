using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class Phase4ConditionalCounterTests
{
    private GameState CreateState()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        return new GameState(p1, p2);
    }

    [Fact]
    public void ConditionalCounter_CountersSpell_WhenOpponentCannotPay()
    {
        // Arrange
        var state = CreateState();

        var targetCard = new GameCard { Name = "Lightning Bolt", ManaCost = ManaCost.Parse("{R}") };
        var targetSpell = new StackObject(targetCard, state.Player2.Id,
            new Dictionary<ManaColor, int> { [ManaColor.Red] = 1 },
            new List<TargetInfo>(), 0);
        state.StackPush(targetSpell);

        var counterCard = new GameCard { Name = "Mana Leak" };
        var counterSpell = new StackObject(counterCard, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(targetCard.Id, state.Player2.Id, ZoneType.Stack) }, 0);

        // Act - opponent has 0 mana in pool
        var effect = new ConditionalCounterEffect(3);
        effect.Resolve(state, counterSpell);

        // Assert - spell should be countered
        state.Stack.OfType<StackObject>().Should().NotContain(s => s.Card.Id == targetCard.Id);
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Id == targetCard.Id);
        state.GameLog.Should().Contain(msg => msg.Contains("countered") && msg.Contains("Mana Leak"));
    }

    [Fact]
    public void ConditionalCounter_DoesNotCounter_WhenOpponentPays()
    {
        // Arrange
        var state = CreateState();

        var targetCard = new GameCard { Name = "Lightning Bolt", ManaCost = ManaCost.Parse("{R}") };
        var targetSpell = new StackObject(targetCard, state.Player2.Id,
            new Dictionary<ManaColor, int> { [ManaColor.Red] = 1 },
            new List<TargetInfo>(), 0);
        state.StackPush(targetSpell);

        // Give opponent enough mana to pay (3 colorless)
        state.Player2.ManaPool.Add(ManaColor.Colorless, 3);

        var counterCard = new GameCard { Name = "Mana Leak" };
        var counterSpell = new StackObject(counterCard, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(targetCard.Id, state.Player2.Id, ZoneType.Stack) }, 0);

        // Act
        var effect = new ConditionalCounterEffect(3);
        effect.Resolve(state, counterSpell);

        // Assert - spell should NOT be countered, mana should be deducted
        state.Stack.OfType<StackObject>().Should().Contain(s => s.Card.Id == targetCard.Id);
        state.Player2.Graveyard.Cards.Should().NotContain(c => c.Id == targetCard.Id);
        state.Player2.ManaPool.Total.Should().Be(0);
        state.GameLog.Should().Contain(msg => msg.Contains("pays") && msg.Contains("3"));
    }

    [Fact]
    public void ConditionalCounter_Fizzles_WhenTargetSpellAlreadyGone()
    {
        // Arrange
        var state = CreateState();

        // Create a target card but do NOT put it on the stack
        var targetCard = new GameCard { Name = "Lightning Bolt", ManaCost = ManaCost.Parse("{R}") };

        var counterCard = new GameCard { Name = "Mana Leak" };
        var counterSpell = new StackObject(counterCard, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(targetCard.Id, state.Player2.Id, ZoneType.Stack) }, 0);

        // Act
        var effect = new ConditionalCounterEffect(3);
        effect.Resolve(state, counterSpell);

        // Assert - should fizzle
        state.GameLog.Should().Contain(msg => msg.Contains("fizzles"));
        state.Player2.Graveyard.Cards.Should().NotContain(c => c.Id == targetCard.Id);
    }

    [Fact]
    public void ConditionalCounter_CountersSpell_WhenOpponentHasInsufficientMana()
    {
        // Arrange
        var state = CreateState();

        var targetCard = new GameCard { Name = "Lightning Bolt", ManaCost = ManaCost.Parse("{R}") };
        var targetSpell = new StackObject(targetCard, state.Player2.Id,
            new Dictionary<ManaColor, int> { [ManaColor.Red] = 1 },
            new List<TargetInfo>(), 0);
        state.StackPush(targetSpell);

        // Give opponent only 2 mana (needs 3)
        state.Player2.ManaPool.Add(ManaColor.Colorless, 2);

        var counterCard = new GameCard { Name = "Mana Leak" };
        var counterSpell = new StackObject(counterCard, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(targetCard.Id, state.Player2.Id, ZoneType.Stack) }, 0);

        // Act
        var effect = new ConditionalCounterEffect(3);
        effect.Resolve(state, counterSpell);

        // Assert - spell should be countered (can't pay full 3)
        state.Stack.OfType<StackObject>().Should().NotContain(s => s.Card.Id == targetCard.Id);
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Id == targetCard.Id);
        state.GameLog.Should().Contain(msg => msg.Contains("countered") && msg.Contains("unable to pay"));
    }

    [Fact]
    public void ConditionalCounter_DeductsFromMixedManaPool()
    {
        // Arrange
        var state = CreateState();

        var targetCard = new GameCard { Name = "Lightning Bolt", ManaCost = ManaCost.Parse("{R}") };
        var targetSpell = new StackObject(targetCard, state.Player2.Id,
            new Dictionary<ManaColor, int> { [ManaColor.Red] = 1 },
            new List<TargetInfo>(), 0);
        state.StackPush(targetSpell);

        // Give opponent mixed mana totaling exactly 3 (1 Red + 2 Blue)
        state.Player2.ManaPool.Add(ManaColor.Red, 1);
        state.Player2.ManaPool.Add(ManaColor.Blue, 2);

        var counterCard = new GameCard { Name = "Mana Leak" };
        var counterSpell = new StackObject(counterCard, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(targetCard.Id, state.Player2.Id, ZoneType.Stack) }, 0);

        // Act
        var effect = new ConditionalCounterEffect(3);
        effect.Resolve(state, counterSpell);

        // Assert - spell resolves (not countered), all mana deducted
        state.Stack.OfType<StackObject>().Should().Contain(s => s.Card.Id == targetCard.Id);
        state.Player2.Graveyard.Cards.Should().NotContain(c => c.Id == targetCard.Id);
        state.Player2.ManaPool.Total.Should().Be(0);
        state.Player2.ManaPool[ManaColor.Red].Should().Be(0);
        state.Player2.ManaPool[ManaColor.Blue].Should().Be(0);
        state.GameLog.Should().Contain(msg => msg.Contains("pays") && msg.Contains("3"));
    }
}
