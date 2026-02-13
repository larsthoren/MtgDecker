using FluentAssertions;
using MtgDecker.Engine.Simulation;

namespace MtgDecker.Engine.Tests.Simulation;

public class GoblinsVsEnchantressTests
{
    private static List<GameCard> CreateGoblinDeck()
    {
        var deck = new List<GameCard>();

        // Lands (24)
        for (int i = 0; i < 14; i++)
            deck.Add(GameCard.Create("Mountain", "Basic Land — Mountain"));
        for (int i = 0; i < 4; i++)
            deck.Add(GameCard.Create("Wooded Foothills"));
        for (int i = 0; i < 2; i++)
            deck.Add(GameCard.Create("Rishadan Port"));
        for (int i = 0; i < 2; i++)
            deck.Add(GameCard.Create("Wasteland"));
        for (int i = 0; i < 2; i++)
            deck.Add(GameCard.Create("Karplusan Forest"));

        // Creatures (32)
        for (int i = 0; i < 4; i++) deck.Add(GameCard.Create("Goblin Lackey"));
        for (int i = 0; i < 4; i++) deck.Add(GameCard.Create("Goblin Piledriver"));
        for (int i = 0; i < 4; i++) deck.Add(GameCard.Create("Goblin Warchief"));
        for (int i = 0; i < 4; i++) deck.Add(GameCard.Create("Goblin Matron"));
        for (int i = 0; i < 4; i++) deck.Add(GameCard.Create("Goblin Ringleader"));
        for (int i = 0; i < 4; i++) deck.Add(GameCard.Create("Mogg Fanatic"));
        for (int i = 0; i < 3; i++) deck.Add(GameCard.Create("Gempalm Incinerator"));
        for (int i = 0; i < 2; i++) deck.Add(GameCard.Create("Siege-Gang Commander"));
        for (int i = 0; i < 1; i++) deck.Add(GameCard.Create("Goblin King"));
        for (int i = 0; i < 2; i++) deck.Add(GameCard.Create("Skirk Prospector"));

        // Spells (4)
        for (int i = 0; i < 4; i++) deck.Add(GameCard.Create("Naturalize"));

        return deck;
    }

    private static List<GameCard> CreateEnchantressDeck()
    {
        var deck = new List<GameCard>();

        // Lands (22)
        for (int i = 0; i < 6; i++)
            deck.Add(GameCard.Create("Plains", "Basic Land — Plains"));
        for (int i = 0; i < 4; i++)
            deck.Add(GameCard.Create("Forest", "Basic Land — Forest"));
        for (int i = 0; i < 4; i++)
            deck.Add(GameCard.Create("Brushland"));
        for (int i = 0; i < 4; i++)
            deck.Add(GameCard.Create("Windswept Heath"));
        for (int i = 0; i < 2; i++)
            deck.Add(GameCard.Create("Serra's Sanctum"));

        // Not included in lands count - Serra's Sanctum is legendary, second copy tests legendary rule

        // Creatures (4)
        for (int i = 0; i < 4; i++) deck.Add(GameCard.Create("Argothian Enchantress"));

        // Enchantments (30)
        for (int i = 0; i < 4; i++) deck.Add(GameCard.Create("Enchantress's Presence"));
        for (int i = 0; i < 4; i++) deck.Add(GameCard.Create("Wild Growth"));
        for (int i = 0; i < 3; i++) deck.Add(GameCard.Create("Exploration"));
        for (int i = 0; i < 2; i++) deck.Add(GameCard.Create("Mirri's Guile"));
        for (int i = 0; i < 2; i++) deck.Add(GameCard.Create("Sterling Grove"));
        for (int i = 0; i < 2; i++) deck.Add(GameCard.Create("Opalescence"));
        for (int i = 0; i < 2; i++) deck.Add(GameCard.Create("Parallax Wave"));
        for (int i = 0; i < 2; i++) deck.Add(GameCard.Create("Aura of Silence"));
        for (int i = 0; i < 2; i++) deck.Add(GameCard.Create("Seal of Cleansing"));
        for (int i = 0; i < 2; i++) deck.Add(GameCard.Create("Solitary Confinement"));
        for (int i = 0; i < 1; i++) deck.Add(GameCard.Create("Sylvan Library"));

        // Spells (6)
        for (int i = 0; i < 4; i++) deck.Add(GameCard.Create("Swords to Plowshares"));
        for (int i = 0; i < 2; i++) deck.Add(GameCard.Create("Replenish"));

        // Pad to 60
        while (deck.Count < 60)
            deck.Add(GameCard.Create("Plains", "Basic Land — Plains"));

        return deck;
    }

    [Fact]
    public async Task SingleGame_CompletesWithoutErrors()
    {
        var runner = new SimulationRunner();
        var result = await runner.RunGameAsync(
            CreateGoblinDeck(), CreateEnchantressDeck(),
            "Goblins", "Enchantress");

        result.TotalTurns.Should().BeGreaterThan(1);
        result.GameLog.Should().NotBeEmpty();
        (result.WinnerName != null || result.IsDraw).Should().BeTrue();
    }

    [Fact]
    public async Task SingleGame_FeaturesNewMechanics()
    {
        var runner = new SimulationRunner();
        var result = await runner.RunGameAsync(
            CreateGoblinDeck(), CreateEnchantressDeck(),
            "Goblins", "Enchantress");

        // Game log should reference at least some of the new mechanics
        var log = string.Join("\n", result.GameLog);

        // At least one of these features should appear in a full game
        var features = new[]
        {
            "enchantment cast", // Enchantress triggers
            "Sterling Grove",   // Shroud grant
            "Opalescence",      // Type-changing
            "Parallax Wave",    // Exile
            "Solitary Confinement", // Player protection
            "Aura of Silence",  // Cost modification
            "searches library", // Tutoring
            "attacks",          // Combat
            "Goblin Warchief",  // Haste grant
            "fade counter",     // Counter system
        };

        features.Count(f => log.Contains(f, StringComparison.OrdinalIgnoreCase))
            .Should().BeGreaterThanOrEqualTo(2,
                "a full game should exercise at least a couple of mechanics");
    }

    [Fact]
    public async Task BatchRun_TenGames_ProducesReasonableStats()
    {
        var runner = new SimulationRunner();
        var batch = await runner.RunBatchAsync(
            CreateGoblinDeck(), CreateEnchantressDeck(),
            10, "Goblins", "Enchantress");

        batch.TotalGames.Should().Be(10);
        batch.Games.Should().OnlyContain(g => g.TotalTurns > 0);
        batch.AverageGameLength.Should().BeGreaterThan(0);

        // At least some games should have a decisive winner (not all draws)
        batch.Draws.Should().BeLessThan(10, "not all games should be draws");
    }
}
