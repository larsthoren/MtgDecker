using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class OrcishBowmastersTests
{
    [Fact]
    public void CardDefinition_OrcishBowmasters_IsRegistered()
    {
        CardDefinitions.TryGet("Orcish Bowmasters", out var def).Should().BeTrue();
        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(2); // {1}{B}
        def.Power.Should().Be(1);
        def.Toughness.Should().Be(1);
        def.CardTypes.Should().Be(CardType.Creature);
        def.HasFlash.Should().BeTrue();
        def.Subtypes.Should().Contain("Orc").And.Contain("Archer");
    }

    [Fact]
    public void CardDefinition_OrcishBowmasters_HasETBTrigger()
    {
        CardDefinitions.TryGet("Orcish Bowmasters", out var def).Should().BeTrue();
        def!.Triggers.Should().Contain(t =>
            t.Event == GameEvent.EnterBattlefield
            && t.Condition == TriggerCondition.Self
            && t.Effect is BowmastersEffect);
    }

    [Fact]
    public void CardDefinition_OrcishBowmasters_HasDrawTrigger()
    {
        CardDefinitions.TryGet("Orcish Bowmasters", out var def).Should().BeTrue();
        def!.Triggers.Should().Contain(t =>
            t.Event == GameEvent.DrawCard
            && t.Condition == TriggerCondition.OpponentDrawsExceptFirst
            && t.Effect is BowmastersEffect);
    }

    [Fact]
    public void GameCard_Create_OrcishBowmasters_LoadsFromRegistry()
    {
        var card = GameCard.Create("Orcish Bowmasters");

        card.ManaCost.Should().NotBeNull();
        card.BasePower.Should().Be(1);
        card.BaseToughness.Should().Be(1);
        card.CardTypes.Should().Be(CardType.Creature);
        card.Subtypes.Should().Contain("Orc");
    }

    private static (GameEngine engine, GameState state, Player p1, Player p2,
        TestDecisionHandler h1, TestDecisionHandler h2) SetupGame()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, p1, p2, h1, h2);
    }

    [Fact]
    public async Task Bowmasters_CastWithFlash_ETBTriggers()
    {
        var (engine, state, p1, p2, h1, h2) = SetupGame();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        // Give P1 mana to cast Bowmasters ({1}{B})
        p1.ManaPool.Add(ManaColor.Black, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 1);

        var bowmasters = GameCard.Create("Orcish Bowmasters");
        p1.Hand.Add(bowmasters);

        // CastSpell puts the spell on the stack
        await engine.ExecuteAction(GameAction.CastSpell(p1.Id, bowmasters.Id));

        // Bowmasters should be on the stack
        state.StackCount.Should().BeGreaterThanOrEqualTo(1);

        // Enqueue card choice for BowmastersEffect: decline creature target → hit opponent
        h1.EnqueueCardChoice(null);

        // Resolve the stack: spell resolves → creature ETB → BowmastersEffect resolves
        await engine.ResolveAllTriggersAsync();

        // Bowmasters on battlefield
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Orcish Bowmasters");

        // Orc Army token created (amass Orcs 1)
        p1.Battlefield.Cards.Should().Contain(c =>
            c.IsToken && c.Subtypes.Contains("Army"));

        // Army token should have 1 +1/+1 counter
        var army = p1.Battlefield.Cards.First(c => c.IsToken && c.Subtypes.Contains("Army"));
        army.GetCounters(CounterType.PlusOnePlusOne).Should().Be(1);

        // Opponent took 1 damage (declined creature target)
        p2.Life.Should().Be(19);
    }

    [Fact]
    public async Task Bowmasters_OpponentDrawsExtraCard_TriggerFires()
    {
        var (engine, state, p1, p2, h1, h2) = SetupGame();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        // Bowmasters already on P1's battlefield
        var bowmasters = GameCard.Create("Orcish Bowmasters");
        p1.Battlefield.Add(bowmasters);
        bowmasters.TurnEnteredBattlefield = state.TurnNumber - 1;

        // Simulate: opponent already drew their first card in draw step
        p2.DrawStepDrawExempted = true;
        p2.DrawsThisTurn = 1;

        // Put a card in opponent's library so they can draw
        p2.Library.Add(GameCard.Create("Island", "Basic Land — Island"));

        // Opponent draws extra card (e.g., from Brainstorm)
        engine.DrawCards(p2, 1);

        // Draw trigger should be on the stack
        state.StackCount.Should().BeGreaterThanOrEqualTo(1,
            "Bowmasters should trigger when opponent draws a non-first card");

        // The trigger should be a TriggeredAbilityStackObject with BowmastersEffect
        var trigger = state.Stack.OfType<TriggeredAbilityStackObject>().FirstOrDefault();
        trigger.Should().NotBeNull();
        trigger!.Effect.Should().BeOfType<BowmastersEffect>();
        trigger.Source.Name.Should().Be("Orcish Bowmasters");
    }

    [Fact]
    public async Task Bowmasters_OpponentDrawsExtraCard_FullResolution()
    {
        var (engine, state, p1, p2, h1, h2) = SetupGame();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        // Bowmasters already on P1's battlefield
        var bowmasters = GameCard.Create("Orcish Bowmasters");
        p1.Battlefield.Add(bowmasters);
        bowmasters.TurnEnteredBattlefield = state.TurnNumber - 1;

        // Simulate: opponent already drew their first card in draw step
        p2.DrawStepDrawExempted = true;
        p2.DrawsThisTurn = 1;

        // Put a card in opponent's library so they can draw
        p2.Library.Add(GameCard.Create("Island", "Basic Land — Island"));

        // Decline creature target → hit opponent
        h1.EnqueueCardChoice(null);

        // Opponent draws extra card
        engine.DrawCards(p2, 1);

        // Resolve the draw trigger
        await engine.ResolveAllTriggersAsync();

        // Stack should be empty after resolution
        state.StackCount.Should().Be(0);

        // Opponent took 1 damage
        p2.Life.Should().Be(19);

        // Orc Army token created on P1's battlefield
        p1.Battlefield.Cards.Should().Contain(c =>
            c.IsToken && c.Subtypes.Contains("Army"));
    }
}
