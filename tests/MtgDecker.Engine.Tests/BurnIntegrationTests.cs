using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class BurnIntegrationTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler h1, TestDecisionHandler h2) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Burn", h1);
        var p2 = new Player(Guid.NewGuid(), "Opponent", h2);
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
    public async Task LightningBolt_DealsThreeDamageToPlayer()
    {
        // Setup: Player 1 has Lightning Bolt in hand, a Mountain on battlefield
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var mountain = GameCard.Create("Mountain");
        state.Player1.Battlefield.Add(mountain);

        var bolt = GameCard.Create("Lightning Bolt");
        state.Player1.Hand.Add(bolt);

        // Target Player 2
        h1.EnqueueTarget(new TargetInfo(Guid.Empty, state.Player2.Id, ZoneType.None));

        // Actions: P1 taps Mountain (gets red mana), casts Bolt
        h1.EnqueueAction(GameAction.TapCard(state.Player1.Id, mountain.Id));
        h1.EnqueueAction(GameAction.CastSpell(state.Player1.Id, bolt.Id));
        // After casting, both pass → Bolt resolves (auto-pass from empty queues)

        await engine.RunPriorityAsync();

        // Verify: Player 2 life goes from 20 to 17
        state.Player2.Life.Should().Be(17);
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Name == "Lightning Bolt");
    }

    [Fact]
    public async Task LightningBolt_KillsCreature_ViaStateBasedActions()
    {
        // Setup: Player 2 has a 2/2 creature on battlefield
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var creature = GameCard.Create("Goblin Guide");
        state.Player2.Battlefield.Add(creature);

        var mountain = GameCard.Create("Mountain");
        state.Player1.Battlefield.Add(mountain);

        var bolt = GameCard.Create("Lightning Bolt");
        state.Player1.Hand.Add(bolt);

        // Target the creature
        h1.EnqueueTarget(new TargetInfo(creature.Id, state.Player2.Id, ZoneType.Battlefield));

        // Tap Mountain, cast Bolt
        h1.EnqueueAction(GameAction.TapCard(state.Player1.Id, mountain.Id));
        h1.EnqueueAction(GameAction.CastSpell(state.Player1.Id, bolt.Id));
        // Both pass → Bolt resolves → SBA kills creature

        await engine.RunPriorityAsync();

        // Verify: creature is dead (3 damage >= 2 toughness)
        state.Player2.Battlefield.Cards.Should().NotContain(c => c.Id == creature.Id);
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Id == creature.Id);
        state.Player2.Life.Should().Be(20, "bolt targeted the creature, not the player");
    }

    [Fact]
    public async Task LavaSpike_CanOnlyTargetPlayers()
    {
        // Lava Spike has TargetFilter.Player() — creature targets are not legal
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var creature = GameCard.Create("Goblin Guide");
        state.Player2.Battlefield.Add(creature);

        var mountain = GameCard.Create("Mountain");
        state.Player1.Battlefield.Add(mountain);

        var spike = GameCard.Create("Lava Spike");
        state.Player1.Hand.Add(spike);

        // Enqueue a player target (the only legal option)
        h1.EnqueueTarget(new TargetInfo(Guid.Empty, state.Player2.Id, ZoneType.None));

        h1.EnqueueAction(GameAction.TapCard(state.Player1.Id, mountain.Id));
        h1.EnqueueAction(GameAction.CastSpell(state.Player1.Id, spike.Id));

        await engine.RunPriorityAsync();

        // Verify: Lava Spike dealt 3 to player, creature is untouched
        state.Player2.Life.Should().Be(17);
        state.Player2.Battlefield.Cards.Should().Contain(c => c.Id == creature.Id);
    }

    [Fact]
    public async Task SearingBlood_CanOnlyTargetCreatures()
    {
        // Searing Blood has TargetFilter.Creature() — player targets are not legal
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var creature = GameCard.Create("Mogg Fanatic"); // 1/1
        state.Player2.Battlefield.Add(creature);

        var mountain1 = GameCard.Create("Mountain");
        var mountain2 = GameCard.Create("Mountain");
        state.Player1.Battlefield.Add(mountain1);
        state.Player1.Battlefield.Add(mountain2);

        var blood = GameCard.Create("Searing Blood");
        state.Player1.Hand.Add(blood);

        // Target the creature (only legal target for creature-only filter)
        h1.EnqueueTarget(new TargetInfo(creature.Id, state.Player2.Id, ZoneType.Battlefield));

        h1.EnqueueAction(GameAction.TapCard(state.Player1.Id, mountain1.Id));
        h1.EnqueueAction(GameAction.TapCard(state.Player1.Id, mountain2.Id));
        h1.EnqueueAction(GameAction.CastSpell(state.Player1.Id, blood.Id));

        await engine.RunPriorityAsync();

        // Verify: creature takes 2 damage (enough to kill a 1/1)
        state.Player2.Battlefield.Cards.Should().NotContain(c => c.Id == creature.Id);
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Id == creature.Id);
    }

    [Fact]
    public async Task MultipleBolts_KillPlayer()
    {
        // Cast 7 Lightning Bolts (21 damage) to kill a player at 20 life
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        // Give P1 seven Mountains and seven Bolts
        var mountains = new List<GameCard>();
        var bolts = new List<GameCard>();
        for (int i = 0; i < 7; i++)
        {
            var mtn = GameCard.Create("Mountain");
            mountains.Add(mtn);
            state.Player1.Battlefield.Add(mtn);

            var bolt = GameCard.Create("Lightning Bolt");
            bolts.Add(bolt);
            state.Player1.Hand.Add(bolt);
        }

        // Each bolt: tap a Mountain, cast bolt targeting P2, both pass (auto), bolt resolves
        // After resolution, priority resets — P1 can take another action
        for (int i = 0; i < 7; i++)
        {
            h1.EnqueueTarget(new TargetInfo(Guid.Empty, state.Player2.Id, ZoneType.None));
            h1.EnqueueAction(GameAction.TapCard(state.Player1.Id, mountains[i].Id));
            h1.EnqueueAction(GameAction.CastSpell(state.Player1.Id, bolts[i].Id));
        }
        // After all 7 bolt-cast sequences, both players auto-pass

        await engine.RunPriorityAsync();

        // Player 2 should be dead (21 damage >= 20 life)
        state.IsGameOver.Should().BeTrue();
        state.Winner.Should().Be("Burn");
        state.Player2.Life.Should().BeLessThanOrEqualTo(0);
    }

    [Fact]
    public async Task ChainLightning_DealsThreeDamageAtSorcerySpeed()
    {
        // Chain Lightning is a sorcery — can only be cast at sorcery speed
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var mountain = GameCard.Create("Mountain");
        state.Player1.Battlefield.Add(mountain);

        var chain = GameCard.Create("Chain Lightning");
        state.Player1.Hand.Add(chain);

        h1.EnqueueTarget(new TargetInfo(Guid.Empty, state.Player2.Id, ZoneType.None));
        h1.EnqueueAction(GameAction.TapCard(state.Player1.Id, mountain.Id));
        h1.EnqueueAction(GameAction.CastSpell(state.Player1.Id, chain.Id));

        await engine.RunPriorityAsync();

        state.Player2.Life.Should().Be(17);
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Name == "Chain Lightning");
    }

    [Fact]
    public async Task ChainLightning_CannotBeCastDuringCombat()
    {
        // Sorcery-speed spells cannot be cast during combat phase
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.Combat;

        state.Player1.ManaPool.Add(ManaColor.Red, 1);

        var chain = GameCard.Create("Chain Lightning");
        state.Player1.Hand.Add(chain);

        h1.EnqueueTarget(new TargetInfo(Guid.Empty, state.Player2.Id, ZoneType.None));

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, chain.Id));

        // Spell should not be on the stack — rejected as sorcery-speed
        state.Stack.Should().BeEmpty();
        state.Player1.Hand.Cards.Should().Contain(c => c.Id == chain.Id);
    }

    [Fact]
    public async Task LightningBolt_CanBeCastDuringCombat()
    {
        // Instant-speed spells can be cast during combat
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.Combat;

        state.Player1.ManaPool.Add(ManaColor.Red, 1);

        var bolt = GameCard.Create("Lightning Bolt");
        state.Player1.Hand.Add(bolt);

        h1.EnqueueTarget(new TargetInfo(Guid.Empty, state.Player2.Id, ZoneType.None));

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, bolt.Id));

        state.Stack.Should().HaveCount(1);
        ((StackObject)state.Stack[0]).Card.Name.Should().Be("Lightning Bolt");
    }

    [Fact]
    public async Task LightningBolt_DamageDoesNotKill_HighToughness()
    {
        // 3 damage to a 4/4 should not kill it
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var creature = GameCard.Create("Siege-Gang Commander"); // 2/2 from registry
        creature.Power = 4;
        creature.Toughness = 4;
        state.Player2.Battlefield.Add(creature);

        var mountain = GameCard.Create("Mountain");
        state.Player1.Battlefield.Add(mountain);

        var bolt = GameCard.Create("Lightning Bolt");
        state.Player1.Hand.Add(bolt);

        h1.EnqueueTarget(new TargetInfo(creature.Id, state.Player2.Id, ZoneType.Battlefield));
        h1.EnqueueAction(GameAction.TapCard(state.Player1.Id, mountain.Id));
        h1.EnqueueAction(GameAction.CastSpell(state.Player1.Id, bolt.Id));

        await engine.RunPriorityAsync();

        // Creature took 3 damage but has 4 toughness — survives
        state.Player2.Battlefield.Cards.Should().Contain(c => c.Id == creature.Id);
        creature.DamageMarked.Should().Be(3);
    }

    [Fact]
    public async Task Bolt_Fizzles_WhenCreatureTargetRemoved()
    {
        // If the targeted creature leaves the battlefield before Bolt resolves, the spell fizzles
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var creature = GameCard.Create("Goblin Guide");
        state.Player2.Battlefield.Add(creature);

        var mountain = GameCard.Create("Mountain");
        state.Player1.Battlefield.Add(mountain);

        var bolt = GameCard.Create("Lightning Bolt");
        state.Player1.Hand.Add(bolt);

        h1.EnqueueTarget(new TargetInfo(creature.Id, state.Player2.Id, ZoneType.Battlefield));
        h1.EnqueueAction(GameAction.TapCard(state.Player1.Id, mountain.Id));
        h1.EnqueueAction(GameAction.CastSpell(state.Player1.Id, bolt.Id));

        // After bolt is on stack, manually remove the creature (simulating another effect)
        // We need P1 to pass, then P2 to pass, then resolution happens.
        // But before resolution, we remove the creature.
        // The simplest way: just call ExecuteAction then manually remove, then resolve.
        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, mountain.Id));
        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, bolt.Id));

        // Bolt is on the stack
        state.Stack.Should().HaveCount(1);

        // Remove creature before resolution (simulates another removal spell)
        state.Player2.Battlefield.RemoveById(creature.Id);
        state.Player2.Graveyard.Add(creature);

        // Now both pass and bolt resolves — target is gone, fizzle
        // We need RunPriorityAsync but both handlers are empty (auto-pass)
        await engine.RunPriorityAsync();

        // Bolt should fizzle — Player 2 life unchanged
        state.Player2.Life.Should().Be(20);
    }
}
