using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class Phase12SurvivalTests
{
    [Fact]
    public void SurvivalOfTheFittest_HasActivatedAbility()
    {
        CardDefinitions.TryGet("Survival of the Fittest", out var def).Should().BeTrue();
        def!.ActivatedAbility.Should().NotBeNull();
        def.ActivatedAbility!.Cost.ManaCost.Should().NotBeNull();
        def.ActivatedAbility.Cost.ManaCost!.ToString().Should().Be("{G}");
        def.ActivatedAbility.Cost.DiscardCardType.Should().Be(CardType.Creature);
        def.ActivatedAbility.Effect.Should().BeOfType<SearchLibraryByTypeEffect>();
    }

    [Fact]
    public async Task Survival_DiscardCreature_SearchesForCreature()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var survival = GameCard.Create("Survival of the Fittest");
        p1.Battlefield.Add(survival);

        // Creature in hand to discard
        var creatureInHand = new GameCard { Name = "Goblin Lackey", CardTypes = CardType.Creature };
        p1.Hand.Add(creatureInHand);

        // Creature in library to find
        var creatureInLib = new GameCard { Name = "Tarmogoyf", CardTypes = CardType.Creature };
        p1.Library.Add(creatureInLib);

        // Give mana
        p1.ManaPool.Add(ManaColor.Green, 1);

        // Enqueue: choose creature to discard, then choose creature from library
        handler.EnqueueCardChoice(creatureInHand.Id); // discard this creature
        handler.EnqueueCardChoice(creatureInLib.Id);   // pick this from library

        await engine.ExecuteAction(GameAction.ActivateAbility(p1.Id, survival.Id), ct: default);

        // Resolve the ability from the stack
        await engine.ResolveAllTriggersAsync();

        // Discarded creature should be in graveyard
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Goblin Lackey");

        // Found creature should be in hand
        p1.Hand.Cards.Should().Contain(c => c.Name == "Tarmogoyf");

        // Mana should be spent
        p1.ManaPool.Total.Should().Be(0);
    }

    [Fact]
    public async Task Survival_NoCreatureInHand_CannotActivate()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var survival = GameCard.Create("Survival of the Fittest");
        p1.Battlefield.Add(survival);

        // Only non-creatures in hand
        p1.Hand.Add(new GameCard { Name = "Lightning Bolt", CardTypes = CardType.Instant });
        p1.ManaPool.Add(ManaColor.Green, 1);

        await engine.ExecuteAction(GameAction.ActivateAbility(p1.Id, survival.Id), ct: default);

        // Ability should not have been activated (nothing on stack)
        state.StackCount.Should().Be(0);
        // Mana should not be spent
        p1.ManaPool.Total.Should().Be(1);
    }

    [Fact]
    public async Task Survival_NoMana_CannotActivate()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var survival = GameCard.Create("Survival of the Fittest");
        p1.Battlefield.Add(survival);

        p1.Hand.Add(new GameCard { Name = "Bear", CardTypes = CardType.Creature });
        // No mana

        await engine.ExecuteAction(GameAction.ActivateAbility(p1.Id, survival.Id), ct: default);

        state.StackCount.Should().Be(0);
    }
}
