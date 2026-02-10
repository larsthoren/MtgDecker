using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class GameEngineIntegrationTests
{
    private GameEngine CreateGame(
        out GameState state,
        out TestDecisionHandler p1Handler,
        out TestDecisionHandler p2Handler,
        int deckSize = 60)
    {
        p1Handler = new TestDecisionHandler();
        p2Handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Alice", p1Handler);
        var p2 = new Player(Guid.NewGuid(), "Bob", p2Handler);

        var deck1 = new DeckBuilder()
            .AddLand("Forest", deckSize / 3)
            .AddCard("Grizzly Bears", deckSize - deckSize / 3, "Creature — Bear")
            .Build();
        var deck2 = new DeckBuilder()
            .AddLand("Mountain", deckSize / 3)
            .AddCard("Goblin Guide", deckSize - deckSize / 3, "Creature — Goblin")
            .Build();

        foreach (var card in deck1) p1.Library.Add(card);
        foreach (var card in deck2) p2.Library.Add(card);

        state = new GameState(p1, p2);
        return new GameEngine(state);
    }

    [Fact]
    public async Task FullGame_StartToTwoTurns_CardCountsCorrect()
    {
        var engine = CreateGame(out var state, out _, out _);

        await engine.StartGameAsync();

        // Both players drew 7, library has 53
        state.Player1.Hand.Count.Should().Be(7);
        state.Player2.Hand.Count.Should().Be(7);
        state.Player1.Library.Count.Should().Be(53);
        state.Player2.Library.Count.Should().Be(53);
        state.ActivePlayer.Should().BeSameAs(state.Player1);

        // Turn 1: P1's turn, first turn = no draw
        state.IsFirstTurn = true;
        await engine.RunTurnAsync();

        state.Player1.Hand.Count.Should().Be(7, "first player skips draw on turn 1");
        state.Player1.Library.Count.Should().Be(53);
        state.ActivePlayer.Should().BeSameAs(state.Player2, "active player switches after turn");
        state.TurnNumber.Should().Be(2);

        // Turn 2: P2's turn, draws a card
        await engine.RunTurnAsync();

        state.Player2.Hand.Count.Should().Be(8, "P2 draws on turn 2");
        state.Player2.Library.Count.Should().Be(52);
        state.ActivePlayer.Should().BeSameAs(state.Player1);
        state.TurnNumber.Should().Be(3);
    }

    [Fact]
    public async Task Turn1_ActivePlayerPlaysCardFromHand()
    {
        var engine = CreateGame(out var state, out var p1Handler, out _);
        await engine.StartGameAsync();

        // Pick first card in hand to play
        var cardToPlay = state.Player1.Hand.Cards[0];
        p1Handler.EnqueueAction(GameAction.PlayCard(state.Player1.Id, cardToPlay.Id));

        state.IsFirstTurn = true;
        await engine.RunTurnAsync();

        state.Player1.Hand.Count.Should().Be(6, "played one card from hand of 7, no draw on turn 1");
        state.Player1.Battlefield.Count.Should().Be(1);
        state.Player1.Battlefield.Cards[0].Should().BeSameAs(cardToPlay);
    }

    [Fact]
    public async Task CardPersistsAcrossTurns_AndUntapsOnOwnersTurn()
    {
        var engine = CreateGame(out var state, out var p1Handler, out _);
        await engine.StartGameAsync();

        // P1 plays a card and taps it on turn 1
        var cardToPlay = state.Player1.Hand.Cards[0];
        p1Handler.EnqueueAction(GameAction.PlayCard(state.Player1.Id, cardToPlay.Id));
        p1Handler.EnqueueAction(GameAction.TapCard(state.Player1.Id, cardToPlay.Id));

        state.IsFirstTurn = true;
        await engine.RunTurnAsync();

        cardToPlay.IsTapped.Should().BeTrue("P1 tapped it during turn 1");
        state.Player1.Battlefield.Count.Should().Be(1);

        // Turn 2: P2's turn — P1's card stays tapped (not P1's untap step)
        await engine.RunTurnAsync();

        cardToPlay.IsTapped.Should().BeTrue("only active player's cards untap");

        // Turn 3: P1's turn — card should untap in untap step
        await engine.RunTurnAsync();

        cardToPlay.IsTapped.Should().BeFalse("P1's card untaps on P1's turn");
        state.Player1.Battlefield.Count.Should().Be(1, "card persists on battlefield");
    }

    [Fact]
    public async Task MulliganOnce_ThenPlayFirstTurn()
    {
        var engine = CreateGame(out var state, out var p1Handler, out _);

        // P1 mulligans once, P2 keeps
        p1Handler.EnqueueMulligan(MulliganDecision.Mulligan);

        await engine.StartGameAsync();

        state.Player1.Hand.Count.Should().Be(6, "mulliganed once, put 1 on bottom");
        state.Player2.Hand.Count.Should().Be(7, "P2 kept opening hand");
        (state.Player1.Hand.Count + state.Player1.Library.Count).Should().Be(60, "no cards lost");

        // Play a turn — should work fine after mulligan
        var cardToPlay = state.Player1.Hand.Cards[0];
        p1Handler.EnqueueAction(GameAction.PlayCard(state.Player1.Id, cardToPlay.Id));

        state.IsFirstTurn = true;
        await engine.RunTurnAsync();

        state.Player1.Hand.Count.Should().Be(5, "played 1 from hand of 6, no draw");
        state.Player1.Battlefield.Count.Should().Be(1);
    }

    [Fact]
    public async Task BothPlayersActInSameTurn()
    {
        var engine = CreateGame(out var state, out var p1Handler, out var p2Handler);
        await engine.StartGameAsync();

        // P1 plays a card, P2 also plays a card (during P1's turn)
        var p1Card = state.Player1.Hand.Cards[0];
        var p2Card = state.Player2.Hand.Cards[0];
        p1Handler.EnqueueAction(GameAction.PlayCard(state.Player1.Id, p1Card.Id));
        p2Handler.EnqueueAction(GameAction.PlayCard(state.Player2.Id, p2Card.Id));

        state.IsFirstTurn = true;
        await engine.RunTurnAsync();

        state.Player1.Battlefield.Count.Should().Be(1);
        state.Player2.Battlefield.Count.Should().Be(1);
        state.Player1.Hand.Count.Should().Be(6);
        state.Player2.Hand.Count.Should().Be(6);
    }

    [Fact]
    public async Task MultiTurn_BoardBuildsUp()
    {
        var engine = CreateGame(out var state, out var p1Handler, out var p2Handler);
        await engine.StartGameAsync();
        state.IsFirstTurn = true;

        // Turn 1 (P1): play a card
        var p1Card1 = state.Player1.Hand.Cards[0];
        p1Handler.EnqueueAction(GameAction.PlayCard(state.Player1.Id, p1Card1.Id));
        await engine.RunTurnAsync();
        state.Player1.Battlefield.Count.Should().Be(1);

        // Turn 2 (P2): play a card
        var p2Card1 = state.Player2.Hand.Cards[0];
        p2Handler.EnqueueAction(GameAction.PlayCard(state.Player2.Id, p2Card1.Id));
        await engine.RunTurnAsync();
        state.Player2.Battlefield.Count.Should().Be(1);

        // Turn 3 (P1): play another card (drew one in draw step)
        var p1Card2 = state.Player1.Hand.Cards[0];
        p1Handler.EnqueueAction(GameAction.PlayCard(state.Player1.Id, p1Card2.Id));
        await engine.RunTurnAsync();
        state.Player1.Battlefield.Count.Should().Be(2);

        // Turn 4 (P2): play another card
        var p2Card2 = state.Player2.Hand.Cards[0];
        p2Handler.EnqueueAction(GameAction.PlayCard(state.Player2.Id, p2Card2.Id));
        await engine.RunTurnAsync();
        state.Player2.Battlefield.Count.Should().Be(2);

        // All played cards should still be on their respective battlefields
        state.Player1.Battlefield.Cards.Should().Contain(p1Card1);
        state.Player1.Battlefield.Cards.Should().Contain(p1Card2);
        state.Player2.Battlefield.Cards.Should().Contain(p2Card1);
        state.Player2.Battlefield.Cards.Should().Contain(p2Card2);
    }

    [Fact]
    public async Task NonActivePlayer_ActsDuringOpponentsTurn()
    {
        var engine = CreateGame(out var state, out _, out var p2Handler);
        await engine.StartGameAsync();

        // During P1's turn, P2 plays a card from hand
        var p2Card = state.Player2.Hand.Cards[0];
        p2Handler.EnqueueAction(GameAction.PlayCard(state.Player2.Id, p2Card.Id));

        state.IsFirstTurn = true;
        await engine.RunTurnAsync();

        state.Player2.Battlefield.Count.Should().Be(1);
        state.Player2.Battlefield.Cards[0].Should().BeSameAs(p2Card);
        state.Player2.Hand.Count.Should().Be(6);
    }

    [Fact]
    public async Task PlayCard_TapIt_MoveToGraveyard_AllInOneTurn()
    {
        var engine = CreateGame(out var state, out var p1Handler, out _);
        await engine.StartGameAsync();

        var card = state.Player1.Hand.Cards[0];
        p1Handler.EnqueueAction(GameAction.PlayCard(state.Player1.Id, card.Id));
        p1Handler.EnqueueAction(GameAction.TapCard(state.Player1.Id, card.Id));
        p1Handler.EnqueueAction(
            GameAction.MoveCard(state.Player1.Id, card.Id, ZoneType.Battlefield, ZoneType.Graveyard));

        state.IsFirstTurn = true;
        await engine.RunTurnAsync();

        state.Player1.Hand.Count.Should().Be(6);
        state.Player1.Battlefield.Count.Should().Be(0);
        state.Player1.Graveyard.Count.Should().Be(1);
        state.Player1.Graveyard.Cards[0].Should().BeSameAs(card);
    }

    [Fact]
    public async Task GameLog_RecordsFullGameFlow()
    {
        var engine = CreateGame(out var state, out var p1Handler, out _);
        await engine.StartGameAsync();

        var card = state.Player1.Hand.Cards[0];
        p1Handler.EnqueueAction(GameAction.PlayCard(state.Player1.Id, card.Id));

        state.IsFirstTurn = true;
        await engine.RunTurnAsync();

        // Verify log contains key events in order
        state.GameLog.Should().Contain(msg => msg.Contains("keeps"));
        state.GameLog.Should().Contain(msg => msg.Contains("Game started"));
        state.GameLog.Should().Contain(msg => msg.Contains("Turn 1"));
        state.GameLog.Should().Contain(msg => msg.Contains("Untap"));
        state.GameLog.Should().Contain(msg => msg.Contains("plays"));

        // Log order: mulligan logs → game started → turn phases
        var gameStartIdx = state.GameLog.FindIndex(m => m.Contains("Game started"));
        var turnIdx = state.GameLog.FindIndex(m => m.Contains("Turn 1"));
        var playIdx = state.GameLog.FindIndex(m => m.Contains("plays"));
        gameStartIdx.Should().BeLessThan(turnIdx);
        turnIdx.Should().BeLessThan(playIdx);
    }

    [Fact]
    public async Task MultipleActionsInSamePriorityWindow()
    {
        var engine = CreateGame(out var state, out var p1Handler, out _);
        await engine.StartGameAsync();

        // P1 plays two cards in the same turn
        var card1 = state.Player1.Hand.Cards[0];
        var card2 = state.Player1.Hand.Cards[1];
        p1Handler.EnqueueAction(GameAction.PlayCard(state.Player1.Id, card1.Id));
        p1Handler.EnqueueAction(GameAction.PlayCard(state.Player1.Id, card2.Id));

        state.IsFirstTurn = true;
        await engine.RunTurnAsync();

        state.Player1.Battlefield.Count.Should().Be(2);
        state.Player1.Hand.Count.Should().Be(5);
    }

    [Fact]
    public async Task FiveTurnGame_VerifyAllCardCounts()
    {
        var engine = CreateGame(out var state, out _, out _);
        await engine.StartGameAsync();
        state.IsFirstTurn = true;

        // Turn 1 (P1): no draw (first turn)
        await engine.RunTurnAsync();
        state.Player1.Hand.Count.Should().Be(7);
        state.Player1.Library.Count.Should().Be(53);

        // Turn 2 (P2): draws
        await engine.RunTurnAsync();
        state.Player2.Hand.Count.Should().Be(8);
        state.Player2.Library.Count.Should().Be(52);

        // Turn 3 (P1): draws
        await engine.RunTurnAsync();
        state.Player1.Hand.Count.Should().Be(8);
        state.Player1.Library.Count.Should().Be(52);

        // Turn 4 (P2): draws
        await engine.RunTurnAsync();
        state.Player2.Hand.Count.Should().Be(9);
        state.Player2.Library.Count.Should().Be(51);

        // Turn 5 (P1): draws
        await engine.RunTurnAsync();
        state.Player1.Hand.Count.Should().Be(9);
        state.Player1.Library.Count.Should().Be(51);

        state.TurnNumber.Should().Be(6);
        state.ActivePlayer.Should().BeSameAs(state.Player2);
    }
}
