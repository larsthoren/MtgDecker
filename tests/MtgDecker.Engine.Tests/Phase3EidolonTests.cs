using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class Phase3EidolonTests
{
    [Fact]
    public void Eidolon_HasSpellCastCmc3Trigger()
    {
        CardDefinitions.TryGet("Eidolon of the Great Revel", out var def);

        var trigger = def!.Triggers.FirstOrDefault(t =>
            t.Event == GameEvent.SpellCast
            && t.Condition == TriggerCondition.AnySpellCastCmc3OrLess);

        trigger.Should().NotBeNull("Eidolon should have a SpellCast CMC<=3 trigger");
        trigger!.Effect.Should().BeOfType<DealDamageEffect>()
            .Which.Amount.Should().Be(2);
    }

    [Fact]
    public async Task Eidolon_Triggers_OnCmc1Spell()
    {
        var h1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var eidolon = GameCard.Create("Eidolon of the Great Revel",
            "Enchantment Creature — Spirit");
        p1.Battlefield.Add(eidolon);

        var spell = new GameCard { Name = "Bolt", ManaCost = ManaCost.Parse("{R}") };
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, spell);

        state.StackCount.Should().Be(1,
            "Eidolon should trigger on CMC 1 spell");
    }

    [Fact]
    public async Task Eidolon_DoesNotTrigger_OnCmc4Spell()
    {
        var h1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var eidolon = GameCard.Create("Eidolon of the Great Revel",
            "Enchantment Creature — Spirit");
        p1.Battlefield.Add(eidolon);

        var spell = new GameCard { Name = "Expensive", ManaCost = ManaCost.Parse("{3}{R}") };
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, spell);

        state.StackCount.Should().Be(0);
    }

    [Fact]
    public async Task Eidolon_DealsDamage_ToCaster()
    {
        var h1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var eidolon = GameCard.Create("Eidolon of the Great Revel",
            "Enchantment Creature — Spirit");
        p1.Battlefield.Add(eidolon);

        var spell = new GameCard { Name = "Bolt", ManaCost = ManaCost.Parse("{R}") };
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, spell);

        var triggered = state.Stack[^1] as TriggeredAbilityStackObject;
        triggered!.TargetPlayerId.Should().Be(state.ActivePlayer.Id);

        state.StackPopTop();
        var controller = state.GetPlayer(triggered.ControllerId);
        var context = new EffectContext(
            state, controller, triggered.Source, controller.DecisionHandler)
        {
            TargetPlayerId = triggered.TargetPlayerId,
        };
        await triggered.Effect.Execute(context);

        var caster = state.GetPlayer(state.ActivePlayer.Id);
        caster.Life.Should().Be(18, "Eidolon deals 2 damage to caster");
    }

    [Fact]
    public async Task Eidolon_Triggers_OnOpponentSpellToo()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var eidolon = GameCard.Create("Eidolon of the Great Revel",
            "Enchantment Creature — Spirit");
        p1.Battlefield.Add(eidolon);

        var spell = new GameCard { Name = "Bolt", ManaCost = ManaCost.Parse("{R}") };
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, spell);

        state.StackCount.Should().Be(1);
    }
}
