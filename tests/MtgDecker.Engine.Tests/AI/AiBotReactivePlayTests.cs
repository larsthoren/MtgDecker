using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.AI;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests.AI;

public class AiBotReactivePlayTests
{
    private static (GameState state, Player bot, Player opponent, AiBotDecisionHandler handler) CreateReactiveScenario()
    {
        var botHandler = new AiBotDecisionHandler { ActionDelayMs = 0 };
        var opponentHandler = new AiBotDecisionHandler { ActionDelayMs = 0 };
        var bot = new Player(Guid.NewGuid(), "Bot", botHandler);
        var opponent = new Player(Guid.NewGuid(), "Opponent", opponentHandler);
        var state = new GameState(bot, opponent);
        // Opponent is the active player — bot is the non-active (reactive) player
        state.ActivePlayer = opponent;
        state.CurrentPhase = Phase.MainPhase1;
        return (state, bot, opponent, botHandler);
    }

    [Fact]
    public async Task GetAction_CountersSpellOnStack_WhenHasCounterspell()
    {
        var (state, bot, opponent, handler) = CreateReactiveScenario();

        // Bot has Counterspell in hand + UU in mana pool
        var counterspell = GameCard.Create("Counterspell", "Instant");
        bot.Hand.Add(counterspell);
        bot.ManaPool.Add(ManaColor.Blue, 2);

        // Opponent's CMC 5 spell on stack (Siege-Gang Commander is registered: {3}{R}{R})
        var opponentSpell = GameCard.Create("Siege-Gang Commander", "Creature");
        state.StackPush(new StackObject(opponentSpell, opponent.Id, new Dictionary<ManaColor, int>(), [], 0));

        var action = await handler.GetAction(state, bot.Id);

        action.Type.Should().Be(ActionType.CastSpell);
        action.CardId.Should().Be(counterspell.Id);
        action.UseAlternateCost.Should().BeFalse();
    }

    [Fact]
    public async Task GetAction_DazeCounters_WhenOpponentTappedOut()
    {
        var (state, bot, opponent, handler) = CreateReactiveScenario();

        // Bot has Daze in hand + an Island on battlefield
        var daze = GameCard.Create("Daze", "Instant");
        bot.Hand.Add(daze);
        var island = GameCard.Create("Island", "Basic Land — Island");
        bot.Battlefield.Add(island);

        // Opponent has NO untapped lands (tapped out)
        var oppMountain = GameCard.Create("Mountain", "Basic Land — Mountain");
        oppMountain.IsTapped = true;
        opponent.Battlefield.Add(oppMountain);

        // Opponent's spell on stack (Goblin Warchief: {1}{R}{R})
        var opponentSpell = GameCard.Create("Goblin Warchief", "Creature");
        state.StackPush(new StackObject(opponentSpell, opponent.Id, new Dictionary<ManaColor, int>(), [], 0));

        var action = await handler.GetAction(state, bot.Id);

        action.Type.Should().Be(ActionType.CastSpell);
        action.CardId.Should().Be(daze.Id);
        action.UseAlternateCost.Should().BeTrue();
    }

    [Fact]
    public async Task GetAction_DazeDoesNotCounter_WhenOpponentHasUntappedLands()
    {
        var (state, bot, opponent, handler) = CreateReactiveScenario();

        // Bot has Daze in hand + an Island on battlefield
        var daze = GameCard.Create("Daze", "Instant");
        bot.Hand.Add(daze);
        var island = GameCard.Create("Island", "Basic Land — Island");
        bot.Battlefield.Add(island);

        // Opponent has an UNTAPPED land
        var oppMountain = GameCard.Create("Mountain", "Basic Land — Mountain");
        opponent.Battlefield.Add(oppMountain);

        // Opponent's spell on stack (Goblin Warchief: {1}{R}{R})
        var opponentSpell = GameCard.Create("Goblin Warchief", "Creature");
        state.StackPush(new StackObject(opponentSpell, opponent.Id, new Dictionary<ManaColor, int>(), [], 0));

        var action = await handler.GetAction(state, bot.Id);

        action.Type.Should().Be(ActionType.PassPriority);
    }

    [Fact]
    public async Task GetAction_ForceOfWill_CountersExpensiveSpell()
    {
        var (state, bot, opponent, handler) = CreateReactiveScenario();

        // Bot has Force of Will + a blue card (Brainstorm) in hand
        // Life starts at 20 (> 1)
        var fow = GameCard.Create("Force of Will", "Instant");
        bot.Hand.Add(fow);
        var brainstorm = GameCard.Create("Brainstorm", "Instant");
        bot.Hand.Add(brainstorm);

        // Opponent's CMC 5 spell on stack (Siege-Gang Commander: {3}{R}{R})
        var opponentSpell = GameCard.Create("Siege-Gang Commander", "Creature");
        state.StackPush(new StackObject(opponentSpell, opponent.Id, new Dictionary<ManaColor, int>(), [], 0));

        var action = await handler.GetAction(state, bot.Id);

        action.Type.Should().Be(ActionType.CastSpell);
        action.CardId.Should().Be(fow.Id);
        action.UseAlternateCost.Should().BeTrue();
    }

