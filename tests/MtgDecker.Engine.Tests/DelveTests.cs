using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class DelveTests
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

        var deck1 = new DeckBuilder().AddLand("Island", 36).AddCard("Murktide Regent", 24, "Creature — Dragon").Build();
        var deck2 = new DeckBuilder().AddLand("Mountain", 36).AddCard("Goblin Guide", 24, "Creature — Goblin").Build();

        foreach (var card in deck1) p1.Library.Add(card);
        foreach (var card in deck2) p2.Library.Add(card);

        state = new GameState(p1, p2);
        return new GameEngine(state);
    }

    [Fact]
    public async Task Delve_ExileGraveyardCards_ReducesGenericCost()
    {
        var engine = CreateGame(out var state, out var p1Handler, out _);
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        // Murktide costs {5}{U}{U} — exile 5 cards to reduce to {U}{U}
        var murktide = state.Player1.Hand.Cards.First(c => c.Name == "Murktide Regent");

        // Put 5 cards in graveyard
        for (int i = 0; i < 5; i++)
            state.Player1.Graveyard.Add(GameCard.Create($"Filler{i}", "Instant"));

        // Give just {U}{U} mana (relies on Delve to cover the {5})
        state.Player1.ManaPool.Add(ManaColor.Blue, 2);

        // Enqueue exile choice: exile all 5
        p1Handler.EnqueueExileChoice((cards, max) => cards.Take(max).ToList());

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, murktide.Id));

        state.Stack.OfType<StackObject>().Should().Contain(so => so.Card.Name == "Murktide Regent");
        state.Player1.Exile.Cards.Count.Should().Be(5, "5 graveyard cards exiled for Delve");
        state.Player1.Graveyard.Count.Should().Be(0, "all graveyard cards were exiled");
    }

    [Fact]
    public async Task Delve_PartialExile_PaysRemainingWithMana()
    {
        var engine = CreateGame(out var state, out var p1Handler, out _);
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var murktide = state.Player1.Hand.Cards.First(c => c.Name == "Murktide Regent");

        // Put 3 cards in graveyard (can only exile 3 of the needed 5)
        for (int i = 0; i < 3; i++)
            state.Player1.Graveyard.Add(GameCard.Create($"Filler{i}", "Instant"));

        // Give {2}{U}{U} — covers remaining 2 generic after exiling 3
        state.Player1.ManaPool.Add(ManaColor.Blue, 2);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 2);

        p1Handler.EnqueueExileChoice((cards, max) => cards.Take(max).ToList());

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, murktide.Id));

        state.Stack.OfType<StackObject>().Should().Contain(so => so.Card.Name == "Murktide Regent");
        state.Player1.Exile.Cards.Count.Should().Be(3);
        state.Player1.ManaPool.Total.Should().Be(0, "remaining 2 generic paid from mana pool");
    }

    [Fact]
    public async Task Delve_ZeroExile_PaysFullMana()
    {
        var engine = CreateGame(out var state, out var p1Handler, out _);
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var murktide = state.Player1.Hand.Cards.First(c => c.Name == "Murktide Regent");

        // No graveyard cards — must pay full {5}{U}{U}
        state.Player1.ManaPool.Add(ManaColor.Blue, 2);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 5);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, murktide.Id));

        state.Stack.OfType<StackObject>().Should().Contain(so => so.Card.Name == "Murktide Regent");
        state.Player1.ManaPool.Total.Should().Be(0);
    }
}
