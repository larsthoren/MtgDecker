using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class StackResolutionTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler h1, TestDecisionHandler h2) CreateSetup()
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
        return (engine, state, h1, h2);
    }

    [Fact]
    public async Task BothPass_StackNonEmpty_ResolvesTop()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var goblin = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        var stackObj = new StackObject(goblin, state.Player1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);
        state.StackPush(stackObj);

        // Both pass -> resolve -> creature enters battlefield
        // Then both pass again -> stack empty -> advance
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));

        await engine.RunPriorityAsync();

        state.Stack.Should().BeEmpty();
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == goblin.Id);
    }

    [Fact]
    public async Task BothPass_StackEmpty_Advances()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();

        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));

        await engine.RunPriorityAsync();

        state.Stack.Should().BeEmpty();
    }

    [Fact]
    public async Task InstantSorcery_ResolvesToGraveyard()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var creature = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        creature.Power = 1;
        creature.Toughness = 1;
        state.Player2.Battlefield.Add(creature);

        var swords = GameCard.Create("Swords to Plowshares");
        var stackObj = new StackObject(swords, state.Player1.Id,
            new Dictionary<ManaColor, int> { [ManaColor.White] = 1 },
            new List<TargetInfo> { new(creature.Id, state.Player2.Id, ZoneType.Battlefield) }, 0);
        state.StackPush(stackObj);

        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));

        await engine.RunPriorityAsync();

        state.Player1.Graveyard.Cards.Should().Contain(c => c.Id == swords.Id);
        state.Player2.Exile.Cards.Should().Contain(c => c.Id == creature.Id);
        state.Player2.Life.Should().Be(21);
    }

    [Fact]
    public async Task LIFO_ResolvesTopFirst()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var card1 = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        var card2 = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        state.StackPush(new StackObject(card1, state.Player1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0));
        state.StackPush(new StackObject(card2, state.Player1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 1));

        // Both pass -> resolve card2 -> priority -> both pass -> resolve card1 -> both pass -> return
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));

        await engine.RunPriorityAsync();

        state.Stack.Should().BeEmpty();
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == card2.Id);
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == card1.Id);
    }

    [Fact]
    public async Task Fizzle_TargetRemoved_SpellGoesToGraveyard()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var creature = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        state.Player2.Battlefield.Add(creature);

        var swords = GameCard.Create("Swords to Plowshares");
        state.StackPush(new StackObject(swords, state.Player1.Id,
            new Dictionary<ManaColor, int> { [ManaColor.White] = 1 },
            new List<TargetInfo> { new(creature.Id, state.Player2.Id, ZoneType.Battlefield) }, 0));

        // Remove target before resolution
        state.Player2.Battlefield.RemoveById(creature.Id);

        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));

        await engine.RunPriorityAsync();

        state.Player1.Graveyard.Cards.Should().Contain(c => c.Id == swords.Id);
        state.Player2.Life.Should().Be(20);
    }
}
