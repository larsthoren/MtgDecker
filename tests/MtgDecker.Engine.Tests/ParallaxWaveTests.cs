using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class ParallaxWaveTests
{
    [Fact]
    public void IsRegistered_WithCorrectCMC()
    {
        CardDefinitions.TryGet("Parallax Wave", out var def).Should().BeTrue();
        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(4);
        def.CardTypes.Should().Be(CardType.Enchantment);
    }

    [Fact]
    public void HasETB_CounterTrigger()
    {
        CardDefinitions.TryGet("Parallax Wave", out var def).Should().BeTrue();
        def!.Triggers.Should().Contain(t =>
            t.Event == GameEvent.EnterBattlefield
            && t.Condition == Engine.Triggers.TriggerCondition.Self
            && t.Effect is AddCountersEffect);
    }

    [Fact]
    public void HasLTB_ReturnTrigger()
    {
        CardDefinitions.TryGet("Parallax Wave", out var def).Should().BeTrue();
        def!.Triggers.Should().Contain(t =>
            t.Event == GameEvent.LeavesBattlefield
            && t.Condition == Engine.Triggers.TriggerCondition.SelfLeavesBattlefield
            && t.Effect is ReturnExiledCardsEffect);
    }

    [Fact]
    public void HasRemoveCounter_ActivatedAbility()
    {
        CardDefinitions.TryGet("Parallax Wave", out var def).Should().BeTrue();
        def!.ActivatedAbility.Should().NotBeNull();
        def.ActivatedAbility!.Cost.RemoveCounterType.Should().Be(CounterType.Fade);
        def.ActivatedAbility.Effect.Should().BeOfType<ExileCreatureEffect>();
    }

    [Fact]
    public async Task ETB_Adds5FadeCounters()
    {
        var handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", handler),
            new Player(Guid.NewGuid(), "P2", handler));
        state.CurrentPhase = Phase.MainPhase1;

        var engine = new GameEngine(state);

        // Setup: give player enough mana to cast Parallax Wave (2WW)
        state.Player1.ManaPool.Add(ManaColor.White, 2);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 2);

        var wave = GameCard.Create("Parallax Wave", "Enchantment");
        state.Player1.Hand.Add(wave);

        // Cast via PlayCard (mana payment path)
        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, wave.Id));

        // Resolve the ETB trigger on the stack
        await engine.ResolveAllTriggersAsync();

        wave.GetCounters(CounterType.Fade).Should().Be(5);
    }

    [Fact]
    public async Task Activate_ExilesCreature_WithCounterPayment()
    {
        var handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", handler),
            new Player(Guid.NewGuid(), "P2", handler));

        var engine = new GameEngine(state);

        var wave = GameCard.Create("Parallax Wave", "Enchantment");
        wave.AddCounters(CounterType.Fade, 5);
        state.Player1.Battlefield.Add(wave);

        var creature = new GameCard { Name = "Grizzly Bears", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        state.Player2.Battlefield.Add(creature);

        // Activate ability targeting creature
        await engine.ExecuteAction(GameAction.ActivateAbility(state.Player1.Id, wave.Id, targetId: creature.Id));
        await engine.ResolveAllTriggersAsync();

        // Counter should decrement
        wave.GetCounters(CounterType.Fade).Should().Be(4);

        // Creature should be exiled
        state.Player2.Battlefield.Contains(creature.Id).Should().BeFalse();
        state.Player2.Exile.Contains(creature.Id).Should().BeTrue();

        // Wave should track the exile
        wave.ExiledCardIds.Should().Contain(creature.Id);
    }

    [Fact]
    public async Task CannotActivate_WithNoCounters()
    {
        var handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", handler),
            new Player(Guid.NewGuid(), "P2", handler));

        var engine = new GameEngine(state);

        var wave = GameCard.Create("Parallax Wave", "Enchantment");
        // No counters added
        state.Player1.Battlefield.Add(wave);

        var creature = new GameCard { Name = "Grizzly Bears", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        state.Player2.Battlefield.Add(creature);

        await engine.ExecuteAction(GameAction.ActivateAbility(state.Player1.Id, wave.Id, targetId: creature.Id));

        // Creature should still be on battlefield
        state.Player2.Battlefield.Contains(creature.Id).Should().BeTrue();
        state.GameLog.Should().Contain(l => l.Contains("no Fade counters"));
    }
}
