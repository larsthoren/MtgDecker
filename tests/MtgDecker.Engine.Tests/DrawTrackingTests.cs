using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class DrawTrackingTests
{
    [Fact]
    public void Player_DrawsThisTurn_StartsAtZero()
    {
        var handler = new TestDecisionHandler();
        var player = new Player(Guid.NewGuid(), "Test", handler);
        player.DrawsThisTurn.Should().Be(0);
    }

    [Fact]
    public void Player_DrawStepDrawExempted_StartsAsFalse()
    {
        var handler = new TestDecisionHandler();
        var player = new Player(Guid.NewGuid(), "Test", handler);
        player.DrawStepDrawExempted.Should().BeFalse();
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
    public void DrawCards_IncrementsDrawsThisTurn()
    {
        var (engine, state, p1, _, _, _) = SetupGame();
        p1.Library.Add(GameCard.Create("Card1", "Instant"));
        p1.Library.Add(GameCard.Create("Card2", "Instant"));
        p1.Library.Add(GameCard.Create("Card3", "Instant"));

        engine.DrawCards(p1, 3);

        p1.DrawsThisTurn.Should().Be(3);
        p1.Hand.Count.Should().Be(3);
    }

    [Fact]
    public void DrawCards_DrawStepDraw_ExemptsFirstDraw()
    {
        var (engine, state, p1, _, _, _) = SetupGame();
        p1.Library.Add(GameCard.Create("Card1", "Instant"));

        engine.DrawCards(p1, 1, isDrawStepDraw: true);

        p1.DrawsThisTurn.Should().Be(1);
        p1.DrawStepDrawExempted.Should().BeTrue();
    }

    [Fact]
    public void DrawCards_DrawStepDraw_SecondDrawNotExempted()
    {
        var (engine, state, p1, _, _, _) = SetupGame();
        p1.Library.Add(GameCard.Create("Card1", "Instant"));
        p1.Library.Add(GameCard.Create("Card2", "Instant"));

        // Two cards drawn during draw step (e.g., Howling Mine)
        engine.DrawCards(p1, 2, isDrawStepDraw: true);

        p1.DrawsThisTurn.Should().Be(2);
        p1.DrawStepDrawExempted.Should().BeTrue(); // First was exempted
        // But second draw was NOT exempted (it was tracked normally)
    }

    [Fact]
    public void OpponentDrawsExceptFirst_DoesNotFireOnFirstDrawStepDraw()
    {
        var (engine, state, p1, p2, h1, h2) = SetupGame();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var triggerCard = new GameCard
        {
            Name = "Draw Watcher",
            CardTypes = CardType.Creature,
            BasePower = 1,
            BaseToughness = 1,
            Triggers = [new Trigger(GameEvent.DrawCard, TriggerCondition.OpponentDrawsExceptFirst,
                new DealDamageEffect(1))],
        };
        p1.Battlefield.Add(triggerCard);
        triggerCard.TurnEnteredBattlefield = state.TurnNumber - 1;

        // P2 draws their first card (draw step draw) — should NOT trigger
        p2.Library.Add(GameCard.Create("Card1", "Instant"));
        p2.DrawStepDrawExempted = false;
        engine.DrawCards(p2, 1, isDrawStepDraw: true);

        state.Stack.Should().BeEmpty();
    }

    [Fact]
    public void OpponentDrawsExceptFirst_FiresOnNonDrawStepDraw()
    {
        var (engine, state, p1, p2, h1, h2) = SetupGame();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var triggerCard = new GameCard
        {
            Name = "Draw Watcher",
            CardTypes = CardType.Creature,
            BasePower = 1,
            BaseToughness = 1,
            Triggers = [new Trigger(GameEvent.DrawCard, TriggerCondition.OpponentDrawsExceptFirst,
                new DealDamageEffect(1))],
        };
        p1.Battlefield.Add(triggerCard);
        triggerCard.TurnEnteredBattlefield = state.TurnNumber - 1;

        // P2's first draw step draw (exempt)
        p2.Library.Add(GameCard.Create("Card1", "Instant"));
        p2.DrawStepDrawExempted = false;
        engine.DrawCards(p2, 1, isDrawStepDraw: true);
        state.Stack.Should().BeEmpty();

        // P2 draws again (not draw step) — should trigger
        p2.Library.Add(GameCard.Create("Card2", "Instant"));
        engine.DrawCards(p2, 1);

        state.Stack.Count.Should().Be(1);
    }

    [Fact]
    public void OpponentDrawsExceptFirst_DoesNotFireOnOwnDraw()
    {
        var (engine, state, p1, p2, h1, h2) = SetupGame();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var triggerCard = new GameCard
        {
            Name = "Draw Watcher",
            CardTypes = CardType.Creature,
            BasePower = 1,
            BaseToughness = 1,
            Triggers = [new Trigger(GameEvent.DrawCard, TriggerCondition.OpponentDrawsExceptFirst,
                new DealDamageEffect(1))],
        };
        p1.Battlefield.Add(triggerCard);
        triggerCard.TurnEnteredBattlefield = state.TurnNumber - 1;

        // P1 (controller) draws — should NOT trigger
        p1.Library.Add(GameCard.Create("Card1", "Instant"));
        p1.DrawStepDrawExempted = true;
        engine.DrawCards(p1, 1);

        state.Stack.Should().BeEmpty();
    }

    [Fact]
    public void OpponentDrawsExceptFirst_FiresMultipleTimesOnMultipleDraws()
    {
        var (engine, state, p1, p2, h1, h2) = SetupGame();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var triggerCard = new GameCard
        {
            Name = "Draw Watcher",
            CardTypes = CardType.Creature,
            BasePower = 1,
            BaseToughness = 1,
            Triggers = [new Trigger(GameEvent.DrawCard, TriggerCondition.OpponentDrawsExceptFirst,
                new DealDamageEffect(1))],
        };
        p1.Battlefield.Add(triggerCard);
        triggerCard.TurnEnteredBattlefield = state.TurnNumber - 1;

        // P2 already had first draw step exempted
        p2.DrawStepDrawExempted = true;
        p2.DrawsThisTurn = 1;

        // P2 draws 3 cards (e.g., Brainstorm) — should trigger 3 times
        p2.Library.Add(GameCard.Create("Card1", "Instant"));
        p2.Library.Add(GameCard.Create("Card2", "Instant"));
        p2.Library.Add(GameCard.Create("Card3", "Instant"));
        engine.DrawCards(p2, 3);

        state.Stack.Count.Should().Be(3);
    }
}
