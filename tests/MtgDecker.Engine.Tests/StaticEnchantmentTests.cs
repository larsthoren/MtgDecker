using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class StaticEnchantmentTests
{
    private static (GameEngine engine, GameState state, Player p1, Player p2) Setup()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, p1, p2);
    }

    // ==================== Crusade Tests ====================

    [Fact]
    public void Crusade_Buffs_White_Creatures()
    {
        var (engine, state, p1, _) = Setup();

        var crusade = GameCard.Create("Crusade", "Enchantment");
        var whiteCreature = new GameCard
        {
            Name = "Savannah Lions", BasePower = 2, BaseToughness = 1,
            CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{W}"),
        };

        p1.Battlefield.Add(crusade);
        p1.Battlefield.Add(whiteCreature);

        engine.RecalculateState();

        whiteCreature.Power.Should().Be(3);
        whiteCreature.Toughness.Should().Be(2);
    }

    [Fact]
    public void Crusade_Does_Not_Buff_NonWhite_Creatures()
    {
        var (engine, state, p1, _) = Setup();

        var crusade = GameCard.Create("Crusade", "Enchantment");
        var redCreature = new GameCard
        {
            Name = "Goblin Grunt", BasePower = 1, BaseToughness = 1,
            CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{R}"),
        };

        p1.Battlefield.Add(crusade);
        p1.Battlefield.Add(redCreature);

        engine.RecalculateState();

        redCreature.Power.Should().Be(1);
        redCreature.Toughness.Should().Be(1);
    }

    [Fact]
    public void Crusade_Buffs_Opponents_White_Creatures_Too()
    {
        var (engine, state, p1, p2) = Setup();

        var crusade = GameCard.Create("Crusade", "Enchantment");
        var opponentWhite = new GameCard
        {
            Name = "White Knight", BasePower = 2, BaseToughness = 2,
            CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{W}{W}"),
        };

        p1.Battlefield.Add(crusade);
        p2.Battlefield.Add(opponentWhite);

        engine.RecalculateState();

        opponentWhite.Power.Should().Be(3);
        opponentWhite.Toughness.Should().Be(3);
    }

    [Fact]
    public void Crusade_Buffs_Multicolored_Creatures_With_White()
    {
        var (engine, state, p1, _) = Setup();

        var crusade = GameCard.Create("Crusade", "Enchantment");
        var multicolor = new GameCard
        {
            Name = "WG Bear", BasePower = 2, BaseToughness = 2,
            CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{W}{G}"),
        };

        p1.Battlefield.Add(crusade);
        p1.Battlefield.Add(multicolor);

        engine.RecalculateState();

        multicolor.Power.Should().Be(3);
        multicolor.Toughness.Should().Be(3);
    }

    // ==================== Absolute Law Tests ====================

    [Fact]
    public void AbsoluteLaw_Grants_ProtectionFromRed_To_All_Creatures()
    {
        var (engine, state, p1, p2) = Setup();

        var law = GameCard.Create("Absolute Law", "Enchantment");
        var creature1 = new GameCard
        {
            Name = "Bear", BasePower = 2, BaseToughness = 2,
            CardTypes = CardType.Creature,
        };
        var creature2 = new GameCard
        {
            Name = "Opponent Bear", BasePower = 2, BaseToughness = 2,
            CardTypes = CardType.Creature,
        };

        p1.Battlefield.Add(law);
        p1.Battlefield.Add(creature1);
        p2.Battlefield.Add(creature2);

        engine.RecalculateState();

        creature1.ActiveKeywords.Should().Contain(Keyword.ProtectionFromRed);
        creature2.ActiveKeywords.Should().Contain(Keyword.ProtectionFromRed);
    }

    [Fact]
    public void AbsoluteLaw_Does_Not_Grant_ProtectionFromRed_To_NonCreatures()
    {
        var (engine, state, p1, _) = Setup();

        var law = GameCard.Create("Absolute Law", "Enchantment");
        var artifact = new GameCard
        {
            Name = "Some Artifact", CardTypes = CardType.Artifact,
        };

        p1.Battlefield.Add(law);
        p1.Battlefield.Add(artifact);

        engine.RecalculateState();

        artifact.ActiveKeywords.Should().NotContain(Keyword.ProtectionFromRed);
    }

    // ==================== Worship Tests ====================

    [Fact]
    public void Worship_Prevents_Lethal_Combat_Damage_When_Controlling_Creature()
    {
        var (engine, state, p1, _) = Setup();

        var worship = GameCard.Create("Worship", "Enchantment");
        var creature = new GameCard
        {
            Name = "Bear", BasePower = 2, BaseToughness = 2,
            CardTypes = CardType.Creature,
        };

        p1.Battlefield.Add(worship);
        p1.Battlefield.Add(creature);
        p1.AdjustLife(-15); // Set life to 5

        engine.RecalculateState();

        // 10 damage should be reduced so life stays at 1
        var result = engine.ApplyLethalDamageProtection(p1, 10);
        result.Should().Be(4); // 5 - 4 = 1
    }

    [Fact]
    public void Worship_Does_Not_Prevent_Damage_Without_Creatures()
    {
        var (engine, state, p1, _) = Setup();

        var worship = GameCard.Create("Worship", "Enchantment");
        p1.Battlefield.Add(worship);
        p1.AdjustLife(-15); // Set life to 5

        engine.RecalculateState();

        var result = engine.ApplyLethalDamageProtection(p1, 10);
        result.Should().Be(10); // No creature, no prevention
    }

    [Fact]
    public void Worship_Does_Not_Prevent_NonLethal_Damage()
    {
        var (engine, state, p1, _) = Setup();

        var worship = GameCard.Create("Worship", "Enchantment");
        var creature = new GameCard
        {
            Name = "Bear", BasePower = 2, BaseToughness = 2,
            CardTypes = CardType.Creature,
        };

        p1.Battlefield.Add(worship);
        p1.Battlefield.Add(creature);
        // Life starts at 20, 5 damage = 15 remaining > 1, no prevention needed

        engine.RecalculateState();

        var result = engine.ApplyLethalDamageProtection(p1, 5);
        result.Should().Be(5);
    }

    [Fact]
    public void Worship_Keeps_Life_At_Exactly_1()
    {
        var (engine, state, p1, _) = Setup();

        var worship = GameCard.Create("Worship", "Enchantment");
        var creature = new GameCard
        {
            Name = "Bear", BasePower = 2, BaseToughness = 2,
            CardTypes = CardType.Creature,
        };

        p1.Battlefield.Add(worship);
        p1.Battlefield.Add(creature);
        p1.AdjustLife(-19); // Set life to 1

        engine.RecalculateState();

        // Already at 1, any damage should be 0
        var result = engine.ApplyLethalDamageProtection(p1, 100);
        result.Should().Be(0);
    }

    [Fact]
    public void Worship_Does_Not_Protect_Opponent()
    {
        var (engine, state, p1, p2) = Setup();

        var worship = GameCard.Create("Worship", "Enchantment");
        var creature = new GameCard
        {
            Name = "Bear", BasePower = 2, BaseToughness = 2,
            CardTypes = CardType.Creature,
        };

        // P1 controls Worship and a creature, but P2 doesn't have Worship
        p1.Battlefield.Add(worship);
        p1.Battlefield.Add(creature);
        p2.AdjustLife(-17); // Set P2 life to 3

        engine.RecalculateState();

        // P2 should not be protected
        var result = engine.ApplyLethalDamageProtection(p2, 10);
        result.Should().Be(10);
    }

    // ==================== Sphere of Resistance Tests ====================

    [Fact]
    public void SphereOfResistance_Increases_Cost_For_Both_Players()
    {
        var (engine, state, p1, p2) = Setup();

        var sphere = GameCard.Create("Sphere of Resistance", "Artifact");
        p1.Battlefield.Add(sphere);
        engine.RecalculateState();

        var spell = new GameCard
        {
            Name = "Lightning Bolt", ManaCost = ManaCost.Parse("{R}"),
            CardTypes = CardType.Instant,
        };

        // Cost mod should apply to both P1 and P2
        engine.ComputeCostModification(spell, p1).Should().Be(1);
        engine.ComputeCostModification(spell, p2).Should().Be(1);
    }

    [Fact]
    public void SphereOfResistance_Stacks_With_Other_Spheres()
    {
        var (engine, state, p1, _) = Setup();

        var sphere1 = GameCard.Create("Sphere of Resistance", "Artifact");
        var sphere2 = GameCard.Create("Sphere of Resistance", "Artifact");
        p1.Battlefield.Add(sphere1);
        p1.Battlefield.Add(sphere2);
        engine.RecalculateState();

        var spell = new GameCard
        {
            Name = "Some Spell", ManaCost = ManaCost.Parse("{1}{R}"),
            CardTypes = CardType.Sorcery,
        };

        engine.ComputeCostModification(spell, p1).Should().Be(2);
    }

    // ==================== Chill Tests ====================

    [Fact]
    public void Chill_Increases_Red_Spell_Cost_By_2()
    {
        var (engine, state, p1, p2) = Setup();

        var chill = GameCard.Create("Chill", "Enchantment");
        p1.Battlefield.Add(chill);
        engine.RecalculateState();

        var redSpell = new GameCard
        {
            Name = "Lightning Bolt", ManaCost = ManaCost.Parse("{R}"),
            CardTypes = CardType.Instant,
        };

        engine.ComputeCostModification(redSpell, p1).Should().Be(2);
        engine.ComputeCostModification(redSpell, p2).Should().Be(2);
    }

    [Fact]
    public void Chill_Does_Not_Affect_NonRed_Spells()
    {
        var (engine, state, p1, _) = Setup();

        var chill = GameCard.Create("Chill", "Enchantment");
        p1.Battlefield.Add(chill);
        engine.RecalculateState();

        var blueSpell = new GameCard
        {
            Name = "Counterspell", ManaCost = ManaCost.Parse("{U}{U}"),
            CardTypes = CardType.Instant,
        };

        engine.ComputeCostModification(blueSpell, p1).Should().Be(0);
    }

    [Fact]
    public void Chill_Affects_Multicolored_Spells_With_Red()
    {
        var (engine, state, p1, _) = Setup();

        var chill = GameCard.Create("Chill", "Enchantment");
        p1.Battlefield.Add(chill);
        engine.RecalculateState();

        var multiSpell = new GameCard
        {
            Name = "RG Spell", ManaCost = ManaCost.Parse("{R}{G}"),
            CardTypes = CardType.Sorcery,
        };

        engine.ComputeCostModification(multiSpell, p1).Should().Be(2);
    }

    // ==================== Gloom Tests ====================

    [Fact]
    public void Gloom_Increases_White_Spell_Cost_By_3()
    {
        var (engine, state, p1, p2) = Setup();

        var gloom = GameCard.Create("Gloom", "Enchantment");
        p1.Battlefield.Add(gloom);
        engine.RecalculateState();

        var whiteSpell = new GameCard
        {
            Name = "Swords to Plowshares", ManaCost = ManaCost.Parse("{W}"),
            CardTypes = CardType.Instant,
        };

        engine.ComputeCostModification(whiteSpell, p1).Should().Be(3);
        engine.ComputeCostModification(whiteSpell, p2).Should().Be(3);
    }

    [Fact]
    public void Gloom_Does_Not_Affect_NonWhite_Spells()
    {
        var (engine, state, p1, _) = Setup();

        var gloom = GameCard.Create("Gloom", "Enchantment");
        p1.Battlefield.Add(gloom);
        engine.RecalculateState();

        var blackSpell = new GameCard
        {
            Name = "Dark Ritual", ManaCost = ManaCost.Parse("{B}"),
            CardTypes = CardType.Instant,
        };

        engine.ComputeCostModification(blackSpell, p1).Should().Be(0);
    }

    // ==================== Null Rod Tests ====================

    [Fact]
    public async Task NullRod_Blocks_Artifact_Activated_Abilities()
    {
        var (engine, state, p1, _) = Setup();

        var nullRod = GameCard.Create("Null Rod", "Artifact");
        var artifactWithAbility = new GameCard
        {
            Id = Guid.NewGuid(),
            Name = "Test Artifact",
            CardTypes = CardType.Artifact,
        };

        p1.Battlefield.Add(nullRod);
        p1.Battlefield.Add(artifactWithAbility);
        engine.RecalculateState();

        var action = GameAction.ActivateAbility(p1.Id, artifactWithAbility.Id);
        await engine.ExecuteAction(action, CancellationToken.None);

        state.GameLog.Should().Contain(l => l.Contains("activated abilities can't be activated"));
    }

    [Fact]
    public async Task NullRod_Blocks_Artifact_Mana_Abilities()
    {
        var (engine, state, p1, _) = Setup();

        var nullRod = GameCard.Create("Null Rod", "Artifact");
        // Sol Ring is an artifact with a mana ability
        var solRing = new GameCard
        {
            Id = Guid.NewGuid(),
            Name = "Sol Ring",
            CardTypes = CardType.Artifact,
            ManaAbility = ManaAbility.Fixed(ManaColor.Colorless, 2),
            BaseManaAbility = ManaAbility.Fixed(ManaColor.Colorless, 2),
        };

        p1.Battlefield.Add(nullRod);
        p1.Battlefield.Add(solRing);
        engine.RecalculateState();

        var tapAction = GameAction.TapCard(p1.Id, solRing.Id);
        await engine.ExecuteAction(tapAction, CancellationToken.None);

        // Sol Ring should NOT produce mana
        state.GameLog.Should().Contain(l => l.Contains("activated abilities can't be activated"));
        solRing.IsTapped.Should().BeFalse(); // Should not have been tapped
        p1.ManaPool.Total.Should().Be(0);
    }

    [Fact]
    public async Task NullRod_Does_Not_Block_NonArtifact_Abilities()
    {
        var (engine, state, p1, _) = Setup();

        var nullRod = GameCard.Create("Null Rod", "Artifact");
        // A land is not an artifact, so its mana ability should work
        var forest = GameCard.Create("Forest", "Basic Land — Forest");

        p1.Battlefield.Add(nullRod);
        p1.Battlefield.Add(forest);
        engine.RecalculateState();

        var tapAction = GameAction.TapCard(p1.Id, forest.Id);
        await engine.ExecuteAction(tapAction, CancellationToken.None);

        forest.IsTapped.Should().BeTrue();
        p1.ManaPool.Total.Should().Be(1);
    }

    // ==================== Cursed Totem Tests ====================

    [Fact]
    public async Task CursedTotem_Blocks_Creature_Activated_Abilities()
    {
        var (engine, state, p1, _) = Setup();

        var totem = GameCard.Create("Cursed Totem", "Artifact");
        var creature = new GameCard
        {
            Id = Guid.NewGuid(),
            Name = "Test Creature",
            CardTypes = CardType.Creature, BasePower = 1, BaseToughness = 1,
        };

        p1.Battlefield.Add(totem);
        p1.Battlefield.Add(creature);
        engine.RecalculateState();

        var action = GameAction.ActivateAbility(p1.Id, creature.Id);
        await engine.ExecuteAction(action, CancellationToken.None);

        state.GameLog.Should().Contain(l => l.Contains("activated abilities can't be activated"));
    }

    [Fact]
    public async Task CursedTotem_Does_Not_Block_NonCreature_Abilities()
    {
        var (engine, state, p1, _) = Setup();

        var totem = GameCard.Create("Cursed Totem", "Artifact");
        // A land is not a creature, so its mana ability should work
        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");

        p1.Battlefield.Add(totem);
        p1.Battlefield.Add(mountain);
        engine.RecalculateState();

        var tapAction = GameAction.TapCard(p1.Id, mountain.Id);
        await engine.ExecuteAction(tapAction, CancellationToken.None);

        mountain.IsTapped.Should().BeTrue();
        p1.ManaPool.Total.Should().Be(1);
    }

    [Fact]
    public async Task CursedTotem_Blocks_Creature_Mana_Abilities()
    {
        var (engine, state, p1, _) = Setup();

        var totem = GameCard.Create("Cursed Totem", "Artifact");
        // A creature that taps for mana (like Birds of Paradise)
        var manaCreature = new GameCard
        {
            Id = Guid.NewGuid(),
            Name = "Birds of Paradise",
            CardTypes = CardType.Creature, BasePower = 0, BaseToughness = 1,
            ManaAbility = ManaAbility.Choice([ManaColor.White, ManaColor.Blue, ManaColor.Black, ManaColor.Red, ManaColor.Green]),
            BaseManaAbility = ManaAbility.Choice([ManaColor.White, ManaColor.Blue, ManaColor.Black, ManaColor.Red, ManaColor.Green]),
            TurnEnteredBattlefield = 0, // No summoning sickness
        };

        p1.Battlefield.Add(totem);
        p1.Battlefield.Add(manaCreature);
        state.TurnNumber = 2; // Ensure no summoning sickness
        engine.RecalculateState();

        var tapAction = GameAction.TapCard(p1.Id, manaCreature.Id);
        await engine.ExecuteAction(tapAction, CancellationToken.None);

        state.GameLog.Should().Contain(l => l.Contains("activated abilities can't be activated"));
        manaCreature.IsTapped.Should().BeFalse();
        p1.ManaPool.Total.Should().Be(0);
    }

    // ==================== Card Registration Tests ====================

    [Fact]
    public void All_Eight_Cards_Are_Registered_In_CardDefinitions()
    {
        var cardNames = new[]
        {
            "Crusade", "Absolute Law", "Worship", "Sphere of Resistance",
            "Chill", "Gloom", "Null Rod", "Cursed Totem",
        };

        foreach (var name in cardNames)
        {
            CardDefinitions.TryGet(name, out var def).Should().BeTrue($"{name} should be registered");
            def.Should().NotBeNull();
        }
    }

    [Fact]
    public void Crusade_Has_Correct_ManaCost()
    {
        CardDefinitions.TryGet("Crusade", out var def).Should().BeTrue();
        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ToString().Should().Be("{W}{W}");
    }

    [Fact]
    public void SphereOfResistance_Is_An_Artifact()
    {
        CardDefinitions.TryGet("Sphere of Resistance", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Artifact);
    }

    [Fact]
    public void NullRod_Is_An_Artifact()
    {
        CardDefinitions.TryGet("Null Rod", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Artifact);
    }

    [Fact]
    public void CursedTotem_Is_An_Artifact()
    {
        CardDefinitions.TryGet("Cursed Totem", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Artifact);
    }

    // ==================== Interaction Tests ====================

    [Fact]
    public void SphereOfResistance_Stacks_With_Chill_For_Red_Spells()
    {
        var (engine, state, p1, _) = Setup();

        var sphere = GameCard.Create("Sphere of Resistance", "Artifact");
        var chill = GameCard.Create("Chill", "Enchantment");
        p1.Battlefield.Add(sphere);
        p1.Battlefield.Add(chill);
        engine.RecalculateState();

        var redSpell = new GameCard
        {
            Name = "Lightning Bolt", ManaCost = ManaCost.Parse("{R}"),
            CardTypes = CardType.Instant,
        };

        // Sphere adds +1, Chill adds +2 = +3 total for red spells
        engine.ComputeCostModification(redSpell, p1).Should().Be(3);
    }

    [Fact]
    public void SphereOfResistance_Only_Adds_1_For_NonRed_Spells_With_Chill()
    {
        var (engine, state, p1, _) = Setup();

        var sphere = GameCard.Create("Sphere of Resistance", "Artifact");
        var chill = GameCard.Create("Chill", "Enchantment");
        p1.Battlefield.Add(sphere);
        p1.Battlefield.Add(chill);
        engine.RecalculateState();

        var blueSpell = new GameCard
        {
            Name = "Counterspell", ManaCost = ManaCost.Parse("{U}{U}"),
            CardTypes = CardType.Instant,
        };

        // Sphere adds +1, Chill doesn't apply = +1
        engine.ComputeCostModification(blueSpell, p1).Should().Be(1);
    }

    [Fact]
    public void Gloom_Stacks_With_SphereOfResistance_For_White_Spells()
    {
        var (engine, state, p1, _) = Setup();

        var sphere = GameCard.Create("Sphere of Resistance", "Artifact");
        var gloom = GameCard.Create("Gloom", "Enchantment");
        p1.Battlefield.Add(sphere);
        p1.Battlefield.Add(gloom);
        engine.RecalculateState();

        var whiteSpell = new GameCard
        {
            Name = "Swords to Plowshares", ManaCost = ManaCost.Parse("{W}"),
            CardTypes = CardType.Instant,
        };

        // Sphere adds +1, Gloom adds +3 = +4 total for white spells
        engine.ComputeCostModification(whiteSpell, p1).Should().Be(4);
    }

    [Fact]
    public void Crusade_Does_Not_Buff_Colorless_Creatures()
    {
        var (engine, state, p1, _) = Setup();

        var crusade = GameCard.Create("Crusade", "Enchantment");
        var colorless = new GameCard
        {
            Name = "Myr Token", BasePower = 1, BaseToughness = 1,
            CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{2}"),
        };

        p1.Battlefield.Add(crusade);
        p1.Battlefield.Add(colorless);

        engine.RecalculateState();

        colorless.Power.Should().Be(1);
        colorless.Toughness.Should().Be(1);
    }
}
