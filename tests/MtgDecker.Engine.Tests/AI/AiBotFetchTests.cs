using FluentAssertions;
using MtgDecker.Engine.AI;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests.AI;

public class AiBotFetchTests
{
    private static (GameState state, Player player, AiBotDecisionHandler bot) CreateGameWithBot()
    {
        var bot = new AiBotDecisionHandler();
        var opponent = new Player(Guid.NewGuid(), "Opponent", new AiBotDecisionHandler());
        var player = new Player(Guid.NewGuid(), "Bot", bot);
        var state = new GameState(player, opponent);
        state.CurrentPhase = Phase.MainPhase1;
        return (state, player, bot);
    }

    [Fact]
    public async Task Bot_Activates_Fetch_Land_During_Main_Phase()
    {
        var (state, player, bot) = CreateGameWithBot();

        // Fetch land on battlefield (use CardDefinitions via Create)
        var fetch = GameCard.Create("Wooded Foothills", "Land");
        player.Battlefield.Add(fetch);

        // A spell in hand that needs mana
        var spell = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        player.Hand.Add(spell);

        // A fetchable target in library
        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
        player.Library.Add(mountain);

        // Land drop already used (so bot won't try to play a land from hand)
        player.LandsPlayedThisTurn = 1;

        var action = await bot.GetAction(state, player.Id);

        action.Type.Should().Be(ActionType.ActivateFetch);
        action.CardId.Should().Be(fetch.Id);
    }

    [Fact]
    public async Task Bot_Plays_Land_Before_Fetching()
    {
        var (state, player, bot) = CreateGameWithBot();

        // Fetch land on battlefield
        var fetch = GameCard.Create("Wooded Foothills", "Land");
        player.Battlefield.Add(fetch);

        // Land in hand — bot should play this first (free land drop)
        var handLand = new GameCard { Name = "Mountain", CardTypes = CardType.Land };
        player.Hand.Add(handLand);

        var action = await bot.GetAction(state, player.Id);

        action.Type.Should().Be(ActionType.PlayCard);
        action.CardId.Should().Be(handLand.Id);
    }

    [Fact]
    public async Task Bot_Does_Not_Fetch_Without_Spells_In_Hand()
    {
        var (state, player, bot) = CreateGameWithBot();

        // Fetch land on battlefield
        var fetch = GameCard.Create("Wooded Foothills", "Land");
        player.Battlefield.Add(fetch);

        // Only lands in hand — no reason to fetch
        player.Hand.Add(new GameCard { Name = "Forest", CardTypes = CardType.Land });
        player.LandsPlayedThisTurn = 1;

        var action = await bot.GetAction(state, player.Id);

        action.Type.Should().Be(ActionType.PassPriority);
    }

    [Fact]
    public async Task Bot_Does_Not_Fetch_Tapped_Fetch_Land()
    {
        var (state, player, bot) = CreateGameWithBot();

        // Tapped fetch land on battlefield
        var fetch = GameCard.Create("Wooded Foothills", "Land");
        fetch.IsTapped = true;
        player.Battlefield.Add(fetch);

        // Spell in hand
        var spell = new GameCard { Name = "Goblin Lackey", CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{R}") };
        player.Hand.Add(spell);
        player.LandsPlayedThisTurn = 1;

        var action = await bot.GetAction(state, player.Id);

        // Should not activate the tapped fetch — should pass instead
        action.Type.Should().NotBe(ActionType.ActivateFetch);
    }

    [Fact]
    public async Task Bot_Fetches_Before_Tapping_Regular_Lands()
    {
        var (state, player, bot) = CreateGameWithBot();

        // Fetch land on battlefield (untapped)
        var fetch = GameCard.Create("Wooded Foothills", "Land");
        player.Battlefield.Add(fetch);

        // Regular land on battlefield (untapped, has mana ability)
        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
        player.Battlefield.Add(mountain);

        // Spell in hand
        var spell = new GameCard { Name = "Goblin Lackey", CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{R}") };
        player.Hand.Add(spell);
        player.LandsPlayedThisTurn = 1;

        var action = await bot.GetAction(state, player.Id);

        // Bot should activate fetch before tapping regular lands for mana
        action.Type.Should().Be(ActionType.ActivateFetch);
        action.CardId.Should().Be(fetch.Id);
    }

    [Fact]
    public async Task Bot_Casts_Spell_With_Cost_Reduction()
    {
        var bot = new AiBotDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Bot", bot);
        var p2 = new Player(Guid.NewGuid(), "Opp", new AiBotDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;

        // Warchief on battlefield — Goblins cost {1} less
        var warchief = GameCard.Create("Goblin Warchief", "Creature — Goblin");
        p1.Battlefield.Add(warchief);

        var engine = new GameEngine(state);
        engine.RecalculateState();

        // Goblin Ringleader costs {3}{R}, reduced to {2}{R} with Warchief
        var ringleader = GameCard.Create("Goblin Ringleader", "Creature — Goblin");
        p1.Hand.Add(ringleader);

        // Only 3 mana: 2 colorless + 1 red (enough for {2}{R} but not {3}{R})
        p1.ManaPool.Add(ManaColor.Red);
        p1.ManaPool.Add(ManaColor.Colorless);
        p1.ManaPool.Add(ManaColor.Colorless);

        // Land drop already used (so bot won't try to play a land)
        p1.LandsPlayedThisTurn = 1;

        var action = await bot.GetAction(state, p1.Id);
        // Bot should try to cast the Ringleader (with cost reduction, it can afford it)
        action.Type.Should().Be(ActionType.PlayCard);
        action.CardId.Should().Be(ringleader.Id);
    }
}
