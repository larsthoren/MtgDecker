using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class SurveilEffectTests
{
    private static (EffectContext context, Player player, GameState state, TestDecisionHandler handler)
        CreateContext()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var source = new GameCard { Name = "Surveyor" };
        var context = new EffectContext(state, p1, source, h1);
        return (context, p1, state, h1);
    }

    [Fact]
    public async Task Surveil1_ChooseGraveyard_CardGoesToGraveyard()
    {
        var (context, player, state, handler) = CreateContext();

        var topCard = new GameCard { Name = "Top Card" };
        player.Library.AddToTop(topCard);

        // Choose the card = put to graveyard
        handler.EnqueueCardChoice(topCard.Id);

        var effect = new SurveilEffect(1);
        await effect.Execute(context);

        player.Graveyard.Cards.Should().Contain(c => c.Name == "Top Card");
        player.Library.Cards.Should().NotContain(c => c.Name == "Top Card");
    }

    [Fact]
    public async Task Surveil1_ChooseKeep_CardStaysOnTop()
    {
        var (context, player, state, handler) = CreateContext();

        var topCard = new GameCard { Name = "Top Card" };
        player.Library.AddToTop(topCard);

        // Decline = keep on top
        handler.EnqueueCardChoice(null);

        var effect = new SurveilEffect(1);
        await effect.Execute(context);

        player.Library.Cards.Should().Contain(c => c.Name == "Top Card");
        player.Graveyard.Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task Surveil2_ChooseOneToGraveyard_OneStaysOnTop()
    {
        var (context, player, state, handler) = CreateContext();

        var card1 = new GameCard { Name = "Card A" };
        var card2 = new GameCard { Name = "Card B" };
        // AddToTop appends to end (top), so add card2 first, then card1
        // PeekTop returns from top down: [card1, card2]
        player.Library.AddToTop(card2);
        player.Library.AddToTop(card1);

        // First card presented: card1 -> graveyard
        handler.EnqueueCardChoice(card1.Id);
        // Second card presented: card2 -> keep (null)
        handler.EnqueueCardChoice(null);

        var effect = new SurveilEffect(2);
        await effect.Execute(context);

        player.Graveyard.Cards.Should().Contain(c => c.Name == "Card A");
        player.Library.Cards.Should().Contain(c => c.Name == "Card B");
        player.Library.Cards.Should().NotContain(c => c.Name == "Card A");
    }

    [Fact]
    public async Task Surveil2_BothToGraveyard()
    {
        var (context, player, state, handler) = CreateContext();

        var card1 = new GameCard { Name = "Card A" };
        var card2 = new GameCard { Name = "Card B" };
        player.Library.AddToTop(card2);
        player.Library.AddToTop(card1);

        handler.EnqueueCardChoice(card1.Id);
        handler.EnqueueCardChoice(card2.Id);

        var effect = new SurveilEffect(2);
        await effect.Execute(context);

        player.Graveyard.Cards.Should().HaveCount(2);
        player.Library.Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task Surveil2_BothKeep()
    {
        var (context, player, state, handler) = CreateContext();

        var card1 = new GameCard { Name = "Card A" };
        var card2 = new GameCard { Name = "Card B" };
        player.Library.AddToTop(card2);
        player.Library.AddToTop(card1);

        handler.EnqueueCardChoice(null);
        handler.EnqueueCardChoice(null);

        var effect = new SurveilEffect(2);
        await effect.Execute(context);

        player.Graveyard.Cards.Should().BeEmpty();
        player.Library.Cards.Should().HaveCount(2);
    }

    [Fact]
    public async Task Surveil_EmptyLibrary_DoesNothing()
    {
        var (context, player, state, handler) = CreateContext();

        // Library is empty - no choices needed

        var effect = new SurveilEffect(2);
        await effect.Execute(context);

        player.Graveyard.Cards.Should().BeEmpty();
        player.Library.Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task Surveil3_OnlyOneCardInLibrary_SurverilsAvailable()
    {
        var (context, player, state, handler) = CreateContext();

        var topCard = new GameCard { Name = "Only Card" };
        player.Library.AddToTop(topCard);

        // Surveil 3 but only 1 card available; choose to graveyard
        handler.EnqueueCardChoice(topCard.Id);

        var effect = new SurveilEffect(3);
        await effect.Execute(context);

        player.Graveyard.Cards.Should().ContainSingle(c => c.Name == "Only Card");
        player.Library.Cards.Should().BeEmpty();
    }
}
