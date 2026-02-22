using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class PlayLandValidationTests
{
    private GameEngine CreateGame(
        out GameState state,
        out TestDecisionHandler p1Handler,
        out TestDecisionHandler p2Handler)
    {
        p1Handler = new TestDecisionHandler();
        p2Handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Alice", p1Handler);
        var p2 = new Player(Guid.NewGuid(), "Bob", p2Handler);

        var deck1 = new DeckBuilder().AddLand("Forest", 36).AddCard("Grizzly Bears", 24, "Creature — Bear").Build();
        var deck2 = new DeckBuilder().AddLand("Mountain", 36).AddCard("Goblin Guide", 24, "Creature — Goblin").Build();

        foreach (var card in deck1) p1.Library.Add(card);
        foreach (var card in deck2) p2.Library.Add(card);

        state = new GameState(p1, p2);
        return new GameEngine(state);
    }

    [Fact]
    public async Task PlayLand_OnOpponentsTurn_IsRejected()
    {
        var engine = CreateGame(out var state, out _, out var p2Handler);
        await engine.StartGameAsync();

        // It's P1's turn — P2 tries to play a land
        state.ActivePlayer.Should().BeSameAs(state.Player1);
        var p2Land = state.Player2.Hand.Cards.First(c => c.IsLand);
        var beforeCount = state.Player2.Battlefield.Cards.Count;

        p2Handler.EnqueueAction(GameAction.PlayLand(state.Player2.Id, p2Land.Id));
        state.CurrentPhase = Phase.MainPhase1;
        await engine.ExecuteAction(GameAction.PlayLand(state.Player2.Id, p2Land.Id));

        state.Player2.Battlefield.Cards.Count.Should().Be(beforeCount, "non-active player cannot play lands");
    }

    [Fact]
    public async Task PlayLand_DuringCombat_IsRejected()
    {
        var engine = CreateGame(out var state, out var p1Handler, out _);
        await engine.StartGameAsync();

        state.CurrentPhase = Phase.Combat;
        var p1Land = state.Player1.Hand.Cards.First(c => c.IsLand);
        var beforeCount = state.Player1.Battlefield.Cards.Count;

        await engine.ExecuteAction(GameAction.PlayLand(state.Player1.Id, p1Land.Id));

        state.Player1.Battlefield.Cards.Count.Should().Be(beforeCount, "cannot play lands during combat");
    }

    [Fact]
    public async Task PlayLand_WhenStackNotEmpty_IsRejected()
    {
        var engine = CreateGame(out var state, out _, out _);
        await engine.StartGameAsync();

        state.CurrentPhase = Phase.MainPhase1;
        // Push a dummy spell on the stack
        var dummyCard = GameCard.Create("Dummy", "Instant");
        state.StackPush(new StackObject(dummyCard, state.Player1.Id, new(), new(), 0));

        var p1Land = state.Player1.Hand.Cards.First(c => c.IsLand);
        var beforeCount = state.Player1.Battlefield.Cards.Count;

        await engine.ExecuteAction(GameAction.PlayLand(state.Player1.Id, p1Land.Id));

        state.Player1.Battlefield.Cards.Count.Should().Be(beforeCount, "cannot play lands while stack is non-empty");
    }
}
