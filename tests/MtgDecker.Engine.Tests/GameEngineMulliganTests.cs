using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class GameEngineMulliganTests
{
    private GameEngine CreateEngineWithDecks(out GameState state, out TestDecisionHandler p1Handler, out TestDecisionHandler p2Handler, int deckSize = 60)
    {
        p1Handler = new TestDecisionHandler();
        p2Handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Alice", p1Handler);
        var p2 = new Player(Guid.NewGuid(), "Bob", p2Handler);

        var deck1 = new DeckBuilder().AddLand("Forest", deckSize / 3).AddCard("Bear", deckSize - deckSize / 3, "Creature — Bear").Build();
        var deck2 = new DeckBuilder().AddLand("Mountain", deckSize / 3).AddCard("Goblin", deckSize - deckSize / 3, "Creature — Goblin").Build();
        foreach (var card in deck1) p1.Library.Add(card);
        foreach (var card in deck2) p2.Library.Add(card);

        state = new GameState(p1, p2);
        return new GameEngine(state);
    }

    [Fact]
    public async Task RunMulliganAsync_KeepImmediately_HandIs7()
    {
        var engine = CreateEngineWithDecks(out var state, out _, out _);

        await engine.RunMulliganAsync(state.Player1);

        state.Player1.Hand.Count.Should().Be(7);
        state.Player1.Library.Count.Should().Be(53);
    }

    [Fact]
    public async Task RunMulliganAsync_MulliganOnce_HandIs6()
    {
        var engine = CreateEngineWithDecks(out var state, out var p1Handler, out _);
        p1Handler.EnqueueMulligan(MulliganDecision.Mulligan);

        await engine.RunMulliganAsync(state.Player1);

        state.Player1.Hand.Count.Should().Be(6);
        state.Player1.Library.Count.Should().Be(54);
    }

    [Fact]
    public async Task RunMulliganAsync_MulliganTwice_HandIs5()
    {
        var engine = CreateEngineWithDecks(out var state, out var p1Handler, out _);
        p1Handler.EnqueueMulligan(MulliganDecision.Mulligan);
        p1Handler.EnqueueMulligan(MulliganDecision.Mulligan);

        await engine.RunMulliganAsync(state.Player1);

        state.Player1.Hand.Count.Should().Be(5);
        state.Player1.Library.Count.Should().Be(55);
    }

    [Fact]
    public async Task RunMulliganAsync_MulliganOnce_CardsReturnedToLibraryBeforeRedraw()
    {
        var engine = CreateEngineWithDecks(out var state, out var p1Handler, out _, deckSize: 60);
        p1Handler.EnqueueMulligan(MulliganDecision.Mulligan);

        await engine.RunMulliganAsync(state.Player1);

        (state.Player1.Hand.Count + state.Player1.Library.Count).Should().Be(60);
    }

    [Fact]
    public async Task RunMulliganAsync_BottomChoice_PutsSelectedCardsOnBottom()
    {
        var engine = CreateEngineWithDecks(out var state, out var p1Handler, out _);
        p1Handler.EnqueueMulligan(MulliganDecision.Mulligan);
        p1Handler.EnqueueBottomChoice((hand, count) => hand.TakeLast(count).ToList());

        await engine.RunMulliganAsync(state.Player1);

        state.Player1.Hand.Count.Should().Be(6);
        state.Player1.Library.Count.Should().Be(54);
    }

    [Fact]
    public async Task RunMulliganAsync_LogsResult()
    {
        var engine = CreateEngineWithDecks(out var state, out _, out _);

        await engine.RunMulliganAsync(state.Player1);

        state.GameLog.Should().Contain(msg => msg.Contains("Alice") && msg.Contains("keeps"));
    }

    [Fact]
    public async Task RunMulliganAsync_MulliganSevenTimes_HandIsZero()
    {
        var engine = CreateEngineWithDecks(out var state, out var p1Handler, out _);
        for (int i = 0; i < 7; i++)
            p1Handler.EnqueueMulligan(MulliganDecision.Mulligan);

        await engine.RunMulliganAsync(state.Player1);

        state.Player1.Hand.Count.Should().Be(0);
        state.GameLog.Should().Contain(msg => msg.Contains("mulliganed to 0"));
    }
}
