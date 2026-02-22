using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class AlternateCostChoiceTests
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

        var deck1 = new DeckBuilder().AddLand("Island", 36).AddCard("Daze", 24, "Instant").Build();
        var deck2 = new DeckBuilder().AddLand("Mountain", 36).AddCard("Goblin Guide", 24, "Creature — Goblin").Build();

        foreach (var card in deck1) p1.Library.Add(card);
        foreach (var card in deck2) p2.Library.Add(card);

        state = new GameState(p1, p2);
        return new GameEngine(state);
    }

    [Fact]
    public async Task CastSpell_WithUseAlternateCostFlag_UsesAlternateCost()
    {
        var engine = CreateGame(out var state, out var p1Handler, out _);
        await engine.StartGameAsync();

        state.CurrentPhase = Phase.MainPhase1;

        // Give player an Island on battlefield and Daze in hand
        var island = GameCard.Create("Island", "Basic Land — Island");
        state.Player1.Battlefield.Add(island);
        var daze = state.Player1.Hand.Cards.First(c => c.Name == "Daze");

        // Need a spell on the stack to counter (Daze targets a spell)
        var targetSpell = GameCard.Create("Lightning Bolt", "Instant");
        state.StackPush(new StackObject(targetSpell, state.Player2.Id, new(), new(), 0));

        // Enqueue target choice for the counterspell
        p1Handler.EnqueueTarget(new TargetInfo(targetSpell.Id, state.Player2.Id, ZoneType.Stack));

        // Cast with UseAlternateCost flag — should return Island, not pay mana
        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, daze.Id, useAlternateCost: true));

        // Island should be returned to hand (alternate cost)
        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Id == island.Id);
        state.Player1.Hand.Cards.Should().Contain(c => c.Id == island.Id);
        // Daze should be on the stack
        state.Stack.OfType<StackObject>().Should().Contain(so => so.Card.Name == "Daze");
    }

    [Fact]
    public async Task CastSpell_WithoutFlag_PaysMana()
    {
        var engine = CreateGame(out var state, out var p1Handler, out _);
        await engine.StartGameAsync();

        state.CurrentPhase = Phase.MainPhase1;

        // Give player mana to pay {1}{U}
        state.Player1.ManaPool.Add(ManaColor.Blue, 1);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 1);

        // Put an Island on battlefield (so alternate cost is also available)
        var island = GameCard.Create("Island", "Basic Land — Island");
        state.Player1.Battlefield.Add(island);

        var daze = state.Player1.Hand.Cards.First(c => c.Name == "Daze");

        // Need a spell to target
        var targetSpell = GameCard.Create("Lightning Bolt", "Instant");
        state.StackPush(new StackObject(targetSpell, state.Player2.Id, new(), new(), 0));
        p1Handler.EnqueueTarget(new TargetInfo(targetSpell.Id, state.Player2.Id, ZoneType.Stack));

        // Cast without flag — should pay mana, Island stays on battlefield
        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, daze.Id));

        // Island should still be on battlefield (mana was paid, not alternate cost)
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == island.Id);
        // Mana pool should be drained
        state.Player1.ManaPool.Total.Should().Be(0);
    }
}
