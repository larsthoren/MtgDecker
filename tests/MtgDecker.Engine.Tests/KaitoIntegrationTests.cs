using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class KaitoIntegrationTests
{
    private const string KaitoName = "Kaito, Bane of Nightmares";

    private static (GameEngine engine, GameState state, Player p1, Player p2,
        TestDecisionHandler h1, TestDecisionHandler h2) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        for (int i = 0; i < 40; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}" });
            p2.Library.Add(new GameCard { Name = $"Card{i}" });
        }
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, p1, p2, h1, h2);
    }

    #region 1. Cast Kaito via engine, verify ETB loyalty

    [Fact]
    public async Task Cast_Kaito_ViaCastSpell_EntersWithLoyalty4()
    {
        var (engine, state, p1, p2, h1, h2) = CreateSetup();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        // Give P1 mana to cast Kaito ({2}{U}{B})
        p1.ManaPool.Add(ManaColor.Blue, 1);
        p1.ManaPool.Add(ManaColor.Black, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 2);

        var kaito = GameCard.Create(KaitoName);
        p1.Hand.Add(kaito);

        // CastSpell puts the spell on the stack
        await engine.ExecuteAction(GameAction.CastSpell(p1.Id, kaito.Id));

        // Kaito should be on the stack
        state.StackCount.Should().BeGreaterThanOrEqualTo(1);

        // Resolve the stack
        await engine.ResolveAllTriggersAsync();

        // Kaito on battlefield
        p1.Battlefield.Cards.Should().Contain(c => c.Name == KaitoName);

        // Verify loyalty counters = starting loyalty of 4
        var kaitoOnBF = p1.Battlefield.Cards.First(c => c.Name == KaitoName);
        kaitoOnBF.GetCounters(CounterType.Loyalty).Should().Be(4);
        kaitoOnBF.Loyalty.Should().Be(4);
        kaitoOnBF.IsPlaneswalker.Should().BeTrue();
    }

    [Fact]
    public async Task Cast_Kaito_ManaPoolDepleted_AfterCasting()
    {
        var (engine, state, p1, p2, h1, h2) = CreateSetup();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        p1.ManaPool.Add(ManaColor.Blue, 1);
        p1.ManaPool.Add(ManaColor.Black, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 2);

        var kaito = GameCard.Create(KaitoName);
        p1.Hand.Add(kaito);

        await engine.ExecuteAction(GameAction.CastSpell(p1.Id, kaito.Id));
        await engine.ResolveAllTriggersAsync();

        // Mana should have been spent
        p1.ManaPool.Total.Should().Be(0);
    }

    #endregion

    #region 2. Activate +1 ability — loyalty goes to 5, emblem created

    [Fact]
    public async Task Activate_PlusOne_LoyaltyGoesTo5_EmblemCreated()
    {
        var (engine, state, p1, p2, h1, h2) = CreateSetup();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var kaito = GameCard.Create(KaitoName);
        kaito.AddCounters(CounterType.Loyalty, 4);
        p1.Battlefield.Add(kaito);

        // Activate +1 ability (index 0)
        await engine.ExecuteAction(GameAction.ActivateLoyaltyAbility(p1.Id, kaito.Id, 0));

        // Loyalty cost is paid immediately: 4 + 1 = 5
        kaito.Loyalty.Should().Be(5);

        // Ability on stack
        state.StackCount.Should().BeGreaterThanOrEqualTo(1);

        // Resolve the stack
        await engine.ResolveAllTriggersAsync();

        // Emblem created
        p1.Emblems.Should().HaveCount(1);
        p1.Emblems[0].Description.Should().Contain("Ninja");
        p1.Emblems[0].Effect.Type.Should().Be(ContinuousEffectType.ModifyPowerToughness);
    }

    [Fact]
    public async Task Activate_PlusOne_Twice_NotAllowedSameTurn()
    {
        var (engine, state, p1, p2, h1, h2) = CreateSetup();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var kaito = GameCard.Create(KaitoName);
        kaito.AddCounters(CounterType.Loyalty, 4);
        p1.Battlefield.Add(kaito);

        // First activation
        await engine.ExecuteAction(GameAction.ActivateLoyaltyAbility(p1.Id, kaito.Id, 0));
        await engine.ResolveAllTriggersAsync();

        kaito.Loyalty.Should().Be(5);
        p1.Emblems.Should().HaveCount(1);

        // Second activation — should be blocked (one ability per PW per turn)
        await engine.ExecuteAction(GameAction.ActivateLoyaltyAbility(p1.Id, kaito.Id, 0));

        // Loyalty should still be 5 (cost not paid)
        kaito.Loyalty.Should().Be(5);
        // No second emblem
        p1.Emblems.Should().HaveCount(1);
    }

    #endregion

    #region 3. Activate 0 ability with opponent life loss

    [Fact]
    public async Task Activate_Zero_SurveisAndDraws_WhenOpponentLostLife()
    {
        var (engine, state, p1, p2, h1, h2) = CreateSetup();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var kaito = GameCard.Create(KaitoName);
        kaito.AddCounters(CounterType.Loyalty, 4);
        p1.Battlefield.Add(kaito);

        // Set up opponent life loss this turn
        p2.LifeLostThisTurn = 3;

        // Library top cards for surveil (P1 already has 40 from setup)
        // Both surveil choices: keep on top (null = don't put in graveyard)
        h1.EnqueueCardChoice(null); // surveil card 1: keep
        h1.EnqueueCardChoice(null); // surveil card 2: keep

        // Activate 0 ability (index 1)
        await engine.ExecuteAction(GameAction.ActivateLoyaltyAbility(p1.Id, kaito.Id, 1));

        // Loyalty stays at 4 (0 cost)
        kaito.Loyalty.Should().Be(4);

        // Resolve the stack
        await engine.ResolveAllTriggersAsync();

        // Should have drawn 1 card (1 opponent lost life)
        // P1 started with 0 cards in hand, draw from surveil+draw effect
        p1.Hand.Cards.Count.Should().BeGreaterThanOrEqualTo(1,
            "should draw at least 1 card because opponent lost life this turn");
    }

    [Fact]
    public async Task Activate_Zero_NoDraws_WhenOpponentDidNotLoseLife()
    {
        var (engine, state, p1, p2, h1, h2) = CreateSetup();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var kaito = GameCard.Create(KaitoName);
        kaito.AddCounters(CounterType.Loyalty, 4);
        p1.Battlefield.Add(kaito);

        // Opponent has NOT lost life
        p2.LifeLostThisTurn = 0;

        // Surveil choices: both keep on top
        h1.EnqueueCardChoice(null);
        h1.EnqueueCardChoice(null);

        await engine.ExecuteAction(GameAction.ActivateLoyaltyAbility(p1.Id, kaito.Id, 1));
        await engine.ResolveAllTriggersAsync();

        // No cards drawn (opponent didn't lose life)
        p1.Hand.Cards.Count.Should().Be(0);
    }

    #endregion

    #region 4. Activate -2 ability — tap and stun target creature

    [Fact]
    public async Task Activate_MinusTwo_TapsAndStuns_OpponentCreature()
    {
        var (engine, state, p1, p2, h1, h2) = CreateSetup();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var kaito = GameCard.Create(KaitoName);
        kaito.AddCounters(CounterType.Loyalty, 4);
        p1.Battlefield.Add(kaito);

        // Target creature on opponent's battlefield
        var creature = new GameCard
        {
            Name = "Grizzly Bears",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        p2.Battlefield.Add(creature);

        // Enqueue target choice for TapAndStunEffect
        h1.EnqueueCardChoice(creature.Id);

        // Activate -2 ability (index 2)
        await engine.ExecuteAction(GameAction.ActivateLoyaltyAbility(p1.Id, kaito.Id, 2));

        // Loyalty: 4 - 2 = 2
        kaito.Loyalty.Should().Be(2);

        // Resolve the stack
        await engine.ResolveAllTriggersAsync();

        // Target creature is tapped with 2 stun counters
        creature.IsTapped.Should().BeTrue();
        creature.GetCounters(CounterType.Stun).Should().Be(2);
    }

    [Fact]
    public async Task Activate_MinusTwo_CannotActivate_WithInsufficientLoyalty()
    {
        var (engine, state, p1, p2, h1, h2) = CreateSetup();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var kaito = GameCard.Create(KaitoName);
        kaito.AddCounters(CounterType.Loyalty, 1); // Only 1 loyalty, need 2
        p1.Battlefield.Add(kaito);

        var creature = new GameCard
        {
            Name = "Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        p2.Battlefield.Add(creature);

        // Try to activate -2 with only 1 loyalty
        await engine.ExecuteAction(GameAction.ActivateLoyaltyAbility(p1.Id, kaito.Id, 2));

        // Loyalty should not change (ability can't be activated)
        kaito.Loyalty.Should().Be(1);

        // Nothing on stack
        state.StackCount.Should().Be(0);

        // Creature should not be affected
        creature.IsTapped.Should().BeFalse();
    }

    #endregion

    #region 5. Creature mode during your turn

    [Fact]
    public void Kaito_DuringControllerTurn_Is3_4_CreatureWithHexproof()
    {
        var (engine, state, p1, p2, h1, h2) = CreateSetup();
        state.ActivePlayer = p1;

        var kaito = GameCard.Create(KaitoName);
        kaito.AddCounters(CounterType.Loyalty, 4);
        p1.Battlefield.Add(kaito);

        engine.RecalculateState();

        kaito.IsCreature.Should().BeTrue();
        kaito.IsPlaneswalker.Should().BeTrue();
        kaito.Power.Should().Be(3);
        kaito.Toughness.Should().Be(4);
        kaito.ActiveKeywords.Should().Contain(Keyword.Hexproof);
    }

    [Fact]
    public void Kaito_DuringControllerTurn_HasNinjaSubtype()
    {
        var (engine, state, p1, p2, h1, h2) = CreateSetup();
        state.ActivePlayer = p1;

        var kaito = GameCard.Create(KaitoName);
        kaito.AddCounters(CounterType.Loyalty, 4);
        p1.Battlefield.Add(kaito);

        engine.RecalculateState();

        // Kaito should have Ninja subtype from CardDefinitions
        kaito.Subtypes.Should().Contain("Ninja");
    }

    #endregion

    #region 6. Not a creature during opponent's turn

    [Fact]
    public void Kaito_DuringOpponentTurn_NotACreature()
    {
        var (engine, state, p1, p2, h1, h2) = CreateSetup();
        state.ActivePlayer = p2; // Opponent's turn

        var kaito = GameCard.Create(KaitoName);
        kaito.AddCounters(CounterType.Loyalty, 4);
        p1.Battlefield.Add(kaito);

        engine.RecalculateState();

        kaito.IsCreature.Should().BeFalse();
        kaito.IsPlaneswalker.Should().BeTrue();
        kaito.ActiveKeywords.Should().NotContain(Keyword.Hexproof);
        // Power should be null (not a creature)
        kaito.Power.Should().BeNull();
    }

    #endregion

    #region 7. SBA removes Kaito at 0 loyalty

    [Fact]
    public async Task SBA_RemovesKaito_AtZeroLoyalty()
    {
        var (engine, state, p1, p2, h1, h2) = CreateSetup();
        state.ActivePlayer = p1;

        var kaito = GameCard.Create(KaitoName);
        // No loyalty counters = 0 loyalty
        p1.Battlefield.Add(kaito);

        await engine.CheckStateBasedActionsAsync();

        p1.Battlefield.Cards.Should().NotContain(c => c.Name == KaitoName);
        p1.Graveyard.Cards.Should().Contain(c => c.Name == KaitoName);
    }

    [Fact]
    public async Task SBA_RemovesKaito_AfterLoyaltyReducedToZero()
    {
        var (engine, state, p1, p2, h1, h2) = CreateSetup();
        state.ActivePlayer = p1;

        var kaito = GameCard.Create(KaitoName);
        kaito.AddCounters(CounterType.Loyalty, 2);
        p1.Battlefield.Add(kaito);

        // Remove loyalty to 0
        kaito.RemoveCounter(CounterType.Loyalty);
        kaito.RemoveCounter(CounterType.Loyalty);
        kaito.Loyalty.Should().Be(0);

        await engine.CheckStateBasedActionsAsync();

        p1.Battlefield.Cards.Should().NotContain(c => c.Name == KaitoName);
        p1.Graveyard.Cards.Should().Contain(c => c.Name == KaitoName);
    }

    #endregion

    #region 8. Ninjutsu during combat

    [Fact]
    public async Task Ninjutsu_ReturnsUnblockedAttacker_KaitoEntersAttacking()
    {
        var (engine, state, p1, p2, h1, h2) = CreateSetup();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.Combat;
        state.CombatStep = CombatStep.DeclareBlockers;

        // Set up combat with an unblocked attacker
        var attacker = new GameCard
        {
            Name = "Ink-Eyes Servant",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
            TurnEnteredBattlefield = state.TurnNumber - 1,
        };
        p1.Battlefield.Add(attacker);

        state.Combat = new CombatState(p1.Id, p2.Id);
        state.Combat.DeclareAttacker(attacker.Id);
        // attacker is NOT blocked

        // Kaito in hand
        var kaito = GameCard.Create(KaitoName);
        p1.Hand.Add(kaito);

        // Give P1 mana to pay ninjutsu cost ({1}{U}{B})
        p1.ManaPool.Add(ManaColor.Blue, 1);
        p1.ManaPool.Add(ManaColor.Black, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 1);

        // Activate ninjutsu
        await engine.ExecuteAction(GameAction.Ninjutsu(p1.Id, kaito.Id, attacker.Id));

        // Attacker returned to hand
        p1.Hand.Cards.Should().Contain(c => c.Name == "Ink-Eyes Servant");
        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Ink-Eyes Servant");

        // Kaito enters battlefield tapped and attacking
        p1.Battlefield.Cards.Should().Contain(c => c.Name == KaitoName);
        var kaitoOnBF = p1.Battlefield.Cards.First(c => c.Name == KaitoName);
        kaitoOnBF.IsTapped.Should().BeTrue();

        // Kaito enters with loyalty counters
        kaitoOnBF.Loyalty.Should().Be(4);

        // Kaito is an attacker
        state.Combat.Attackers.Should().Contain(kaitoOnBF.Id);
    }

    [Fact]
    public async Task Ninjutsu_FailsOnBlockedAttacker()
    {
        var (engine, state, p1, p2, h1, h2) = CreateSetup();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.Combat;
        state.CombatStep = CombatStep.DeclareBlockers;

        var attacker = new GameCard
        {
            Name = "Goblin Piker",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 1,
            TurnEnteredBattlefield = state.TurnNumber - 1,
        };
        p1.Battlefield.Add(attacker);

        var blocker = new GameCard
        {
            Name = "Wall of Stone",
            CardTypes = CardType.Creature,
            BasePower = 0,
            BaseToughness = 4,
            TurnEnteredBattlefield = state.TurnNumber - 1,
        };
        p2.Battlefield.Add(blocker);

        state.Combat = new CombatState(p1.Id, p2.Id);
        state.Combat.DeclareAttacker(attacker.Id);
        state.Combat.DeclareBlocker(blocker.Id, attacker.Id); // Blocked!

        var kaito = GameCard.Create(KaitoName);
        p1.Hand.Add(kaito);

        p1.ManaPool.Add(ManaColor.Blue, 1);
        p1.ManaPool.Add(ManaColor.Black, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 1);

        await engine.ExecuteAction(GameAction.Ninjutsu(p1.Id, kaito.Id, attacker.Id));

        // Kaito should still be in hand (ninjutsu failed)
        p1.Hand.Cards.Should().Contain(c => c.Name == KaitoName);
        p1.Battlefield.Cards.Should().NotContain(c => c.Name == KaitoName);

        // Attacker should still be on battlefield
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Goblin Piker");
    }

    [Fact]
    public async Task Ninjutsu_FailsWithoutEnoughMana()
    {
        var (engine, state, p1, p2, h1, h2) = CreateSetup();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.Combat;
        state.CombatStep = CombatStep.DeclareBlockers;

        var attacker = new GameCard
        {
            Name = "Rogue",
            CardTypes = CardType.Creature,
            BasePower = 1,
            BaseToughness = 1,
            TurnEnteredBattlefield = state.TurnNumber - 1,
        };
        p1.Battlefield.Add(attacker);

        state.Combat = new CombatState(p1.Id, p2.Id);
        state.Combat.DeclareAttacker(attacker.Id);

        var kaito = GameCard.Create(KaitoName);
        p1.Hand.Add(kaito);

        // Only 1 mana — not enough for {1}{U}{B}
        p1.ManaPool.Add(ManaColor.Blue, 1);

        await engine.ExecuteAction(GameAction.Ninjutsu(p1.Id, kaito.Id, attacker.Id));

        // Kaito still in hand
        p1.Hand.Cards.Should().Contain(c => c.Name == KaitoName);
        p1.Battlefield.Cards.Should().NotContain(c => c.Name == KaitoName);
    }

    [Fact]
    public async Task Ninjutsu_FailsOutsideCombat()
    {
        var (engine, state, p1, p2, h1, h2) = CreateSetup();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1; // Not combat phase!

        var kaito = GameCard.Create(KaitoName);
        p1.Hand.Add(kaito);

        // Even with mana, ninjutsu requires combat
        p1.ManaPool.Add(ManaColor.Blue, 1);
        p1.ManaPool.Add(ManaColor.Black, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 1);

        // Need an "attacker" to reference (but we're not in combat)
        var creature = new GameCard
        {
            Name = "Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        p1.Battlefield.Add(creature);

        await engine.ExecuteAction(GameAction.Ninjutsu(p1.Id, kaito.Id, creature.Id));

        // Kaito should remain in hand
        p1.Hand.Cards.Should().Contain(c => c.Name == KaitoName);
    }

    #endregion

    #region Emblem integration with Kaito creature mode

    [Fact]
    public async Task Emblem_BoostsKaito_InCreatureMode()
    {
        var (engine, state, p1, p2, h1, h2) = CreateSetup();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var kaito = GameCard.Create(KaitoName);
        kaito.AddCounters(CounterType.Loyalty, 4);
        p1.Battlefield.Add(kaito);

        // Activate +1 to create a Ninja emblem
        await engine.ExecuteAction(GameAction.ActivateLoyaltyAbility(p1.Id, kaito.Id, 0));
        await engine.ResolveAllTriggersAsync();

        // Kaito should be a 3/4 Ninja creature during controller's turn
        // Emblem gives Ninjas +1/+1, so Kaito should be 4/5
        kaito.IsCreature.Should().BeTrue();
        kaito.Subtypes.Should().Contain("Ninja");
        kaito.Power.Should().Be(4, "Kaito is a 3/4 Ninja + emblem gives +1/+1");
        kaito.Toughness.Should().Be(5, "Kaito is a 3/4 Ninja + emblem gives +1/+1");
    }

    #endregion
}
