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
    public async Task Ponder_ReorderAndKeep_DrawsLastPlaced()
    {
        // Library top-to-bottom: E, D, C, B, A
        var (state, player, handler) = CreateSetup("A", "B", "C", "D", "E");

        // Place order: D first (deepest), E second, C last (top) → draws C
        handler.EnqueueReorder(cards =>
        {
            var d = cards.First(c => c.Name == "D");
            var e = cards.First(c => c.Name == "E");
            var c = cards.First(c => c.Name == "C");
            return new List<GameCard> { d, e, c };
        }, shuffle: false);

        var effect = new PonderEffect();
        var spell = CreateSpell(state);
        await effect.ResolveAsync(state, spell, handler);

        // Player draws C (last placed = top)
        player.Hand.Cards.Should().HaveCount(1);
        player.Hand.Cards[0].Name.Should().Be("C");
        player.Library.Count.Should().Be(4);
    }

    [Fact]
    public async Task Ponder_KeepOriginalOrder_DrawsOriginalTop()
    {
        // Library top-to-bottom: E, D, C, B, A
        var (state, player, handler) = CreateSetup("A", "B", "C", "D", "E");

        // Place in original order: C first (deepest), D second, E last (top)
        handler.EnqueueReorder(cards =>
        {
            var c = cards.First(x => x.Name == "C");
            var d = cards.First(x => x.Name == "D");
            var e = cards.First(x => x.Name == "E");
            return new List<GameCard> { c, d, e };
        }, shuffle: false);

        var effect = new PonderEffect();
        var spell = CreateSpell(state);
        await effect.ResolveAsync(state, spell, handler);

        // Player draws E (original top card)
        player.Hand.Cards.Should().HaveCount(1);
        player.Hand.Cards[0].Name.Should().Be("E");

        // Library: D, C, B, A (top-to-bottom)
        player.Library.Count.Should().Be(4);
        player.Library.Cards[3].Name.Should().Be("D"); // new top
        player.Library.Cards[2].Name.Should().Be("C");
        player.Library.Cards[1].Name.Should().Be("B");
        player.Library.Cards[0].Name.Should().Be("A");
    }

    [Fact]
    public async Task Ponder_Shuffle_DrawsAfterShuffle()
    {
        // 20 cards to make shuffle detection reliable
        var cardNames = Enumerable.Range(1, 20).Select(i => $"Card{i}").ToArray();
        var (state, player, handler) = CreateSetup(cardNames);

        // Keep original order but shuffle
        handler.EnqueueReorder(cards => cards.ToList(), shuffle: true);

        var effect = new PonderEffect();
        var spell = CreateSpell(state);
        await effect.ResolveAsync(state, spell, handler);

        // Player draws 1 card after shuffle
        player.Hand.Cards.Should().HaveCount(1);
        player.Library.Count.Should().Be(19);

        // All 20 cards accounted for
        var allCards = player.Library.Cards.Select(c => c.Name)
            .Append(player.Hand.Cards[0].Name)
            .OrderBy(n => n).ToList();
        allCards.Should().BeEquivalentTo(cardNames.OrderBy(n => n));
    }

    [Fact]
    public async Task Ponder_FewerThan3_TwoCards()
    {
        // Only 2 cards in library
        var (state, player, handler) = CreateSetup("X", "Y");

        // Place X first (deepest), Y last (top)
        handler.EnqueueReorder(cards =>
        {
            var x = cards.First(c => c.Name == "X");
            var y = cards.First(c => c.Name == "Y");
            return new List<GameCard> { x, y };
        }, shuffle: false);

        var effect = new PonderEffect();
        var spell = CreateSpell(state);
        await effect.ResolveAsync(state, spell, handler);

        // Player draws Y (last placed = top)
        player.Hand.Cards.Should().HaveCount(1);
        player.Hand.Cards[0].Name.Should().Be("Y");
        player.Library.Count.Should().Be(1);
        player.Library.Cards[0].Name.Should().Be("X");
    }

    [Fact]
    public async Task Ponder_OneCard_DrawsIt()
    {
        // Only 1 card — only option is to place it back and draw
        var (state, player, handler) = CreateSetup("OnlyCard");

        handler.EnqueueReorder(cards => cards.ToList(), shuffle: false);

        var effect = new PonderEffect();
        var spell = CreateSpell(state);
        await effect.ResolveAsync(state, spell, handler);

        player.Hand.Cards.Should().HaveCount(1);
        player.Hand.Cards[0].Name.Should().Be("OnlyCard");
        player.Library.Count.Should().Be(0);
    }

    [Fact]
    public async Task Ponder_EmptyLibrary_NoDraw()
    {
        var (state, player, handler) = CreateSetup();

        var effect = new PonderEffect();
        var spell = CreateSpell(state);
        await effect.ResolveAsync(state, spell, handler);

        player.Hand.Cards.Should().BeEmpty();
        player.Library.Count.Should().Be(0);
    }

    [Fact]
    public async Task Ponder_LogsKeepOrder()
    {
        var (state, player, handler) = CreateSetup("A", "B", "C", "D", "E");

        handler.EnqueueReorder(cards => cards.ToList(), shuffle: false);

        var effect = new PonderEffect();
        var spell = CreateSpell(state);
        await effect.ResolveAsync(state, spell, handler);

        state.GameLog.Should().Contain(entry => entry.Contains("puts cards back in chosen order"));
        state.GameLog.Should().Contain(entry => entry.Contains("keeps the card order"));
        state.GameLog.Should().Contain(entry => entry.Contains("draws a card"));
    }

    [Fact]
    public async Task Ponder_LogsShuffle()
    {
        var (state, player, handler) = CreateSetup("A", "B", "C", "D", "E");

        handler.EnqueueReorder(cards => cards.ToList(), shuffle: true);

        var effect = new PonderEffect();
        var spell = CreateSpell(state);
        await effect.ResolveAsync(state, spell, handler);

        state.GameLog.Should().Contain(entry => entry.Contains("puts cards back in chosen order"));
        state.GameLog.Should().Contain(entry => entry.Contains("shuffles their library"));
        state.GameLog.Should().Contain(entry => entry.Contains("draws a card"));
    }

    [Fact]
    public async Task Ponder_ReorderChangesLibraryOrder()
    {
        // Library top-to-bottom: E, D, C, B, A
        var (state, player, handler) = CreateSetup("A", "B", "C", "D", "E");

        // Place E first (deepest), C second, D last (top) → draw D
        handler.EnqueueReorder(cards =>
        {
            var e = cards.First(c => c.Name == "E");
            var c = cards.First(x => x.Name == "C");
            var d = cards.First(x => x.Name == "D");
            return new List<GameCard> { e, c, d };
        }, shuffle: false);

        var effect = new PonderEffect();
        var spell = CreateSpell(state);
        await effect.ResolveAsync(state, spell, handler);

        // Player draws D (last placed = top)
        player.Hand.Cards[0].Name.Should().Be("D");

        // Library top-to-bottom: C, E, B, A
        player.Library.Cards[3].Name.Should().Be("C"); // new top
        player.Library.Cards[2].Name.Should().Be("E");
        player.Library.Cards[1].Name.Should().Be("B");
        player.Library.Cards[0].Name.Should().Be("A");
    }
}
