using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class DeferredCardsIntegrationTests
{
    [Fact]
    public void AllEnchantressNonLandCards_AreRegistered()
    {
        var cards = new[]
        {
            "Argothian Enchantress", "Swords to Plowshares", "Replenish",
            "Enchantress's Presence", "Wild Growth", "Exploration",
            "Mirri's Guile", "Opalescence", "Parallax Wave",
            "Sterling Grove", "Aura of Silence", "Seal of Cleansing",
            "Solitary Confinement", "Sylvan Library"
        };

        foreach (var name in cards)
            CardDefinitions.TryGet(name, out _).Should().BeTrue($"'{name}' should be registered");
    }

    [Fact]
    public void Opalescence_Plus_ParallaxWave_WaveBecomes4x4Creature()
    {
        var handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", handler),
            new Player(Guid.NewGuid(), "P2", handler));

        var engine = new GameEngine(state);

        var opalescence = GameCard.Create("Opalescence", "Enchantment");
        state.Player1.Battlefield.Add(opalescence);

        var wave = GameCard.Create("Parallax Wave", "Enchantment");
        state.Player1.Battlefield.Add(wave);

        engine.RecalculateState();

        wave.IsCreature.Should().BeTrue("Opalescence makes Parallax Wave a creature");
        wave.Power.Should().Be(4, "Parallax Wave has CMC 4 ({2}{W}{W})");
        wave.Toughness.Should().Be(4, "Parallax Wave has CMC 4 ({2}{W}{W})");
    }

    [Fact]
    public void Opalescence_Plus_SolitaryConfinement_Becomes3x3_EffectsStillActive()
    {
        var handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", handler),
            new Player(Guid.NewGuid(), "P2", handler));

        var engine = new GameEngine(state);

        var opalescence = GameCard.Create("Opalescence", "Enchantment");
        state.Player1.Battlefield.Add(opalescence);

        var confinement = GameCard.Create("Solitary Confinement", "Enchantment");
        state.Player1.Battlefield.Add(confinement);

        engine.RecalculateState();

        confinement.IsCreature.Should().BeTrue("Opalescence makes Solitary Confinement a creature");
        confinement.Power.Should().Be(3, "Solitary Confinement has CMC 3 ({2}{W})");
        confinement.Toughness.Should().Be(3, "Solitary Confinement has CMC 3 ({2}{W})");

        // Continuous effects from Solitary Confinement should still be active
        state.ActiveEffects.Should().Contain(e => e.Type == ContinuousEffectType.SkipDraw,
            "Solitary Confinement still skips draw");
        state.ActiveEffects.Should().Contain(e => e.Type == ContinuousEffectType.GrantPlayerShroud,
            "Solitary Confinement still grants player shroud");
        state.ActiveEffects.Should().Contain(e => e.Type == ContinuousEffectType.PreventDamageToPlayer,
            "Solitary Confinement still prevents damage");
    }

    [Fact]
    public async Task ParallaxWave_FullLifecycle_ExileTwoCreatures()
    {
        var handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", handler),
            new Player(Guid.NewGuid(), "P2", handler));

        var engine = new GameEngine(state);

        // Parallax Wave with 5 fade counters on P1's battlefield
        var wave = GameCard.Create("Parallax Wave", "Enchantment");
        wave.AddCounters(CounterType.Fade, 5);
        state.Player1.Battlefield.Add(wave);

        // P2 has 2 creatures
        var bear = new GameCard
        {
            Name = "Grizzly Bears", CardTypes = CardType.Creature,
            BasePower = 2, BaseToughness = 2
        };
        var angel = new GameCard
        {
            Name = "Serra Angel", CardTypes = CardType.Creature,
            BasePower = 4, BaseToughness = 4
        };
        state.Player2.Battlefield.Add(bear);
        state.Player2.Battlefield.Add(angel);

        // Activation 1: exile the bear
        await engine.ExecuteAction(
            GameAction.ActivateAbility(state.Player1.Id, wave.Id, targetId: bear.Id));

        state.Player2.Battlefield.Contains(bear.Id).Should().BeFalse("bear should be exiled");
        state.Player2.Exile.Contains(bear.Id).Should().BeTrue("bear should be in exile zone");
        wave.GetCounters(CounterType.Fade).Should().Be(4, "one counter used");

        // Activation 2: exile the angel
        await engine.ExecuteAction(
            GameAction.ActivateAbility(state.Player1.Id, wave.Id, targetId: angel.Id));

        state.Player2.Battlefield.Contains(angel.Id).Should().BeFalse("angel should be exiled");
        state.Player2.Exile.Contains(angel.Id).Should().BeTrue("angel should be in exile zone");
        wave.GetCounters(CounterType.Fade).Should().Be(3, "two counters used total");

        // Wave tracks both exiled cards
        wave.ExiledCardIds.Should().HaveCount(2);
        wave.ExiledCardIds.Should().Contain(bear.Id);
        wave.ExiledCardIds.Should().Contain(angel.Id);
    }
}
