using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class Phase12OathOfDruidsTests
{
    [Fact]
    public void OathOfDruids_HasAnyUpkeepTrigger()
    {
        CardDefinitions.TryGet("Oath of Druids", out var def).Should().BeTrue();
        def!.Triggers.Should().ContainSingle();
        def.Triggers[0].Event.Should().Be(GameEvent.Upkeep);
        def.Triggers[0].Condition.Should().Be(TriggerCondition.AnyUpkeep);
        def.Triggers[0].Effect.Should().BeOfType<OathOfDruidsEffect>();
    }

    [Fact]
    public async Task OathEffect_OpponentHasMoreCreatures_RevealsUntilCreature()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.ActivePlayer = p1;

        // P2 has more creatures than P1
        p2.Battlefield.Add(new GameCard { Name = "Bear", CardTypes = CardType.Creature });
        // P1 has no creatures

        // Set up P1's library: non-creature, non-creature, creature
        var land = new GameCard { Name = "Forest", CardTypes = CardType.Land };
        var spell = new GameCard { Name = "Lightning Bolt", CardTypes = CardType.Instant };
        var creature = new GameCard { Name = "Tarmogoyf", CardTypes = CardType.Creature, BasePower = 4, BaseToughness = 5 };

        // Library draws from top (end of list), so add in reverse order
        p1.Library.Add(creature);  // bottom
        p1.Library.Add(spell);     // middle
        p1.Library.Add(land);      // top (drawn first)

        var effect = new OathOfDruidsEffect();
        var oathCard = GameCard.Create("Oath of Druids");
        var context = new EffectContext(state, p1, oathCard, handler);
        await effect.Execute(context);

        // Creature should be on battlefield
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Tarmogoyf");

        // Non-creatures should be in graveyard
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Forest");
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Lightning Bolt");

        // Library should have been drawn from
        p1.Library.Count.Should().Be(0);
    }

    [Fact]
    public async Task OathEffect_OpponentNotMoreCreatures_DoesNothing()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.ActivePlayer = p1;

        // Equal creatures
        p1.Battlefield.Add(new GameCard { Name = "Bear", CardTypes = CardType.Creature });
        p2.Battlefield.Add(new GameCard { Name = "Bear", CardTypes = CardType.Creature });

        p1.Library.Add(new GameCard { Name = "Tarmogoyf", CardTypes = CardType.Creature });

        var effect = new OathOfDruidsEffect();
        var oathCard = GameCard.Create("Oath of Druids");
        var context = new EffectContext(state, p1, oathCard, handler);
        await effect.Execute(context);

        // Nothing should happen
        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Tarmogoyf");
        p1.Library.Count.Should().Be(1);
    }

    [Fact]
    public async Task OathEffect_NoCreatureInLibrary_AllToGraveyard()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.ActivePlayer = p1;

        p2.Battlefield.Add(new GameCard { Name = "Bear", CardTypes = CardType.Creature });

        // Library has only non-creatures
        p1.Library.Add(new GameCard { Name = "Forest", CardTypes = CardType.Land });
        p1.Library.Add(new GameCard { Name = "Island", CardTypes = CardType.Land });

        var effect = new OathOfDruidsEffect();
        var oathCard = GameCard.Create("Oath of Druids");
        var context = new EffectContext(state, p1, oathCard, handler);
        await effect.Execute(context);

        p1.Battlefield.Cards.Should().NotContain(c => c.IsCreature);
        p1.Graveyard.Cards.Should().HaveCount(2);
        p1.Library.Count.Should().Be(0);
    }

    [Fact]
    public async Task OathEffect_EmptyLibrary_DoesNothing()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.ActivePlayer = p1;

        p2.Battlefield.Add(new GameCard { Name = "Bear", CardTypes = CardType.Creature });
        // Empty library

        var effect = new OathOfDruidsEffect();
        var oathCard = GameCard.Create("Oath of Druids");
        var context = new EffectContext(state, p1, oathCard, handler);
        await effect.Execute(context);

        p1.Battlefield.Cards.Should().BeEmpty();
        p1.Graveyard.Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task OathEffect_CreatureOnTop_OnlyThatCreature()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.ActivePlayer = p1;

        p2.Battlefield.Add(new GameCard { Name = "Bear", CardTypes = CardType.Creature });

        // Creature is on top
        var creature = new GameCard { Name = "Tarmogoyf", CardTypes = CardType.Creature, BasePower = 4, BaseToughness = 5 };
        p1.Library.Add(new GameCard { Name = "Forest", CardTypes = CardType.Land }); // bottom
        p1.Library.Add(creature); // top

        var effect = new OathOfDruidsEffect();
        var oathCard = GameCard.Create("Oath of Druids");
        var context = new EffectContext(state, p1, oathCard, handler);
        await effect.Execute(context);

        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Tarmogoyf");
        p1.Graveyard.Cards.Should().BeEmpty();
        p1.Library.Count.Should().Be(1); // Forest still in library
    }

    [Fact]
    public void CollectBoardTriggers_AnyUpkeep_MatchesAnyUpkeep()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        state.ActivePlayer = p2; // P2's upkeep

        // P1 controls Oath
        var oath = GameCard.Create("Oath of Druids");
        p1.Battlefield.Add(oath);

        engine.QueueBoardTriggersOnStackAsync(GameEvent.Upkeep, null);

        // Oath should trigger even though it's P2's upkeep
        state.StackCount.Should().BeGreaterThan(0);
    }
}
