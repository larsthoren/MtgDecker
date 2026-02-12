using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class TypeChangingTests
{
    // --- Task 12: EffectiveCardTypes property tests ---

    [Fact]
    public void EffectiveCardTypes_DefaultsToNull_IsCreatureUsesCardTypes()
    {
        var card = new GameCard
        {
            Name = "Grizzly Bears",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };

        card.EffectiveCardTypes.Should().BeNull();
        card.IsCreature.Should().BeTrue();
        card.IsLand.Should().BeFalse();
    }

    [Fact]
    public void EffectiveCardTypes_CanAddCreatureType()
    {
        var card = new GameCard
        {
            Name = "Enchantress's Presence",
            CardTypes = CardType.Enchantment,
            ManaCost = ManaCost.Parse("{2}{G}"),
        };

        card.IsCreature.Should().BeFalse("enchantment is not a creature by default");

        card.EffectiveCardTypes = card.CardTypes | CardType.Creature;

        card.IsCreature.Should().BeTrue("EffectiveCardTypes now includes Creature");
        card.EffectiveCardTypes.Should().HaveFlag(CardType.Enchantment);
        card.EffectiveCardTypes.Should().HaveFlag(CardType.Creature);
    }

    [Fact]
    public void IsCreature_UsesEffectiveCardTypes_WhenSet()
    {
        var card = new GameCard
        {
            Name = "Test Enchantment",
            CardTypes = CardType.Enchantment,
        };

        card.IsCreature.Should().BeFalse();

        card.EffectiveCardTypes = CardType.Enchantment | CardType.Creature;
        card.IsCreature.Should().BeTrue();
    }

    [Fact]
    public void IsLand_UsesEffectiveCardTypes_WhenSet()
    {
        var card = new GameCard
        {
            Name = "Test Artifact",
            CardTypes = CardType.Artifact,
        };

        card.IsLand.Should().BeFalse();

        card.EffectiveCardTypes = CardType.Artifact | CardType.Land;
        card.IsLand.Should().BeTrue();
    }

    [Fact]
    public void SettingEffectiveCardTypes_ToNull_ResetsToBaseCardTypes()
    {
        var card = new GameCard
        {
            Name = "Test Enchantment",
            CardTypes = CardType.Enchantment,
        };

        card.EffectiveCardTypes = CardType.Enchantment | CardType.Creature;
        card.IsCreature.Should().BeTrue();

        card.EffectiveCardTypes = null;
        card.IsCreature.Should().BeFalse("base CardTypes does not include Creature");
    }

    // --- Task 13: BecomeCreature continuous effect tests ---

    [Fact]
    public void BecomeCreature_MakesEnchantmentACreature_WithPTEqualToCMC()
    {
        var handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", handler),
            new Player(Guid.NewGuid(), "P2", handler));

        var engine = new GameEngine(state);

        // Use real Opalescence (has BecomeCreature effect in CardDefinitions)
        var opalescence = GameCard.Create("Opalescence", "Enchantment");
        state.Player1.Battlefield.Add(opalescence);

        // Place an enchantment that should become a creature
        var enchantment = GameCard.Create("Enchantress's Presence", "Enchantment");
        state.Player1.Battlefield.Add(enchantment);

        engine.RecalculateState();

        enchantment.IsCreature.Should().BeTrue("BecomeCreature adds Creature type");
        enchantment.EffectiveCardTypes.Should().HaveFlag(CardType.Enchantment);
        enchantment.EffectiveCardTypes.Should().HaveFlag(CardType.Creature);
        enchantment.Power.Should().Be(3, "CMC of {2}{G} is 3");
        enchantment.Toughness.Should().Be(3);
    }

    [Fact]
    public void BecomeCreature_ExcludesAuras()
    {
        var handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", handler),
            new Player(Guid.NewGuid(), "P2", handler));

        var engine = new GameEngine(state);

        // Use real Opalescence
        var opalescence = GameCard.Create("Opalescence", "Enchantment");
        state.Player1.Battlefield.Add(opalescence);

        // Place a land for Wild Growth to attach to
        var forest = GameCard.Create("Forest", "Basic Land - Forest");
        state.Player1.Battlefield.Add(forest);

        // Aura should NOT be affected
        var aura = GameCard.Create("Wild Growth", "Enchantment - Aura");
        aura.AttachedTo = forest.Id;
        state.Player1.Battlefield.Add(aura);

        // Non-aura enchantment should be affected
        var enchantment = GameCard.Create("Enchantress's Presence", "Enchantment");
        state.Player1.Battlefield.Add(enchantment);

        engine.RecalculateState();

        aura.IsCreature.Should().BeFalse("auras are excluded from BecomeCreature");
        enchantment.IsCreature.Should().BeTrue("non-aura enchantments become creatures");
    }

    [Fact]
    public void BecomeCreature_AppliesBeforeLordPTEffects()
    {
        var handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", handler),
            new Player(Guid.NewGuid(), "P2", handler));

        var engine = new GameEngine(state);

        // Opalescence provides BecomeCreature effect (registered in CardDefinitions)
        var opalescence = GameCard.Create("Opalescence", "Enchantment");
        state.Player1.Battlefield.Add(opalescence);

        // Goblin King provides +1/+1 to Goblin creatures (registered in CardDefinitions)
        var king = GameCard.Create("Goblin King", "Creature - Goblin");
        state.Player1.Battlefield.Add(king);

        // Custom enchantment with Goblin subtype: becomes creature via Opalescence,
        // then gets +1/+1 from Goblin King (layer ordering test)
        var enchantment = new GameCard
        {
            Name = "Goblin Enchantment",
            CardTypes = CardType.Enchantment,
            ManaCost = ManaCost.Parse("{2}{G}"), // CMC = 3
            Subtypes = ["Goblin"],
        };
        state.Player1.Battlefield.Add(enchantment);

        engine.RecalculateState();

        // Layer 1: BecomeCreature makes it a 3/3 creature (CMC = 3)
        // Layer 2: Goblin King gives +1/+1 to Goblin creatures
        // Result: 4/4
        enchantment.IsCreature.Should().BeTrue("Opalescence makes enchantments into creatures");
        enchantment.Power.Should().Be(4, "3 (CMC) + 1 (Goblin King) = 4");
        enchantment.Toughness.Should().Be(4, "3 (CMC) + 1 (Goblin King) = 4");
    }
}
