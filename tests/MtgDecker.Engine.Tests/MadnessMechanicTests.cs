using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

// ReSharper disable InconsistentNaming

namespace MtgDecker.Engine.Tests;

public class MadnessMechanicTests
{
    private (GameState state, GameEngine engine, TestDecisionHandler h1, TestDecisionHandler h2)
        CreateEngineState()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (state, engine, h1, h2);
    }

    // ─── Card Registration Tests ─────────────────────────────────────────

    #region Basking Rootwalla Registration

    [Fact]
    public void BaskingRootwalla_IsRegistered()
    {
        CardDefinitions.TryGet("Basking Rootwalla", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Creature);
    }

    [Fact]
    public void BaskingRootwalla_HasCorrectStats()
    {
        CardDefinitions.TryGet("Basking Rootwalla", out var def);

        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(1);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Green).WhoseValue.Should().Be(1);
        def.Power.Should().Be(1);
        def.Toughness.Should().Be(1);
        def.Subtypes.Should().Contain("Lizard");
    }

    [Fact]
    public void BaskingRootwalla_HasMadnessCostZero()
    {
        CardDefinitions.TryGet("Basking Rootwalla", out var def);
        def!.MadnessCost.Should().NotBeNull();
        def.MadnessCost!.ConvertedManaCost.Should().Be(0);
    }

    [Fact]
    public void BaskingRootwalla_HasOncePerTurnPumpAbility()
    {
        CardDefinitions.TryGet("Basking Rootwalla", out var def);
        def!.ActivatedAbilities.Should().HaveCount(1);
        def.ActivatedAbilities[0].OncePerTurn.Should().BeTrue();
        def.ActivatedAbilities[0].Cost.ManaCost.Should().NotBeNull();
        def.ActivatedAbilities[0].Cost.ManaCost!.ConvertedManaCost.Should().Be(2);
    }

    #endregion

    #region Arrogant Wurm Registration

    [Fact]
    public void ArrogantWurm_IsRegistered()
    {
        CardDefinitions.TryGet("Arrogant Wurm", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Creature);
    }

    [Fact]
    public void ArrogantWurm_HasCorrectStats()
    {
        CardDefinitions.TryGet("Arrogant Wurm", out var def);

        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(5);
        def.Power.Should().Be(4);
        def.Toughness.Should().Be(4);
        def.Subtypes.Should().Contain("Wurm");
    }

    [Fact]
    public void ArrogantWurm_HasMadnessCost()
    {
        CardDefinitions.TryGet("Arrogant Wurm", out var def);
        def!.MadnessCost.Should().NotBeNull();
        def.MadnessCost!.ConvertedManaCost.Should().Be(3);
        def.MadnessCost.ColorRequirements.Should().ContainKey(ManaColor.Green).WhoseValue.Should().Be(1);
    }

    [Fact]
    public void ArrogantWurm_HasTrample()
    {
        CardDefinitions.TryGet("Arrogant Wurm", out var def);
        def!.ContinuousEffects.Should().Contain(e =>
            e.GrantedKeyword == Keyword.Trample);
    }

    #endregion

    #region Circular Logic Registration

    [Fact]
    public void CircularLogic_IsRegistered()
    {
        CardDefinitions.TryGet("Circular Logic", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Instant);
    }

    [Fact]
    public void CircularLogic_HasCorrectCost()
    {
        CardDefinitions.TryGet("Circular Logic", out var def);
        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(3);
    }

    [Fact]
    public void CircularLogic_HasMadnessCost()
    {
        CardDefinitions.TryGet("Circular Logic", out var def);
        def!.MadnessCost.Should().NotBeNull();
        def.MadnessCost!.ConvertedManaCost.Should().Be(1);
        def.MadnessCost.ColorRequirements.Should().ContainKey(ManaColor.Blue).WhoseValue.Should().Be(1);
    }

    [Fact]
    public void CircularLogic_HasCircularLogicEffect()
    {
        CardDefinitions.TryGet("Circular Logic", out var def);
        def!.Effect.Should().BeOfType<CircularLogicEffect>();
    }

    #endregion

    // ─── Madness Mechanic Tests ──────────────────────────────────────────

    #region Madness via Wild Mongrel

    [Fact]
    public async Task Madness_DiscardViaWildMongrel_CastsForMadnessCost()
    {
        var (state, engine, h1, h2) = CreateEngineState();

        // Setup: Wild Mongrel on battlefield, Basking Rootwalla in hand
        var mongrel = new GameCard
        {
            Name = "Wild Mongrel",
            CardTypes = CardType.Creature,
            TurnEnteredBattlefield = 0,
        };
        state.Player1.Battlefield.Add(mongrel);

        var rootwalla = new GameCard
        {
            Name = "Basking Rootwalla",
            ManaCost = ManaCost.Parse("{G}"),
            CardTypes = CardType.Creature,
        };
        state.Player1.Hand.Add(rootwalla);

        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        // Player chooses to discard Rootwalla
        h1.EnqueueCardChoice(rootwalla.Id); // Choose Rootwalla to discard
        h1.EnqueueMadnessChoice(true); // Yes, cast for madness

        // Activate Wild Mongrel's ability (discard a card: +1/+1)
        h1.EnqueueAction(GameAction.ActivateAbility(state.Player1.Id, mongrel.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        // After stack resolves, pass to end priority
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));

        await engine.RunPriorityAsync(CancellationToken.None);

        // Rootwalla should be on the battlefield (resolved from stack via madness)
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Name == "Basking Rootwalla");
        // Not in graveyard
        state.Player1.Graveyard.Cards.Should().NotContain(c => c.Name == "Basking Rootwalla");
        // Not in exile
        state.Player1.Exile.Cards.Should().NotContain(c => c.Name == "Basking Rootwalla");
    }

    [Fact]
    public async Task Madness_DeclinedCast_CardGoesToGraveyard()
    {
        var (state, engine, h1, h2) = CreateEngineState();

        var mongrel = new GameCard
        {
            Name = "Wild Mongrel",
            CardTypes = CardType.Creature,
            TurnEnteredBattlefield = 0,
        };
        state.Player1.Battlefield.Add(mongrel);

        var rootwalla = new GameCard
        {
            Name = "Basking Rootwalla",
            ManaCost = ManaCost.Parse("{G}"),
            CardTypes = CardType.Creature,
        };
        state.Player1.Hand.Add(rootwalla);

        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        h1.EnqueueCardChoice(rootwalla.Id); // Choose Rootwalla to discard
        h1.EnqueueMadnessChoice(false); // No, don't cast for madness

        h1.EnqueueAction(GameAction.ActivateAbility(state.Player1.Id, mongrel.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));

        await engine.RunPriorityAsync(CancellationToken.None);

        // Rootwalla should be in graveyard
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Name == "Basking Rootwalla");
        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Name == "Basking Rootwalla");
    }

    #endregion

    #region Madness via HandleDiscardAsync with Mana Cost

    [Fact]
    public async Task Madness_WithZeroCost_AutoCasts()
    {
        var (state, engine, h1, h2) = CreateEngineState();

        var rootwalla = new GameCard
        {
            Name = "Basking Rootwalla",
            ManaCost = ManaCost.Parse("{G}"),
            CardTypes = CardType.Creature,
        };

        h1.EnqueueMadnessChoice(true); // Cast for madness cost {0}

        await engine.HandleDiscardAsync(rootwalla, state.Player1, CancellationToken.None);

        // Should be on the stack as a madness cast
        state.Stack.Should().HaveCount(1);
        var stackObj = state.Stack[0] as StackObject;
        stackObj.Should().NotBeNull();
        stackObj!.Card.Name.Should().Be("Basking Rootwalla");
        stackObj.IsMadness.Should().BeTrue();

        // Not in graveyard or exile
        state.Player1.Graveyard.Cards.Should().NotContain(c => c.Name == "Basking Rootwalla");
        state.Player1.Exile.Cards.Should().NotContain(c => c.Name == "Basking Rootwalla");
    }

    #endregion

    // ─── Once Per Turn Ability Tests ─────────────────────────────────────

    #region Once Per Turn

    [Fact]
    public async Task BaskingRootwalla_OncePerTurn_BlocksSecondActivation()
    {
        var (state, engine, h1, h2) = CreateEngineState();

        var rootwalla = new GameCard
        {
            Name = "Basking Rootwalla",
            CardTypes = CardType.Creature,
            TurnEnteredBattlefield = 0,
        };
        state.Player1.Battlefield.Add(rootwalla);
        state.Player1.ManaPool.Add(ManaColor.Green, 4);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 4);

        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        // First activation succeeds
        h1.EnqueueAction(GameAction.ActivateAbility(state.Player1.Id, rootwalla.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));

        // Second activation should fail
        h1.EnqueueAction(GameAction.ActivateAbility(state.Player1.Id, rootwalla.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));

        await engine.RunPriorityAsync(CancellationToken.None);

        // Should have log about once-per-turn
        state.GameLog.Should().Contain(l => l.Contains("once each turn"));
    }

    [Fact]
    public async Task BaskingRootwalla_OncePerTurn_ResetsNextTurn()
    {
        var (state, engine, h1, h2) = CreateEngineState();

        var rootwalla = new GameCard
        {
            Name = "Basking Rootwalla",
            CardTypes = CardType.Creature,
            TurnEnteredBattlefield = 0,
        };
        state.Player1.Battlefield.Add(rootwalla);

        // Track: activated index 0 this turn
        rootwalla.AbilitiesActivatedThisTurn.Add(0);

        // Simulate turn start clearing
        state.TurnNumber = 2;
        foreach (var card in state.Player1.Battlefield.Cards)
            card.AbilitiesActivatedThisTurn.Clear();

        rootwalla.AbilitiesActivatedThisTurn.Should().BeEmpty();
    }

    #endregion

    // ─── Circular Logic Tests ────────────────────────────────────────────

    #region Circular Logic

    [Fact]
    public void CircularLogicEffect_CountersSpell_WhenOpponentCannotPay()
    {
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", new TestDecisionHandler()),
            new Player(Guid.NewGuid(), "P2", new TestDecisionHandler()));

        // Put some cards in caster's (P1) graveyard
        state.Player1.Graveyard.Add(new GameCard { Name = "Card A" });
        state.Player1.Graveyard.Add(new GameCard { Name = "Card B" });
        state.Player1.Graveyard.Add(new GameCard { Name = "Card C" });

        // Opponent (P2) has a spell on the stack with no mana
        var targetSpell = new GameCard { Name = "Lightning Bolt", CardTypes = CardType.Instant };
        var targetStackObj = new StackObject(targetSpell, state.Player2.Id,
            new Dictionary<ManaColor, int>(), [], 0);
        state.StackPush(targetStackObj);

        // Circular Logic spell targeting the Lightning Bolt
        var circularLogic = new GameCard { Name = "Circular Logic", CardTypes = CardType.Instant };
        var clStackObj = new StackObject(circularLogic, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            [new TargetInfo(targetSpell.Id, state.Player2.Id, ZoneType.Stack)],
            1);

        var effect = new CircularLogicEffect();
        effect.Resolve(state, clStackObj);

        // Lightning Bolt should be countered (in graveyard)
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Name == "Lightning Bolt");
        state.Stack.OfType<StackObject>().Should().NotContain(s => s.Card.Name == "Lightning Bolt");
    }

    [Fact]
    public void CircularLogicEffect_SpellResolves_WhenOpponentCanPay()
    {
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", new TestDecisionHandler()),
            new Player(Guid.NewGuid(), "P2", new TestDecisionHandler()));

        // 2 cards in caster's graveyard = cost of {2}
        state.Player1.Graveyard.Add(new GameCard { Name = "Card A" });
        state.Player1.Graveyard.Add(new GameCard { Name = "Card B" });

        // Opponent has enough mana
        state.Player2.ManaPool.Add(ManaColor.Red, 3);

        var targetSpell = new GameCard { Name = "Lightning Bolt", CardTypes = CardType.Instant };
        var targetStackObj = new StackObject(targetSpell, state.Player2.Id,
            new Dictionary<ManaColor, int>(), [], 0);
        state.StackPush(targetStackObj);

        var circularLogic = new GameCard { Name = "Circular Logic", CardTypes = CardType.Instant };
        var clStackObj = new StackObject(circularLogic, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            [new TargetInfo(targetSpell.Id, state.Player2.Id, ZoneType.Stack)],
            1);

        var effect = new CircularLogicEffect();
        effect.Resolve(state, clStackObj);

        // Lightning Bolt should still be on the stack (not countered)
        state.Stack.OfType<StackObject>().Should().Contain(s => s.Card.Name == "Lightning Bolt");
        // Mana should be deducted
        state.Player2.ManaPool.Total.Should().Be(1); // 3 - 2 = 1
    }

    [Fact]
    public void CircularLogicEffect_EmptyGraveyard_DoesNotCounter()
    {
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", new TestDecisionHandler()),
            new Player(Guid.NewGuid(), "P2", new TestDecisionHandler()));

        // Empty graveyard = cost of 0 = always resolves
        var targetSpell = new GameCard { Name = "Lightning Bolt", CardTypes = CardType.Instant };
        var targetStackObj = new StackObject(targetSpell, state.Player2.Id,
            new Dictionary<ManaColor, int>(), [], 0);
        state.StackPush(targetStackObj);

        var circularLogic = new GameCard { Name = "Circular Logic", CardTypes = CardType.Instant };
        var clStackObj = new StackObject(circularLogic, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            [new TargetInfo(targetSpell.Id, state.Player2.Id, ZoneType.Stack)],
            1);

        var effect = new CircularLogicEffect();
        effect.Resolve(state, clStackObj);

        // Spell should still be on the stack
        state.Stack.OfType<StackObject>().Should().Contain(s => s.Card.Name == "Lightning Bolt");
    }

    #endregion

    // ─── Madness with Non-Zero Cost Tests ────────────────────────────────

    #region Arrogant Wurm Madness

    [Fact]
    public async Task ArrogantWurm_MadnessWithMana_PutsOnStack()
    {
        var (state, engine, h1, h2) = CreateEngineState();

        var mongrel = new GameCard
        {
            Name = "Wild Mongrel",
            CardTypes = CardType.Creature,
            TurnEnteredBattlefield = 0,
        };
        state.Player1.Battlefield.Add(mongrel);

        var wurm = new GameCard
        {
            Name = "Arrogant Wurm",
            ManaCost = ManaCost.Parse("{3}{G}{G}"),
            CardTypes = CardType.Creature,
        };
        state.Player1.Hand.Add(wurm);

        // Give player enough mana for madness cost {2}{G}
        state.Player1.ManaPool.Add(ManaColor.Green, 1);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 2);

        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        h1.EnqueueCardChoice(wurm.Id); // Choose Wurm to discard
        h1.EnqueueMadnessChoice(true); // Cast for madness

        h1.EnqueueAction(GameAction.ActivateAbility(state.Player1.Id, mongrel.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        // Pass for madness-cast Wurm on stack
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));

        await engine.RunPriorityAsync(CancellationToken.None);

        // Wurm should be on the battlefield after resolving
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Name == "Arrogant Wurm");
    }

    [Fact]
    public async Task ArrogantWurm_MadnessCannotPay_GoesToGraveyard()
    {
        var (state, engine, h1, h2) = CreateEngineState();

        var mongrel = new GameCard
        {
            Name = "Wild Mongrel",
            CardTypes = CardType.Creature,
            TurnEnteredBattlefield = 0,
        };
        state.Player1.Battlefield.Add(mongrel);

        var wurm = new GameCard
        {
            Name = "Arrogant Wurm",
            ManaCost = ManaCost.Parse("{3}{G}{G}"),
            CardTypes = CardType.Creature,
        };
        state.Player1.Hand.Add(wurm);

        // No mana available
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        h1.EnqueueCardChoice(wurm.Id); // Choose Wurm to discard
        h1.EnqueueMadnessChoice(true); // Try to cast for madness (but can't pay)

        h1.EnqueueAction(GameAction.ActivateAbility(state.Player1.Id, mongrel.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));

        await engine.RunPriorityAsync(CancellationToken.None);

        // Wurm should be in graveyard (couldn't pay)
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Name == "Arrogant Wurm");
        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Name == "Arrogant Wurm");
    }

    #endregion

    // ─── Non-Madness Discard Unaffected ──────────────────────────────────

    #region Non-Madness Cards

    [Fact]
    public async Task NonMadnessCard_DiscardsNormally()
    {
        var (state, engine, h1, h2) = CreateEngineState();

        var mongrel = new GameCard
        {
            Name = "Wild Mongrel",
            CardTypes = CardType.Creature,
            TurnEnteredBattlefield = 0,
        };
        state.Player1.Battlefield.Add(mongrel);

        var normalCard = new GameCard
        {
            Name = "Forest",
            CardTypes = CardType.Land,
        };
        state.Player1.Hand.Add(normalCard);

        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        h1.EnqueueCardChoice(normalCard.Id); // Choose Forest to discard

        h1.EnqueueAction(GameAction.ActivateAbility(state.Player1.Id, mongrel.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));

        await engine.RunPriorityAsync(CancellationToken.None);

        // Normal card goes straight to graveyard
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Name == "Forest");
        state.Player1.Exile.Cards.Should().NotContain(c => c.Name == "Forest");
    }

    #endregion

    // ─── HandleDiscardAsync Direct Tests ─────────────────────────────────

    #region HandleDiscardAsync

    [Fact]
    public async Task HandleDiscardAsync_MadnessCard_ExilesThenCasts()
    {
        var (state, engine, h1, h2) = CreateEngineState();

        var rootwalla = new GameCard
        {
            Name = "Basking Rootwalla",
            ManaCost = ManaCost.Parse("{G}"),
            CardTypes = CardType.Creature,
        };

        h1.EnqueueMadnessChoice(true);

        await engine.HandleDiscardAsync(rootwalla, state.Player1, CancellationToken.None);

        // Should be on the stack as a madness cast
        state.Stack.Should().HaveCount(1);
        var stackObj = state.Stack[0] as StackObject;
        stackObj.Should().NotBeNull();
        stackObj!.Card.Name.Should().Be("Basking Rootwalla");
        stackObj.IsMadness.Should().BeTrue();

        // Not in graveyard or exile
        state.Player1.Graveyard.Cards.Should().NotContain(c => c.Name == "Basking Rootwalla");
        state.Player1.Exile.Cards.Should().NotContain(c => c.Name == "Basking Rootwalla");
    }

    [Fact]
    public async Task HandleDiscardAsync_MadnessDeclined_GoesToGraveyard()
    {
        var (state, engine, h1, h2) = CreateEngineState();

        var rootwalla = new GameCard
        {
            Name = "Basking Rootwalla",
            ManaCost = ManaCost.Parse("{G}"),
            CardTypes = CardType.Creature,
        };

        h1.EnqueueMadnessChoice(false);

        await engine.HandleDiscardAsync(rootwalla, state.Player1, CancellationToken.None);

        state.Stack.Should().BeEmpty();
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Name == "Basking Rootwalla");
    }

    [Fact]
    public async Task HandleDiscardAsync_NonMadnessCard_GoesToGraveyard()
    {
        var (state, engine, h1, h2) = CreateEngineState();

        var card = new GameCard
        {
            Name = "Mountain",
            CardTypes = CardType.Land,
        };

        await engine.HandleDiscardAsync(card, state.Player1, CancellationToken.None);

        state.Player1.Graveyard.Cards.Should().Contain(c => c.Name == "Mountain");
        state.Stack.Should().BeEmpty();
    }

    #endregion

    // ─── GameState HandleDiscardAsync Delegate ───────────────────────────

    #region Delegate Setup

    [Fact]
    public void GameEngine_SetsHandleDiscardAsyncOnGameState()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);

        state.HandleDiscardAsync.Should().BeNull();

        _ = new GameEngine(state);

        state.HandleDiscardAsync.Should().NotBeNull();
    }

    #endregion

    // ─── IsMadness on StackObject ────────────────────────────────────────

    #region StackObject IsMadness

    [Fact]
    public void StackObject_IsMadness_DefaultsFalse()
    {
        var card = new GameCard { Name = "Test" };
        var so = new StackObject(card, Guid.NewGuid(),
            new Dictionary<ManaColor, int>(), [], 0);
        so.IsMadness.Should().BeFalse();
    }

    [Fact]
    public void StackObject_IsMadness_CanBeSetTrue()
    {
        var card = new GameCard { Name = "Test" };
        var so = new StackObject(card, Guid.NewGuid(),
            new Dictionary<ManaColor, int>(), [], 0)
        {
            IsMadness = true,
        };
        so.IsMadness.Should().BeTrue();
    }

    #endregion
}
