using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class UntilEndOfTurnTests
{
    [Fact]
    public void StripEndOfTurnEffects_Removes_Temporary_Effects()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var goblin = new GameCard
        {
            Name = "Goblin", BasePower = 1, BaseToughness = 1,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };
        p1.Battlefield.Add(goblin);

        // Add a temporary buff
        state.ActiveEffects.Add(new ContinuousEffect(
            Guid.NewGuid(), ContinuousEffectType.ModifyPowerToughness,
            (c, _) => c.IsCreature && c.Subtypes.Contains("Goblin"),
            PowerMod: 3, UntilEndOfTurn: true));

        engine.RecalculateState();
        goblin.Power.Should().Be(4); // 1 + 3

        // Simulate end of turn
        engine.StripEndOfTurnEffects();
        engine.RecalculateState();

        goblin.Power.Should().Be(1); // back to base
    }

    [Fact]
    public void StripEndOfTurnEffects_Keeps_Permanent_Effects()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var king = GameCard.Create("Goblin King", "Creature — Goblin");
        var goblin = new GameCard
        {
            Name = "Goblin", BasePower = 1, BaseToughness = 1,
            CardTypes = CardType.Creature, Subtypes = ["Goblin"]
        };
        p1.Battlefield.Add(king);
        p1.Battlefield.Add(goblin);

        engine.RecalculateState();
        goblin.Power.Should().Be(2);

        engine.StripEndOfTurnEffects();
        engine.RecalculateState();

        // King's permanent buff still applies
        goblin.Power.Should().Be(2);
    }
}
