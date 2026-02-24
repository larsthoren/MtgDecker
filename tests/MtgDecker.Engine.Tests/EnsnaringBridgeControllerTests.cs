using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class EnsnaringBridgeControllerTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler p1Handler, TestDecisionHandler p2Handler) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);

        for (int i = 0; i < 40; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}", TypeLine = "Creature" });
            p2.Library.Add(new GameCard { Name = $"Card{i}", TypeLine = "Creature" });
        }

        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, h1, h2);
    }

    private static void ReduceHandTo(Player player, int targetCount)
    {
        while (player.Hand.Cards.Count > targetCount)
        {
            var card = player.Hand.Cards[0];
            player.Hand.RemoveById(card.Id);
        }
    }

    [Fact]
    public async Task DefenderControlsBridge_With1Card_AttackerPower3_CannotAttack()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        // Attacker (P1) has a 3/3 creature
        var creature = new GameCard { Name = "Bear", TypeLine = "Creature", Power = 3, Toughness = 3, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(creature);

        // Defender (P2) controls Ensnaring Bridge with 1 card in hand
        var bridge = GameCard.Create("Ensnaring Bridge", "Artifact");
        state.Player2.Battlefield.Add(bridge);
        ReduceHandTo(state.Player2, 1);

        p1Handler.EnqueueAttackers(new List<Guid> { creature.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid>());

        await engine.RunCombatAsync(CancellationToken.None);

        // 3/3 creature should NOT be able to attack because defender's bridge controller hand = 1
        state.Player2.Life.Should().Be(20, "creature power (3) > bridge controller hand size (1), attack should be prevented");
    }

    [Fact]
    public async Task AttackerControlsBridge_AttackerHas1Card_Power1Creature_CanAttack()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        // Attacker (P1) controls Bridge with 1 card in hand
        var bridge = GameCard.Create("Ensnaring Bridge", "Artifact");
        state.Player1.Battlefield.Add(bridge);
        ReduceHandTo(state.Player1, 1);

        // Attacker has a 1/1 creature (power <= P1's hand of 1)
        var smallCreature = new GameCard { Name = "Elf", TypeLine = "Creature", Power = 1, Toughness = 1, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(smallCreature);

        p1Handler.EnqueueAttackers(new List<Guid> { smallCreature.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid>());

        await engine.RunCombatAsync(CancellationToken.None);

        // 1/1 can attack since power (1) <= P1's hand size (1)
        state.Player2.Life.Should().Be(19, "1/1 creature can attack — power <= bridge controller hand size");
    }

    [Fact]
    public async Task AttackerControlsBridge_AttackerHas1Card_Power2Creature_CannotAttack()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        // Attacker (P1) controls Bridge with 1 card in hand
        var bridge = GameCard.Create("Ensnaring Bridge", "Artifact");
        state.Player1.Battlefield.Add(bridge);
        ReduceHandTo(state.Player1, 1);

        // Attacker has a 2/2 creature (power > P1's hand of 1)
        var creature = new GameCard { Name = "Bear", TypeLine = "Creature", Power = 2, Toughness = 2, TurnEnteredBattlefield = 0 };
        state.Player1.Battlefield.Add(creature);

        p1Handler.EnqueueAttackers(new List<Guid> { creature.Id });

        await engine.RunCombatAsync(CancellationToken.None);

        // 2/2 should NOT attack because P1 controls bridge with hand=1
        state.Player2.Life.Should().Be(20);
    }
}
