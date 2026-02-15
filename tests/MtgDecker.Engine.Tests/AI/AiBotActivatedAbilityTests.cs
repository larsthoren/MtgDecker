using FluentAssertions;
using MtgDecker.Engine.AI;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests.AI;

public class AiBotActivatedAbilityTests
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
    public async Task Bot_Activates_MoggFanatic_To_Kill_1Toughness_Creature()
    {
        var (state, bot, opponent) = CreateGameWithBot();

        // Put a Mogg Fanatic on the bot's battlefield
        var fanatic = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        bot.Battlefield.Add(fanatic);

        // Put a 1-toughness creature on opponent's battlefield
        var bird = new GameCard { Name = "Birds of Paradise", CardTypes = CardType.Creature, BasePower = 0, BaseToughness = 1 };
        opponent.Battlefield.Add(bird);

        // Bot needs a spell in hand (otherwise it would just pass since no work to do)
        // Actually, the ability check happens before spell casting, so no spell needed.

        var action = await bot.DecisionHandler.GetAction(state, bot.Id);

        action.Type.Should().Be(ActionType.ActivateAbility);
        action.CardId.Should().Be(fanatic.Id);
        action.TargetCardId.Should().Be(bird.Id);
    }

    [Fact]
    public async Task Bot_Does_Not_Sacrifice_MoggFanatic_When_No_Good_Target()
    {
        var (state, bot, opponent) = CreateGameWithBot();

        // Mogg Fanatic on battlefield
        var fanatic = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        bot.Battlefield.Add(fanatic);

        // Opponent has no creatures, only 20 life (not worth pinging)
        // Bot has no spells in hand either
        // Empty hand => passes immediately before even reaching ability check
        // So let's give it a spell it can't cast to keep it from passing early
        bot.Hand.Add(new GameCard { Name = "Goblin Warchief", CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{1}{R}{R}") });

        var action = await bot.DecisionHandler.GetAction(state, bot.Id);

        // Should NOT activate Mogg Fanatic — no creature to kill
        action.Type.Should().NotBe(ActionType.ActivateAbility);
    }

    [Fact]
    public async Task Bot_Uses_SkirkProspector_When_It_Enables_Casting_Spell()
    {
        var (state, bot, opponent) = CreateGameWithBot();

        // Skirk Prospector and a Goblin token on battlefield
        var prospector = GameCard.Create("Skirk Prospector", "Creature — Goblin");
        var token = new GameCard
        {
            Name = "Goblin", CardTypes = CardType.Creature,
            Subtypes = ["Goblin"], BasePower = 1, BaseToughness = 1, IsToken = true
        };
        bot.Battlefield.Add(prospector);
        bot.Battlefield.Add(token);

        // A spell in hand costing {1}{R} = 2 total, and bot has 1 red mana already
        var matron = new GameCard
        {
            Name = "Goblin Matron", CardTypes = CardType.Creature,
            ManaCost = ManaCost.Parse("{2}{R}")
        };
        bot.Hand.Add(matron);
        bot.ManaPool.Add(ManaColor.Red, 2);

        // Total mana = 2R, spell costs {2}{R} = 3 total. Need exactly 1 more.
        var action = await bot.DecisionHandler.GetAction(state, bot.Id);

        action.Type.Should().Be(ActionType.ActivateAbility);
        action.CardId.Should().Be(prospector.Id);
    }

    [Fact]
    public async Task Bot_Does_Not_Use_SkirkProspector_When_Not_Needed()
    {
        var (state, bot, opponent) = CreateGameWithBot();

        var prospector = GameCard.Create("Skirk Prospector", "Creature — Goblin");
        var token = new GameCard
        {
            Name = "Goblin", CardTypes = CardType.Creature,
            Subtypes = ["Goblin"], BasePower = 1, BaseToughness = 1, IsToken = true
        };
        bot.Battlefield.Add(prospector);
        bot.Battlefield.Add(token);

        // Spell costs {R} = 1 total, bot already has 1R — doesn't need extra mana
        var lackey = new GameCard
        {
            Name = "Goblin Lackey", CardTypes = CardType.Creature,
            ManaCost = ManaCost.Parse("{R}")
        };
        bot.Hand.Add(lackey);
        bot.ManaPool.Add(ManaColor.Red, 1);

        var action = await bot.DecisionHandler.GetAction(state, bot.Id);

        // Should cast the spell, not activate Prospector
        action.Type.Should().Be(ActionType.CastSpell);
        action.CardId.Should().Be(lackey.Id);
    }

    [Fact]
    public async Task Bot_Does_Not_Target_Shroud_Creature_With_DealDamageEffect()
    {
        var (state, bot, opponent) = CreateGameWithBot();

        var fanatic = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        bot.Battlefield.Add(fanatic);

        // Opponent has a 1-toughness creature with shroud — should NOT be targeted
        var enchantress = new GameCard
        {
            Name = "Argothian Enchantress", CardTypes = CardType.Creature,
            BasePower = 0, BaseToughness = 1
        };
        enchantress.ActiveKeywords.Add(Keyword.Shroud);
        opponent.Battlefield.Add(enchantress);

        bot.Hand.Add(new GameCard { Name = "Filler", CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{5}{R}{R}") });

        var action = await bot.DecisionHandler.GetAction(state, bot.Id);

        // Should NOT target the shroud creature
        action.Type.Should().NotBe(ActionType.ActivateAbility);
    }

    [Fact]
    public async Task Bot_Does_Not_Target_Shroud_Creature_With_ExileEffect()
    {
        var (state, bot, opponent) = CreateGameWithBot();

        // Parallax Wave with a fade counter
        var wave = GameCard.Create("Parallax Wave");
        wave.AddCounters(CounterType.Fade, 5);
        bot.Battlefield.Add(wave);

        // Opponent has only a shrouded creature
        var shrouded = new GameCard
        {
            Name = "Nimble Mongoose", CardTypes = CardType.Creature,
            BasePower = 1, BaseToughness = 1
        };
        shrouded.ActiveKeywords.Add(Keyword.Shroud);
        opponent.Battlefield.Add(shrouded);

        bot.Hand.Add(new GameCard { Name = "Filler", CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{5}{R}{R}") });

        var action = await bot.DecisionHandler.GetAction(state, bot.Id);

        // Should NOT target the shroud creature with exile
        action.Type.Should().NotBe(ActionType.ActivateAbility);
    }

    [Fact]
    public async Task Bot_Does_Not_Activate_Tapped_Creature()
    {
        var (state, bot, opponent) = CreateGameWithBot();

        // Goblin Sharpshooter (tap ability) but already tapped
        var shooter = GameCard.Create("Goblin Sharpshooter", "Creature — Goblin");
        shooter.IsTapped = true;
        bot.Battlefield.Add(shooter);

        var bird = new GameCard { Name = "Bird", CardTypes = CardType.Creature, BasePower = 1, BaseToughness = 1 };
        opponent.Battlefield.Add(bird);

        // Need something in hand to avoid early pass
        bot.Hand.Add(new GameCard { Name = "Spell", CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{5}{R}{R}") });

        var action = await bot.DecisionHandler.GetAction(state, bot.Id);

        // Should not activate the tapped sharpshooter
        action.Type.Should().NotBe(ActionType.ActivateAbility);
    }
}
