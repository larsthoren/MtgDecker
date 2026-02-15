using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class PlayerProtectionTests
{
    private static (GameEngine engine, GameState state, Player p1, Player p2, TestDecisionHandler handler1) Setup()
    {
        var handler1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, p1, p2, handler1);
    }

    [Fact]
    public async Task PlayerShroud_Prevents_Activated_Ability_Targeting_Player()
    {
        var (engine, state, p1, p2, handler1) = Setup();

        // P2 has enchantment granting player shroud
        var enchantment = new GameCard { Name = "Solitary Confinement", CardTypes = CardType.Enchantment };
        p2.Battlefield.Add(enchantment);
        state.ActiveEffects.Add(new ContinuousEffect(
            enchantment.Id, ContinuousEffectType.GrantPlayerShroud, (_, _) => true));

        // P1 has Mogg Fanatic to activate targeting P2
        var fanatic = GameCard.Create("Mogg Fanatic");
        p1.Battlefield.Add(fanatic);

        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var initialLife = p2.Life;
        await engine.ExecuteAction(
            GameAction.ActivateAbility(p1.Id, fanatic.Id, targetPlayerId: p2.Id), default);

        // P2 should not have taken damage (shroud prevented targeting)
        p2.Life.Should().Be(initialLife);
        state.GameLog.Should().Contain(l => l.Contains("shroud") && l.Contains("cannot be targeted"));
    }

    [Fact]
    public async Task PlayerShroud_Does_Not_Affect_Other_Player()
    {
        var (engine, state, p1, p2, handler1) = Setup();

        // P2 has enchantment granting player shroud
        var enchantment = new GameCard { Name = "Solitary Confinement", CardTypes = CardType.Enchantment };
        p2.Battlefield.Add(enchantment);
        state.ActiveEffects.Add(new ContinuousEffect(
            enchantment.Id, ContinuousEffectType.GrantPlayerShroud, (_, _) => true));

        // P2 has Mogg Fanatic to activate targeting P1 (who does NOT have shroud)
        var fanatic = GameCard.Create("Mogg Fanatic");
        p2.Battlefield.Add(fanatic);

        state.ActivePlayer = p2;
        state.CurrentPhase = Phase.MainPhase1;

        var initialLife = p1.Life;
        await engine.ExecuteAction(
            GameAction.ActivateAbility(p2.Id, fanatic.Id, targetPlayerId: p1.Id), default);
        await engine.ResolveAllTriggersAsync();

        // P1 should have taken damage (no shroud)
        p1.Life.Should().BeLessThan(initialLife);
    }

    [Fact]
    public async Task DamagePrevention_Prevents_DealDamageEffect_To_Player()
    {
        var (engine, state, p1, p2, _) = Setup();

        // P2 has enchantment granting damage prevention
        var enchantment = new GameCard { Name = "Solitary Confinement", CardTypes = CardType.Enchantment };
        p2.Battlefield.Add(enchantment);
        state.ActiveEffects.Add(new ContinuousEffect(
            enchantment.Id, ContinuousEffectType.PreventDamageToPlayer, (_, _) => true));

        var source = new GameCard { Name = "Fireball", CardTypes = CardType.Sorcery };

        var effect = new DealDamageEffect(5);
        var context = new EffectContext(state, p1, source, p1.DecisionHandler)
        {
            TargetPlayerId = p2.Id,
        };

        var initialLife = p2.Life;
        await effect.Execute(context);

        // Damage should be prevented
        p2.Life.Should().Be(initialLife);
        state.GameLog.Should().Contain(l => l.Contains("prevented") && l.Contains("protection"));
    }

    [Fact]
    public async Task DamagePrevention_Does_Not_Affect_Creature_Damage()
    {
        var (engine, state, p1, p2, _) = Setup();

        // P2 has enchantment granting damage prevention to player
        var enchantment = new GameCard { Name = "Solitary Confinement", CardTypes = CardType.Enchantment };
        p2.Battlefield.Add(enchantment);
        state.ActiveEffects.Add(new ContinuousEffect(
            enchantment.Id, ContinuousEffectType.PreventDamageToPlayer, (_, _) => true));

        // Target is a creature, not the player
        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p2.Battlefield.Add(creature);

        var source = new GameCard { Name = "Shock Source", CardTypes = CardType.Creature };

        var effect = new DealDamageEffect(2);
        var context = new EffectContext(state, p1, source, p1.DecisionHandler)
        {
            Target = creature,
        };

        await effect.Execute(context);

        // Creature should have taken damage (damage prevention only applies to players)
        creature.DamageMarked.Should().Be(2);
    }

    [Fact]
    public async Task CombatDamage_Prevented_To_Protected_Player()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P2 has enchantment granting damage prevention
        var enchantment = new GameCard { Name = "Solitary Confinement", CardTypes = CardType.Enchantment };
        p2.Battlefield.Add(enchantment);
        state.ActiveEffects.Add(new ContinuousEffect(
            enchantment.Id, ContinuousEffectType.PreventDamageToPlayer, (_, _) => true));

        // P1 has an attacker
        var attacker = new GameCard
        {
            Name = "Grizzly Bears", CardTypes = CardType.Creature,
            BasePower = 2, BaseToughness = 2,
            TurnEnteredBattlefield = 0, // No summoning sickness
        };
        p1.Battlefield.Add(attacker);

        state.ActivePlayer = p1;
        state.TurnNumber = 1;

        // Enqueue attacker choice for P1
        handler1.EnqueueAttackers(new List<Guid> { attacker.Id });
        // P2 has no blockers, so no blocker choice needed

        var initialLife = p2.Life;
        await engine.RunCombatAsync(default);

        // P2 should not have lost life (damage prevented)
        p2.Life.Should().Be(initialLife);
        state.GameLog.Should().Contain(l => l.Contains("prevented") && l.Contains("protection"));
    }
}
