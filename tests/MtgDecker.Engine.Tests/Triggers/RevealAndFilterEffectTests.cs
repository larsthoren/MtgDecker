using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests.Triggers;

public class RevealAndFilterEffectTests
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
    public async Task Execute_MatchingCardsGoToHand()
    {
        var (state, player, handler) = CreateSetup();
        // Library is bottom-first. Add bottom first, then top.
        player.Library.Add(new GameCard { Name = "Extra", Subtypes = [] });
        player.Library.Add(new GameCard { Name = "Mountain", CardTypes = CardType.Land, Subtypes = ["Mountain"] });
        player.Library.Add(new GameCard { Name = "Goblin Matron", Subtypes = ["Goblin"] });
        player.Library.Add(new GameCard { Name = "Lightning Bolt", Subtypes = [] });
        player.Library.Add(new GameCard { Name = "Goblin Piledriver", Subtypes = ["Goblin", "Warrior"] });

        var effect = new RevealAndFilterEffect(4, "Goblin");
        var context = new EffectContext(state, player, new GameCard { Name = "Ringleader" }, handler);

        await effect.Execute(context);

        player.Hand.Cards.Should().HaveCount(2);
        player.Hand.Cards.Select(c => c.Name).Should().Contain("Goblin Matron", "Goblin Piledriver");
        player.Library.Count.Should().Be(3); // 1 extra + 2 non-goblin to bottom
    }

    [Fact]
    public async Task Execute_NonMatchingGoToBottomOfLibrary()
    {
        var (state, player, handler) = CreateSetup();
        player.Library.Add(new GameCard { Name = "Bottom Card", Subtypes = [] });
        player.Library.Add(new GameCard { Name = "Non Goblin 1", Subtypes = [] });
        player.Library.Add(new GameCard { Name = "Non Goblin 2", Subtypes = [] });

        var effect = new RevealAndFilterEffect(2, "Goblin");
        var context = new EffectContext(state, player, new GameCard { Name = "Ringleader" }, handler);

        await effect.Execute(context);

        player.Hand.Count.Should().Be(0); // No goblins found
        player.Library.Count.Should().Be(3); // All returned to library
    }

    [Fact]
    public async Task Execute_LessThanNCardsInLibrary_RevealsWhatExists()
    {
        var (state, player, handler) = CreateSetup();
        player.Library.Add(new GameCard { Name = "Goblin Lackey", Subtypes = ["Goblin"] });

        var effect = new RevealAndFilterEffect(4, "Goblin");
        var context = new EffectContext(state, player, new GameCard { Name = "Ringleader" }, handler);

        await effect.Execute(context);

        player.Hand.Cards.Should().HaveCount(1);
        player.Hand.Cards[0].Name.Should().Be("Goblin Lackey");
        player.Library.Count.Should().Be(0);
    }

    [Fact]
    public async Task Execute_EmptyLibrary_DoesNothing()
    {
        var (state, player, handler) = CreateSetup();

        var effect = new RevealAndFilterEffect(4, "Goblin");
        var context = new EffectContext(state, player, new GameCard { Name = "Ringleader" }, handler);

        await effect.Execute(context);

        player.Hand.Count.Should().Be(0);
    }
}
