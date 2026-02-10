using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class PlayerManaTests
{
    private Player CreatePlayer() =>
        new(Guid.NewGuid(), "Alice", new TestDecisionHandler());

    [Fact]
    public void NewPlayer_HasEmptyManaPool()
    {
        var player = CreatePlayer();

        player.ManaPool.Total.Should().Be(0);
    }

    [Fact]
    public void NewPlayer_LandsPlayedThisTurn_IsZero()
    {
        var player = CreatePlayer();

        player.LandsPlayedThisTurn.Should().Be(0);
    }

    [Fact]
    public void ManaPool_CanAddAndTrackMana()
    {
        var player = CreatePlayer();

        player.ManaPool.Add(ManaColor.Green, 1);
        player.ManaPool.Add(ManaColor.Red, 2);

        player.ManaPool[ManaColor.Green].Should().Be(1);
        player.ManaPool[ManaColor.Red].Should().Be(2);
        player.ManaPool.Total.Should().Be(3);
    }

    [Fact]
    public void LandsPlayedThisTurn_CanBeSetAndReset()
    {
        var player = CreatePlayer();

        player.LandsPlayedThisTurn = 1;
        player.LandsPlayedThisTurn.Should().Be(1);

        player.LandsPlayedThisTurn = 0;
        player.LandsPlayedThisTurn.Should().Be(0);
    }
}
