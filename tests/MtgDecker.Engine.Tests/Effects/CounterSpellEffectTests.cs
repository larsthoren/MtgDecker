using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests.Effects;

public class CounterSpellEffectTests
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

    private static StackObject CreateSpell(string name, Guid controllerId, List<TargetInfo> targets)
    {
        var card = GameCard.Create(name);
        return new StackObject(card, controllerId,
            new Dictionary<ManaColor, int>(), targets, 0);
    }

    [Fact]
    public void CounterSpellEffect_RemovesTargetFromStack()
    {
        // Arrange: creature spell on the stack, counterspell targeting it
        var (state, p1, p2) = CreateGameState();
        var creatureCard = GameCard.Create("Grizzly Bears", "Creature - Bear");
        var creatureSpell = new StackObject(creatureCard, p2.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);
        state.StackPush(creatureSpell);

        var target = new TargetInfo(creatureCard.Id, p2.Id, ZoneType.Stack);
        var counterSpell = CreateSpell("Counterspell", p1.Id, new List<TargetInfo> { target });

        var effect = new CounterSpellEffect();

        // Act
        effect.Resolve(state, counterSpell);

        // Assert: creature spell removed from stack
        state.Stack.OfType<StackObject>().Should().NotContain(s => s.Card.Id == creatureCard.Id);
    }

    [Fact]
    public void CounterSpellEffect_PutsCounteredCardInOwnersGraveyard()
    {
        // Arrange
        var (state, p1, p2) = CreateGameState();
        var creatureCard = GameCard.Create("Grizzly Bears", "Creature - Bear");
        var creatureSpell = new StackObject(creatureCard, p2.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);
        state.StackPush(creatureSpell);

        var target = new TargetInfo(creatureCard.Id, p2.Id, ZoneType.Stack);
        var counterSpell = CreateSpell("Counterspell", p1.Id, new List<TargetInfo> { target });

        var effect = new CounterSpellEffect();

        // Act
        effect.Resolve(state, counterSpell);

        // Assert: creature card is in owner's graveyard
        p2.Graveyard.Cards.Should().Contain(c => c.Id == creatureCard.Id);
    }

    [Fact]
    public void CounterSpellEffect_Fizzles_WhenTargetAlreadyResolved()
    {
        // Arrange: target spell not on stack anymore (already resolved)
        var (state, p1, p2) = CreateGameState();
        var creatureCard = GameCard.Create("Grizzly Bears", "Creature - Bear");
        // Note: creature spell is NOT on the stack
        var target = new TargetInfo(creatureCard.Id, p2.Id, ZoneType.Stack);
        var counterSpell = CreateSpell("Counterspell", p1.Id, new List<TargetInfo> { target });

        var effect = new CounterSpellEffect();

        // Act - should not throw
        var act = () => effect.Resolve(state, counterSpell);

        // Assert
        act.Should().NotThrow();
        p2.Graveyard.Cards.Should().BeEmpty();
    }

    [Fact]
    public void CounterSpellEffect_Fizzles_LogsFizzleMessage()
    {
        // Arrange
        var (state, p1, p2) = CreateGameState();
        var creatureCard = GameCard.Create("Grizzly Bears", "Creature - Bear");
        var target = new TargetInfo(creatureCard.Id, p2.Id, ZoneType.Stack);
        var counterSpell = CreateSpell("Counterspell", p1.Id, new List<TargetInfo> { target });

        var effect = new CounterSpellEffect();

        // Act
        effect.Resolve(state, counterSpell);

        // Assert: log mentions fizzle
        state.GameLog.Should().ContainSingle()
            .Which.Should().Contain("Counterspell")
            .And.Contain("fizzle");
    }

    [Fact]
    public void CounterSpellEffect_LogsCounteredSpellName()
    {
        // Arrange
        var (state, p1, p2) = CreateGameState();
        var creatureCard = GameCard.Create("Grizzly Bears", "Creature - Bear");
        var creatureSpell = new StackObject(creatureCard, p2.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);
        state.StackPush(creatureSpell);

        var target = new TargetInfo(creatureCard.Id, p2.Id, ZoneType.Stack);
        var counterSpell = CreateSpell("Counterspell", p1.Id, new List<TargetInfo> { target });

        var effect = new CounterSpellEffect();

        // Act
        effect.Resolve(state, counterSpell);

        // Assert: log mentions both the counter and the countered spell
        state.GameLog.Should().ContainSingle()
            .Which.Should().Contain("Grizzly Bears")
            .And.Contain("countered")
            .And.Contain("Counterspell");
    }

    [Fact]
    public void CounterSpellEffect_NoTargets_DoesNothing()
    {
        // Arrange
        var (state, p1, p2) = CreateGameState();
        var counterSpell = CreateSpell("Counterspell", p1.Id, new List<TargetInfo>());

        var effect = new CounterSpellEffect();

        // Act
        effect.Resolve(state, counterSpell);

        // Assert
        state.GameLog.Should().BeEmpty();
        state.Stack.Should().BeEmpty();
    }

    [Fact]
    public void CounterSpellEffect_CountersPlayer1Spell_PutsInPlayer1Graveyard()
    {
        // Arrange: P1 casts a spell, P2 counters it - card goes to P1's graveyard
        var (state, p1, p2) = CreateGameState();
        var sorceryCard = GameCard.Create("Divination", "Sorcery");
        var sorcerySpell = new StackObject(sorceryCard, p1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);
        state.StackPush(sorcerySpell);

        var target = new TargetInfo(sorceryCard.Id, p1.Id, ZoneType.Stack);
        var counterSpell = CreateSpell("Cancel", p2.Id, new List<TargetInfo> { target });

        var effect = new CounterSpellEffect();

        // Act
        effect.Resolve(state, counterSpell);

        // Assert: card goes to the controller's (P1's) graveyard
        p1.Graveyard.Cards.Should().Contain(c => c.Id == sorceryCard.Id);
        p2.Graveyard.Cards.Should().BeEmpty();
    }
}
