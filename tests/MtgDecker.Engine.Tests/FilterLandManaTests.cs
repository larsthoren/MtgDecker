using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class FilterLandManaTests
{
    private GameEngine CreateEngine(out GameState state, out Player player1, out Player player2)
    {
        var p1Handler = new TestDecisionHandler();
        var p2Handler = new TestDecisionHandler();
        player1 = new Player(Guid.NewGuid(), "Alice", p1Handler);
        player2 = new Player(Guid.NewGuid(), "Bob", p2Handler);

        // Fill libraries so StartGame doesn't fail
        for (int i = 0; i < 40; i++)
        {
            player1.Library.Add(new GameCard { Name = $"Card{i}" });
            player2.Library.Add(new GameCard { Name = $"Card{i}" });
        }

        state = new GameState(player1, player2);
        return new GameEngine(state);
    }

    [Fact]
    public async Task FilterLand_WithSufficientMana_ProducesBothColors()
    {
        // Arrange: Skycloud Expanse on battlefield, 1 colorless mana in pool
        var engine = CreateEngine(out var state, out var p1, out _);
        await engine.StartGameAsync();

        var skycloud = GameCard.Create("Skycloud Expanse", "Land");
        p1.Battlefield.Add(skycloud);
        p1.ManaPool.Add(ManaColor.Colorless); // 1 generic mana to pay activation cost {1}

        // Act: tap the filter land
        await engine.ExecuteAction(GameAction.TapCard(p1.Id, skycloud.Id));

        // Assert: activation cost {1} consumed the colorless, produced W and U
        skycloud.IsTapped.Should().BeTrue();
        p1.ManaPool[ManaColor.White].Should().Be(1, "filter land should produce White");
        p1.ManaPool[ManaColor.Blue].Should().Be(1, "filter land should produce Blue");
        p1.ManaPool[ManaColor.Colorless].Should().Be(0, "activation cost should consume the colorless mana");
        p1.ManaPool.Total.Should().Be(2, "net result should be 2 mana (W + U) after paying {1}");
    }

    [Fact]
    public async Task FilterLand_WithInsufficientMana_RejectsTap()
    {
        // Arrange: Skycloud Expanse on battlefield, NO mana in pool
        var engine = CreateEngine(out var state, out var p1, out _);
        await engine.StartGameAsync();

        var skycloud = GameCard.Create("Skycloud Expanse", "Land");
        p1.Battlefield.Add(skycloud);
        // No mana in pool — can't pay {1} activation cost

        // Act: attempt to tap the filter land
        await engine.ExecuteAction(GameAction.TapCard(p1.Id, skycloud.Id));

        // Assert: tap should be rejected — land stays untapped, pool empty
        skycloud.IsTapped.Should().BeFalse("filter land should not tap if activation cost can't be paid");
        p1.ManaPool.Total.Should().Be(0, "no mana should be produced");
        state.GameLog.Should().Contain(l => l.Contains("cannot pay"), "should log the rejection");
    }

    [Fact]
    public async Task FilterLand_NetManaGain_IsCorrect()
    {
        // Arrange: Skycloud Expanse on battlefield, 2 Red mana in pool
        var engine = CreateEngine(out var state, out var p1, out _);
        await engine.StartGameAsync();

        var skycloud = GameCard.Create("Skycloud Expanse", "Land");
        p1.Battlefield.Add(skycloud);
        p1.ManaPool.Add(ManaColor.Red, 2); // 2R in pool, {1} activation cost will consume 1R

        // Act: tap the filter land
        await engine.ExecuteAction(GameAction.TapCard(p1.Id, skycloud.Id));

        // Assert: 1R spent on activation, W+U produced → R:1, W:1, U:1 (3 total)
        skycloud.IsTapped.Should().BeTrue();
        p1.ManaPool[ManaColor.Red].Should().Be(1, "1 Red should remain after paying {1}");
        p1.ManaPool[ManaColor.White].Should().Be(1, "White should be produced");
        p1.ManaPool[ManaColor.Blue].Should().Be(1, "Blue should be produced");
        p1.ManaPool.Total.Should().Be(3, "net gain of 1 mana: started with 2R, spent 1 for activation, gained W+U");
    }

    [Fact]
    public async Task FilterLand_Undo_RestoresOriginalState()
    {
        // Arrange: Skycloud Expanse on battlefield, 1 Green mana in pool
        var engine = CreateEngine(out var state, out var p1, out _);
        await engine.StartGameAsync();

        var skycloud = GameCard.Create("Skycloud Expanse", "Land");
        p1.Battlefield.Add(skycloud);
        p1.ManaPool.Add(ManaColor.Green); // 1G for activation cost

        // Act: tap the filter land (spends G, gains W+U)
        await engine.ExecuteAction(GameAction.TapCard(p1.Id, skycloud.Id));

        // Verify the tap worked
        skycloud.IsTapped.Should().BeTrue();
        p1.ManaPool[ManaColor.White].Should().Be(1);
        p1.ManaPool[ManaColor.Blue].Should().Be(1);
        p1.ManaPool[ManaColor.Green].Should().Be(0);

        // Act: undo the tap
        var undoResult = engine.UndoLastAction(p1.Id);

        // Assert: everything restored to pre-tap state
        undoResult.Should().BeTrue("undo should succeed for filter land tap");
        skycloud.IsTapped.Should().BeFalse("land should be untapped after undo");
        p1.ManaPool[ManaColor.Green].Should().Be(1, "activation cost mana should be restored");
        p1.ManaPool[ManaColor.White].Should().Be(0, "produced White should be removed");
        p1.ManaPool[ManaColor.Blue].Should().Be(0, "produced Blue should be removed");
        p1.ManaPool.Total.Should().Be(1, "pool should be back to original 1G");
    }
}
