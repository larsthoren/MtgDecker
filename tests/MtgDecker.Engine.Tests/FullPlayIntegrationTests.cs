using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class FullPlayIntegrationTests
{
    [Fact]
    public async Task WildGrowth_On_Forest_Produces_Double_Green()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var forest = GameCard.Create("Forest");
        p1.Battlefield.Add(forest);

        var wildGrowth = GameCard.Create("Wild Growth");
        wildGrowth.AttachedTo = forest.Id;
        p1.Battlefield.Add(wildGrowth);

        var tap = GameAction.TapCard(p1.Id, forest.Id);
        await engine.ExecuteAction(tap);

        p1.ManaPool.Available[ManaColor.Green].Should().Be(2);
    }

    [Fact]
    public async Task AuraOfSilence_Sacrifice_Destroys_Enchantment()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var aura = GameCard.Create("Aura of Silence");
        p1.Battlefield.Add(aura);

        var target = new GameCard { Name = "Enemy Enchantment", CardTypes = CardType.Enchantment };
        p2.Battlefield.Add(target);

        var action = GameAction.ActivateAbility(p1.Id, aura.Id, target.Id);
        await engine.ExecuteAction(action);
        await engine.ResolveAllTriggersAsync();

        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Aura of Silence");
        p2.Battlefield.Cards.Should().NotContain(c => c.Name == "Enemy Enchantment");
    }

    [Fact]
    public void SerrasSanctum_Scales_With_Enchantment_Count()
    {
        CardDefinitions.TryGet("Serra's Sanctum", out var def).Should().BeTrue();
        def!.ManaAbility.Should().NotBeNull();
        def.ManaAbility!.Type.Should().Be(ManaAbilityType.Dynamic);
    }

    [Fact]
    public void GempalmIncinerator_Has_Cycling_And_CyclingTrigger()
    {
        CardDefinitions.TryGet("Gempalm Incinerator", out var def).Should().BeTrue();
        def!.CyclingCost.Should().NotBeNull();
        def.CyclingTriggers.Should().ContainSingle();
    }

    [Fact]
    public void Replenish_Has_Effect()
    {
        CardDefinitions.TryGet("Replenish", out var def).Should().BeTrue();
        def!.Effect.Should().NotBeNull();
    }

    [Fact]
    public async Task SterlingGrove_Protects_Enchantments_From_Targeting()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var grove = GameCard.Create("Sterling Grove");
        p1.Battlefield.Add(grove);
        var presence = GameCard.Create("Enchantress's Presence");
        p1.Battlefield.Add(presence);
        engine.RecalculateState();

        // Enchantress's Presence should have shroud
        presence.ActiveKeywords.Should().Contain(Keyword.Shroud);
        // Grove itself should not (ExcludeSelf)
        grove.ActiveKeywords.Should().NotContain(Keyword.Shroud);
    }

    [Fact]
    public async Task Full_Cycle_Gempalm_Deals_Damage_And_Draws()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // Setup: 2 goblins on battlefield
        p1.Battlefield.Add(new GameCard { Name = "G1", CardTypes = CardType.Creature, Subtypes = ["Goblin"], BasePower = 1, BaseToughness = 1 });
        p1.Battlefield.Add(new GameCard { Name = "G2", CardTypes = CardType.Creature, Subtypes = ["Goblin"], BasePower = 1, BaseToughness = 1 });

        // Target creature for Gempalm trigger
        var target = new GameCard { Name = "Elf", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 3 };
        p2.Battlefield.Add(target);

        // Card to draw
        p1.Library.Add(new GameCard { Name = "TopCard" });

        // Gempalm in hand with cycling mana
        var gempalm = GameCard.Create("Gempalm Incinerator");
        p1.Hand.Add(gempalm);
        p1.ManaPool.Add(ManaColor.Red, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 1);

        // Queue: target the Elf for the cycling trigger
        handler.EnqueueCardChoice(target.Id);

        // Cycle
        var action = GameAction.Cycle(p1.Id, gempalm.Id);
        await engine.ExecuteAction(action);

        // Card should be in graveyard, drew a card
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Gempalm Incinerator");
        p1.Hand.Cards.Should().Contain(c => c.Name == "TopCard");

        // Trigger on stack
        state.Stack.Should().ContainSingle();

        // Resolve trigger
        await engine.ResolveAllTriggersAsync();

        // 2 goblins = 2 damage to Elf
        target.DamageMarked.Should().Be(2);
    }

    [Fact]
    public async Task AuraOfSilence_Increases_Opponent_Enchantment_Cost()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var aura = GameCard.Create("Aura of Silence");
        p1.Battlefield.Add(aura);

        engine.RecalculateState();

        // Cost modification: opponent's enchantments cost 2 more
        var opponentCard = new GameCard { Name = "Test Enchantment", CardTypes = CardType.Enchantment };
        var costMod = engine.ComputeCostModification(opponentCard, p2);
        costMod.Should().Be(2);

        // Controller's enchantments should NOT cost more
        var ownCard = new GameCard { Name = "Test Enchantment", CardTypes = CardType.Enchantment };
        var ownCostMod = engine.ComputeCostModification(ownCard, p1);
        ownCostMod.Should().Be(0);
    }

    [Fact]
    public async Task Mountainwalk_Prevents_Blocking_When_Defender_Controls_Mountain()
    {
        var p1Handler = new TestDecisionHandler();
        var p2Handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", p1Handler);
        var p2 = new Player(Guid.NewGuid(), "P2", p2Handler);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // Goblin Pyromancer (has mountainwalk from ETB effect) - use a custom creature with mountainwalk
        var attacker = new GameCard
        {
            Name = "Walker",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
            TurnEnteredBattlefield = 0, // no summoning sickness
        };
        p1.Battlefield.Add(attacker);

        // Grant mountainwalk via active keyword
        attacker.ActiveKeywords.Add(Keyword.Mountainwalk);

        // Defender controls a Mountain
        var mountain = GameCard.Create("Mountain");
        p2.Battlefield.Add(mountain);

        var blocker = new GameCard
        {
            Name = "Blocker",
            CardTypes = CardType.Creature,
            BasePower = 3,
            BaseToughness = 3,
        };
        p2.Battlefield.Add(blocker);

        // Declare attacker
        p1Handler.EnqueueAttackers([attacker.Id]);
        // P2 attempts to block
        p2Handler.EnqueueBlockers(new Dictionary<Guid, Guid> { { blocker.Id, attacker.Id } });

        state.TurnNumber = 1;
        state.CurrentPhase = Phase.Combat;

        await engine.RunCombatAsync(CancellationToken.None);

        // Mountainwalk should prevent the block, dealing damage directly to player
        p2.Life.Should().Be(18); // 20 - 2 damage from unblocked attacker
    }

    [Fact]
    public async Task SerrasSanctum_Produces_Mana_Equal_To_Enchantment_Count()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // Place Serra's Sanctum and 3 enchantments
        var sanctum = GameCard.Create("Serra's Sanctum");
        p1.Battlefield.Add(sanctum);
        p1.Battlefield.Add(new GameCard { Name = "Ench1", CardTypes = CardType.Enchantment });
        p1.Battlefield.Add(new GameCard { Name = "Ench2", CardTypes = CardType.Enchantment });
        p1.Battlefield.Add(new GameCard { Name = "Ench3", CardTypes = CardType.Enchantment });

        var tap = GameAction.TapCard(p1.Id, sanctum.Id);
        await engine.ExecuteAction(tap);

        // Should produce 3 white mana (one per enchantment)
        p1.ManaPool.Available[ManaColor.White].Should().Be(3);
    }

    [Fact]
    public async Task Replenish_Returns_Enchantments_Not_Creatures()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.TurnNumber = 5;

        // Put enchantments and a creature in graveyard
        var ench1 = new GameCard { Name = "Sterling Grove", CardTypes = CardType.Enchantment };
        var ench2 = new GameCard { Name = "Exploration", CardTypes = CardType.Enchantment };
        var creature = new GameCard { Name = "Goblin", CardTypes = CardType.Creature };
        p1.Graveyard.Add(ench1);
        p1.Graveyard.Add(ench2);
        p1.Graveyard.Add(creature);

        var replenish = new GameCard { Name = "Replenish" };
        var spell = new StackObject(replenish, p1.Id, new(), new(), 1);

        CardDefinitions.TryGet("Replenish", out var def).Should().BeTrue();
        def!.Effect!.Resolve(state, spell);

        p1.Battlefield.Cards.Should().HaveCount(2);
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Sterling Grove");
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Exploration");
        p1.Graveyard.Cards.Should().ContainSingle(c => c.Name == "Goblin");
    }
}
