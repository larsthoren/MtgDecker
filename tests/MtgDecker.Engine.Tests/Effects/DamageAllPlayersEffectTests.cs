using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests.Effects;

public class DamageAllPlayersEffectTests
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

    private static StackObject CreateSpell(string name, Guid controllerId)
    {
        var card = GameCard.Create(name);
        return new StackObject(card, controllerId,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);
    }

    [Fact]
    public void Resolve_DealsDamageToAllPlayers()
    {
        var (state, p1, p2) = CreateGameState();
        var effect = new DamageAllPlayersEffect(4);
        var spell = CreateSpell("Flame Rift", p1.Id);

        effect.Resolve(state, spell);

        p1.Life.Should().Be(16);
        p2.Life.Should().Be(16);
    }

    [Fact]
    public void Resolve_CanKillBothPlayers()
    {
        var (state, p1, p2) = CreateGameState();
        p1.AdjustLife(-17); // at 3 life
        p2.AdjustLife(-18); // at 2 life

        var effect = new DamageAllPlayersEffect(4);
        var spell = CreateSpell("Flame Rift", p1.Id);

        effect.Resolve(state, spell);

        p1.Life.Should().Be(-1);
        p2.Life.Should().Be(-2);
    }

    [Fact]
    public void Resolve_LogsCorrectMessage()
    {
        var (state, p1, p2) = CreateGameState();
        var effect = new DamageAllPlayersEffect(4);
        var spell = CreateSpell("Flame Rift", p1.Id);

        effect.Resolve(state, spell);

        state.GameLog.Should().ContainSingle()
            .Which.Should().Contain("Flame Rift").And.Contain("4 damage to each player");
    }
}
