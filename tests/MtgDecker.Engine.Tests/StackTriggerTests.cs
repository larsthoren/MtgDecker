using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;
using NSubstitute;

namespace MtgDecker.Engine.Tests;

public class StackTriggerTests
{
    private (GameState state, Player player1, Player player2, TestDecisionHandler handler1, TestDecisionHandler handler2, GameEngine engine) CreateSetup()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Player 1", handler1);
        var p2 = new Player(Guid.NewGuid(), "Player 2", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (state, p1, p2, handler1, handler2, engine);
    }

    [Fact]
    public async Task ETB_Self_Trigger_Goes_On_Stack()
    {
        var (state, p1, _, _, _, engine) = CreateSetup();

        var matron = GameCard.Create("Goblin Matron");
        matron.TurnEnteredBattlefield = state.TurnNumber;
        p1.Battlefield.Add(matron);
        p1.Library.Add(GameCard.Create("Mogg Fanatic"));

        await engine.QueueSelfTriggersOnStackAsync(GameEvent.EnterBattlefield, matron, p1);

        state.Stack.Should().ContainSingle();
        state.Stack[0].Should().BeOfType<TriggeredAbilityStackObject>();
        var taso = (TriggeredAbilityStackObject)state.Stack[0];
        taso.Source.Name.Should().Be("Goblin Matron");
        taso.Effect.Should().BeOfType<SearchLibraryEffect>();
    }

    [Fact]
    public async Task Board_Triggers_Collect_From_Active_Player_APNAP()
    {
        var (state, p1, p2, _, _, engine) = CreateSetup();

        var enchantress1 = GameCard.Create("Enchantress's Presence");
        p1.Battlefield.Add(enchantress1);

        var enchantress2 = GameCard.Create("Argothian Enchantress");
        p2.Battlefield.Add(enchantress2);

        var enchantment = new GameCard { Name = "Test Enchantment", CardTypes = CardType.Enchantment };

        await engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, enchantment);

        // Only active player's enchantress triggers (ControllerCastsEnchantment checks ActivePlayer)
        state.Stack.Should().ContainSingle();
        var taso = (TriggeredAbilityStackObject)state.Stack[0];
        taso.ControllerId.Should().Be(p1.Id);
    }

    [Fact]
    public async Task Delayed_Triggers_Go_On_Stack()
    {
        var (state, p1, _, _, _, engine) = CreateSetup();

        var effect = Substitute.For<IEffect>();
        state.DelayedTriggers.Add(new DelayedTrigger(GameEvent.EndStep, effect, p1.Id));

        await engine.QueueDelayedTriggersOnStackAsync(GameEvent.EndStep);

        state.Stack.Should().ContainSingle();
        state.Stack[0].Should().BeOfType<TriggeredAbilityStackObject>();
        state.DelayedTriggers.Should().BeEmpty();
    }

    [Fact]
    public async Task Attack_Trigger_Goes_On_Stack()
    {
        var (state, p1, _, _, _, engine) = CreateSetup();

        var piledriver = GameCard.Create("Goblin Piledriver");
        piledriver.TurnEnteredBattlefield = state.TurnNumber - 1; // no summoning sickness
        p1.Battlefield.Add(piledriver);

        await engine.QueueAttackTriggersOnStackAsync(piledriver);

        state.Stack.Should().ContainSingle();
        state.Stack[0].Should().BeOfType<TriggeredAbilityStackObject>();
        var taso = (TriggeredAbilityStackObject)state.Stack[0];
        taso.Source.Name.Should().Be("Goblin Piledriver");
        taso.ControllerId.Should().Be(p1.Id);
    }

    [Fact]
    public async Task APNAP_Active_Player_Triggers_Resolve_Last()
    {
        // In APNAP ordering, active player's triggers go on stack first (bottom),
        // non-active player's on top. LIFO means non-active resolves first.
        var (state, p1, p2, _, _, engine) = CreateSetup();

        // Give both players a creature with AnyCreatureDies trigger
        var p1Creature = new GameCard
        {
            Name = "Blood Artist P1",
            CardTypes = CardType.Creature,
            Triggers = [new Trigger(GameEvent.Dies, TriggerCondition.AnyCreatureDies, Substitute.For<IEffect>())],
        };
        p1.Battlefield.Add(p1Creature);

        var p2Creature = new GameCard
        {
            Name = "Blood Artist P2",
            CardTypes = CardType.Creature,
            Triggers = [new Trigger(GameEvent.Dies, TriggerCondition.AnyCreatureDies, Substitute.For<IEffect>())],
        };
        p2.Battlefield.Add(p2Creature);

        var deadCreature = new GameCard { Name = "Dead Guy", CardTypes = CardType.Creature };

        await engine.QueueBoardTriggersOnStackAsync(GameEvent.Dies, deadCreature);

        state.Stack.Should().HaveCount(2);
        // Non-active player's trigger on top (resolves first in LIFO)
        var top = (TriggeredAbilityStackObject)state.Stack[^1];
        top.ControllerId.Should().Be(p2.Id);
        // Active player's trigger on bottom (resolves last)
        var bottom = (TriggeredAbilityStackObject)state.Stack[0];
        bottom.ControllerId.Should().Be(p1.Id);
    }

    [Fact]
    public async Task Self_Trigger_Not_Queued_For_Wrong_Event()
    {
        var (state, p1, _, _, _, engine) = CreateSetup();

        var matron = GameCard.Create("Goblin Matron");
        p1.Battlefield.Add(matron);

        // Goblin Matron only triggers on ETB, not on Dies
        await engine.QueueSelfTriggersOnStackAsync(GameEvent.Dies, matron, p1);

        state.Stack.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveAllTriggersAsync_Resolves_All_Stack_Items()
    {
        var (state, p1, _, _, _, engine) = CreateSetup();

        var effect1 = Substitute.For<IEffect>();
        effect1.Execute(Arg.Any<EffectContext>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var effect2 = Substitute.For<IEffect>();
        effect2.Execute(Arg.Any<EffectContext>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var source = new GameCard { Name = "Test Source" };
        state.Stack.Add(new TriggeredAbilityStackObject(source, p1.Id, effect1));
        state.Stack.Add(new TriggeredAbilityStackObject(source, p1.Id, effect2));

        await engine.ResolveAllTriggersAsync();

        state.Stack.Should().BeEmpty();
        await effect1.Received(1).Execute(Arg.Any<EffectContext>(), Arg.Any<CancellationToken>());
        await effect2.Received(1).Execute(Arg.Any<EffectContext>(), Arg.Any<CancellationToken>());
    }
}
