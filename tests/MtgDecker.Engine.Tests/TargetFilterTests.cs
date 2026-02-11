using FluentAssertions;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class TargetFilterTests
{
    [Fact]
    public void CreatureFilter_MatchesCreatureOnBattlefield()
    {
        var filter = TargetFilter.Creature();
        var creature = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        filter.IsLegal(creature, ZoneType.Battlefield).Should().BeTrue();
    }

    [Fact]
    public void CreatureFilter_RejectsLand()
    {
        var filter = TargetFilter.Creature();
        var land = GameCard.Create("Forest", "Basic Land — Forest");
        filter.IsLegal(land, ZoneType.Battlefield).Should().BeFalse();
    }

    [Fact]
    public void CreatureFilter_RejectsCreatureNotOnBattlefield()
    {
        var filter = TargetFilter.Creature();
        var creature = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        filter.IsLegal(creature, ZoneType.Hand).Should().BeFalse();
    }

    [Fact]
    public void EnchantmentOrArtifactFilter_MatchesEnchantmentOnBattlefield()
    {
        var filter = TargetFilter.EnchantmentOrArtifact();
        var enchantment = GameCard.Create("Wild Growth", "Enchantment");
        filter.IsLegal(enchantment, ZoneType.Battlefield).Should().BeTrue();
    }

    [Fact]
    public void EnchantmentOrArtifactFilter_RejectsCreature()
    {
        var filter = TargetFilter.EnchantmentOrArtifact();
        var creature = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        filter.IsLegal(creature, ZoneType.Battlefield).Should().BeFalse();
    }
}
