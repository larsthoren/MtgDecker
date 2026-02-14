using FluentAssertions;
using MtgDecker.Engine.AI;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests.AI;

public class AiBotDeferredCardTests
{
    private static (GameState state, Player bot, Player opponent) CreateGameWithBot()
    {
        var botHandler = new AiBotDecisionHandler { ActionDelayMs = 0 };
        var opponentHandler = new AiBotDecisionHandler { ActionDelayMs = 0 };
        var bot = new Player(Guid.NewGuid(), "Bot", botHandler);
        var opponent = new Player(Guid.NewGuid(), "Opponent", opponentHandler);
        var state = new GameState(bot, opponent);
        state.CurrentPhase = Phase.MainPhase1;
        return (state, bot, opponent);
    }

    [Fact]
    public async Task Bot_Discards_For_UpkeepCost_When_Hand_Has_Cards()
    {
        // AI's ChooseCard method is used for upkeep costs (e.g., Solitary Confinement discard).
        // When optional=true and the hand has cards, the AI should return a card (not null).
        var bot = new AiBotDecisionHandler { ActionDelayMs = 0 };

        var cards = new List<GameCard>
        {
            new() { Name = "Forest", CardTypes = CardType.Land },
            new() { Name = "Llanowar Elves", CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{G}") },
        };

        var result = await bot.ChooseCard(cards, "Choose a card to discard", optional: true);

        result.Should().NotBeNull("AI should choose to discard when it has cards");
    }

    [Fact]
    public async Task Bot_Activates_ParallaxWave_Against_Opponent_Creature()
    {
        var (state, bot, opponent) = CreateGameWithBot();

        // Put Parallax Wave on the bot's battlefield with 5 fade counters
        var wave = GameCard.Create("Parallax Wave", "Enchantment");
        wave.AddCounters(CounterType.Fade, 5);
        bot.Battlefield.Add(wave);

        // Opponent has a creature
        var angel = new GameCard
        {
            Name = "Serra Angel", CardTypes = CardType.Creature,
            BasePower = 4, BaseToughness = 4
        };
        opponent.Battlefield.Add(angel);

        var action = await bot.DecisionHandler.GetAction(state, bot.Id);

        action.Type.Should().Be(ActionType.ActivateAbility);
        action.CardId.Should().Be(wave.Id);
        action.TargetCardId.Should().Be(angel.Id);
    }

    [Fact]
    public async Task Bot_Does_Not_Activate_ParallaxWave_Without_Counters()
    {
        var (state, bot, opponent) = CreateGameWithBot();

        // Parallax Wave with 0 fade counters
        var wave = GameCard.Create("Parallax Wave", "Enchantment");
        // No counters added — starts at 0
        bot.Battlefield.Add(wave);

        var angel = new GameCard
        {
            Name = "Serra Angel", CardTypes = CardType.Creature,
            BasePower = 4, BaseToughness = 4
        };
        opponent.Battlefield.Add(angel);

        // Need something in hand to avoid early pass
        bot.Hand.Add(new GameCard { Name = "Spell", CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{5}{W}{W}") });

        var action = await bot.DecisionHandler.GetAction(state, bot.Id);

        // Should not activate the Wave — no counters
        action.Type.Should().NotBe(ActionType.ActivateAbility,
            "Wave has no fade counters so it cannot be activated");
    }

    [Fact]
    public async Task Bot_Does_Not_Activate_ParallaxWave_Without_Creature_Targets()
    {
        var (state, bot, opponent) = CreateGameWithBot();

        // Parallax Wave with counters
        var wave = GameCard.Create("Parallax Wave", "Enchantment");
        wave.AddCounters(CounterType.Fade, 5);
        bot.Battlefield.Add(wave);

        // Opponent has no creatures

        // Need something in hand to avoid early pass
        bot.Hand.Add(new GameCard { Name = "Spell", CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{5}{W}{W}") });

        var action = await bot.DecisionHandler.GetAction(state, bot.Id);

        // Should not activate the Wave — no creature targets
        action.Type.Should().NotBe(ActionType.ActivateAbility,
            "Wave has no creature targets so it should not activate");
    }

    [Fact]
    public async Task Bot_Targets_Biggest_Threat_With_ParallaxWave()
    {
        var (state, bot, opponent) = CreateGameWithBot();

        var wave = GameCard.Create("Parallax Wave", "Enchantment");
        wave.AddCounters(CounterType.Fade, 5);
        bot.Battlefield.Add(wave);

        // Opponent has two creatures — bot should target the bigger one
        var bird = new GameCard
        {
            Name = "Birds of Paradise", CardTypes = CardType.Creature,
            BasePower = 0, BaseToughness = 1
        };
        var angel = new GameCard
        {
            Name = "Serra Angel", CardTypes = CardType.Creature,
            BasePower = 4, BaseToughness = 4
        };
        opponent.Battlefield.Add(bird);
        opponent.Battlefield.Add(angel);

        var action = await bot.DecisionHandler.GetAction(state, bot.Id);

        action.Type.Should().Be(ActionType.ActivateAbility);
        action.TargetCardId.Should().Be(angel.Id,
            "AI should target the biggest creature (Serra Angel with power 4)");
    }
}
