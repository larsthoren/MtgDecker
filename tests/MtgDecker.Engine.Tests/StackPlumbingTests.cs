using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class StackPlumbingTests
{
    [Fact]
    public void ActionType_HasCastSpell()
    {
        Enum.IsDefined(typeof(ActionType), "CastSpell").Should().BeTrue();
    }

    [Fact]
    public void GameAction_CastSpell_Factory()
    {
        var playerId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var action = GameAction.CastSpell(playerId, cardId);

        action.Type.Should().Be(ActionType.CastSpell);
        action.PlayerId.Should().Be(playerId);
        action.CardId.Should().Be(cardId);
    }

    [Fact]
    public void GameState_HasEmptyStack()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        state.Stack.Should().BeEmpty();
    }

    [Fact]
    public void CardDefinition_SwordsHasTargetFilter()
    {
        CardDefinitions.TryGet("Swords to Plowshares", out var def).Should().BeTrue();
        def!.TargetFilter.Should().NotBeNull();
    }

    [Fact]
    public void CardDefinition_SwordsHasSpellEffect()
    {
        CardDefinitions.TryGet("Swords to Plowshares", out var def).Should().BeTrue();
        def!.Effect.Should().NotBeNull();
    }

    [Fact]
    public void CardDefinition_NaturalizeHasTargetFilter()
    {
        CardDefinitions.TryGet("Naturalize", out var def).Should().BeTrue();
        def!.TargetFilter.Should().NotBeNull();
    }

    [Fact]
    public void CardDefinition_MoggFanatic_NoTargetFilter()
    {
        CardDefinitions.TryGet("Mogg Fanatic", out var def).Should().BeTrue();
        def!.TargetFilter.Should().BeNull();
        def.Effect.Should().BeNull();
    }
}
