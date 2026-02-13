using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class StackTargetingTests
{
    [Fact]
    public void SpellFilter_MatchesStackTarget()
    {
        var filter = TargetFilter.Spell();
        var card = new GameCard { Name = "Lightning Bolt" };
        filter.IsLegal(card, ZoneType.Stack).Should().BeTrue();
    }

    [Fact]
    public void SpellFilter_DoesNotMatchBattlefieldCard()
    {
        var filter = TargetFilter.Spell();
        var card = new GameCard { Name = "Bear", CardTypes = CardType.Creature };
        filter.IsLegal(card, ZoneType.Battlefield).Should().BeFalse();
    }

    [Fact]
    public void SpellFilter_DoesNotMatchHandCard()
    {
        var filter = TargetFilter.Spell();
        var card = new GameCard { Name = "Spell", CardTypes = CardType.Instant };
        filter.IsLegal(card, ZoneType.Hand).Should().BeFalse();
    }

    [Fact]
    public void SpellFilter_DoesNotMatchPlayerTarget()
    {
        var filter = TargetFilter.Spell();
        var playerSentinel = new GameCard { Name = "Player" };
        filter.IsLegal(playerSentinel, ZoneType.None).Should().BeFalse();
    }

    [Fact]
    public async Task CastSpell_WithSpellFilter_PresentStackObjectsAsTargets()
    {
        // Setup: Player A casts a creature spell that goes on the stack,
        // then Player B casts a counterspell targeting it
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Alice", handler1);
        var p2 = new Player(Guid.NewGuid(), "Bob", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // Register a simple creature and a counterspell-like card
        CardDefinitions.Register(new CardDefinition(
            ManaCost: ManaCost.Parse("{1}{G}"),
            ManaAbility: null,
            Power: 2,
            Toughness: 2,
            CardTypes: CardType.Creature,
            TargetFilter: null,
            Effect: null
        ) { Name = "Test Bear" });

        CardDefinitions.Register(new CardDefinition(
            ManaCost: ManaCost.Parse("{1}{U}"),
            ManaAbility: null,
            Power: null,
            Toughness: null,
            CardTypes: CardType.Instant,
            TargetFilter: TargetFilter.Spell(),
            Effect: new TestNoOpEffect()
        ) { Name = "Test Counterspell" });

        try
        {
            // Give P1 lands and a creature spell
            var forest1 = GameCard.Create("Forest", "Basic Land — Forest");
            var forest2 = GameCard.Create("Forest", "Basic Land — Forest");
            p1.Battlefield.Add(forest1);
            p1.Battlefield.Add(forest2);
            var bear = new GameCard { Name = "Test Bear", CardTypes = CardType.Creature, Power = 2, Toughness = 2 };
            p1.Hand.Add(bear);

            // Give P2 a counterspell
            var counter = new GameCard { Name = "Test Counterspell", CardTypes = CardType.Instant };
            p2.Hand.Add(counter);

            state.CurrentPhase = Phase.MainPhase1;
            state.ActivePlayer = p1;

            // P1: tap forests for mana and cast the bear
            await engine.ExecuteAction(GameAction.TapCard(p1.Id, forest1.Id));
            await engine.ExecuteAction(GameAction.TapCard(p1.Id, forest2.Id));
            await engine.ExecuteAction(GameAction.CastSpell(p1.Id, bear.Id));

            // Bear should now be on the stack
            state.Stack.Should().HaveCount(1);
            state.Stack[0].Card.Name.Should().Be("Test Bear");

            // P2: add mana directly to pool (Island not registered in CardDefinitions)
            p2.ManaPool.Add(ManaColor.Blue, 2);

            // Enqueue the target: the bear on the stack
            handler2.EnqueueTarget(new TargetInfo(bear.Id, p1.Id, ZoneType.Stack));

            await engine.ExecuteAction(GameAction.CastSpell(p2.Id, counter.Id));

            // The counterspell should be on the stack targeting the bear
            state.Stack.Should().HaveCount(2);
            var counterOnStack = state.Stack[1];
            counterOnStack.Card.Name.Should().Be("Test Counterspell");
            counterOnStack.Targets.Should().HaveCount(1);
            counterOnStack.Targets[0].CardId.Should().Be(bear.Id);
            counterOnStack.Targets[0].Zone.Should().Be(ZoneType.Stack);
        }
        finally
        {
            CardDefinitions.Unregister("Test Bear");
            CardDefinitions.Unregister("Test Counterspell");
        }
    }

    [Fact]
    public async Task CastSpell_WithSpellFilter_NoSpellsOnStack_CannotCast()
    {
        // If no spells are on the stack, a counterspell has no legal targets
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Alice", handler1);
        var p2 = new Player(Guid.NewGuid(), "Bob", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        CardDefinitions.Register(new CardDefinition(
            ManaCost: ManaCost.Parse("{U}"),
            ManaAbility: null,
            Power: null,
            Toughness: null,
            CardTypes: CardType.Instant,
            TargetFilter: TargetFilter.Spell(),
            Effect: new TestNoOpEffect()
        ) { Name = "Test Counter2" });

        try
        {
            var counter = new GameCard { Name = "Test Counter2", CardTypes = CardType.Instant };
            p2.Hand.Add(counter);

            state.CurrentPhase = Phase.MainPhase1;
            state.ActivePlayer = p2;

            // Add mana directly to pool
            p2.ManaPool.Add(ManaColor.Blue, 1);
            await engine.ExecuteAction(GameAction.CastSpell(p2.Id, counter.Id));

            // Should NOT be on the stack (no legal targets)
            state.Stack.Should().BeEmpty();
            // Card should still be in hand
            p2.Hand.Cards.Should().Contain(c => c.Name == "Test Counter2");
        }
        finally
        {
            CardDefinitions.Unregister("Test Counter2");
        }
    }

    [Fact]
    public async Task ResolveStack_WithStackTarget_FizzlesIfTargetAlreadyResolved()
    {
        // If the targeted spell has already resolved (left the stack), the counterspell fizzles
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Alice", handler1);
        var p2 = new Player(Guid.NewGuid(), "Bob", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // Manually set up a counterspell on the stack targeting a spell that's no longer there
        var targetCard = new GameCard { Name = "Lightning Bolt", CardTypes = CardType.Instant };
        var counterCard = new GameCard { Name = "Counterspell", CardTypes = CardType.Instant };

        CardDefinitions.Register(new CardDefinition(
            ManaCost: ManaCost.Parse("{U}{U}"),
            ManaAbility: null,
            Power: null,
            Toughness: null,
            CardTypes: CardType.Instant,
            TargetFilter: TargetFilter.Spell(),
            Effect: new TestNoOpEffect()
        ) { Name = "Counterspell" });

        try
        {
            // Put the counterspell on the stack targeting a card that ISN'T on the stack
            var counterStackObj = new StackObject(
                counterCard, p2.Id,
                new Dictionary<ManaColor, int> { [ManaColor.Blue] = 2 },
                new List<TargetInfo> { new(targetCard.Id, p1.Id, ZoneType.Stack) },
                0);
            state.Stack.Add(counterStackObj);

            // The target spell is NOT on the stack (already resolved)
            // Resolve should cause the counterspell to fizzle
            state.CurrentPhase = Phase.MainPhase1;
            state.ActivePlayer = p1;

            // Both players pass priority to trigger resolution
            handler1.EnqueueAction(GameAction.Pass(p1.Id));
            handler2.EnqueueAction(GameAction.Pass(p2.Id));

            await engine.RunPriorityAsync();

            // Counterspell should have fizzled and gone to graveyard
            state.Stack.Should().BeEmpty();
            p2.Graveyard.Cards.Should().Contain(c => c.Name == "Counterspell");
        }
        finally
        {
            CardDefinitions.Unregister("Counterspell");
        }
    }

    [Fact]
    public async Task ResolveStack_WithStackTarget_DoesNotFizzleIfTargetStillOnStack()
    {
        // If the targeted spell is still on the stack, the counterspell should resolve normally
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Alice", handler1);
        var p2 = new Player(Guid.NewGuid(), "Bob", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var targetCard = new GameCard { Name = "Lightning Bolt", CardTypes = CardType.Instant };
        var counterCard = new GameCard { Name = "Counterspell2", CardTypes = CardType.Instant };

        CardDefinitions.Register(new CardDefinition(
            ManaCost: ManaCost.Parse("{U}{U}"),
            ManaAbility: null,
            Power: null,
            Toughness: null,
            CardTypes: CardType.Instant,
            TargetFilter: TargetFilter.Spell(),
            Effect: new TestNoOpEffect()
        ) { Name = "Counterspell2" });

        try
        {
            // Put target spell on the stack first
            var boltStackObj = new StackObject(
                targetCard, p1.Id,
                new Dictionary<ManaColor, int> { [ManaColor.Red] = 1 },
                new List<TargetInfo>(),
                0);
            state.Stack.Add(boltStackObj);

            // Then put counterspell on top targeting the bolt
            var counterStackObj = new StackObject(
                counterCard, p2.Id,
                new Dictionary<ManaColor, int> { [ManaColor.Blue] = 2 },
                new List<TargetInfo> { new(targetCard.Id, p1.Id, ZoneType.Stack) },
                1);
            state.Stack.Add(counterStackObj);

            state.CurrentPhase = Phase.MainPhase1;
            state.ActivePlayer = p1;

            // Both pass priority repeatedly to resolve entire stack
            handler1.EnqueueAction(GameAction.Pass(p1.Id));
            handler2.EnqueueAction(GameAction.Pass(p2.Id));
            handler1.EnqueueAction(GameAction.Pass(p1.Id));
            handler2.EnqueueAction(GameAction.Pass(p2.Id));

            await engine.RunPriorityAsync();

            // Counterspell should have resolved normally (not fizzled) and gone to graveyard
            p2.Graveyard.Cards.Should().Contain(c => c.Name == "Counterspell2");
            // The log should NOT contain a fizzle message for the counterspell
            state.GameLog.Should().NotContain(l => l.Contains("Counterspell2") && l.Contains("fizzles"));
            // The log should contain a resolve message
            state.GameLog.Should().Contain(l => l.Contains("Resolving Counterspell2"));
        }
        finally
        {
            CardDefinitions.Unregister("Counterspell2");
        }
    }
}
