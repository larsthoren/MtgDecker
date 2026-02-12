using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class ExileTrackingTests
{
    [Fact]
    public async Task ExileCreatureEffect_MovesTargetToExile()
    {
        var handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", handler),
            new Player(Guid.NewGuid(), "P2", handler));

        var source = new GameCard { Name = "Parallax Wave" };
        var creature = new GameCard { Name = "Grizzly Bears", CardTypes = CardType.Creature };
        state.Player1.Battlefield.Add(source);
        state.Player1.Battlefield.Add(creature);

        var effect = new ExileCreatureEffect();
        var context = new EffectContext(state, state.Player1, source, handler)
        {
            Target = creature,
        };

        await effect.Execute(context);

        state.Player1.Battlefield.Contains(creature.Id).Should().BeFalse();
        state.Player1.Exile.Contains(creature.Id).Should().BeTrue();
        source.ExiledCardIds.Should().Contain(creature.Id);
    }

    [Fact]
    public async Task ExileCreatureEffect_TracksMultipleExiles()
    {
        var handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", handler),
            new Player(Guid.NewGuid(), "P2", handler));

        var source = new GameCard { Name = "Parallax Wave" };
        var creature1 = new GameCard { Name = "Bear 1", CardTypes = CardType.Creature };
        var creature2 = new GameCard { Name = "Bear 2", CardTypes = CardType.Creature };
        state.Player1.Battlefield.Add(source);
        state.Player1.Battlefield.Add(creature1);
        state.Player2.Battlefield.Add(creature2);

        var effect = new ExileCreatureEffect();

        await effect.Execute(new EffectContext(state, state.Player1, source, handler)
        {
            Target = creature1,
        });
        await effect.Execute(new EffectContext(state, state.Player1, source, handler)
        {
            Target = creature2,
        });

        source.ExiledCardIds.Should().HaveCount(2);
        source.ExiledCardIds.Should().Contain(creature1.Id);
        source.ExiledCardIds.Should().Contain(creature2.Id);
        state.Player1.Exile.Contains(creature1.Id).Should().BeTrue();
        state.Player2.Exile.Contains(creature2.Id).Should().BeTrue();
    }

    [Fact]
    public void ExiledCardIds_StartsEmpty()
    {
        var card = new GameCard { Name = "Test" };
        card.ExiledCardIds.Should().BeEmpty();
    }
}
