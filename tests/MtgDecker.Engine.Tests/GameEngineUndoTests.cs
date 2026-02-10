using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class GameEngineUndoTests
{
    private GameEngine CreateEngine(out GameState state, out Player player1, out Player player2)
    {
        var p1Handler = new TestDecisionHandler();
        var p2Handler = new TestDecisionHandler();
        player1 = new Player(Guid.NewGuid(), "Alice", p1Handler);
        player2 = new Player(Guid.NewGuid(), "Bob", p2Handler);
        state = new GameState(player1, player2);
        return new GameEngine(state);
    }

    [Fact]
    public async Task UndoPlayCard_ReturnsCardFromBattlefieldToHand()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        // Use sandbox card (no ManaCost, not a land) to avoid mana/land-drop logic
        var card = new GameCard { Name = "Widget", TypeLine = "Artifact" };
        p1.Hand.Add(card);
        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, card.Id));

        var result = engine.UndoLastAction(p1.Id);

        result.Should().BeTrue();
        p1.Battlefield.Count.Should().Be(0);
        p1.Hand.Count.Should().Be(1);
        p1.Hand.Cards[0].Should().BeSameAs(card);
    }

    [Fact]
    public async Task UndoTapCard_UntapsTheCard()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        var card = new GameCard { Name = "Forest", TypeLine = "Basic Land — Forest" };
        p1.Battlefield.Add(card);
        await engine.ExecuteAction(GameAction.TapCard(p1.Id, card.Id));

        var result = engine.UndoLastAction(p1.Id);

        result.Should().BeTrue();
        card.IsTapped.Should().BeFalse();
    }

    [Fact]
    public async Task UndoUntapCard_RetapsTheCard()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        var card = new GameCard { Name = "Forest", TypeLine = "Basic Land — Forest", IsTapped = true };
        p1.Battlefield.Add(card);
        await engine.ExecuteAction(GameAction.UntapCard(p1.Id, card.Id));

        var result = engine.UndoLastAction(p1.Id);

        result.Should().BeTrue();
        card.IsTapped.Should().BeTrue();
    }

    [Fact]
    public async Task UndoMoveCard_ReversesSourceAndDestination()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        var card = new GameCard { Name = "Bear", TypeLine = "Creature — Bear" };
        p1.Battlefield.Add(card);
        await engine.ExecuteAction(GameAction.MoveCard(p1.Id, card.Id, ZoneType.Battlefield, ZoneType.Graveyard));

        var result = engine.UndoLastAction(p1.Id);

        result.Should().BeTrue();
        p1.Graveyard.Count.Should().Be(0);
        p1.Battlefield.Count.Should().Be(1);
        p1.Battlefield.Cards[0].Should().BeSameAs(card);
    }

    [Fact]
    public void Undo_EmptyHistory_ReturnsFalse()
    {
        var engine = CreateEngine(out _, out var p1, out _);

        var result = engine.UndoLastAction(p1.Id);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Undo_LogsReversal_PlayCard()
    {
        var engine = CreateEngine(out var state, out var p1, out _);
        var card = new GameCard { Name = "Widget", TypeLine = "Artifact" };
        p1.Hand.Add(card);
        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, card.Id));
        state.GameLog.Clear();

        engine.UndoLastAction(p1.Id);

        state.GameLog.Should().Contain(l => l.Contains("undoes") && l.Contains("Widget"));
    }

    [Fact]
    public async Task Undo_LogsReversal_TapCard()
    {
        var engine = CreateEngine(out var state, out var p1, out _);
        var card = new GameCard { Name = "Forest", TypeLine = "Basic Land — Forest" };
        p1.Battlefield.Add(card);
        await engine.ExecuteAction(GameAction.TapCard(p1.Id, card.Id));
        state.GameLog.Clear();

        engine.UndoLastAction(p1.Id);

        state.GameLog.Should().Contain(l => l.Contains("undoes tapping") && l.Contains("Forest"));
    }

    [Fact]
    public async Task Undo_LogsReversal_MoveCard()
    {
        var engine = CreateEngine(out var state, out var p1, out _);
        var card = new GameCard { Name = "Bear", TypeLine = "Creature — Bear" };
        p1.Battlefield.Add(card);
        await engine.ExecuteAction(GameAction.MoveCard(p1.Id, card.Id, ZoneType.Battlefield, ZoneType.Graveyard));
        state.GameLog.Clear();

        engine.UndoLastAction(p1.Id);

        state.GameLog.Should().Contain(l => l.Contains("undoes moving") && l.Contains("Bear"));
    }

    [Fact]
    public async Task MultipleUndos_ReverseInOrder()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        // Use non-land sandbox cards so both can be played without land-drop limit
        var card1 = new GameCard { Name = "Widget1", TypeLine = "Artifact" };
        var card2 = new GameCard { Name = "Widget2", TypeLine = "Artifact" };
        p1.Hand.Add(card1);
        p1.Hand.Add(card2);

        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, card1.Id));
        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, card2.Id));

        // Undo Widget2 first (LIFO)
        engine.UndoLastAction(p1.Id).Should().BeTrue();
        p1.Battlefield.Count.Should().Be(1);
        p1.Battlefield.Cards[0].Name.Should().Be("Widget1");
        p1.Hand.Count.Should().Be(1);
        p1.Hand.Cards[0].Name.Should().Be("Widget2");

        // Undo Widget1
        engine.UndoLastAction(p1.Id).Should().BeTrue();
        p1.Battlefield.Count.Should().Be(0);
        p1.Hand.Count.Should().Be(2);
    }

    [Fact]
    public async Task ActionHistory_PushedOnSuccessfulAction()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        var card = new GameCard { Name = "Widget", TypeLine = "Artifact" };
        p1.Hand.Add(card);

        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, card.Id));

        p1.ActionHistory.Count.Should().Be(1);
        p1.ActionHistory.Peek().Type.Should().Be(ActionType.PlayCard);
    }

    [Fact]
    public async Task ActionHistory_NotPushedOnFailedAction()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        // Try to play a card that's not in hand
        var fakeId = Guid.NewGuid();

        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, fakeId));

        p1.ActionHistory.Count.Should().Be(0);
    }

    [Fact]
    public async Task ActionHistory_PerPlayer_IndependentStacks()
    {
        var engine = CreateEngine(out _, out var p1, out var p2);
        // Use sandbox cards to avoid land-drop issues
        var card1 = new GameCard { Name = "Widget1", TypeLine = "Artifact" };
        var card2 = new GameCard { Name = "Widget2", TypeLine = "Artifact" };
        p1.Hand.Add(card1);
        p2.Hand.Add(card2);

        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, card1.Id));
        await engine.ExecuteAction(GameAction.PlayCard(p2.Id, card2.Id));

        // Player 1 can undo their action even though Player 2 acted last
        engine.UndoLastAction(p1.Id).Should().BeTrue();
        p1.Battlefield.Count.Should().Be(0);
        p1.Hand.Count.Should().Be(1);

        // Player 2's action is unaffected
        p2.Battlefield.Count.Should().Be(1);
    }

    [Fact]
    public async Task Undo_PopOnlyOnSuccess_PlayCard()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        var card = new GameCard { Name = "Widget", TypeLine = "Artifact" };
        p1.Hand.Add(card);
        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, card.Id));

        // Manually remove the card from battlefield (simulating external interference)
        p1.Battlefield.RemoveById(card.Id);

        // Undo should fail because card is gone — and NOT consume the history entry
        var result = engine.UndoLastAction(p1.Id);
        result.Should().BeFalse();
        p1.ActionHistory.Count.Should().Be(1, "history should not be consumed on failed undo");
    }

    [Fact]
    public async Task Undo_PopOnlyOnSuccess_MoveCard()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        var card = new GameCard { Name = "Bear", TypeLine = "Creature — Bear" };
        p1.Battlefield.Add(card);
        await engine.ExecuteAction(GameAction.MoveCard(p1.Id, card.Id, ZoneType.Battlefield, ZoneType.Graveyard));

        // Manually remove the card from graveyard
        p1.Graveyard.RemoveById(card.Id);

        // Undo should fail — card not in destination zone
        var result = engine.UndoLastAction(p1.Id);
        result.Should().BeFalse();
        p1.ActionHistory.Count.Should().Be(1, "history should not be consumed on failed undo");
    }

    [Fact]
    public async Task Undo_PopOnlyOnSuccess_TapCard()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        var card = new GameCard { Name = "Forest", TypeLine = "Basic Land — Forest" };
        p1.Battlefield.Add(card);
        await engine.ExecuteAction(GameAction.TapCard(p1.Id, card.Id));

        // Manually remove the card from battlefield
        p1.Battlefield.RemoveById(card.Id);

        // Undo should fail — card not on battlefield
        var result = engine.UndoLastAction(p1.Id);
        result.Should().BeFalse();
        p1.ActionHistory.Count.Should().Be(1, "history should not be consumed on failed undo");
    }

    [Fact]
    public async Task Undo_AfterTapThenUntap_UndoesInCorrectOrder()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        var card = new GameCard { Name = "Forest", TypeLine = "Basic Land — Forest" };
        p1.Battlefield.Add(card);

        await engine.ExecuteAction(GameAction.TapCard(p1.Id, card.Id));
        await engine.ExecuteAction(GameAction.UntapCard(p1.Id, card.Id));

        // Undo untap → card should be tapped again
        engine.UndoLastAction(p1.Id).Should().BeTrue();
        card.IsTapped.Should().BeTrue();

        // Undo tap → card should be untapped
        engine.UndoLastAction(p1.Id).Should().BeTrue();
        card.IsTapped.Should().BeFalse();
    }

    [Fact]
    public async Task Undo_MoveToExile_ReversesCorrectly()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        var card = new GameCard { Name = "Bear", TypeLine = "Creature — Bear" };
        p1.Battlefield.Add(card);

        await engine.ExecuteAction(GameAction.MoveCard(p1.Id, card.Id, ZoneType.Battlefield, ZoneType.Exile));

        engine.UndoLastAction(p1.Id).Should().BeTrue();
        p1.Exile.Count.Should().Be(0);
        p1.Battlefield.Count.Should().Be(1);
    }
}
