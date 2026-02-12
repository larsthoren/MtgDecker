using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class LeaveBattlefieldTests
{
    [Fact]
    public async Task ReturnExiledCardsEffect_ReturnsAllTrackedCards()
    {
        var handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", handler),
            new Player(Guid.NewGuid(), "P2", handler));

        var source = new GameCard { Name = "Parallax Wave" };
        var creature1 = new GameCard { Name = "Bear 1", CardTypes = CardType.Creature };
        var creature2 = new GameCard { Name = "Bear 2", CardTypes = CardType.Creature };

        // Simulate exile: creatures are in exile and tracked by source
        state.Player1.Exile.Add(creature1);
        state.Player1.Exile.Add(creature2);
        source.ExiledCardIds.Add(creature1.Id);
        source.ExiledCardIds.Add(creature2.Id);

        var effect = new ReturnExiledCardsEffect();
        var context = new EffectContext(state, state.Player1, source, handler);

        await effect.Execute(context);

        state.Player1.Battlefield.Contains(creature1.Id).Should().BeTrue();
        state.Player1.Battlefield.Contains(creature2.Id).Should().BeTrue();
        state.Player1.Exile.Contains(creature1.Id).Should().BeFalse();
        state.Player1.Exile.Contains(creature2.Id).Should().BeFalse();
        source.ExiledCardIds.Should().BeEmpty();
    }

    [Fact]
    public async Task ReturnExiledCardsEffect_ReturnsToOwnersBattlefield()
    {
        var handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", handler),
            new Player(Guid.NewGuid(), "P2", handler));

        var source = new GameCard { Name = "Parallax Wave" };
        var p1Creature = new GameCard { Name = "P1 Bear", CardTypes = CardType.Creature };
        var p2Creature = new GameCard { Name = "P2 Bear", CardTypes = CardType.Creature };

        // Exile from different players
        state.Player1.Exile.Add(p1Creature);
        state.Player2.Exile.Add(p2Creature);
        source.ExiledCardIds.Add(p1Creature.Id);
        source.ExiledCardIds.Add(p2Creature.Id);

        var effect = new ReturnExiledCardsEffect();
        var context = new EffectContext(state, state.Player1, source, handler);

        await effect.Execute(context);

        state.Player1.Battlefield.Contains(p1Creature.Id).Should().BeTrue();
        state.Player2.Battlefield.Contains(p2Creature.Id).Should().BeTrue();
    }

    [Fact]
    public async Task ReturnExiledCardsEffect_HandlesEmptyExiledCardIds()
    {
        var handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", handler),
            new Player(Guid.NewGuid(), "P2", handler));

        var source = new GameCard { Name = "Parallax Wave" };

        var effect = new ReturnExiledCardsEffect();
        var context = new EffectContext(state, state.Player1, source, handler);

        // Should not throw
        await effect.Execute(context);

        source.ExiledCardIds.Should().BeEmpty();
    }

    [Fact]
    public void SelfLeavesBattlefield_TriggerConditionExists()
    {
        var condition = TriggerCondition.SelfLeavesBattlefield;
        condition.Should().BeDefined();
    }
}
