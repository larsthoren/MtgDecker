using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.AI;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests.AI;

public class AiBotRampAndUtilityTests
{
    // Helper: bot is active player in MainPhase1
    private static (GameState state, Player bot, Player opponent, AiBotDecisionHandler handler) CreateMainPhaseScenario()
    {
        var botHandler = new AiBotDecisionHandler { ActionDelayMs = 0 };
        var opponentHandler = new AiBotDecisionHandler { ActionDelayMs = 0 };
        var bot = new Player(Guid.NewGuid(), "Bot", botHandler);
        var opponent = new Player(Guid.NewGuid(), "Opponent", opponentHandler);
        var state = new GameState(bot, opponent);
        state.ActivePlayer = bot;
        state.CurrentPhase = Phase.MainPhase1;
        return (state, bot, opponent, botHandler);
    }

    // Helper: bot is non-active (reactive) player
    private static (GameState state, Player bot, Player opponent, AiBotDecisionHandler handler) CreateReactiveScenario()
    {
        var botHandler = new AiBotDecisionHandler { ActionDelayMs = 0 };
        var opponentHandler = new AiBotDecisionHandler { ActionDelayMs = 0 };
        var bot = new Player(Guid.NewGuid(), "Bot", botHandler);
        var opponent = new Player(Guid.NewGuid(), "Opponent", opponentHandler);
        var state = new GameState(bot, opponent);
        state.ActivePlayer = opponent;
        state.CurrentPhase = Phase.MainPhase1;
        return (state, bot, opponent, botHandler);
    }

    [Fact]
    public async Task GetAction_CastsDarkRitual_WhenItEnablesExpensiveSpell()
    {
        // Arrange: Bot has Dark Ritual + Goblin Ringleader ({3}{R}) in hand
        // With 1 Swamp + 1 Mountain untapped: potentialMana = 2, can't afford 4-CMC Ringleader
        // Dark Ritual costs {B} (1 mana) and adds {B}{B}{B} (3 mana)
        // After ramp: potentialMana = 2 + 3 - 1 = 4, can afford Ringleader
        var (state, bot, opponent, handler) = CreateMainPhaseScenario();
        bot.LandsPlayedThisTurn = 1; // already played land

        var swamp = GameCard.Create("Swamp", "Basic Land — Swamp");
        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
        bot.Battlefield.Add(swamp);
        bot.Battlefield.Add(mountain);

        var darkRitual = GameCard.Create("Dark Ritual", "Instant");
        var ringleader = GameCard.Create("Goblin Ringleader", "Creature — Goblin");
        bot.Hand.Add(darkRitual);
        bot.Hand.Add(ringleader);

        // Act
        var action = await handler.GetAction(state, bot.Id);

        // Assert: Bot should tap Swamp (to cast Dark Ritual) or cast Dark Ritual directly
        // The first action should be part of the ramp sequence
        action.Type.Should().BeOneOf(ActionType.TapCard, ActionType.CastSpell);

        // If it's a TapCard, drain the queue to find the CastSpell for Dark Ritual
        if (action.Type == ActionType.TapCard)
        {
            action.CardId.Should().Be(swamp.Id, "should tap Swamp to produce {B} for Dark Ritual");
            // Next action should be CastSpell for Dark Ritual
            var nextAction = await handler.GetAction(state, bot.Id);
            nextAction.Type.Should().Be(ActionType.CastSpell);
            nextAction.CardId.Should().Be(darkRitual.Id);
        }
        else
        {
            action.CardId.Should().Be(darkRitual.Id);
        }
    }

    [Fact]
    public async Task GetAction_DoesNotCastDarkRitual_WhenAlreadyCanAffordSpells()
    {
        // Arrange: Bot has Dark Ritual + Goblin Lackey ({R}) in hand
        // Pool has {R} already — can cast Lackey without ramp
        var (state, bot, opponent, handler) = CreateMainPhaseScenario();
        bot.LandsPlayedThisTurn = 1;

        var darkRitual = GameCard.Create("Dark Ritual", "Instant");
        var lackey = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        bot.Hand.Add(darkRitual);
        bot.Hand.Add(lackey);

        bot.ManaPool.Add(ManaColor.Red);

        // Act
        var action = await handler.GetAction(state, bot.Id);

        // Assert: Bot should cast the proactive spell (Lackey), not the ramp spell
        action.Type.Should().Be(ActionType.CastSpell);
        action.CardId.Should().Be(lackey.Id);
    }

    [Fact]
    public async Task GetAction_CastsInstantUtility_DuringOpponentEndStep()
    {
        // Arrange: Bot is non-active player, opponent's End phase, stack empty
        // Bot has Brainstorm + {U} in pool
        var (state, bot, opponent, handler) = CreateReactiveScenario();
        state.CurrentPhase = Phase.End;

        var brainstorm = GameCard.Create("Brainstorm", "Instant");
        bot.Hand.Add(brainstorm);
        bot.ManaPool.Add(ManaColor.Blue);

        // Act
        var action = await handler.GetAction(state, bot.Id);

        // Assert: Bot should cast Brainstorm
        action.Type.Should().Be(ActionType.CastSpell);
        action.CardId.Should().Be(brainstorm.Id);
    }

    [Fact]
    public async Task GetAction_DoesNotCastInstantUtility_DuringMainPhase()
    {
        // Arrange: Bot is non-active player, MainPhase1, has Brainstorm + {U}
        var (state, bot, opponent, handler) = CreateReactiveScenario();
        // state.CurrentPhase is already MainPhase1 from helper

        var brainstorm = GameCard.Create("Brainstorm", "Instant");
        bot.Hand.Add(brainstorm);
        bot.ManaPool.Add(ManaColor.Blue);

        // Act
        var action = await handler.GetAction(state, bot.Id);

        // Assert: Bot should pass (not cast utility spells during main phase)
        action.Type.Should().Be(ActionType.PassPriority);
    }
}
