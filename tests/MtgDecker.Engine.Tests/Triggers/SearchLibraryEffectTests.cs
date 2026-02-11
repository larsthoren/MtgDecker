using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests.Triggers;

public class SearchLibraryEffectTests
{
    private (GameState state, Player player, TestDecisionHandler handler) CreateSetup()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Player 1", handler);
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        return (state, p1, handler);
    }

    [Fact]
    public async Task Execute_FindsMatchingCard_AddsToHand()
    {
        var (state, player, handler) = CreateSetup();
        var goblin = new GameCard { Name = "Goblin Piledriver", Subtypes = ["Goblin", "Warrior"] };
        var nonGoblin = new GameCard { Name = "Mountain", CardTypes = CardType.Land };
        player.Library.Add(nonGoblin);
        player.Library.Add(goblin);
        handler.EnqueueCardChoice(goblin.Id);

        var effect = new SearchLibraryEffect("Goblin");
        var source = new GameCard { Name = "Goblin Matron" };
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        player.Hand.Cards.Should().Contain(c => c.Id == goblin.Id);
        player.Library.Cards.Should().NotContain(c => c.Id == goblin.Id);
    }

    [Fact]
    public async Task Execute_ShufflesLibraryAfterSearch()
    {
        var (state, player, handler) = CreateSetup();
        // Add 20 cards so shuffle is detectable
        for (int i = 0; i < 20; i++)
            player.Library.Add(new GameCard { Name = $"Card {i}", Subtypes = i < 5 ? ["Goblin"] : [] });
        handler.EnqueueCardChoice(player.Library.Cards[19].Id); // pick last goblin

        var effect = new SearchLibraryEffect("Goblin");
        var context = new EffectContext(state, player, new GameCard { Name = "Matron" }, handler);

        await effect.Execute(context);

        // Library was shuffled — count should be 19 (one moved to hand)
        player.Library.Count.Should().Be(19);
        player.Hand.Count.Should().Be(1);
    }

    [Fact]
    public async Task Execute_NoMatchingCards_SkipsSearch()
    {
        var (state, player, handler) = CreateSetup();
        player.Library.Add(new GameCard { Name = "Mountain", CardTypes = CardType.Land, Subtypes = ["Mountain"] });

        var effect = new SearchLibraryEffect("Goblin");
        var context = new EffectContext(state, player, new GameCard { Name = "Matron" }, handler);

        await effect.Execute(context);

        player.Hand.Count.Should().Be(0);
    }

    [Fact]
    public async Task Execute_PlayerDeclinesOptional_NoCardAdded()
    {
        var (state, player, handler) = CreateSetup();
        player.Library.Add(new GameCard { Name = "Goblin Piledriver", Subtypes = ["Goblin"] });
        handler.EnqueueCardChoice(null); // Player declines

        var effect = new SearchLibraryEffect("Goblin", optional: true);
        var context = new EffectContext(state, player, new GameCard { Name = "Matron" }, handler);

        await effect.Execute(context);

        player.Hand.Count.Should().Be(0);
        player.Library.Count.Should().Be(1);
    }
}
