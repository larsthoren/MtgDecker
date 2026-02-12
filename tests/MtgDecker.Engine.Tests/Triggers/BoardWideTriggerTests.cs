using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests.Triggers;

public class BoardWideTriggerTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler p1Handler, TestDecisionHandler p2Handler) CreateSetup()
    {
        var p1Handler = new TestDecisionHandler();
        var p2Handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "Player 1", p1Handler),
            new Player(Guid.NewGuid(), "Player 2", p2Handler));
        var engine = new GameEngine(state);
        return (engine, state, p1Handler, p2Handler);
    }

    [Fact]
    public async Task AnyCreatureDies_TriggersFires_WhenCreatureDiesInCombat()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        // Put a permanent with AnyCreatureDies trigger on player1's battlefield
        var observer = new GameCard
        {
            Name = "Goblin Sharpshooter",
            CardTypes = CardType.Creature,
            BasePower = 1,
            BaseToughness = 1,
            Subtypes = ["Goblin"],
            Triggers = [new Trigger(GameEvent.Dies, TriggerCondition.AnyCreatureDies, new UntapSelfEffect())],
        };
        observer.IsTapped = true; // Start tapped
        state.Player1.Battlefield.Add(observer);

        // Put a creature to die on player2's battlefield (will block and die)
        var blocker = new GameCard
        {
            Name = "Token",
            CardTypes = CardType.Creature,
            BasePower = 1,
            BaseToughness = 1,
        };
        state.Player2.Battlefield.Add(blocker);

        // Put attacker on player1's battlefield
        var attacker = new GameCard
        {
            Name = "Big Attacker",
            CardTypes = CardType.Creature,
            BasePower = 5,
            BaseToughness = 5,
            TurnEnteredBattlefield = 0, // No summoning sickness
        };
        state.Player1.Battlefield.Add(attacker);

        state.ActivePlayer = state.Player1;
        state.TurnNumber = 1;

        // Declare attackers: the big creature
        p1Handler.EnqueueAttackers([attacker.Id]);
        // Declare blockers: the 1/1 blocks the attacker
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid> { { blocker.Id, attacker.Id } });

        await engine.RunCombatAsync(CancellationToken.None);

        // The blocker should have died, triggering AnyCreatureDies -> UntapSelfEffect on Sharpshooter
        observer.IsTapped.Should().BeFalse("Sharpshooter should untap when any creature dies");
    }

    [Fact]
    public async Task AnyCreatureDies_DoesNotFireForNonCreatureDeath()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();

        // Put a permanent with AnyCreatureDies trigger on player1's battlefield
        var observer = new GameCard
        {
            Name = "Goblin Sharpshooter",
            CardTypes = CardType.Creature,
            BasePower = 1,
            BaseToughness = 1,
            Subtypes = ["Goblin"],
            Triggers = [new Trigger(GameEvent.Dies, TriggerCondition.AnyCreatureDies, new UntapSelfEffect())],
        };
        observer.IsTapped = true;
        state.Player1.Battlefield.Add(observer);

        // Move a non-creature to graveyard manually (not a creature death)
        var enchantment = new GameCard { Name = "Wild Growth", CardTypes = CardType.Enchantment };
        state.Player2.Battlefield.Add(enchantment);
        state.Player2.Battlefield.RemoveById(enchantment.Id);
        state.Player2.Graveyard.Add(enchantment);

        // Sharpshooter should still be tapped (no creature died)
        observer.IsTapped.Should().BeTrue();
    }

    [Fact]
    public async Task SpellCast_ControllerCastsEnchantment_TriggersDrawForEnchantress()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        state.ActivePlayer = state.Player1;
        state.CurrentPhase = Phase.MainPhase1;

        // Put Argothian Enchantress on battlefield with draw trigger
        var enchantress = new GameCard
        {
            Name = "Argothian Enchantress",
            CardTypes = CardType.Creature | CardType.Enchantment,
            BasePower = 0,
            BaseToughness = 1,
            Subtypes = ["Human", "Druid"],
            Triggers = [new Trigger(GameEvent.SpellCast, TriggerCondition.ControllerCastsEnchantment, new DrawCardEffect())],
        };
        state.Player1.Battlefield.Add(enchantress);

        // Put some cards in library to draw from
        var cardInLibrary = new GameCard { Name = "Some Card" };
        state.Player1.Library.Add(cardInLibrary);

        // Cast an enchantment
        var enchantment = new GameCard
        {
            Name = "Wild Growth",
            CardTypes = CardType.Enchantment,
            ManaCost = MtgDecker.Engine.Mana.ManaCost.Parse("{G}"),
        };
        state.Player1.Hand.Add(enchantment);
        state.Player1.ManaPool.Add(ManaColor.Green, 1);

        var handCountBefore = state.Player1.Hand.Count;
        p1Handler.EnqueueAction(GameAction.PlayCard(state.Player1.Id, enchantment.Id));
        p1Handler.EnqueueAction(GameAction.Pass(state.Player1.Id));
        p2Handler.EnqueueAction(GameAction.Pass(state.Player2.Id));

        await engine.RunPriorityAsync();

        // After casting the enchantment, enchantress should trigger draw
        state.GameLog.Should().Contain(l => l.Contains("draws a card"));
    }

    [Fact]
    public async Task SpellCast_OpponentEnchantment_DoesNotTriggerYourEnchantress()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        state.ActivePlayer = state.Player2;
        state.CurrentPhase = Phase.MainPhase1;

        // Put Enchantress on player1's battlefield
        var enchantress = new GameCard
        {
            Name = "Argothian Enchantress",
            CardTypes = CardType.Creature | CardType.Enchantment,
            BasePower = 0,
            BaseToughness = 1,
            Subtypes = ["Human", "Druid"],
            Triggers = [new Trigger(GameEvent.SpellCast, TriggerCondition.ControllerCastsEnchantment, new DrawCardEffect())],
        };
        state.Player1.Battlefield.Add(enchantress);

        // Player2 casts an enchantment
        var enchantment = new GameCard
        {
            Name = "Wild Growth",
            CardTypes = CardType.Enchantment,
            ManaCost = MtgDecker.Engine.Mana.ManaCost.Parse("{G}"),
        };
        state.Player2.Hand.Add(enchantment);
        state.Player2.ManaPool.Add(ManaColor.Green, 1);

        p2Handler.EnqueueAction(GameAction.PlayCard(state.Player2.Id, enchantment.Id));
        p2Handler.EnqueueAction(GameAction.Pass(state.Player2.Id));
        p1Handler.EnqueueAction(GameAction.Pass(state.Player1.Id));

        await engine.RunPriorityAsync();

        // Player1's enchantress should NOT trigger for opponent's enchantment cast
        state.GameLog.Should().NotContain(l => l.Contains("Player 1") && l.Contains("draws a card"));
    }

    [Fact]
    public async Task CombatDamageDealt_SelfDealsCombatDamage_TriggersFires()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        state.ActivePlayer = state.Player1;
        state.TurnNumber = 1;

        // Goblin Lackey deals combat damage -> put a Goblin from hand
        var lackey = new GameCard
        {
            Name = "Goblin Lackey",
            CardTypes = CardType.Creature,
            BasePower = 1,
            BaseToughness = 1,
            Subtypes = ["Goblin"],
            TurnEnteredBattlefield = 0, // No summoning sickness
            Triggers = [new Trigger(GameEvent.CombatDamageDealt, TriggerCondition.SelfDealsCombatDamage,
                new PutCreatureFromHandEffect("Goblin"))],
        };
        state.Player1.Battlefield.Add(lackey);

        var goblinInHand = new GameCard
        {
            Name = "Goblin Piledriver",
            CardTypes = CardType.Creature,
            BasePower = 1,
            BaseToughness = 2,
            Subtypes = ["Goblin"],
        };
        state.Player1.Hand.Add(goblinInHand);

        // Declare lackey as attacker, no blockers
        p1Handler.EnqueueAttackers([lackey.Id]);
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid>());

        // The trigger should auto-choose the Goblin from hand
        p1Handler.EnqueueCardChoice(goblinInHand.Id);

        await engine.RunCombatAsync(CancellationToken.None);

        // Goblin Piledriver should be on battlefield
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Name == "Goblin Piledriver");
        state.Player1.Hand.Cards.Should().NotContain(c => c.Name == "Goblin Piledriver");
    }

    [Fact]
    public async Task CombatDamageDealt_Blocked_SelfDealsCombatDamage_DoesNotTrigger()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        state.ActivePlayer = state.Player1;
        state.TurnNumber = 1;

        var lackey = new GameCard
        {
            Name = "Goblin Lackey",
            CardTypes = CardType.Creature,
            BasePower = 1,
            BaseToughness = 1,
            Subtypes = ["Goblin"],
            TurnEnteredBattlefield = 0,
            Triggers = [new Trigger(GameEvent.CombatDamageDealt, TriggerCondition.SelfDealsCombatDamage,
                new PutCreatureFromHandEffect("Goblin"))],
        };
        state.Player1.Battlefield.Add(lackey);

        var blocker = new GameCard
        {
            Name = "Wall",
            CardTypes = CardType.Creature,
            BasePower = 0,
            BaseToughness = 4,
        };
        state.Player2.Battlefield.Add(blocker);

        var goblinInHand = new GameCard
        {
            Name = "Goblin Piledriver",
            CardTypes = CardType.Creature,
            BasePower = 1,
            BaseToughness = 2,
            Subtypes = ["Goblin"],
        };
        state.Player1.Hand.Add(goblinInHand);

        p1Handler.EnqueueAttackers([lackey.Id]);
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid> { { blocker.Id, lackey.Id } });

        await engine.RunCombatAsync(CancellationToken.None);

        // Lackey was blocked, so no combat damage to player -> no trigger
        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Name == "Goblin Piledriver");
        state.Player1.Hand.Cards.Should().Contain(c => c.Name == "Goblin Piledriver");
    }
}
