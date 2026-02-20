using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class IntuitionTests
{
    [Fact]
    public async Task Intuition_CasterSearches3_OpponentPicks1ForHand()
    {
        var (state, h1, h2) = TestHelper.CreateStateWithHandlers();

        var card1 = GameCard.Create("Card A");
        var card2 = GameCard.Create("Card B");
        var card3 = GameCard.Create("Card C");
        state.Player1.Library.AddToTop(card1);
        state.Player1.Library.AddToTop(card2);
        state.Player1.Library.AddToTop(card3);

        // Caster picks all 3
        h1.EnqueueCardChoice(card1.Id);
        h1.EnqueueCardChoice(card2.Id);
        h1.EnqueueCardChoice(card3.Id);

        // Opponent picks card2 for hand
        h2.EnqueueCardChoice(card2.Id);

        var intuition = GameCard.Create("Intuition");
        var spell = new StackObject(intuition, state.Player1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);

        await new IntuitionEffect().ResolveAsync(state, spell, h1);

        state.Player1.Hand.Cards.Should().Contain(c => c.Name == "Card B");
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Name == "Card A");
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Name == "Card C");
        state.Player1.Library.Cards.Should().NotContain(c => c.Name == "Card A");
        state.Player1.Library.Cards.Should().NotContain(c => c.Name == "Card B");
        state.Player1.Library.Cards.Should().NotContain(c => c.Name == "Card C");
    }

    [Fact]
    public async Task Intuition_EmptyLibrary_FindsNothing()
    {
        var (state, h1, h2) = TestHelper.CreateStateWithHandlers();
        // Library is empty

        var intuition = GameCard.Create("Intuition");
        var spell = new StackObject(intuition, state.Player1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);

        await new IntuitionEffect().ResolveAsync(state, spell, h1);

        state.Player1.Hand.Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task Intuition_FewerThan3CardsInLibrary_WorksWithAvailable()
    {
        var (state, h1, h2) = TestHelper.CreateStateWithHandlers();

        var card1 = GameCard.Create("Card A");
        state.Player1.Library.AddToTop(card1);

        h1.EnqueueCardChoice(card1.Id);
        // Only 1 card available — goes to hand automatically

        var intuition = GameCard.Create("Intuition");
        var spell = new StackObject(intuition, state.Player1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);

        await new IntuitionEffect().ResolveAsync(state, spell, h1);

        state.Player1.Hand.Cards.Should().Contain(c => c.Name == "Card A");
    }
}