    [Fact]
    public async Task GetAction_DoesNotCounter_CheapSpells_WithForceOfWill()
    {
        var (state, bot, opponent, handler) = CreateReactiveScenario();

        // Bot has Force of Will + a blue card (Brainstorm) in hand
        var fow = GameCard.Create("Force of Will", "Instant");
        bot.Hand.Add(fow);
        var brainstorm = GameCard.Create("Brainstorm", "Instant");
        bot.Hand.Add(brainstorm);

        // Opponent's CMC 1 spell on stack (Goblin Lackey: {R})
        var opponentSpell = GameCard.Create("Goblin Lackey", "Creature");
        state.StackPush(new StackObject(opponentSpell, opponent.Id, new Dictionary<ManaColor, int>(), [], 0));

        var action = await handler.GetAction(state, bot.Id);

        action.Type.Should().Be(ActionType.PassPriority);
    }

    [Fact]
    public async Task GetAction_CastsInstantRemoval_DuringOpponentCombat()
    {
        var (state, bot, opponent, handler) = CreateReactiveScenario();

        // Bot has Lightning Bolt in hand + R in mana pool
        var bolt = GameCard.Create("Lightning Bolt", "Instant");
        bot.Hand.Add(bolt);
        bot.ManaPool.Add(ManaColor.Red);

        // Opponent has a creature on battlefield
        var creature = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        opponent.Battlefield.Add(creature);

        // Phase is Combat with DeclareAttackers, stack is empty
        state.CurrentPhase = Phase.Combat;
        state.CombatStep = CombatStep.DeclareAttackers;

        var action = await handler.GetAction(state, bot.Id);

        action.Type.Should().Be(ActionType.CastSpell);
        action.CardId.Should().Be(bolt.Id);
        action.UseAlternateCost.Should().BeFalse();
    }

    [Fact]
    public async Task GetAction_DoesNotCastRemoval_InMainPhase_AsNonActivePlayer()
    {
        var (state, bot, opponent, handler) = CreateReactiveScenario();

        // Bot has Lightning Bolt in hand + R in mana pool
        var bolt = GameCard.Create("Lightning Bolt", "Instant");
        bot.Hand.Add(bolt);
        bot.ManaPool.Add(ManaColor.Red);

        // Opponent has a creature on battlefield
        var creature = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        opponent.Battlefield.Add(creature);

        // Phase is MainPhase1 (default from helper), stack is empty
        // Bot should wait for combat or end step, not cast now

        var action = await handler.GetAction(state, bot.Id);

        action.Type.Should().Be(ActionType.PassPriority);
    }

    [Fact]
    public async Task GetAction_CastsRemoval_DuringEndStep()
    {
        var (state, bot, opponent, handler) = CreateReactiveScenario();

        // Bot has Swords to Plowshares in hand + W in mana pool
        var swords = GameCard.Create("Swords to Plowshares", "Instant");
        bot.Hand.Add(swords);
        bot.ManaPool.Add(ManaColor.White);

        // Opponent has a creature on battlefield
        var creature = GameCard.Create("Siege-Gang Commander", "Creature — Goblin");
        opponent.Battlefield.Add(creature);

        // Phase is End, stack is empty
        state.CurrentPhase = Phase.End;

        var action = await handler.GetAction(state, bot.Id);

        action.Type.Should().Be(ActionType.CastSpell);
        action.CardId.Should().Be(swords.Id);
        action.UseAlternateCost.Should().BeFalse();
    }

    [Fact]
    public async Task GetAction_PassesPriority_WhenNoReactiveSpells()
    {
        var (state, bot, opponent, handler) = CreateReactiveScenario();

        // Bot has only creatures in hand (no counterspells)
        var creature = GameCard.Create("Goblin Lackey", "Creature");
        bot.Hand.Add(creature);
        bot.ManaPool.Add(ManaColor.Red, 1);

        // Opponent's spell on stack (Siege-Gang Commander: {3}{R}{R})
        var opponentSpell = GameCard.Create("Siege-Gang Commander", "Creature");
        state.StackPush(new StackObject(opponentSpell, opponent.Id, new Dictionary<ManaColor, int>(), [], 0));

        var action = await handler.GetAction(state, bot.Id);

        action.Type.Should().Be(ActionType.PassPriority);
    }
}
