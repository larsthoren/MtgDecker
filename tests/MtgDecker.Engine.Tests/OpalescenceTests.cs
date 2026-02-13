using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class OpalescenceTests
{
    [Fact]
    public void IsRegistered_WithCorrectCMC()
    {
        CardDefinitions.TryGet("Opalescence", out var def).Should().BeTrue();
        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(4);
        def.CardTypes.Should().Be(CardType.Enchantment);
    }

    [Fact]
    public void HasBecomeCreatureEffect()
    {
        CardDefinitions.TryGet("Opalescence", out var def).Should().BeTrue();
        def!.ContinuousEffects.Should().HaveCount(1);
        def.ContinuousEffects[0].Type.Should().Be(ContinuousEffectType.BecomeCreature);
        def.ContinuousEffects[0].SetPowerToughnessToCMC.Should().BeTrue();
    }

    [Fact]
    public void MakesEnchantressPresence_A3x3Creature()
    {
        var handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", handler),
            new Player(Guid.NewGuid(), "P2", handler));

        var engine = new GameEngine(state);

        var opalescence = GameCard.Create("Opalescence", "Enchantment");
        state.Player1.Battlefield.Add(opalescence);

        var presence = GameCard.Create("Enchantress's Presence", "Enchantment");
        state.Player1.Battlefield.Add(presence);

        engine.RecalculateState();

        presence.IsCreature.Should().BeTrue("Opalescence makes non-aura enchantments into creatures");
        presence.Power.Should().Be(3, "Enchantress's Presence has CMC 3");
        presence.Toughness.Should().Be(3);
    }

    [Fact]
    public void DoesNotAffectItself()
    {
        var handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", handler),
            new Player(Guid.NewGuid(), "P2", handler));

        var engine = new GameEngine(state);

        var opalescence = GameCard.Create("Opalescence", "Enchantment");
        state.Player1.Battlefield.Add(opalescence);

        engine.RecalculateState();

        opalescence.IsCreature.Should().BeFalse("Opalescence excludes itself via SourceId skip");
        opalescence.EffectiveCardTypes.Should().BeNull("should not be modified by its own effect");
    }

    [Fact]
    public void DoesNotAffectAuras()
    {
        var handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", handler),
            new Player(Guid.NewGuid(), "P2", handler));

        var engine = new GameEngine(state);

        var opalescence = GameCard.Create("Opalescence", "Enchantment");
        state.Player1.Battlefield.Add(opalescence);

        // Place a land for Wild Growth to attach to
        var forest = GameCard.Create("Forest", "Basic Land - Forest");
        state.Player1.Battlefield.Add(forest);

        var wildGrowth = GameCard.Create("Wild Growth", "Enchantment - Aura");
        wildGrowth.AttachedTo = forest.Id;
        state.Player1.Battlefield.Add(wildGrowth);

        engine.RecalculateState();

        wildGrowth.IsCreature.Should().BeFalse("Wild Growth is an Aura and should be excluded");
    }

    [Fact]
    public void SterlingGrove_Becomes2x2_StillGrantsShroud()
    {
        var handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", handler),
            new Player(Guid.NewGuid(), "P2", handler));

        var engine = new GameEngine(state);

        var opalescence = GameCard.Create("Opalescence", "Enchantment");
        state.Player1.Battlefield.Add(opalescence);

        var grove = GameCard.Create("Sterling Grove", "Enchantment");
        state.Player1.Battlefield.Add(grove);

        // Add another enchantment to verify shroud grant
        var presence = GameCard.Create("Enchantress's Presence", "Enchantment");
        state.Player1.Battlefield.Add(presence);

        engine.RecalculateState();

        grove.IsCreature.Should().BeTrue("Sterling Grove becomes a creature via Opalescence");
        grove.Power.Should().Be(2, "Sterling Grove has CMC 2 ({G}{W})");
        grove.Toughness.Should().Be(2);

        // Sterling Grove grants shroud to other enchantments the controller controls (excluding itself)
        presence.ActiveKeywords.Should().Contain(Keyword.Shroud, "Sterling Grove still grants shroud");
    }

    [Fact]
    public void EnchantmentCreature_HasSummoningSickness()
    {
        var handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", handler),
            new Player(Guid.NewGuid(), "P2", handler));

        var engine = new GameEngine(state);

        var opalescence = GameCard.Create("Opalescence", "Enchantment");
        state.Player1.Battlefield.Add(opalescence);

        var presence = GameCard.Create("Enchantress's Presence", "Enchantment");
        presence.TurnEnteredBattlefield = state.TurnNumber;
        state.Player1.Battlefield.Add(presence);

        engine.RecalculateState();

        presence.IsCreature.Should().BeTrue();
        presence.HasSummoningSickness(state.TurnNumber).Should().BeTrue(
            "enchantment-turned-creature that entered this turn has summoning sickness");
    }

    [Fact]
    public async Task EnchantmentCreature_DiesToLethalDamage()
    {
        var handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", handler),
            new Player(Guid.NewGuid(), "P2", handler));

        var engine = new GameEngine(state);

        var opalescence = GameCard.Create("Opalescence", "Enchantment");
        state.Player1.Battlefield.Add(opalescence);

        var presence = GameCard.Create("Enchantress's Presence", "Enchantment");
        state.Player1.Battlefield.Add(presence);

        // Recalculate to make it a 3/3 creature
        engine.RecalculateState();
        presence.IsCreature.Should().BeTrue();
        presence.Toughness.Should().Be(3);

        // Deal lethal damage
        presence.DamageMarked = 3;

        // Check state-based actions
        await engine.CheckStateBasedActionsAsync(default);

        // Enchantress's Presence should be in graveyard
        state.Player1.Battlefield.Contains(presence.Id).Should().BeFalse(
            "enchantment-creature with lethal damage should die");
        state.Player1.Graveyard.Contains(presence.Id).Should().BeTrue(
            "dead enchantment-creature goes to graveyard");
    }
}
