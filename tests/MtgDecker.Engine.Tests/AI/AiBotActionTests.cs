using FluentAssertions;
using MtgDecker.Engine.AI;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests.AI;

public class AiBotActionTests
{
    private static (GameState state, Player player) CreateGameWithBot()
    {
        var bot = new AiBotDecisionHandler();
        var opponent = new Player(Guid.NewGuid(), "Opponent", new AiBotDecisionHandler());
        var player = new Player(Guid.NewGuid(), "Bot", bot);
        var state = new GameState(player, opponent);
        state.CurrentPhase = Phase.MainPhase1;
        return (state, player);
    }

    [Fact]
    public async Task GetAction_WithLandInHand_PlaysLand()
    {
        var (state, player) = CreateGameWithBot();
        var mountain = new GameCard { Name = "Mountain", CardTypes = CardType.Land };
        player.Hand.Add(mountain);

        var action = await ((AiBotDecisionHandler)player.DecisionHandler).GetAction(state, player.Id);

        action.Type.Should().Be(ActionType.PlayCard);
        action.CardId.Should().Be(mountain.Id);
    }

    [Fact]
    public async Task GetAction_LandAlreadyPlayed_DoesNotPlayAnotherLand()
    {
        var (state, player) = CreateGameWithBot();
        player.LandsPlayedThisTurn = 1;
        player.Hand.Add(new GameCard { Name = "Mountain", CardTypes = CardType.Land });

        var action = await ((AiBotDecisionHandler)player.DecisionHandler).GetAction(state, player.Id);

        action.Type.Should().Be(ActionType.PassPriority);
    }

    [Fact]
    public async Task GetAction_WithAffordableSpell_CastsIt()
    {
        var (state, player) = CreateGameWithBot();
        var goblin = new GameCard { Name = "Goblin Lackey", CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{R}") };
        player.Hand.Add(goblin);
        player.ManaPool.Add(ManaColor.Red);

        var action = await ((AiBotDecisionHandler)player.DecisionHandler).GetAction(state, player.Id);

        action.Type.Should().Be(ActionType.PlayCard);
        action.CardId.Should().Be(goblin.Id);
    }

    [Fact]
    public async Task GetAction_CastsExpensiveSpellFirst()
    {
        var (state, player) = CreateGameWithBot();
        var cheap = new GameCard { Name = "Mogg Fanatic", CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{R}") };
        var expensive = new GameCard { Name = "Goblin Warchief", CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{1}{R}{R}") };
        player.Hand.Add(cheap);
        player.Hand.Add(expensive);
        player.ManaPool.Add(ManaColor.Red, 3);

        var action = await ((AiBotDecisionHandler)player.DecisionHandler).GetAction(state, player.Id);

        action.CardId.Should().Be(expensive.Id);
    }

    [Fact]
    public async Task GetAction_NotEnoughMana_Passes()
    {
        var (state, player) = CreateGameWithBot();
        player.Hand.Add(new GameCard { Name = "Siege-Gang Commander", CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{3}{R}{R}") });
        player.ManaPool.Add(ManaColor.Red, 1);

        var action = await ((AiBotDecisionHandler)player.DecisionHandler).GetAction(state, player.Id);

        action.Type.Should().Be(ActionType.PassPriority);
    }

    [Fact]
    public async Task GetAction_EmptyHand_Passes()
    {
        var (state, player) = CreateGameWithBot();

        var action = await ((AiBotDecisionHandler)player.DecisionHandler).GetAction(state, player.Id);

        action.Type.Should().Be(ActionType.PassPriority);
    }

    [Fact]
    public async Task GetAction_PlaysLandBeforeSpell()
    {
        var (state, player) = CreateGameWithBot();
        player.Hand.Add(new GameCard { Name = "Mountain", CardTypes = CardType.Land });
        player.Hand.Add(new GameCard { Name = "Mogg Fanatic", CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{R}") });

        var action = await ((AiBotDecisionHandler)player.DecisionHandler).GetAction(state, player.Id);

        action.CardId.Should().NotBeNull();
        // The land should be played (land has priority over spells)
        var card = player.Hand.Cards.First(c => c.Id == action.CardId);
        card.IsLand.Should().BeTrue();
    }

    [Fact]
    public async Task GetAction_NonMainPhase_Passes()
    {
        var (state, player) = CreateGameWithBot();
        state.CurrentPhase = Phase.Upkeep;
        player.Hand.Add(new GameCard { Name = "Mountain", CardTypes = CardType.Land });

        var action = await ((AiBotDecisionHandler)player.DecisionHandler).GetAction(state, player.Id);

        action.Type.Should().Be(ActionType.PassPriority);
    }

    [Fact]
    public async Task GetAction_MainPhase2_CanActToo()
    {
        var (state, player) = CreateGameWithBot();
        state.CurrentPhase = Phase.MainPhase2;
        var mountain = new GameCard { Name = "Mountain", CardTypes = CardType.Land };
        player.Hand.Add(mountain);

        var action = await ((AiBotDecisionHandler)player.DecisionHandler).GetAction(state, player.Id);

        action.Type.Should().Be(ActionType.PlayCard);
        action.CardId.Should().Be(mountain.Id);
    }

    [Fact]
    public async Task GetAction_WithOnlyUnaffordableSpells_AndNoLandDrop_Passes()
    {
        var (state, player) = CreateGameWithBot();
        player.LandsPlayedThisTurn = 1; // already played land
        player.Hand.Add(new GameCard { Name = "Inferno Titan", CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{4}{R}{R}") });
        // No mana in pool

        var action = await ((AiBotDecisionHandler)player.DecisionHandler).GetAction(state, player.Id);

        action.Type.Should().Be(ActionType.PassPriority);
    }

    [Fact]
    public async Task GetAction_Player2AsBot_ResolvesCorrectly()
    {
        // Ensure the bot works when it's Player2, not just Player1
        var bot = new AiBotDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Human", new AiBotDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "Bot", bot);
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;

        var mountain = new GameCard { Name = "Mountain", CardTypes = CardType.Land };
        p2.Hand.Add(mountain);

        var action = await bot.GetAction(state, p2.Id);

        action.Type.Should().Be(ActionType.PlayCard);
        action.CardId.Should().Be(mountain.Id);
    }
}
