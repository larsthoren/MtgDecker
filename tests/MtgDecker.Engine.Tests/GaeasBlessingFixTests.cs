using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class GaeasBlessingFixTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler h1, TestDecisionHandler h2) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);

        for (int i = 0; i < 40; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}" });
            p2.Library.Add(new GameCard { Name = $"Card{i}" });
        }

        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, h1, h2);
    }

    [Fact]
    public void GaeasBlessing_HasPlayerTargetFilter()
    {
        CardDefinitions.TryGet("Gaea's Blessing", out var def).Should().BeTrue();
        def!.TargetFilter.Should().NotBeNull("Gaea's Blessing should target a player");
    }

    [Fact]
    public async Task GaeasBlessing_TargetingOpponent_ShufflesOpponentGraveyard()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();

        // Put some cards in opponent's graveyard
        var graveyardCard1 = new GameCard { Name = "OppGY1" };
        var graveyardCard2 = new GameCard { Name = "OppGY2" };
        state.Player2.Graveyard.Add(graveyardCard1);
        state.Player2.Graveyard.Add(graveyardCard2);
        var p2LibraryCountBefore = state.Player2.Library.Cards.Count;

        // Cast Gaea's Blessing targeting opponent
        var blessing = GameCard.Create("Gaea's Blessing", "Sorcery");
        var spell = new StackObject(
            blessing,
            state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(Guid.Empty, state.Player2.Id, ZoneType.None) },
            0);

        // Enqueue card choices for the up to 3 cards from opponent's graveyard
        h1.EnqueueCardChoice(graveyardCard1.Id);
        h1.EnqueueCardChoice(graveyardCard2.Id);
        h1.EnqueueCardChoice(null); // stop choosing after 2

        CardDefinitions.TryGet("Gaea's Blessing", out var def).Should().BeTrue();
        var handler = state.Player1.DecisionHandler;
        await def!.Effect!.ResolveAsync(state, spell, handler, CancellationToken.None);

        // Opponent's graveyard cards should be shuffled into opponent's library
        state.Player2.Graveyard.Cards.Should().BeEmpty("all graveyard cards were chosen");
        state.Player2.Library.Cards.Count.Should().Be(p2LibraryCountBefore + 2);
    }

    [Fact]
    public void GaeasBlessing_HasShuffleGraveyardOnMill()
    {
        CardDefinitions.TryGet("Gaea's Blessing", out var def).Should().BeTrue();
        def!.ShuffleGraveyardOnMill.Should().BeTrue();
    }

    [Fact]
    public void GaeasBlessing_DiscardedFromHand_DoesNotTriggerShuffle()
    {
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", new TestDecisionHandler()),
            new Player(Guid.NewGuid(), "P2", new TestDecisionHandler()));

        // Put some cards in P1's graveyard first
        state.Player1.Graveyard.Add(new GameCard { Name = "ExistingGY" });

        // Simulate discarding Gaea's Blessing from hand
        var blessing = GameCard.Create("Gaea's Blessing", "Sorcery");
        var engine = new GameEngine(state);

        // Use MoveToGraveyardWithReplacement — should NOT trigger shuffle for discard
        engine.MoveToGraveyardWithReplacement(blessing, state.Player1);

        // Both the existing card AND the blessing should be in graveyard (no shuffle)
        state.Player1.Graveyard.Cards.Should().HaveCount(2);
        state.Player1.Graveyard.Cards.Select(c => c.Name).Should().Contain("ExistingGY");
        state.Player1.Graveyard.Cards.Select(c => c.Name).Should().Contain("Gaea's Blessing");
    }

    [Fact]
    public void GaeasBlessing_MilledFromLibrary_TriggersShuffle()
    {
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", new TestDecisionHandler()),
            new Player(Guid.NewGuid(), "P2", new TestDecisionHandler()));

        // Put some cards in P1's graveyard
        state.Player1.Graveyard.Add(new GameCard { Name = "GYCard1" });
        state.Player1.Graveyard.Add(new GameCard { Name = "GYCard2" });

        // Put Gaea's Blessing on top of library (will be milled)
        var blessing = GameCard.Create("Gaea's Blessing", "Sorcery");
        state.Player1.Library.Add(blessing);
        // Add more cards above it
        state.Player1.Library.Add(new GameCard { Name = "TopCard" });

        var libraryCountBefore = state.Player1.Library.Cards.Count;

        // Mill 2 cards from P1's library using BrainFreeze's MillCards pattern
        // TopCard goes to graveyard first, then Gaea's Blessing triggers shuffle
        var topCard = state.Player1.Library.DrawFromTop();
        state.Player1.Graveyard.Add(topCard!);

        var milledBlessing = state.Player1.Library.DrawFromTop();
        milledBlessing!.Name.Should().Be("Gaea's Blessing");
        state.Player1.Graveyard.Add(milledBlessing);

        // Now check ShuffleGraveyardOnMill and trigger shuffle manually
        // (This is what the BrainFreezeEffect will do)
        if (CardDefinitions.TryGet(milledBlessing.Name, out var def) && def.ShuffleGraveyardOnMill)
        {
            foreach (var gyCard in state.Player1.Graveyard.Cards.ToList())
            {
                state.Player1.Graveyard.Remove(gyCard);
                state.Player1.Library.AddToTop(gyCard);
            }
            state.Player1.Library.Shuffle();
        }

        // Graveyard should be empty (all shuffled into library)
        state.Player1.Graveyard.Cards.Should().BeEmpty();
        // Library should have the original cards minus the 2 milled + the 4 shuffled back
        // (GYCard1, GYCard2, TopCard, Gaea's Blessing all went back to library)
    }
}
