using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class CastSpellStackIntegrationTests
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
    public async Task CastCreature_Resolve_EntersBattlefield()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var goblin = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        state.Player1.Hand.Add(goblin);
        state.Player1.ManaPool.Add(ManaColor.Red, 1);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, goblin.Id));
        state.Stack.Should().HaveCount(1);

        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        await engine.RunPriorityAsync();

        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == goblin.Id);
        state.Stack.Should().BeEmpty();
    }

    [Fact]
    public async Task CastSwords_Resolve_ExilesAndGainsLife()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var creature = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        creature.Power = 1;
        state.Player2.Battlefield.Add(creature);

        var swords = GameCard.Create("Swords to Plowshares");
        state.Player1.Hand.Add(swords);
        state.Player1.ManaPool.Add(ManaColor.White, 1);
        h1.EnqueueTarget(new TargetInfo(creature.Id, state.Player2.Id, ZoneType.Battlefield));

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, swords.Id));

        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        await engine.RunPriorityAsync();

        state.Player2.Exile.Cards.Should().Contain(c => c.Id == creature.Id);
        state.Player2.Life.Should().Be(21);
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Id == swords.Id);
    }

    [Fact]
    public async Task CastNaturalize_Resolve_DestroysEnchantment()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var enchantment = GameCard.Create("Wild Growth", "Enchantment");
        state.Player2.Battlefield.Add(enchantment);

        var naturalize = GameCard.Create("Naturalize");
        state.Player1.Hand.Add(naturalize);
        state.Player1.ManaPool.Add(ManaColor.Green, 2);
        h1.EnqueueTarget(new TargetInfo(enchantment.Id, state.Player2.Id, ZoneType.Battlefield));

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, naturalize.Id));

        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        await engine.RunPriorityAsync();

        state.Player2.Graveyard.Cards.Should().Contain(c => c.Id == enchantment.Id);
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Id == naturalize.Id);
    }

    [Fact]
    public async Task CreatureThenInstantResponse_LIFO_Resolution()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        // P1 casts a creature at sorcery speed (stack empty)
        var goblin = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        state.Player1.Hand.Add(goblin);
        state.Player1.ManaPool.Add(ManaColor.Red, 1);
        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, goblin.Id));

        state.Stack.Should().HaveCount(1);

        // P2 responds with an instant targeting a creature already on P1's battlefield
        var existingCreature = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        existingCreature.Power = 1;
        state.Player1.Battlefield.Add(existingCreature);

        var swords = GameCard.Create("Swords to Plowshares");
        state.Player2.Hand.Add(swords);
        state.Player2.ManaPool.Add(ManaColor.White, 1);
        h2.EnqueueTarget(new TargetInfo(existingCreature.Id, state.Player1.Id, ZoneType.Battlefield));

        // P1 passes priority, P2 casts instant in response, then both pass to resolve
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.CastSpell(state.Player2.Id, swords.Id));
        // After P2 casts, priority resets — both pass to resolve top (Swords)
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        // After Swords resolves, priority resets — both pass to resolve Mogg Fanatic
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        // Stack empty — both pass to exit
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));

        await engine.RunPriorityAsync();

        // LIFO: Swords resolved first (exiling existing creature), then Mogg resolved (entering battlefield)
        state.Player1.Exile.Cards.Should().Contain(c => c.Id == existingCreature.Id);
        state.Player1.Life.Should().Be(21); // Gained 1 life from Swords
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == goblin.Id);
        state.Stack.Should().BeEmpty();
    }

    [Fact]
    public async Task LandDrop_StillImmediate_NoStack()
    {
        var (engine, state, _, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var forest = GameCard.Create("Forest", "Basic Land — Forest");
        state.Player1.Hand.Add(forest);

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, forest.Id));

        state.Stack.Should().BeEmpty();
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == forest.Id);
    }

    [Fact]
    public async Task SandboxMode_StillImmediate_NoStack()
    {
        var (engine, state, _, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var unknownCard = new GameCard { Name = "Unknown Spell", TypeLine = "Creature" };
        state.Player1.Hand.Add(unknownCard);

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, unknownCard.Id));

        state.Stack.Should().BeEmpty();
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == unknownCard.Id);
    }
}
