using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests.Triggers;

public class CreateTokensEffectTests
{
    private (GameState state, Player player, TestDecisionHandler handler) CreateSetup()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Player 1", handler);
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        return (state, p1, handler);
    }

    [Fact]
    public async Task Execute_CreatesTokensOnBattlefield()
    {
        var (state, player, handler) = CreateSetup();
        var source = new GameCard { Name = "Siege-Gang Commander" };
        var effect = new CreateTokensEffect("Goblin", 1, 1, CardType.Creature, ["Goblin"], count: 3);
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        player.Battlefield.Count.Should().Be(3);
        player.Battlefield.Cards.Should().AllSatisfy(c =>
        {
            c.Name.Should().Be("Goblin");
            c.Power.Should().Be(1);
            c.Toughness.Should().Be(1);
            c.IsToken.Should().BeTrue();
            c.IsCreature.Should().BeTrue();
            c.Subtypes.Should().Contain("Goblin");
        });
    }

    [Fact]
    public async Task Execute_TokensHaveSummoningSickness()
    {
        var (state, player, handler) = CreateSetup();
        state.TurnNumber = 3;
        var source = new GameCard { Name = "Commander" };
        var effect = new CreateTokensEffect("Goblin", 1, 1, CardType.Creature, ["Goblin"], count: 1);
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        var token = player.Battlefield.Cards[0];
        token.TurnEnteredBattlefield.Should().Be(3);
        token.HasSummoningSickness(3).Should().BeTrue();
    }

    [Fact]
    public async Task Execute_TokensHaveUniqueIds()
    {
        var (state, player, handler) = CreateSetup();
        var source = new GameCard { Name = "Commander" };
        var effect = new CreateTokensEffect("Goblin", 1, 1, CardType.Creature, ["Goblin"], count: 3);
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        var ids = player.Battlefield.Cards.Select(c => c.Id).ToList();
        ids.Should().OnlyHaveUniqueItems();
    }
}
