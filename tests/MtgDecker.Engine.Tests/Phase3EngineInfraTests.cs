using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class Phase3EngineInfraTests
{
    private static (GameEngine engine, GameState state, Player p1, Player p2,
        TestDecisionHandler h1, TestDecisionHandler h2) Setup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, p1, p2, h1, h2);
    }

    // === AnySpellCastCmc3OrLess trigger condition ===

    [Fact]
    public async Task AnySpellCastCmc3OrLess_Fires_WhenCmc3SpellCast()
    {
        var (engine, state, p1, p2, h1, h2) = Setup();
        var tracker = new DamageTracker();

        var eidolon = new GameCard
        {
            Name = "Test Eidolon",
            CardTypes = CardType.Creature,
            Triggers = [new Trigger(GameEvent.SpellCast,
                TriggerCondition.AnySpellCastCmc3OrLess, tracker)]
        };
        p1.Battlefield.Add(eidolon);

        var spell = new GameCard { Name = "Cheap Spell", ManaCost = ManaCost.Parse("{1}{R}") };
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, spell);

        state.StackCount.Should().BeGreaterThan(0, "trigger should fire for CMC 2 spell");
    }

    [Fact]
    public async Task AnySpellCastCmc3OrLess_DoesNotFire_WhenCmc4SpellCast()
    {
        var (engine, state, p1, p2, h1, h2) = Setup();

        var eidolon = new GameCard
        {
            Name = "Test Eidolon",
            CardTypes = CardType.Creature,
            Triggers = [new Trigger(GameEvent.SpellCast,
                TriggerCondition.AnySpellCastCmc3OrLess, new DealDamageEffect(2))]
        };
        p1.Battlefield.Add(eidolon);

        var spell = new GameCard { Name = "Expensive Spell", ManaCost = ManaCost.Parse("{3}{R}") };
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, spell);

        state.StackCount.Should().Be(0, "trigger should NOT fire for CMC 4 spell");
    }

    [Fact]
    public async Task AnySpellCastCmc3OrLess_SetsTargetPlayerId_ToActivePlayer()
    {
        var (engine, state, p1, p2, h1, h2) = Setup();

        var eidolon = new GameCard
        {
            Name = "Test Eidolon",
            CardTypes = CardType.Creature,
            Triggers = [new Trigger(GameEvent.SpellCast,
                TriggerCondition.AnySpellCastCmc3OrLess, new DealDamageEffect(2))]
        };
        p1.Battlefield.Add(eidolon);

        var spell = new GameCard { Name = "Bolt", ManaCost = ManaCost.Parse("{R}") };
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, spell);

        state.StackCount.Should().Be(1);
        var stackObj = state.Stack[0] as TriggeredAbilityStackObject;
        stackObj.Should().NotBeNull();
        stackObj!.TargetPlayerId.Should().Be(state.ActivePlayer.Id,
            "Eidolon deals damage to the spell's caster (active player)");
    }

    // === SelfInGraveyardDuringUpkeep trigger condition ===

    [Fact]
    public async Task GraveyardTrigger_Fires_WhenCardInGraveyardDuringUpkeep()
    {
        var (engine, state, p1, p2, h1, h2) = Setup();
        var tracker = new DamageTracker();

        var squee = new GameCard
        {
            Name = "Test Squee",
            CardTypes = CardType.Creature,
            Triggers = [new Trigger(GameEvent.Upkeep,
                TriggerCondition.SelfInGraveyardDuringUpkeep, tracker)]
        };
        p1.Graveyard.Add(squee);

        await engine.QueueGraveyardTriggersOnStackAsync(GameEvent.Upkeep);

        state.StackCount.Should().Be(1,
            "graveyard trigger should fire for active player's card in graveyard during upkeep");
    }

    [Fact]
    public async Task GraveyardTrigger_DoesNotFire_ForNonActivePlayer()
    {
        var (engine, state, p1, p2, h1, h2) = Setup();

        var squee = new GameCard
        {
            Name = "Test Squee",
            CardTypes = CardType.Creature,
            Triggers = [new Trigger(GameEvent.Upkeep,
                TriggerCondition.SelfInGraveyardDuringUpkeep, new DrawCardEffect())]
        };
        p2.Graveyard.Add(squee);

        await engine.QueueGraveyardTriggersOnStackAsync(GameEvent.Upkeep);

        state.StackCount.Should().Be(0,
            "graveyard trigger should NOT fire for non-active player's upkeep");
    }

    // === SacrificeCardType on ActivatedAbilityCost ===

    [Fact]
    public void ActivatedAbilityCost_HasSacrificeCardType_Property()
    {
        var cost = new ActivatedAbilityCost(SacrificeCardType: CardType.Land);
        cost.SacrificeCardType.Should().Be(CardType.Land);
    }

    // Helper
    private class DamageTracker : IEffect
    {
        public bool Fired { get; private set; }
        public Task Execute(EffectContext context, CancellationToken ct = default)
        {
            Fired = true;
            return Task.CompletedTask;
        }
    }
}
