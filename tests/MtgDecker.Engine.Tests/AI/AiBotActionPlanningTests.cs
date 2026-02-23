using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.AI;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests.AI;

public class AiBotActionPlanningTests
{
    [Fact]
    public async Task GetAction_DoesNotCastCounterspellProactively()
    {
        var state = CreateMinimalGameState();
        var player = state.Player1;
        var daze = GameCard.Create("Daze", "Instant", "", "{1}{U}", null, null);
        player.Hand.Add(daze);

        var island = GameCard.Create("Island", "Basic Land \u2014 Island", "", null, null, null);
        island.ManaAbility = ManaAbility.Fixed(ManaColor.Blue);
        player.Battlefield.Add(island);
        player.ManaPool.Add(ManaColor.Blue);
        player.ManaPool.Add(ManaColor.Colorless);

        var bot = (AiBotDecisionHandler)player.DecisionHandler;
        bot.ActionDelayMs = 0;
        var action = await bot.GetAction(state, player.Id);

        action.Type.Should().NotBe(ActionType.CastSpell);
    }

    [Fact]
    public async Task GetAction_DoesNotCastInstantRemovalProactively()
    {
        var state = CreateMinimalGameState();
        var player = state.Player1;
        var bolt = GameCard.Create("Lightning Bolt", "Instant", "", "{R}", null, null);
        player.Hand.Add(bolt);

        var mountain = GameCard.Create("Mountain", "Basic Land \u2014 Mountain", "", null, null, null);
        mountain.ManaAbility = ManaAbility.Fixed(ManaColor.Red);
        player.Battlefield.Add(mountain);
        player.ManaPool.Add(ManaColor.Red);

        var bot = (AiBotDecisionHandler)player.DecisionHandler;
        bot.ActionDelayMs = 0;
        var action = await bot.GetAction(state, player.Id);

        action.Type.Should().NotBe(ActionType.CastSpell);
    }

    [Fact]
    public async Task GetAction_CastsProactiveSpells()
    {
        var state = CreateMinimalGameState();
        var player = state.Player1;
        var lackey = GameCard.Create("Goblin Lackey", "Creature \u2014 Goblin", "", "{R}", null, null);
        player.Hand.Add(lackey);
        player.ManaPool.Add(ManaColor.Red);
        player.LandsPlayedThisTurn = 1;

        var bot = (AiBotDecisionHandler)player.DecisionHandler;
        bot.ActionDelayMs = 0;
        var action = await bot.GetAction(state, player.Id);

        action.Type.Should().Be(ActionType.CastSpell);
        action.CardId.Should().Be(lackey.Id);
    }

    [Fact]
    public async Task GetAction_TapsCorrectNumberThenCasts()
    {
        var state = CreateMinimalGameState();
        var player = state.Player1;
        player.LandsPlayedThisTurn = 1;

        var creature = GameCard.Create("Mogg Fanatic", "Creature \u2014 Goblin", "", "{R}", null, null);
        player.Hand.Add(creature);

        for (int i = 0; i < 3; i++)
        {
            var mtn = GameCard.Create("Mountain", "Basic Land \u2014 Mountain", "", null, null, null);
            mtn.ManaAbility = ManaAbility.Fixed(ManaColor.Red);
            player.Battlefield.Add(mtn);
        }

        var bot = (AiBotDecisionHandler)player.DecisionHandler;
        bot.ActionDelayMs = 0;

        // First call should return TapCard
        var action1 = await bot.GetAction(state, player.Id);
        action1.Type.Should().Be(ActionType.TapCard);

        // Simulate the tap producing mana
        var tappedCard = player.Battlefield.Cards.First(c => c.Id == action1.CardId);
        tappedCard.IsTapped = true;
        player.ManaPool.Add(ManaColor.Red);

        // Second call should return CastSpell (1 red needed, have it now)
        var action2 = await bot.GetAction(state, player.Id);
        action2.Type.Should().Be(ActionType.CastSpell);
        action2.CardId.Should().Be(creature.Id);
    }

    private static GameState CreateMinimalGameState()
    {
        var bot = new AiBotDecisionHandler { ActionDelayMs = 0 };
        var p1 = new Player(Guid.NewGuid(), "Bot", bot);
        var p2 = new Player(Guid.NewGuid(), "Opponent", new AiBotDecisionHandler { ActionDelayMs = 0 });
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = p1;
        state.PriorityPlayer = p1;
        return state;
    }
}
