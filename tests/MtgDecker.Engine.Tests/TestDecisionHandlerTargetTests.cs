using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class TestDecisionHandlerTargetTests
{
    [Fact]
    public async Task ChooseTarget_DequeuesEnqueued()
    {
        var handler = new TestDecisionHandler();
        var target = new TargetInfo(Guid.NewGuid(), Guid.NewGuid(), ZoneType.Battlefield);
        handler.EnqueueTarget(target);

        var eligible = new List<GameCard> { GameCard.Create("Mogg Fanatic", "Creature — Goblin") };
        var result = await handler.ChooseTarget("Swords to Plowshares", eligible);

        result.Should().Be(target);
    }

    [Fact]
    public async Task ChooseTarget_DefaultsToFirstEligible()
    {
        var handler = new TestDecisionHandler();
        var card = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        var playerId = Guid.NewGuid();

        var eligible = new List<GameCard> { card };
        var result = await handler.ChooseTarget("Swords to Plowshares", eligible, playerId);

        result.Should().NotBeNull();
        result!.CardId.Should().Be(card.Id);
    }
}
