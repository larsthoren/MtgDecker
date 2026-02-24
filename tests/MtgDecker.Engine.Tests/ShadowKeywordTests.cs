using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class ShadowKeywordTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler p1Handler, TestDecisionHandler p2Handler) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);

        for (int i = 0; i < 40; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}", TypeLine = "Creature" });
            p2.Library.Add(new GameCard { Name = $"Card{i}", TypeLine = "Creature" });
        }

        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, h1, h2);
    }

    [Fact]
    public async Task ShadowCreature_CannotBeBlockedByNonShadow()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var shadowAttacker = new GameCard
        {
            Name = "Soltari Foot Soldier", TypeLine = "Creature — Soltari Soldier",
            Power = 1, Toughness = 1, TurnEnteredBattlefield = 0
        };
        shadowAttacker.ActiveKeywords.Add(Keyword.Shadow);
        state.Player1.Battlefield.Add(shadowAttacker);

        var normalBlocker = new GameCard
        {
            Name = "Bear", TypeLine = "Creature — Bear",
            Power = 2, Toughness = 2, TurnEnteredBattlefield = 0
        };
        state.Player2.Battlefield.Add(normalBlocker);

        p1Handler.EnqueueAttackers(new List<Guid> { shadowAttacker.Id });
        // Defender tries to block with normal creature — should be rejected
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid> { { normalBlocker.Id, shadowAttacker.Id } });

        await engine.RunCombatAsync(CancellationToken.None);

        // Shadow creature should deal damage unblocked
        state.Player2.Life.Should().Be(19, "shadow creature cannot be blocked by non-shadow");
    }

    [Fact]
    public async Task ShadowCreature_CanBeBlockedByShadow()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var shadowAttacker = new GameCard
        {
            Name = "Soltari Foot Soldier", TypeLine = "Creature — Soltari Soldier",
            Power = 1, Toughness = 1, TurnEnteredBattlefield = 0
        };
        shadowAttacker.ActiveKeywords.Add(Keyword.Shadow);
        state.Player1.Battlefield.Add(shadowAttacker);

        var shadowBlocker = new GameCard
        {
            Name = "Shadow Blocker", TypeLine = "Creature — Spirit",
            Power = 2, Toughness = 2, TurnEnteredBattlefield = 0
        };
        shadowBlocker.ActiveKeywords.Add(Keyword.Shadow);
        state.Player2.Battlefield.Add(shadowBlocker);

        p1Handler.EnqueueAttackers(new List<Guid> { shadowAttacker.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid> { { shadowBlocker.Id, shadowAttacker.Id } });

        await engine.RunCombatAsync(CancellationToken.None);

        // Blocked — no player damage
        state.Player2.Life.Should().Be(20, "shadow creature blocked by shadow creature");
    }

    [Fact]
    public async Task ShadowBlocker_CannotBlockNonShadow()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var normalAttacker = new GameCard
        {
            Name = "Bear", TypeLine = "Creature — Bear",
            Power = 3, Toughness = 3, TurnEnteredBattlefield = 0
        };
        state.Player1.Battlefield.Add(normalAttacker);

        var shadowBlocker = new GameCard
        {
            Name = "Shadow Blocker", TypeLine = "Creature — Spirit",
            Power = 2, Toughness = 2, TurnEnteredBattlefield = 0
        };
        shadowBlocker.ActiveKeywords.Add(Keyword.Shadow);
        state.Player2.Battlefield.Add(shadowBlocker);

        p1Handler.EnqueueAttackers(new List<Guid> { normalAttacker.Id });
        // Shadow creature tries to block normal attacker — should be rejected
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid> { { shadowBlocker.Id, normalAttacker.Id } });

        await engine.RunCombatAsync(CancellationToken.None);

        // Normal attacker deals damage unblocked
        state.Player2.Life.Should().Be(17, "shadow creature cannot block non-shadow attacker");
    }

    [Fact]
    public async Task ProtectionFromBlack_CannotBeBlockedByBlackCreature()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var protectedAttacker = new GameCard
        {
            Name = "Soltari Monk", TypeLine = "Creature — Soltari Monk Cleric",
            Power = 2, Toughness = 1, TurnEnteredBattlefield = 0
        };
        protectedAttacker.ActiveKeywords.Add(Keyword.ProtectionFromBlack);
        state.Player1.Battlefield.Add(protectedAttacker);

        var blackBlocker = new GameCard
        {
            Name = "Black Knight", TypeLine = "Creature — Human Knight",
            Power = 2, Toughness = 2, TurnEnteredBattlefield = 0,
            ManaCost = ManaCost.Parse("{B}{B}")
        };
        state.Player2.Battlefield.Add(blackBlocker);

        p1Handler.EnqueueAttackers(new List<Guid> { protectedAttacker.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid> { { blackBlocker.Id, protectedAttacker.Id } });

        await engine.RunCombatAsync(CancellationToken.None);

        state.Player2.Life.Should().Be(18, "protection from black prevents blocking by black creature");
    }

    [Fact]
    public async Task ProtectionFromRed_CannotBeBlockedByRedCreature()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var protectedAttacker = new GameCard
        {
            Name = "Soltari Priest", TypeLine = "Creature — Soltari Cleric",
            Power = 2, Toughness = 1, TurnEnteredBattlefield = 0
        };
        protectedAttacker.ActiveKeywords.Add(Keyword.ProtectionFromRed);
        state.Player1.Battlefield.Add(protectedAttacker);

        var redBlocker = new GameCard
        {
            Name = "Goblin Guide", TypeLine = "Creature — Goblin Scout",
            Power = 2, Toughness = 2, TurnEnteredBattlefield = 0,
            ManaCost = ManaCost.Parse("{R}")
        };
        state.Player2.Battlefield.Add(redBlocker);

        p1Handler.EnqueueAttackers(new List<Guid> { protectedAttacker.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid> { { redBlocker.Id, protectedAttacker.Id } });

        await engine.RunCombatAsync(CancellationToken.None);

        state.Player2.Life.Should().Be(18, "protection from red prevents blocking by red creature");
    }

    [Fact]
    public async Task ProtectionFromBlack_CanBeBlockedByNonBlackCreature()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var protectedAttacker = new GameCard
        {
            Name = "Protected Creature", TypeLine = "Creature — Human",
            Power = 2, Toughness = 2, TurnEnteredBattlefield = 0
        };
        protectedAttacker.ActiveKeywords.Add(Keyword.ProtectionFromBlack);
        state.Player1.Battlefield.Add(protectedAttacker);

        var whiteBlocker = new GameCard
        {
            Name = "White Knight", TypeLine = "Creature — Human Knight",
            Power = 2, Toughness = 2, TurnEnteredBattlefield = 0,
            ManaCost = ManaCost.Parse("{W}{W}")
        };
        state.Player2.Battlefield.Add(whiteBlocker);

        p1Handler.EnqueueAttackers(new List<Guid> { protectedAttacker.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid> { { whiteBlocker.Id, protectedAttacker.Id } });

        await engine.RunCombatAsync(CancellationToken.None);

        state.Player2.Life.Should().Be(20, "white creature can block creature with protection from black");
    }

    [Fact]
    public async Task FlyingCreature_CannotBeBlockedByNonFlying()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var flyingAttacker = new GameCard
        {
            Name = "Bird", TypeLine = "Creature — Bird",
            Power = 2, Toughness = 2, TurnEnteredBattlefield = 0
        };
        flyingAttacker.ActiveKeywords.Add(Keyword.Flying);
        state.Player1.Battlefield.Add(flyingAttacker);

        var groundBlocker = new GameCard
        {
            Name = "Bear", TypeLine = "Creature — Bear",
            Power = 2, Toughness = 2, TurnEnteredBattlefield = 0
        };
        state.Player2.Battlefield.Add(groundBlocker);

        p1Handler.EnqueueAttackers(new List<Guid> { flyingAttacker.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid> { { groundBlocker.Id, flyingAttacker.Id } });

        await engine.RunCombatAsync(CancellationToken.None);

        state.Player2.Life.Should().Be(18, "flying creature cannot be blocked by non-flying");
    }

    [Fact]
    public async Task FlyingCreature_CanBeBlockedByFlying()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var flyingAttacker = new GameCard
        {
            Name = "Bird", TypeLine = "Creature — Bird",
            Power = 2, Toughness = 2, TurnEnteredBattlefield = 0
        };
        flyingAttacker.ActiveKeywords.Add(Keyword.Flying);
        state.Player1.Battlefield.Add(flyingAttacker);

        var flyingBlocker = new GameCard
        {
            Name = "Angel", TypeLine = "Creature — Angel",
            Power = 3, Toughness = 3, TurnEnteredBattlefield = 0
        };
        flyingBlocker.ActiveKeywords.Add(Keyword.Flying);
        state.Player2.Battlefield.Add(flyingBlocker);

        p1Handler.EnqueueAttackers(new List<Guid> { flyingAttacker.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid> { { flyingBlocker.Id, flyingAttacker.Id } });

        await engine.RunCombatAsync(CancellationToken.None);

        state.Player2.Life.Should().Be(20, "flying creature can be blocked by another flying creature");
    }

    // ==================== Card Definition Tests ====================

    [Fact]
    public void SoltariFootSoldier_IsRegistered()
    {
        CardDefinitions.TryGet("Soltari Foot Soldier", out var def).Should().BeTrue();
        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ColorRequirements.Should().ContainKey(ManaColor.White);
        def.Power.Should().Be(1);
        def.Toughness.Should().Be(1);
        def.CardTypes.HasFlag(CardType.Creature).Should().BeTrue();
        def.Subtypes.Should().Contain("Soltari");
        def.Subtypes.Should().Contain("Soldier");
        def.ContinuousEffects.Should().HaveCount(1);
        def.ContinuousEffects[0].GrantedKeyword.Should().Be(Keyword.Shadow);
    }

    [Fact]
    public void SoltariMonk_IsRegistered_WithShadowAndProtectionFromBlack()
    {
        CardDefinitions.TryGet("Soltari Monk", out var def).Should().BeTrue();
        def!.ManaCost.Should().NotBeNull();
        def.Power.Should().Be(2);
        def.Toughness.Should().Be(1);
        def.Subtypes.Should().Contain("Soltari");
        def.Subtypes.Should().Contain("Monk");
        def.Subtypes.Should().Contain("Cleric");
        def.ContinuousEffects.Should().HaveCount(2);
        def.ContinuousEffects.Should().Contain(e => e.GrantedKeyword == Keyword.Shadow);
        def.ContinuousEffects.Should().Contain(e => e.GrantedKeyword == Keyword.ProtectionFromBlack);
    }

    [Fact]
    public void SoltariPriest_IsRegistered_WithShadowAndProtectionFromRed()
    {
        CardDefinitions.TryGet("Soltari Priest", out var def).Should().BeTrue();
        def!.ManaCost.Should().NotBeNull();
        def.Power.Should().Be(2);
        def.Toughness.Should().Be(1);
        def.Subtypes.Should().Contain("Soltari");
        def.Subtypes.Should().Contain("Cleric");
        def.ContinuousEffects.Should().HaveCount(2);
        def.ContinuousEffects.Should().Contain(e => e.GrantedKeyword == Keyword.Shadow);
        def.ContinuousEffects.Should().Contain(e => e.GrantedKeyword == Keyword.ProtectionFromRed);
    }

    [Fact]
    public void SoltariChampion_IsRegistered_WithShadowAndAttackTrigger()
    {
        CardDefinitions.TryGet("Soltari Champion", out var def).Should().BeTrue();
        def!.ManaCost.Should().NotBeNull();
        def.Power.Should().Be(2);
        def.Toughness.Should().Be(2);
        def.Subtypes.Should().Contain("Soltari");
        def.Subtypes.Should().Contain("Soldier");
        def.ContinuousEffects.Should().Contain(e => e.GrantedKeyword == Keyword.Shadow);
        def.Triggers.Should().HaveCount(1);
        def.Triggers[0].Condition.Should().Be(TriggerCondition.SelfAttacks);
        def.Triggers[0].Effect.Should().BeOfType<PumpAllOtherCreaturesEffect>();
    }

    [Fact]
    public void XantidSwarm_IsRegistered_WithFlyingAndAttackTrigger()
    {
        CardDefinitions.TryGet("Xantid Swarm", out var def).Should().BeTrue();
        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ColorRequirements.Should().ContainKey(ManaColor.Green);
        def.Power.Should().Be(0);
        def.Toughness.Should().Be(1);
        def.Subtypes.Should().Contain("Insect");
        def.ContinuousEffects.Should().Contain(e => e.GrantedKeyword == Keyword.Flying);
        def.Triggers.Should().HaveCount(1);
        def.Triggers[0].Condition.Should().Be(TriggerCondition.SelfAttacks);
        def.Triggers[0].Effect.Should().BeOfType<XantidSwarmEffect>();
    }

    // ==================== PumpAllOtherCreaturesEffect Tests ====================

    [Fact]
    public async Task PumpAllOtherCreaturesEffect_PumpsOtherCreatures()
    {
        var h1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var champion = new GameCard { Name = "Soltari Champion", TypeLine = "Creature", Power = 2, Toughness = 2 };
        var bear1 = new GameCard { Name = "Bear1", TypeLine = "Creature — Bear", Power = 2, Toughness = 2 };
        var bear2 = new GameCard { Name = "Bear2", TypeLine = "Creature — Bear", Power = 3, Toughness = 3 };
        p1.Battlefield.Add(champion);
        p1.Battlefield.Add(bear1);
        p1.Battlefield.Add(bear2);

        var effect = new PumpAllOtherCreaturesEffect(1, 1);
        var context = new EffectContext(state, p1, champion, h1);

        await effect.Execute(context);

        // Two continuous effects should be added (one for each other creature)
        state.ActiveEffects.Count.Should().Be(2);

        // Apply continuous effects
        var engine = new GameEngine(state);
        engine.RecalculateState();

        bear1.EffectivePower.Should().Be(3, "bear1 should get +1 power");
        bear1.EffectiveToughness.Should().Be(3, "bear1 should get +1 toughness");
        bear2.EffectivePower.Should().Be(4, "bear2 should get +1 power");
        bear2.EffectiveToughness.Should().Be(4, "bear2 should get +1 toughness");
    }

    [Fact]
    public async Task PumpAllOtherCreaturesEffect_DoesNotPumpSelf()
    {
        var h1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var champion = new GameCard { Name = "Soltari Champion", TypeLine = "Creature", Power = 2, Toughness = 2 };
        p1.Battlefield.Add(champion);

        var effect = new PumpAllOtherCreaturesEffect(1, 1);
        var context = new EffectContext(state, p1, champion, h1);

        await effect.Execute(context);

        // No creatures to pump (only self)
        state.ActiveEffects.Count.Should().Be(0);
    }

    [Fact]
    public async Task PumpAllOtherCreaturesEffect_DoesNotPumpOpponentCreatures()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);

        var champion = new GameCard { Name = "Soltari Champion", TypeLine = "Creature", Power = 2, Toughness = 2 };
        var opponentBear = new GameCard { Name = "Opponent Bear", TypeLine = "Creature — Bear", Power = 2, Toughness = 2 };
        p1.Battlefield.Add(champion);
        p2.Battlefield.Add(opponentBear);

        var effect = new PumpAllOtherCreaturesEffect(1, 1);
        var context = new EffectContext(state, p1, champion, h1);

        await effect.Execute(context);

        // No creatures to pump (only self on controller's side)
        state.ActiveEffects.Count.Should().Be(0);

        var engine = new GameEngine(state);
        engine.RecalculateState();

        // Opponent creature should not be pumped
        opponentBear.EffectivePower.Should().BeNull("opponent creature should not be pumped");
    }

    // ==================== Xantid Swarm Effect Tests ====================

    [Fact]
    public async Task XantidSwarmEffect_PreventsOpponentFromCastingSpells()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);

        var swarm = new GameCard { Name = "Xantid Swarm", TypeLine = "Creature — Insect", Power = 0, Toughness = 1 };
        p1.Battlefield.Add(swarm);

        var effect = new XantidSwarmEffect();
        var context = new EffectContext(state, p1, swarm, h1);

        await effect.Execute(context);

        // A PreventSpellCasting effect should be added targeting the opponent
        state.ActiveEffects.Count.Should().Be(1);
        state.ActiveEffects[0].Type.Should().Be(ContinuousEffectType.PreventSpellCasting);
        state.ActiveEffects[0].UntilEndOfTurn.Should().BeTrue();

        // The effect should apply to the opponent (P2), not the controller (P1)
        state.ActiveEffects[0].Applies(new GameCard(), p2).Should().BeTrue("effect should apply to opponent");
        state.ActiveEffects[0].Applies(new GameCard(), p1).Should().BeFalse("effect should not apply to controller");
    }

    // ==================== Integration: Soltari Champion Attack Pump ====================

    [Fact]
    public async Task SoltariChampion_AttackTrigger_PumpsOtherCreatures()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var champion = GameCard.Create("Soltari Champion");
        champion.TurnEnteredBattlefield = 0;
        state.Player1.Battlefield.Add(champion);

        var bear = new GameCard
        {
            Name = "Bear", TypeLine = "Creature — Bear",
            Power = 2, Toughness = 2, TurnEnteredBattlefield = 0
        };
        state.Player1.Battlefield.Add(bear);

        // Champion attacks, bear stays back
        p1Handler.EnqueueAttackers(new List<Guid> { champion.Id });
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid>()); // no blockers

        await engine.RunCombatAsync(CancellationToken.None);

        // Champion should deal 2 damage (shadow, unblockable by non-shadow)
        state.Player2.Life.Should().Be(18, "Soltari Champion deals 2 damage unblocked");

        // Bear should have been pumped +1/+1 by the trigger
        // Apply continuous effects to check
        engine.RecalculateState();
        bear.EffectivePower.Should().Be(3, "bear should be pumped +1 power by champion trigger");
        bear.EffectiveToughness.Should().Be(3, "bear should be pumped +1 toughness by champion trigger");
    }
}
