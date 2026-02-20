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
    public async Task Ponder_ReorderAndKeep_DrawsTopCard()
    {
        // Library top-to-bottom: E, D, C, B, A
        var (state, player, handler) = CreateSetup("A", "B", "C", "D", "E");

        // Reorder: click D first (goes deepest), then E (middle), C auto-placed (top)
        // Top 3 peeked: E, D, C
        var top3 = player.Library.PeekTop(3).ToList(); // [C, D, E] — wait, PeekTop returns top first
        // Actually PeekTop(3) returns [E, D, C] (top to bottom within the 3)
        // Let me just use card IDs from the library
        var cardE = player.Library.Cards[4]; // top
        var cardD = player.Library.Cards[3];
        var cardC = player.Library.Cards[2];

        // Click D first (goes to library), then E (goes on top of D), C auto-placed on top
        handler.EnqueueCardChoice(cardD.Id); // pick 1 of 3: D goes deepest
        handler.EnqueueCardChoice(cardE.Id); // pick 2 of 3: E goes on top of D
        // C is auto-placed on top
        // Shuffle prompt: choose card = keep order
        handler.EnqueueCardChoice(Guid.NewGuid()); // any non-null = keep order

        var effect = new PonderEffect();
        var spell = CreateSpell(state);
        await effect.ResolveAsync(state, spell, handler);

        // Player draws C (the last card placed = top of library)
        player.Hand.Cards.Should().HaveCount(1);
        player.Hand.Cards[0].Name.Should().Be("C");

        // Library top-to-bottom: E, D, B, A (C was drawn)
        player.Library.Count.Should().Be(4);
    }

    [Fact]
    public async Task Ponder_KeepOriginalOrder_DrawsOriginalTop()
    {
        // Library top-to-bottom: E, D, C, B, A
        var (state, player, handler) = CreateSetup("A", "B", "C", "D", "E");

        var cardE = player.Library.Cards[4]; // top
        var cardD = player.Library.Cards[3];
        var cardC = player.Library.Cards[2];

        // Click in reverse order to restore original: C first, D second, E auto-placed on top
        handler.EnqueueCardChoice(cardC.Id); // C goes deepest
        handler.EnqueueCardChoice(cardD.Id); // D goes on top of C
        // E auto-placed on top (original order restored)
        handler.EnqueueCardChoice(Guid.NewGuid()); // keep order

        var effect = new PonderEffect();
        var spell = CreateSpell(state);
        await effect.ResolveAsync(state, spell, handler);

        // Player draws E (original top card)
        player.Hand.Cards.Should().HaveCount(1);
        player.Hand.Cards[0].Name.Should().Be("E");

        // Library: D, C, B, A
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

        var top3 = player.Library.PeekTop(3).ToList();

        // Click cards in any order (doesn't matter since we shuffle)
        handler.EnqueueCardChoice(top3[0].Id); // first pick
        handler.EnqueueCardChoice(top3[1].Id); // second pick
        // third auto-placed
        handler.EnqueueCardChoice(null); // skip = shuffle

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

        var cardY = player.Library.Cards[1]; // top
        var cardX = player.Library.Cards[0]; // bottom

        // Click X first (deepest), Y auto-placed on top
        handler.EnqueueCardChoice(cardX.Id);
        // Y auto-placed
        handler.EnqueueCardChoice(Guid.NewGuid()); // keep order

        var effect = new PonderEffect();
        var spell = CreateSpell(state);
        await effect.ResolveAsync(state, spell, handler);

        // Player draws Y (top)
        player.Hand.Cards.Should().HaveCount(1);
        player.Hand.Cards[0].Name.Should().Be("Y");
        player.Library.Count.Should().Be(1);
        player.Library.Cards[0].Name.Should().Be("X");
    }

    [Fact]
    public async Task Ponder_OneCard_AutoPlacedAndDrawn()
    {
        // Only 1 card — auto-placed, no clicking needed
        var (state, player, handler) = CreateSetup("OnlyCard");

        handler.EnqueueCardChoice(Guid.NewGuid()); // keep order (shuffle prompt)

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
        var top3 = player.Library.PeekTop(3).ToList();

        handler.EnqueueCardChoice(top3[0].Id);
        handler.EnqueueCardChoice(top3[1].Id);
        handler.EnqueueCardChoice(Guid.NewGuid()); // keep order

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
        var top3 = player.Library.PeekTop(3).ToList();

        handler.EnqueueCardChoice(top3[0].Id);
        handler.EnqueueCardChoice(top3[1].Id);
        handler.EnqueueCardChoice(null); // shuffle

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
        // Verify that reordering actually changes the library order
        // Library top-to-bottom: E, D, C, B, A
        var (state, player, handler) = CreateSetup("A", "B", "C", "D", "E");

        var cardE = player.Library.Cards[4]; // top
        var cardD = player.Library.Cards[3];
        var cardC = player.Library.Cards[2];

        // Put E deepest, C middle, D on top → draw D
        handler.EnqueueCardChoice(cardE.Id); // E goes deepest
        handler.EnqueueCardChoice(cardC.Id); // C on top of E
        // D auto-placed on top of C
        handler.EnqueueCardChoice(Guid.NewGuid()); // keep order

        var effect = new PonderEffect();
        var spell = CreateSpell(state);
        await effect.ResolveAsync(state, spell, handler);

        // Player draws D (the auto-placed top card)
        player.Hand.Cards[0].Name.Should().Be("D");

        // Library top-to-bottom: C, E, B, A
        player.Library.Cards[3].Name.Should().Be("C"); // new top
        player.Library.Cards[2].Name.Should().Be("E");
        player.Library.Cards[1].Name.Should().Be("B");
        player.Library.Cards[0].Name.Should().Be("A");
    }
}
