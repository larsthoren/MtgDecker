using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class AnnihilatorTests
{
    [Fact]
    public async Task Annihilator_DefenderSacrificesNPermanents()
    {
        var (state, h1, h2) = TestHelper.CreateStateWithHandlers();

        var land1 = new GameCard { Name = "Island", CardTypes = CardType.Land };
        var land2 = new GameCard { Name = "Mountain", CardTypes = CardType.Land };
        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature, Power = 2, Toughness = 2 };
        state.Player2.Battlefield.Add(land1);
        state.Player2.Battlefield.Add(land2);
        state.Player2.Battlefield.Add(creature);

        h2.EnqueueCardChoice(land1.Id);
        h2.EnqueueCardChoice(land2.Id);
        h2.EnqueueCardChoice(creature.Id);

        var source = new GameCard { Name = "Emrakul", CardTypes = CardType.Creature };
        var ctx = new EffectContext(state, state.Player1, source, h1)
        {
            FireLeaveBattlefieldTriggers = _ => Task.CompletedTask,
        };

        await new AnnihilatorEffect(3).Execute(ctx);

        state.Player2.Battlefield.Cards.Should().BeEmpty();
        state.Player2.Graveyard.Cards.Should().HaveCount(3);
    }

    [Fact]
    public async Task Annihilator_FewerPermanentsThanN_SacrificesAll()
    {
        var (state, h1, h2) = TestHelper.CreateStateWithHandlers();

        var land = new GameCard { Name = "Island", CardTypes = CardType.Land };
        state.Player2.Battlefield.Add(land);

        h2.EnqueueCardChoice(land.Id);

        var source = new GameCard { Name = "Emrakul", CardTypes = CardType.Creature };
        var ctx = new EffectContext(state, state.Player1, source, h1)
        {
            FireLeaveBattlefieldTriggers = _ => Task.CompletedTask,
        };

        await new AnnihilatorEffect(6).Execute(ctx);

        state.Player2.Battlefield.Cards.Should().BeEmpty();
        state.Player2.Graveyard.Cards.Should().HaveCount(1);
    }

    [Fact]
    public async Task Annihilator_NoPermanents_DoesNothing()
    {
        var (state, h1, h2) = TestHelper.CreateStateWithHandlers();

        var source = new GameCard { Name = "Emrakul", CardTypes = CardType.Creature };
        var ctx = new EffectContext(state, state.Player1, source, h1)
        {
            FireLeaveBattlefieldTriggers = _ => Task.CompletedTask,
        };

        await new AnnihilatorEffect(6).Execute(ctx);

        state.Player2.Battlefield.Cards.Should().BeEmpty();
        state.Player2.Graveyard.Cards.Should().BeEmpty();
    }
}
