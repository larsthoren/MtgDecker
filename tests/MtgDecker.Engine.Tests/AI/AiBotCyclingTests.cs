using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.AI;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests.AI;

public class AiBotCyclingTests
{
    private static (AiBotDecisionHandler bot, GameState state, Player player) CreateGameWithBot()
    {
        var bot = new AiBotDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Bot", bot);
        var p2 = new Player(Guid.NewGuid(), "Opponent", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        return (bot, state, p1);
    }

    [Fact]
    public async Task AiBot_Prefers_Casting_Over_Cycling_When_Affordable()
    {
        var (bot, state, player) = CreateGameWithBot();

        // Gempalm Incinerator: cast cost {1}{R}, cycling cost {1}{R}
        var gempalm = GameCard.Create("Gempalm Incinerator");
        player.Hand.Add(gempalm);

        // Give enough mana to cast AND cycle — bot should prefer casting
        player.ManaPool.Add(ManaColor.Red, 2);
        player.ManaPool.Add(ManaColor.Colorless, 2);
        player.LandsPlayedThisTurn = 1;

        var action = await bot.GetAction(state, player.Id);

        // When bot can afford to cast, it should prefer casting the creature
        action.Type.Should().Be(ActionType.PlayCard);
        action.CardId.Should().Be(gempalm.Id);
    }

    [Fact]
    public async Task AiBot_Casts_When_Both_Cast_And_Cycle_Cost_Same()
    {
        var (bot, state, player) = CreateGameWithBot();

        // With exactly {1}{R} — bot can cast or cycle (same cost). Should prefer casting.
        var gempalm = GameCard.Create("Gempalm Incinerator");
        player.Hand.Add(gempalm);
        player.ManaPool.Add(ManaColor.Red, 1);
        player.ManaPool.Add(ManaColor.Colorless, 1);
        player.LandsPlayedThisTurn = 1;

        var action = await bot.GetAction(state, player.Id);

        action.Type.Should().Be(ActionType.PlayCard);
        action.CardId.Should().Be(gempalm.Id);
    }

    [Fact]
    public async Task AiBot_Cycles_When_Cannot_Afford_Cast_But_Can_Cycle()
    {
        var (bot, state, player) = CreateGameWithBot();

        // Create a card with a higher cast cost but cheaper cycling cost
        // Gempalm has same cost for both, so simulate by giving the GameCard a higher ManaCost
        var gempalm = GameCard.Create("Gempalm Incinerator");
        // Override ManaCost to be more expensive than cycling cost
        gempalm.ManaCost = ManaCost.Parse("{3}{R}{R}");
        player.Hand.Add(gempalm);

        // {1}{R} — enough for cycling ({1}{R}) but not for cast ({3}{R}{R})
        player.ManaPool.Add(ManaColor.Red, 1);
        player.ManaPool.Add(ManaColor.Colorless, 1);
        player.LandsPlayedThisTurn = 1;

        var action = await bot.GetAction(state, player.Id);

        action.Type.Should().Be(ActionType.Cycle);
        action.CardId.Should().Be(gempalm.Id);
    }

    [Fact]
    public async Task AiBot_Does_Not_Cycle_When_Insufficient_Mana_For_Cycling()
    {
        var (bot, state, player) = CreateGameWithBot();

        var gempalm = GameCard.Create("Gempalm Incinerator");
        player.Hand.Add(gempalm);

        // Only {R} — not enough for cycling cost {1}{R}
        player.ManaPool.Add(ManaColor.Red, 1);
        player.LandsPlayedThisTurn = 1;

        var action = await bot.GetAction(state, player.Id);

        // Cannot afford to cast or cycle, should pass
        action.Type.Should().Be(ActionType.PassPriority);
    }
}
