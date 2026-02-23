using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.AI;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests.AI;

public class AiBotSpellSequencingTests
{
    private static Player CreatePlayer()
    {
        return new Player(Guid.NewGuid(), "Bot", new AiBotDecisionHandler { ActionDelayMs = 0 });
    }

    [Fact]
    public void ChooseBestProactiveSpell_PrefersCheaperOnCurve()
    {
        // With 3 mana available, picks the 3-drop over 2-drop (highest CMC that fits)
        var piledriver = GameCard.Create("Goblin Piledriver"); // CMC 2: {1}{R}
        var warchief = GameCard.Create("Goblin Warchief");     // CMC 3: {1}{R}{R}

        var player = CreatePlayer();
        player.ManaPool.Add(ManaColor.Red, 3);

        var opponent = new Player(Guid.NewGuid(), "Opp", new AiBotDecisionHandler { ActionDelayMs = 0 });
        var state = new GameState(player, opponent);

        var hand = new List<GameCard> { piledriver, warchief };

        var result = AiBotDecisionHandler.ChooseBestProactiveSpell(hand, player, state, new List<GameCard>());

        result.Should().NotBeNull();
        result!.Name.Should().Be("Goblin Warchief");
    }

    [Fact]
    public void ChooseBestProactiveSpell_ExcludesCounterspells()
    {
        // Won't pick Daze (Counterspell role) when there's a proactive creature
        var daze = GameCard.Create("Daze");               // Counterspell role
        var delver = GameCard.Create("Delver of Secrets"); // Proactive creature

        var player = CreatePlayer();
        player.ManaPool.Add(ManaColor.Blue, 2);

        var opponent = new Player(Guid.NewGuid(), "Opp", new AiBotDecisionHandler { ActionDelayMs = 0 });
        var state = new GameState(player, opponent);

        var hand = new List<GameCard> { daze, delver };

        var result = AiBotDecisionHandler.ChooseBestProactiveSpell(hand, player, state, new List<GameCard>());

        result.Should().NotBeNull();
        result!.Name.Should().Be("Delver of Secrets");
    }

    [Fact]
    public void ChooseBestProactiveSpell_ExcludesInstantRemoval()
    {
        // Won't pick Lightning Bolt (InstantRemoval) when there's a proactive creature
        var bolt = GameCard.Create("Lightning Bolt");  // InstantRemoval role
        var lackey = GameCard.Create("Goblin Lackey"); // Proactive creature

        var player = CreatePlayer();
        player.ManaPool.Add(ManaColor.Red, 1);

        var opponent = new Player(Guid.NewGuid(), "Opp", new AiBotDecisionHandler { ActionDelayMs = 0 });
        var state = new GameState(player, opponent);

        var hand = new List<GameCard> { bolt, lackey };

        var result = AiBotDecisionHandler.ChooseBestProactiveSpell(hand, player, state, new List<GameCard>());

        result.Should().NotBeNull();
        result!.Name.Should().Be("Goblin Lackey");
    }

    [Fact]
    public void ChooseBestProactiveSpell_ReturnsNull_WhenOnlyReactiveSpells()
    {
        // Returns null if only counters/removal in hand
        var bolt = GameCard.Create("Lightning Bolt"); // InstantRemoval
        var daze = GameCard.Create("Daze");           // Counterspell

        var player = CreatePlayer();
        player.ManaPool.Add(ManaColor.Red, 1);
        player.ManaPool.Add(ManaColor.Blue, 2);

        var opponent = new Player(Guid.NewGuid(), "Opp", new AiBotDecisionHandler { ActionDelayMs = 0 });
        var state = new GameState(player, opponent);

        var hand = new List<GameCard> { bolt, daze };

        var result = AiBotDecisionHandler.ChooseBestProactiveSpell(hand, player, state, new List<GameCard>());

        result.Should().BeNull();
    }
}
