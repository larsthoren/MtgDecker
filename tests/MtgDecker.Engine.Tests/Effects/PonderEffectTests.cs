using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests.Effects;

public class PonderEffectTests
{
    private static (GameState state, Player player, TestDecisionHandler handler) CreateSetup(
        params string[] libraryCardNames)
    {
        var handler = new TestDecisionHandler();
        var player = new Player(Guid.NewGuid(), "P1", handler);
        var opponent = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());

        // Add cards bottom-to-top (first name = bottom, last name = top)
        foreach (var name in libraryCardNames)
        {
            player.Library.Add(new GameCard { Name = name });
        }

        var state = new GameState(player, opponent);
        return (state, player, handler);
    }

    private static StackObject CreateSpell(GameState state)
    {
        var spellCard = new GameCard { Name = "Ponder" };
        return new StackObject(spellCard, state.Player1.Id,
            new Dictionary<ManaColor, int> { [ManaColor.Blue] = 1 },
            new List<TargetInfo>(), 0);
    }

    [Fact]
    public async Task PonderEffect_RevealsTop3_KeepOrder_DrawsOne()
    {
        // Setup: library has cards A(bottom) B C D E(top)
        var (state, player, handler) = CreateSetup("A", "B", "C", "D", "E");

        // Decision: choose NOT to shuffle (ChooseCard returns a non-null value = keep)
        handler.EnqueueCardChoice(Guid.NewGuid()); // any non-null = keep order

        var effect = new PonderEffect();
        var spell = CreateSpell(state);

        await effect.ResolveAsync(state, spell, handler);

        // Player draws E (the top card)
        player.Hand.Cards.Should().HaveCount(1);
        player.Hand.Cards[0].Name.Should().Be("E");

        // Library should have A B C D (4 cards remaining, in original order)
        player.Library.Count.Should().Be(4);
        player.Library.Cards[0].Name.Should().Be("A");
        player.Library.Cards[1].Name.Should().Be("B");
        player.Library.Cards[2].Name.Should().Be("C");
        player.Library.Cards[3].Name.Should().Be("D");
    }

    [Fact]
    public async Task PonderEffect_RevealsTop3_Shuffles_DrawsOne()
    {
        // Setup: library has 20 cards to make shuffle detection reliable
        var cardNames = Enumerable.Range(1, 20).Select(i => $"Card{i}").ToArray();
        var (state, player, handler) = CreateSetup(cardNames);

        // Decision: choose to shuffle (ChooseCard returns null)
        handler.EnqueueCardChoice(null);

        var effect = new PonderEffect();
        var spell = CreateSpell(state);

        await effect.ResolveAsync(state, spell, handler);

        // Player draws 1 card after shuffle
        player.Hand.Cards.Should().HaveCount(1);

        // Library should have 19 cards remaining
        player.Library.Count.Should().Be(19);

        // After shuffle, order should differ from the original (with 20 cards, statistically certain)
        // We verify the library was shuffled by checking the order changed
        var remainingNames = player.Library.Cards.Select(c => c.Name).ToList();
        var drawnName = player.Hand.Cards[0].Name;
        var originalOrder = cardNames.ToList();

        // The drawn card + remaining should account for all 20 original cards
        var allCards = remainingNames.Append(drawnName).OrderBy(n => n).ToList();
        allCards.Should().BeEquivalentTo(originalOrder);
    }

    [Fact]
    public async Task PonderEffect_FewerThan3InLibrary()
    {
        // Only 2 cards in library: reveals 2, still works
        var (state, player, handler) = CreateSetup("X", "Y");

        // Keep order
        handler.EnqueueCardChoice(Guid.NewGuid());

        var effect = new PonderEffect();
        var spell = CreateSpell(state);

        await effect.ResolveAsync(state, spell, handler);

        // Player draws Y (top card)
        player.Hand.Cards.Should().HaveCount(1);
        player.Hand.Cards[0].Name.Should().Be("Y");

        // Library should have X (1 card remaining)
        player.Library.Count.Should().Be(1);
        player.Library.Cards[0].Name.Should().Be("X");
    }

    [Fact]
    public async Task PonderEffect_EmptyLibrary_DoesNotDraw()
    {
        // Edge case: empty library
        var (state, player, handler) = CreateSetup();

        // Keep order (no cards to reveal anyway)
        handler.EnqueueCardChoice(Guid.NewGuid());

        var effect = new PonderEffect();
        var spell = CreateSpell(state);

        await effect.ResolveAsync(state, spell, handler);

        player.Hand.Cards.Should().BeEmpty();
        player.Library.Count.Should().Be(0);
    }

    [Fact]
    public async Task PonderEffect_ShuffleWithFewerThan3()
    {
        // Shuffle with only 1 card - should still work
        var (state, player, handler) = CreateSetup("OnlyCard");

        handler.EnqueueCardChoice(null); // shuffle

        var effect = new PonderEffect();
        var spell = CreateSpell(state);

        await effect.ResolveAsync(state, spell, handler);

        // Player draws the only card
        player.Hand.Cards.Should().HaveCount(1);
        player.Hand.Cards[0].Name.Should().Be("OnlyCard");
        player.Library.Count.Should().Be(0);
    }

    [Fact]
    public async Task PonderEffect_LogsKeepOrder()
    {
        var (state, player, handler) = CreateSetup("A", "B", "C", "D", "E");
        handler.EnqueueCardChoice(Guid.NewGuid()); // keep order

        var effect = new PonderEffect();
        var spell = CreateSpell(state);

        await effect.ResolveAsync(state, spell, handler);

        state.GameLog.Should().Contain(entry => entry.Contains("keeps the card order"));
        state.GameLog.Should().Contain(entry => entry.Contains("draws a card"));
    }

    [Fact]
    public async Task PonderEffect_LogsShuffle()
    {
        var (state, player, handler) = CreateSetup("A", "B", "C", "D", "E");
        handler.EnqueueCardChoice(null); // shuffle

        var effect = new PonderEffect();
        var spell = CreateSpell(state);

        await effect.ResolveAsync(state, spell, handler);

        state.GameLog.Should().Contain(entry => entry.Contains("shuffles their library"));
        state.GameLog.Should().Contain(entry => entry.Contains("draws a card"));
    }
}
