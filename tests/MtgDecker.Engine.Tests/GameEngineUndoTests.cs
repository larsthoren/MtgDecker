using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
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

    // === Task 1: TapCard undo should remove mana ===

    [Fact]
    public async Task UndoTapCard_RemovesManaProducedByFixedAbility()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
        p1.Battlefield.Add(mountain);

        await engine.ExecuteAction(GameAction.TapCard(p1.Id, mountain.Id));
        p1.ManaPool[ManaColor.Red].Should().Be(1);

        engine.UndoLastAction(p1.Id).Should().BeTrue();
        p1.ManaPool[ManaColor.Red].Should().Be(0, "mana should be removed on undo");
        p1.ManaPool.Total.Should().Be(0);
    }

    [Fact]
    public async Task UndoTapCard_RemovesManaProducedByChoiceAbility()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        var handler = (TestDecisionHandler)p1.DecisionHandler;
        var forest = GameCard.Create("Karplusan Forest", "Land");
        p1.Battlefield.Add(forest);
        handler.EnqueueManaColor(ManaColor.Green);

        await engine.ExecuteAction(GameAction.TapCard(p1.Id, forest.Id));
        p1.ManaPool[ManaColor.Green].Should().Be(1);

        engine.UndoLastAction(p1.Id).Should().BeTrue();
        p1.ManaPool[ManaColor.Green].Should().Be(0, "chosen mana should be removed on undo");
    }

    [Fact]
    public async Task UndoTapCard_DoesNotRemoveManaWhenNoAbility()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        var creature = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        p1.Battlefield.Add(creature);
        // Pre-add some mana to ensure undo doesn't touch it
        p1.ManaPool.Add(ManaColor.Red, 2);

        await engine.ExecuteAction(GameAction.TapCard(p1.Id, creature.Id));

        engine.UndoLastAction(p1.Id).Should().BeTrue();
        p1.ManaPool[ManaColor.Red].Should().Be(2, "unrelated mana should not be affected");
    }

    [Fact]
    public async Task UndoTapCard_TwiceTwoLands_RemovesAllMana()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        var forest1 = GameCard.Create("Forest", "Basic Land — Forest");
        var forest2 = GameCard.Create("Forest", "Basic Land — Forest");
        p1.Battlefield.Add(forest1);
        p1.Battlefield.Add(forest2);

        await engine.ExecuteAction(GameAction.TapCard(p1.Id, forest1.Id));
        await engine.ExecuteAction(GameAction.TapCard(p1.Id, forest2.Id));
        p1.ManaPool[ManaColor.Green].Should().Be(2);

        engine.UndoLastAction(p1.Id).Should().BeTrue();
        p1.ManaPool[ManaColor.Green].Should().Be(1);

        engine.UndoLastAction(p1.Id).Should().BeTrue();
        p1.ManaPool[ManaColor.Green].Should().Be(0);
    }

    // === Task 2: PlayCard undo should handle land drops, mana refund, zones ===

    [Fact]
    public async Task UndoPlayLand_DecrementsLandsPlayedThisTurn()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        var forest = GameCard.Create("Forest", "Basic Land — Forest");
        p1.Hand.Add(forest);

        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, forest.Id));
        p1.LandsPlayedThisTurn.Should().Be(1);

        engine.UndoLastAction(p1.Id).Should().BeTrue();
        p1.LandsPlayedThisTurn.Should().Be(0, "land drop should be reversed on undo");
        p1.Battlefield.Count.Should().Be(0);
        p1.Hand.Count.Should().Be(1);
    }

    [Fact]
    public async Task UndoPlayLand_ThenPlayAgain_Succeeds()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        var forest = GameCard.Create("Forest", "Basic Land — Forest");
        p1.Hand.Add(forest);

        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, forest.Id));
        engine.UndoLastAction(p1.Id).Should().BeTrue();

        // Should be able to play a land again after undo
        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, forest.Id));
        p1.Battlefield.Count.Should().Be(1);
        p1.LandsPlayedThisTurn.Should().Be(1);
    }

    [Fact]
    public async Task UndoCastSpell_RefundsMana()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        var lackey = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        p1.Hand.Add(lackey);
        p1.ManaPool.Add(ManaColor.Red, 1);

        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, lackey.Id));
        p1.ManaPool[ManaColor.Red].Should().Be(0);

        engine.UndoLastAction(p1.Id).Should().BeTrue();
        p1.ManaPool[ManaColor.Red].Should().Be(1, "mana should be refunded on undo");
        p1.Hand.Count.Should().Be(1);
    }

    [Fact]
    public async Task UndoCastInstant_ReturnsCardFromGraveyardToHand()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        var swords = GameCard.Create("Swords to Plowshares", "Instant");
        p1.Hand.Add(swords);
        p1.ManaPool.Add(ManaColor.White, 1);

        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, swords.Id));
        p1.Graveyard.Count.Should().Be(1);
        p1.Battlefield.Count.Should().Be(0);

        engine.UndoLastAction(p1.Id).Should().BeTrue();
        p1.Graveyard.Count.Should().Be(0, "instant should be removed from graveyard on undo");
        p1.Hand.Count.Should().Be(1);
        p1.ManaPool[ManaColor.White].Should().Be(1, "mana should be refunded");
    }

    [Fact]
    public async Task UndoCastSorcery_ReturnsCardFromGraveyardToHand()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        var replenish = GameCard.Create("Replenish", "Sorcery");
        p1.Hand.Add(replenish);
        p1.ManaPool.Add(ManaColor.White, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 3);

        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, replenish.Id));
        p1.Graveyard.Count.Should().Be(1);

        engine.UndoLastAction(p1.Id).Should().BeTrue();
        p1.Graveyard.Count.Should().Be(0);
        p1.Hand.Count.Should().Be(1);
        p1.ManaPool[ManaColor.White].Should().Be(1, "colored mana refunded");
        p1.ManaPool.Total.Should().Be(4, "all mana refunded");
    }
}
