using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class PlayerTargetingTests
{
    [Fact]
    public void CreatureOrPlayer_MatchesCreatureOnBattlefield()
    {
        var filter = TargetFilter.CreatureOrPlayer();
        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature };
        filter.IsLegal(creature, ZoneType.Battlefield).Should().BeTrue();
    }

    [Fact]
    public void CreatureOrPlayer_MatchesPlayerTarget()
    {
        var filter = TargetFilter.CreatureOrPlayer();
        // Player targets use a sentinel card with ZoneType.None
        var playerSentinel = new GameCard { Name = "Player" };
        filter.IsLegal(playerSentinel, ZoneType.None).Should().BeTrue();
    }

    [Fact]
    public void CreatureOrPlayer_RejectsLand()
    {
        var filter = TargetFilter.CreatureOrPlayer();
        var land = new GameCard { Name = "Forest", CardTypes = CardType.Land };
        filter.IsLegal(land, ZoneType.Battlefield).Should().BeFalse();
    }

    [Fact]
    public void CreatureOrPlayer_RejectsCreatureInHand()
    {
        var filter = TargetFilter.CreatureOrPlayer();
        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature };
        filter.IsLegal(creature, ZoneType.Hand).Should().BeFalse();
    }

    [Fact]
    public void Player_DoesNotMatchCreature()
    {
        var filter = TargetFilter.Player();
        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature };
        filter.IsLegal(creature, ZoneType.Battlefield).Should().BeFalse();
    }

    [Fact]
    public void Player_MatchesPlayerTarget()
    {
        var filter = TargetFilter.Player();
        var playerSentinel = new GameCard { Name = "Player" };
        filter.IsLegal(playerSentinel, ZoneType.None).Should().BeTrue();
    }

    [Fact]
    public void Player_RejectsNonPlayerInNonBattlefieldZone()
    {
        var filter = TargetFilter.Player();
        var card = new GameCard { Name = "Spell", CardTypes = CardType.Instant };
        filter.IsLegal(card, ZoneType.Hand).Should().BeFalse();
    }

    [Fact]
    public void Creature_DoesNotMatchPlayerTarget()
    {
        // Existing Creature() filter should NOT match player targets
        var filter = TargetFilter.Creature();
        var playerSentinel = new GameCard { Name = "Player" };
        filter.IsLegal(playerSentinel, ZoneType.None).Should().BeFalse();
    }

    [Fact]
    public async Task CastSpell_WithCreatureOrPlayerFilter_IncludesPlayersInEligibleTargets()
    {
        // Integration test: when a spell with CreatureOrPlayer filter is cast,
        // both creatures and players should be in the eligible target list
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Alice", handler1);
        var p2 = new Player(Guid.NewGuid(), "Bob", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        CardDefinitions.Register(new CardDefinition(
            ManaCost: ManaCost.Parse("{R}"),
            ManaAbility: null,
            Power: null,
            Toughness: null,
            CardTypes: CardType.Instant,
            TargetFilter: TargetFilter.CreatureOrPlayer(),
            Effect: new TestNoOpEffect()
        ) { Name = "Test Bolt" });

        try
        {
            // Give P1 a mountain and the bolt
            var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
            p1.Battlefield.Add(mountain);
            var bolt = new GameCard { Name = "Test Bolt", CardTypes = CardType.Instant };
            p1.Hand.Add(bolt);

            // Put a creature on opponent's battlefield
            var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature, Power = 2, Toughness = 2 };
            p2.Battlefield.Add(creature);

            // Setup: tap mountain for mana
            state.CurrentPhase = Phase.MainPhase1;
            state.ActivePlayer = p1;
            await engine.ExecuteAction(GameAction.TapCard(p1.Id, mountain.Id));

            // Choose to target the player (Bob) instead of the creature
            handler1.EnqueueTarget(new TargetInfo(Guid.Empty, p2.Id, ZoneType.None));

            // Cast the spell
            await engine.ExecuteAction(GameAction.CastSpell(p1.Id, bolt.Id));

            // The spell should be on the stack with a player target
            state.Stack.Should().HaveCount(1);
            var stackObj = (StackObject)state.Stack[0];
            stackObj.Targets.Should().HaveCount(1);
            stackObj.Targets[0].CardId.Should().Be(Guid.Empty);
            stackObj.Targets[0].PlayerId.Should().Be(p2.Id);
            stackObj.Targets[0].Zone.Should().Be(ZoneType.None);
        }
        finally
        {
            CardDefinitions.Unregister("Test Bolt");
        }
    }

    [Fact]
    public async Task CastSpell_PlayerOnlyFilter_NoCreaturesIncluded()
    {
        // A spell with Player-only filter should still find valid targets (players)
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Alice", handler1);
        var p2 = new Player(Guid.NewGuid(), "Bob", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        CardDefinitions.Register(new CardDefinition(
            ManaCost: ManaCost.Parse("{R}"),
            ManaAbility: null,
            Power: null,
            Toughness: null,
            CardTypes: CardType.Sorcery,
            TargetFilter: TargetFilter.Player(),
            Effect: new TestNoOpEffect()
        ) { Name = "Lava Axe Test" });

        try
        {
            var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
            p1.Battlefield.Add(mountain);
            var axe = new GameCard { Name = "Lava Axe Test", CardTypes = CardType.Sorcery };
            p1.Hand.Add(axe);

            // Creature on battlefield -- should NOT be eligible for Player() filter
            var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature, Power = 2, Toughness = 2 };
            p2.Battlefield.Add(creature);

            state.CurrentPhase = Phase.MainPhase1;
            state.ActivePlayer = p1;
            await engine.ExecuteAction(GameAction.TapCard(p1.Id, mountain.Id));

            // Target the player
            handler1.EnqueueTarget(new TargetInfo(Guid.Empty, p2.Id, ZoneType.None));

            await engine.ExecuteAction(GameAction.CastSpell(p1.Id, axe.Id));

            state.Stack.Should().HaveCount(1);
            ((StackObject)state.Stack[0]).Targets[0].Zone.Should().Be(ZoneType.None);
            ((StackObject)state.Stack[0]).Targets[0].PlayerId.Should().Be(p2.Id);
        }
        finally
        {
            CardDefinitions.Unregister("Lava Axe Test");
        }
    }

    [Fact]
    public async Task CastSpell_CreatureOrPlayerFilter_NoCreatures_StillHasPlayerTargets()
    {
        // Even with no creatures on battlefield, player targets should be available
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Alice", handler1);
        var p2 = new Player(Guid.NewGuid(), "Bob", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        CardDefinitions.Register(new CardDefinition(
            ManaCost: ManaCost.Parse("{R}"),
            ManaAbility: null,
            Power: null,
            Toughness: null,
            CardTypes: CardType.Instant,
            TargetFilter: TargetFilter.CreatureOrPlayer(),
            Effect: new TestNoOpEffect()
        ) { Name = "Face Bolt" });

        try
        {
            var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
            p1.Battlefield.Add(mountain);
            var bolt = new GameCard { Name = "Face Bolt", CardTypes = CardType.Instant };
            p1.Hand.Add(bolt);

            // No creatures anywhere

            state.CurrentPhase = Phase.MainPhase1;
            state.ActivePlayer = p1;
            await engine.ExecuteAction(GameAction.TapCard(p1.Id, mountain.Id));

            // Target opponent player
            handler1.EnqueueTarget(new TargetInfo(Guid.Empty, p2.Id, ZoneType.None));

            await engine.ExecuteAction(GameAction.CastSpell(p1.Id, bolt.Id));

            // Should succeed -- player is a valid target even with no creatures
            state.Stack.Should().HaveCount(1);
            ((StackObject)state.Stack[0]).Targets[0].PlayerId.Should().Be(p2.Id);
        }
        finally
        {
            CardDefinitions.Unregister("Face Bolt");
        }
    }
}

/// <summary>
/// A no-op spell effect for testing targeting mechanics without side effects.
/// </summary>
internal class TestNoOpEffect : SpellEffect
{
}
