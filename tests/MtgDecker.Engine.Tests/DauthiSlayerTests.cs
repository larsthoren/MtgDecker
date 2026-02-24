using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class DauthiSlayerTests
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

    [Fact]
    public void DauthiSlayer_IsRegistered_WithCorrectStats()
    {
        CardDefinitions.TryGet("Dauthi Slayer", out var def).Should().BeTrue();
        def!.ManaCost!.ConvertedManaCost.Should().Be(2);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Black).WhoseValue.Should().Be(2);
        def.Power.Should().Be(2);
        def.Toughness.Should().Be(2);
        def.CardTypes.Should().Be(CardType.Creature);
        def.Subtypes.Should().BeEquivalentTo(new[] { "Dauthi", "Soldier" });
    }

    [Fact]
    public void DauthiSlayer_HasShadow()
    {
        var card = GameCard.Create("Dauthi Slayer", "Creature — Dauthi Soldier");
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", h1),
            new Player(Guid.NewGuid(), "P2", h2));
        state.Player1.Battlefield.Add(card);
        var engine = new GameEngine(state);
        engine.RecalculateState();

        card.ActiveKeywords.Should().Contain(Keyword.Shadow);
    }

    [Fact]
    public void DauthiSlayer_HasMustAttack()
    {
        CardDefinitions.TryGet("Dauthi Slayer", out var def).Should().BeTrue();
        def!.MustAttack.Should().BeTrue();
    }

    [Fact]
    public async Task MustAttack_ForcesCreatureIntoAttack_EvenWhenPlayerChoosesNone()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var slayer = GameCard.Create("Dauthi Slayer", "Creature — Dauthi Soldier");
        slayer.TurnEnteredBattlefield = 0; // No summoning sickness
        state.Player1.Battlefield.Add(slayer);
        engine.RecalculateState();

        // Player tries to declare no attackers
        p1Handler.EnqueueAttackers(Array.Empty<Guid>());
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid>());

        await engine.RunCombatAsync(CancellationToken.None);

        // Dauthi Slayer should have been forced to attack
        slayer.IsTapped.Should().BeTrue("Dauthi Slayer must attack each combat if able");
        state.Player2.Life.Should().Be(18, "Dauthi Slayer has 2 power and shadow (unblockable by non-shadow)");
    }
}
