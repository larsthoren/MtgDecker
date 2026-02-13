using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class CyclingTests
{
    [Fact]
    public void GempalmIncinerator_Has_CyclingCost()
    {
        CardDefinitions.TryGet("Gempalm Incinerator", out var def).Should().BeTrue();
        def!.CyclingCost.Should().NotBeNull();
        def.CyclingCost!.ToString().Should().Be("{1}{R}");
    }

    [Fact]
    public async Task Cycle_Discards_Card_And_Draws()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var gempalm = GameCard.Create("Gempalm Incinerator");
        p1.Hand.Add(gempalm);
        p1.Library.Add(new GameCard { Name = "Drawn Card" });

        p1.ManaPool.Add(ManaColor.Red, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 1);

        var action = GameAction.Cycle(p1.Id, gempalm.Id);
        await engine.ExecuteAction(action);

        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Gempalm Incinerator");
        p1.Hand.Cards.Should().Contain(c => c.Name == "Drawn Card");
        // Cycling trigger should be on the stack
        state.Stack.Should().ContainSingle();
        state.Stack[0].Should().BeOfType<TriggeredAbilityStackObject>();
    }

    [Fact]
    public async Task GempalmIncinerator_Trigger_Deals_Damage_Equal_To_Goblin_Count()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        p1.Battlefield.Add(new GameCard { Name = "Goblin 1", CardTypes = CardType.Creature, Subtypes = ["Goblin"], BasePower = 1, BaseToughness = 1 });
        p1.Battlefield.Add(new GameCard { Name = "Goblin 2", CardTypes = CardType.Creature, Subtypes = ["Goblin"], BasePower = 1, BaseToughness = 1 });
        p1.Battlefield.Add(new GameCard { Name = "Goblin 3", CardTypes = CardType.Creature, Subtypes = ["Goblin"], BasePower = 1, BaseToughness = 1 });

        var target = new GameCard { Name = "Elf", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 3 };
        p2.Battlefield.Add(target);

        handler.EnqueueCardChoice(target.Id);

        var effect = new GempalmIncineratorEffect();
        var context = new EffectContext(state, p1, new GameCard { Name = "Gempalm Incinerator" }, handler);
        await effect.Execute(context);

        target.DamageMarked.Should().Be(3);
    }

    [Fact]
    public async Task Cycle_Without_Enough_Mana_Fails()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var gempalm = GameCard.Create("Gempalm Incinerator");
        p1.Hand.Add(gempalm);
        p1.Library.Add(new GameCard { Name = "Drawn Card" });

        // Only 1 red mana, need {1}{R}
        p1.ManaPool.Add(ManaColor.Red, 1);

        var action = GameAction.Cycle(p1.Id, gempalm.Id);
        await engine.ExecuteAction(action);

        // Card should still be in hand
        p1.Hand.Cards.Should().Contain(c => c.Name == "Gempalm Incinerator");
        p1.Graveyard.Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task Cycle_Card_Without_CyclingCost_Fails()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // Goblin Lackey has no cycling cost
        var lackey = GameCard.Create("Goblin Lackey");
        p1.Hand.Add(lackey);
        p1.Library.Add(new GameCard { Name = "Drawn Card" });

        p1.ManaPool.Add(ManaColor.Red, 2);

        var action = GameAction.Cycle(p1.Id, lackey.Id);
        await engine.ExecuteAction(action);

        // Card should still be in hand
        p1.Hand.Cards.Should().Contain(c => c.Name == "Goblin Lackey");
    }

    [Fact]
    public async Task GempalmIncinerator_Trigger_With_Zero_Goblins_Deals_No_Damage()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var target = new GameCard { Name = "Elf", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 3 };
        p2.Battlefield.Add(target);

        var effect = new GempalmIncineratorEffect();
        var context = new EffectContext(state, p1, new GameCard { Name = "Gempalm Incinerator" }, handler);
        await effect.Execute(context);

        target.DamageMarked.Should().Be(0);
    }
}
