using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class EtbCountersTests
{
    [Fact]
    public async Task ParallaxWave_EntersWithFadeCounters_Immediately()
    {
        var handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", handler),
            new Player(Guid.NewGuid(), "P2", handler));
        state.CurrentPhase = Phase.MainPhase1;

        var engine = new GameEngine(state);

        // Give player enough mana to cast Parallax Wave (2WW)
        state.Player1.ManaPool.Add(ManaColor.White, 2);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 2);

        var wave = GameCard.Create("Parallax Wave", "Enchantment");
        state.Player1.Hand.Add(wave);

        // Cast Parallax Wave
        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, wave.Id));

        // Counters should be present IMMEDIATELY — no need to resolve triggers
        wave.GetCounters(CounterType.Fade).Should().Be(5,
            "Parallax Wave should enter with 5 fade counters immediately, not via a trigger on the stack");
    }

    [Fact]
    public async Task ParallaxWave_NoAddCountersTriggerOnStack()
    {
        var handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", handler),
            new Player(Guid.NewGuid(), "P2", handler));
        state.CurrentPhase = Phase.MainPhase1;

        var engine = new GameEngine(state);

        state.Player1.ManaPool.Add(ManaColor.White, 2);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 2);

        var wave = GameCard.Create("Parallax Wave", "Enchantment");
        state.Player1.Hand.Add(wave);

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, wave.Id));

        // The stack should NOT contain an AddCountersEffect trigger for Parallax Wave
        // (The ETB counter placement should be immediate, not a triggered ability)
        var stackTop = state.StackPeekTop();
        if (stackTop is TriggeredAbilityStackObject triggered)
        {
            triggered.Effect.Should().NotBeOfType<AddCountersEffect>(
                "AddCountersEffect should not be on the stack — counters should be placed immediately at ETB");
        }
    }

    [Fact]
    public async Task GemstoneMineLand_EntersWithMiningCounters_Immediately()
    {
        var handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", handler),
            new Player(Guid.NewGuid(), "P2", handler));
        state.CurrentPhase = Phase.MainPhase1;

        var engine = new GameEngine(state);

        var mine = GameCard.Create("Gemstone Mine", "Land");
        state.Player1.Hand.Add(mine);

        // Play Gemstone Mine as a land drop
        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, mine.Id));

        // Counters should be present IMMEDIATELY — no need to resolve triggers
        mine.GetCounters(CounterType.Mining).Should().Be(3,
            "Gemstone Mine should enter with 3 mining counters immediately, not via a trigger on the stack");
    }

    [Fact]
    public async Task GemstoneMineLand_NoAddCountersTriggerOnStack()
    {
        var handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", handler),
            new Player(Guid.NewGuid(), "P2", handler));
        state.CurrentPhase = Phase.MainPhase1;

        var engine = new GameEngine(state);

        var mine = GameCard.Create("Gemstone Mine", "Land");
        state.Player1.Hand.Add(mine);

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, mine.Id));

        // The stack should be empty — no trigger should be queued for counter placement
        state.StackCount.Should().Be(0,
            "No AddCountersEffect trigger should be on the stack for Gemstone Mine");
    }

    [Fact]
    public async Task ParallaxWave_ActivatedAbility_StillWorks_AfterImmediateCounters()
    {
        var handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", handler),
            new Player(Guid.NewGuid(), "P2", handler));
        state.CurrentPhase = Phase.MainPhase1;

        var engine = new GameEngine(state);

        // Give player enough mana to cast Parallax Wave (2WW)
        state.Player1.ManaPool.Add(ManaColor.White, 2);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 2);

        var wave = GameCard.Create("Parallax Wave", "Enchantment");
        state.Player1.Hand.Add(wave);

        // Cast Parallax Wave
        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, wave.Id));

        // Resolve any triggers (should be none for counters, but resolve anyway to be safe)
        await engine.ResolveAllTriggersAsync();

        // Verify counters are present
        wave.GetCounters(CounterType.Fade).Should().Be(5);

        // Set up a creature to exile
        var creature = new GameCard { Name = "Grizzly Bears", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        state.Player2.Battlefield.Add(creature);

        // Activate ability to exile a creature
        await engine.ExecuteAction(GameAction.ActivateAbility(state.Player1.Id, wave.Id, targetId: creature.Id));
        await engine.ResolveAllTriggersAsync();

        // Counter should decrement
        wave.GetCounters(CounterType.Fade).Should().Be(4);

        // Creature should be exiled
        state.Player2.Battlefield.Contains(creature.Id).Should().BeFalse();
        state.Player2.Exile.Contains(creature.Id).Should().BeTrue();
    }

    [Fact]
    public void ParallaxWave_CardDefinition_UsesEntersWithCounters()
    {
        CardDefinitions.TryGet("Parallax Wave", out var def).Should().BeTrue();
        def!.EntersWithCounters.Should().NotBeNull();
        def.EntersWithCounters.Should().ContainKey(CounterType.Fade);
        def.EntersWithCounters![CounterType.Fade].Should().Be(5);
    }

    [Fact]
    public void GemstoneMineLand_CardDefinition_UsesEntersWithCounters()
    {
        CardDefinitions.TryGet("Gemstone Mine", out var def).Should().BeTrue();
        def!.EntersWithCounters.Should().NotBeNull();
        def.EntersWithCounters.Should().ContainKey(CounterType.Mining);
        def.EntersWithCounters![CounterType.Mining].Should().Be(3);
    }

    [Fact]
    public void ParallaxWave_CardDefinition_NoETBAddCountersTrigger()
    {
        CardDefinitions.TryGet("Parallax Wave", out var def).Should().BeTrue();
        def!.Triggers.Should().NotContain(t =>
            t.Event == GameEvent.EnterBattlefield
            && t.Condition == TriggerCondition.Self
            && t.Effect is AddCountersEffect,
            "Parallax Wave should not have an AddCountersEffect ETB trigger — counters are placed via EntersWithCounters");
    }

    [Fact]
    public void GemstoneMineLand_CardDefinition_NoETBAddCountersTrigger()
    {
        CardDefinitions.TryGet("Gemstone Mine", out var def).Should().BeTrue();
        def!.Triggers.Should().NotContain(t =>
            t.Event == GameEvent.EnterBattlefield
            && t.Condition == TriggerCondition.Self
            && t.Effect is AddCountersEffect,
            "Gemstone Mine should not have an AddCountersEffect ETB trigger — counters are placed via EntersWithCounters");
    }
}
