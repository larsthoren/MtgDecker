using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class DelayedTriggerTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler p1Handler, TestDecisionHandler p2Handler) CreateSetup()
    {
        var p1Handler = new TestDecisionHandler();
        var p2Handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "Player 1", p1Handler),
            new Player(Guid.NewGuid(), "Player 2", p2Handler));
        var engine = new GameEngine(state);

        // Add library cards so draw phase works
        for (int i = 0; i < 10; i++)
            state.Player1.Library.Add(new GameCard { Name = $"P1 Card {i}" });
        for (int i = 0; i < 10; i++)
            state.Player2.Library.Add(new GameCard { Name = $"P2 Card {i}" });

        return (engine, state, p1Handler, p2Handler);
    }

    private void EnqueuePassAll(TestDecisionHandler handler, Guid playerId, int count = 20)
    {
        for (int i = 0; i < count; i++)
            handler.EnqueueAction(GameAction.Pass(playerId));
    }

    [Fact]
    public async Task DelayedTrigger_FiresAtEndStep_DestroysAllGoblins()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();

        var goblin = new GameCard
        {
            Name = "Goblin Lackey",
            CardTypes = CardType.Creature,
            BasePower = 1,
            BaseToughness = 1,
            Subtypes = ["Goblin"],
        };
        state.Player1.Battlefield.Add(goblin);

        state.DelayedTriggers.Add(new DelayedTrigger(
            GameEvent.EndStep,
            new DestroyAllSubtypeEffect("Goblin"),
            state.Player1.Id));

        state.ActivePlayer = state.Player1;
        state.IsFirstTurn = true; // Skip first draw

        EnqueuePassAll(p1Handler, state.Player1.Id);
        EnqueuePassAll(p2Handler, state.Player2.Id);

        await engine.RunTurnAsync();

        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Name == "Goblin Lackey");
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Name == "Goblin Lackey");
        state.GameLog.Should().Contain(l => l.Contains("Goblin Lackey") && l.Contains("destroyed"));
    }

    [Fact]
    public async Task DelayedTrigger_RemovedAfterFiring()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();

        state.DelayedTriggers.Add(new DelayedTrigger(
            GameEvent.EndStep,
            new DrawCardEffect(),
            state.Player1.Id));

        state.ActivePlayer = state.Player1;
        state.IsFirstTurn = true;

        EnqueuePassAll(p1Handler, state.Player1.Id);
        EnqueuePassAll(p2Handler, state.Player2.Id);

        await engine.RunTurnAsync();

        state.DelayedTriggers.Should().BeEmpty("delayed trigger should be removed after firing");
    }

    [Fact]
    public async Task DelayedTrigger_DoesNotFireForWrongEvent()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();

        // Register a delayed trigger for SpellCast (not EndStep or Upkeep)
        // SpellCast is never fired via QueueDelayedTriggersOnStackAsync, so it stays
        state.DelayedTriggers.Add(new DelayedTrigger(
            GameEvent.SpellCast,
            new DestroyAllSubtypeEffect("Goblin"),
            state.Player1.Id));

        var goblin = new GameCard
        {
            Name = "Goblin Lackey",
            CardTypes = CardType.Creature,
            Subtypes = ["Goblin"],
        };
        state.Player1.Battlefield.Add(goblin);

        state.ActivePlayer = state.Player1;
        state.IsFirstTurn = true;

        EnqueuePassAll(p1Handler, state.Player1.Id);
        EnqueuePassAll(p2Handler, state.Player2.Id);

        await engine.RunTurnAsync();

        // SpellCast delayed triggers are not processed during a turn, so it stays
        state.DelayedTriggers.Should().HaveCount(1);
    }

    [Fact]
    public async Task UpkeepTrigger_MirrisGuile_RearrangesTopCards()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();

        // Put Mirri's Guile on battlefield
        var guile = GameCard.Create("Mirri's Guile");
        state.Player1.Battlefield.Add(guile);

        state.ActivePlayer = state.Player1;
        state.IsFirstTurn = true; // Skip first draw

        EnqueuePassAll(p1Handler, state.Player1.Id);
        EnqueuePassAll(p2Handler, state.Player2.Id);

        await engine.RunTurnAsync();

        // Verify the rearrange log message appeared during upkeep
        state.GameLog.Should().Contain(l => l.Contains("rearranges top") && l.Contains("cards"));
    }

    [Fact]
    public async Task UpkeepTrigger_DoesNotFireForOpponent()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();

        // Put Mirri's Guile on Player2's battlefield
        var guile = GameCard.Create("Mirri's Guile");
        state.Player2.Battlefield.Add(guile);

        state.ActivePlayer = state.Player1;
        state.IsFirstTurn = true;

        EnqueuePassAll(p1Handler, state.Player1.Id);
        EnqueuePassAll(p2Handler, state.Player2.Id);

        await engine.RunTurnAsync();

        // Player2's Mirri's Guile should NOT fire during Player1's upkeep
        state.GameLog.Should().NotContain(l => l.Contains("rearranges top"));
    }

    [Fact]
    public async Task PyromancerETB_PumpsGoblins_ThenDestroysAtEndStep()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();

        state.ActivePlayer = state.Player1;
        state.CurrentPhase = Phase.MainPhase1;

        // Put a goblin on the battlefield
        var goblin = new GameCard
        {
            Name = "Goblin Lackey",
            CardTypes = CardType.Creature,
            BasePower = 1,
            BaseToughness = 1,
            Subtypes = ["Goblin"],
        };
        state.Player1.Battlefield.Add(goblin);

        // Play Goblin Pyromancer
        var pyromancer = GameCard.Create("Goblin Pyromancer");
        state.Player1.Hand.Add(pyromancer);
        state.Player1.ManaPool.Add(ManaColor.Red, 4);

        // Play Pyromancer, then pass everything
        p1Handler.EnqueueAction(GameAction.CastSpell(state.Player1.Id, pyromancer.Id));
        EnqueuePassAll(p1Handler, state.Player1.Id);
        EnqueuePassAll(p2Handler, state.Player2.Id);

        await engine.RunPriorityAsync();

        // After ETB: should see pump effect added and delayed trigger registered
        state.GameLog.Should().Contain(l => l.Contains("Goblins get +3/+0"));
        state.DelayedTriggers.Should().HaveCount(1);
        state.DelayedTriggers[0].FireOn.Should().Be(GameEvent.EndStep);
    }
}
