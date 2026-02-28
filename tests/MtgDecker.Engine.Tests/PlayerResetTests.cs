using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class PlayerResetTests
{
    [Fact]
    public void ResetTurnState_ClearsAllPerTurnTracking()
    {
        var player = new Player(Guid.NewGuid(), "Test", new TestDecisionHandler());
        player.CreaturesDiedThisTurn = 3;
        player.DrawsThisTurn = 2;
        player.DrawStepDrawExempted = true;
        player.LifeLostThisTurn = 5;
        player.PermanentLeftBattlefieldThisTurn = true;
        player.PlaneswalkerAbilitiesUsedThisTurn.Add(Guid.NewGuid());

        player.ResetTurnState();

        player.CreaturesDiedThisTurn.Should().Be(0);
        player.DrawsThisTurn.Should().Be(0);
        player.DrawStepDrawExempted.Should().BeFalse();
        player.LifeLostThisTurn.Should().Be(0);
        player.PermanentLeftBattlefieldThisTurn.Should().BeFalse();
        player.PlaneswalkerAbilitiesUsedThisTurn.Should().BeEmpty();
    }

    [Fact]
    public void ResetTurnState_ClearsBattlefieldPerTurnFlags()
    {
        var player = new Player(Guid.NewGuid(), "Test", new TestDecisionHandler());
        var card = new GameCard { Name = "Test" };
        card.CarpetUsedThisTurn = true;
        card.AbilitiesActivatedThisTurn.Add(1);
        player.Battlefield.Add(card);

        player.ResetTurnState();

        card.CarpetUsedThisTurn.Should().BeFalse();
        card.AbilitiesActivatedThisTurn.Should().BeEmpty();
    }
}
