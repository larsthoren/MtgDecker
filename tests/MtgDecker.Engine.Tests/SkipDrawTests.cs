using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class SkipDrawTests
{
    private static (GameEngine engine, GameState state, Player p1, Player p2) Setup()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, p1, p2);
    }

    [Fact]
    public void SkipDraw_Prevents_Draw_Step()
    {
        var (engine, state, p1, _) = Setup();

        // Put a card in library so there's something to draw
        p1.Library.Add(new GameCard { Name = "Forest", CardTypes = CardType.Land });

        // Add SkipDraw effect with source on P1's battlefield
        var enchantment = new GameCard { Name = "Solitary Confinement", CardTypes = CardType.Enchantment };
        p1.Battlefield.Add(enchantment);
        state.ActiveEffects.Add(new ContinuousEffect(
            enchantment.Id, ContinuousEffectType.SkipDraw, (_, _) => true));

        state.ActivePlayer = p1;
        var initialHandCount = p1.Hand.Count;

        engine.ExecuteTurnBasedAction(Phase.Draw);

        // No card drawn
        p1.Hand.Count.Should().Be(initialHandCount);
        state.GameLog.Should().Contain(l => l.Contains("draw is skipped"));
    }

    [Fact]
    public void SkipDraw_Only_Affects_Controller()
    {
        var (engine, state, p1, p2) = Setup();

        // P1 has SkipDraw source on their battlefield
        var enchantment = new GameCard { Name = "Solitary Confinement", CardTypes = CardType.Enchantment };
        p1.Battlefield.Add(enchantment);
        state.ActiveEffects.Add(new ContinuousEffect(
            enchantment.Id, ContinuousEffectType.SkipDraw, (_, _) => true));

        // P2 is active player — should draw normally
        state.ActivePlayer = p2;
        p2.Library.Add(new GameCard { Name = "Mountain", CardTypes = CardType.Land });
        var initialHandCount = p2.Hand.Count;

        engine.ExecuteTurnBasedAction(Phase.Draw);

        // P2 should have drawn a card
        p2.Hand.Count.Should().Be(initialHandCount + 1);
        state.GameLog.Should().Contain(l => l.Contains("draws a card"));
    }

    [Fact]
    public void Normal_Draw_Works_Without_SkipDraw()
    {
        var (engine, state, p1, _) = Setup();

        p1.Library.Add(new GameCard { Name = "Forest", CardTypes = CardType.Land });
        state.ActivePlayer = p1;
        var initialHandCount = p1.Hand.Count;

        engine.ExecuteTurnBasedAction(Phase.Draw);

        p1.Hand.Count.Should().Be(initialHandCount + 1);
        state.GameLog.Should().Contain(l => l.Contains("draws a card"));
    }
}
