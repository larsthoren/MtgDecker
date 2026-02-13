using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class CostModificationTests
{
    [Fact]
    public void ManaCost_WithGenericReduction_Reduces_Generic()
    {
        var cost = ManaCost.Parse("{3}{R}");
        var reduced = cost.WithGenericReduction(1);
        reduced.GenericCost.Should().Be(2);
        reduced.ColorRequirements.Should().ContainKey(ManaColor.Red);
        reduced.ColorRequirements[ManaColor.Red].Should().Be(1);
    }

    [Fact]
    public void ManaCost_WithGenericReduction_Cannot_Go_Below_Zero()
    {
        var cost = ManaCost.Parse("{1}{R}");
        var reduced = cost.WithGenericReduction(5);
        reduced.GenericCost.Should().Be(0);
    }

    [Fact]
    public void ManaCost_WithGenericReduction_Zero_Returns_Same_Values()
    {
        var cost = ManaCost.Parse("{2}{G}{G}");
        var reduced = cost.WithGenericReduction(0);
        reduced.GenericCost.Should().Be(2);
        reduced.ColorRequirements[ManaColor.Green].Should().Be(2);
    }

    [Fact]
    public void ManaCost_WithGenericReduction_Preserves_ConvertedManaCost()
    {
        var cost = ManaCost.Parse("{3}{R}");
        var reduced = cost.WithGenericReduction(2);
        reduced.GenericCost.Should().Be(1);
        reduced.ConvertedManaCost.Should().Be(2); // 1 generic + 1 red
    }

    [Fact]
    public async Task Warchief_Reduces_Goblin_Spell_Cost()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        // Warchief on battlefield — Goblins cost {1} less
        var warchief = GameCard.Create("Goblin Warchief", "Creature — Goblin");
        p1.Battlefield.Add(warchief);
        engine.RecalculateState();

        // Goblin Ringleader normally costs {3}{R} = 4 total
        // With Warchief: {2}{R} = 3 total
        var ringleader = GameCard.Create("Goblin Ringleader", "Creature — Goblin");
        p1.Hand.Add(ringleader);

        // Only 3 mana available (2 colorless + 1 red) — enough for reduced cost, not full
        p1.ManaPool.Add(ManaColor.Red);
        p1.ManaPool.Add(ManaColor.Colorless);
        p1.ManaPool.Add(ManaColor.Colorless);

        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, ringleader.Id));

        // Should be cast successfully with reduced cost
        p1.Hand.Cards.Should().NotContain(c => c.Name == "Goblin Ringleader");
    }

    [Fact]
    public async Task Warchief_Does_Not_Reduce_NonGoblin_Cost()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        var warchief = GameCard.Create("Goblin Warchief", "Creature — Goblin");
        p1.Battlefield.Add(warchief);
        engine.RecalculateState();

        // Naturalize costs {1}{G} — not a Goblin, shouldn't be reduced
        var naturalize = GameCard.Create("Naturalize", "Instant");
        p1.Hand.Add(naturalize);

        // Only 1 green mana — not enough for {1}{G} without reduction
        p1.ManaPool.Add(ManaColor.Green);

        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, naturalize.Id));

        // Should NOT be cast — still in hand
        p1.Hand.Cards.Should().Contain(c => c.Name == "Naturalize");
    }

    [Fact]
    public async Task Warchief_Reduces_Cost_In_CastSpell_Path()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        // Warchief on battlefield
        var warchief = GameCard.Create("Goblin Warchief", "Creature — Goblin");
        p1.Battlefield.Add(warchief);
        engine.RecalculateState();

        // Goblin Ringleader: {3}{R} → reduced to {2}{R}
        var ringleader = GameCard.Create("Goblin Ringleader", "Creature — Goblin");
        p1.Hand.Add(ringleader);

        // 3 mana: 1 red + 2 colorless — enough for reduced cost
        p1.ManaPool.Add(ManaColor.Red);
        p1.ManaPool.Add(ManaColor.Colorless);
        p1.ManaPool.Add(ManaColor.Colorless);

        // Use CastSpell action (goes through stack)
        handler.EnqueueAction(GameAction.Pass(p1.Id));
        await engine.ExecuteAction(GameAction.CastSpell(p1.Id, ringleader.Id));

        // Should be on the stack (cast successfully)
        state.Stack.Should().ContainSingle()
            .Which.Should().BeOfType<StackObject>()
            .Which.Card.Name.Should().Be("Goblin Ringleader");
    }

    [Fact]
    public async Task Multiple_Warchiefs_Stack_Cost_Reduction()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        // Two Warchiefs on battlefield — Goblins cost {2} less total
        var warchief1 = GameCard.Create("Goblin Warchief", "Creature — Goblin");
        var warchief2 = GameCard.Create("Goblin Warchief", "Creature — Goblin");
        p1.Battlefield.Add(warchief1);
        p1.Battlefield.Add(warchief2);
        engine.RecalculateState();

        // Goblin Ringleader normally costs {3}{R} = 4 total
        // With 2 Warchiefs: {1}{R} = 2 total
        var ringleader = GameCard.Create("Goblin Ringleader", "Creature — Goblin");
        p1.Hand.Add(ringleader);

        // Only 2 mana available (1 colorless + 1 red)
        p1.ManaPool.Add(ManaColor.Red);
        p1.ManaPool.Add(ManaColor.Colorless);

        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, ringleader.Id));

        // Should be cast successfully with double reduction
        p1.Hand.Cards.Should().NotContain(c => c.Name == "Goblin Ringleader");
    }
}
