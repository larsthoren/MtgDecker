using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests.Effects;

/// <summary>
/// A test async effect that calls ChooseCard on the decision handler during resolution.
/// </summary>
public class TestAsyncEffect : SpellEffect
{
    public Guid? ChosenCardId { get; private set; }

    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var controller = spell.ControllerId == state.Player1.Id ? state.Player1 : state.Player2;
        var options = controller.Hand.Cards;
        ChosenCardId = await handler.ChooseCard(options, "Choose a card", optional: false, ct: ct);
    }
}

public class AsyncSpellEffectTests
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
    public async Task AsyncEffect_CanCallDecisionHandler_DuringResolution()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        // Put a card in P1's hand to be the ChooseCard option
        var handCard = GameCard.Create("Target Card");
        state.Player1.Hand.Add(handCard);

        // Register a test async effect for "Async Spell"
        var asyncEffect = new TestAsyncEffect();
        CardDefinitions.Register(new CardDefinition(
            ManaCost: ManaCost.Parse("{U}"),
            ManaAbility: null,
            Power: null,
            Toughness: null,
            CardTypes: CardType.Instant,
            Effect: asyncEffect
        ) { Name = "Async Spell" });

        try
        {
            // Enqueue the card choice that the async effect will request
            h1.EnqueueCardChoice(handCard.Id);

            // Put the async spell on the stack
            var spellCard = GameCard.Create("Async Spell");
            var stackObj = new StackObject(spellCard, state.Player1.Id,
                new Dictionary<ManaColor, int> { [ManaColor.Blue] = 1 },
                new List<TargetInfo>(), 0);
            state.Stack.Add(stackObj);

            // Both pass priority -> stack resolves
            h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
            h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
            // After resolution, both pass again to exit priority
            h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
            h2.EnqueueAction(GameAction.Pass(state.Player2.Id));

            await engine.RunPriorityAsync();

            // Verify the async effect called ChooseCard and got the result
            asyncEffect.ChosenCardId.Should().Be(handCard.Id);

            // Verify the spell resolved (went to graveyard)
            state.Player1.Graveyard.Cards.Should().Contain(c => c.Id == spellCard.Id);
            state.Stack.Should().BeEmpty();
        }
        finally
        {
            // Clean up the registered card definition
            CardDefinitions.Unregister("Async Spell");
        }
    }

    [Fact]
    public async Task SyncEffect_StillWorksViaResolveAsync()
    {
        // Verify backward-compat: sync Resolve() is called by default ResolveAsync()
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", new TestDecisionHandler()),
            new Player(Guid.NewGuid(), "P2", new TestDecisionHandler()));

        var creature = GameCard.Create("Mogg Fanatic", "Creature - Goblin");
        creature.Power = 1;
        state.Player2.Battlefield.Add(creature);

        var swords = GameCard.Create("Swords to Plowshares");
        var spell = new StackObject(swords, state.Player1.Id,
            new Dictionary<ManaColor, int> { [ManaColor.White] = 1 },
            new List<TargetInfo> { new(creature.Id, state.Player2.Id, ZoneType.Battlefield) }, 0);

        var effect = new SwordsToPlowsharesEffect();

        // Call ResolveAsync on a sync-only effect - should delegate to Resolve()
        await effect.ResolveAsync(state, spell, state.Player1.DecisionHandler);

        state.Player2.Battlefield.Cards.Should().NotContain(c => c.Id == creature.Id);
        state.Player2.Exile.Cards.Should().Contain(c => c.Id == creature.Id);
        state.Player2.Life.Should().Be(21);
    }

    [Fact]
    public void SpellEffect_ResolveIsVirtual_NotAbstract()
    {
        // Verify that SpellEffect can be instantiated without overriding Resolve
        var effect = new TestAsyncEffect();
        // Calling Resolve (sync) should not throw - it's a virtual no-op
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", new TestDecisionHandler()),
            new Player(Guid.NewGuid(), "P2", new TestDecisionHandler()));
        var card = GameCard.Create("Test");
        var spell = new StackObject(card, state.Player1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);

        var act = () => effect.Resolve(state, spell);
        act.Should().NotThrow();
    }
}
