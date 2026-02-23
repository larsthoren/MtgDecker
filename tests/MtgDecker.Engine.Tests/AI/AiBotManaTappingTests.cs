using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.AI;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests.AI;

public class AiBotManaTappingTests
{
    [Fact]
    public void PlanTapSequence_OneManaSpell_TapsOneLand()
    {
        var mountain1 = CreateLand("Mountain");
        var mountain2 = CreateLand("Mountain");
        var mountain3 = CreateLand("Mountain");
        var untappedLands = new List<GameCard> { mountain1, mountain2, mountain3 };
        var cost = ManaCost.Parse("{R}");

        var tapIds = AiBotDecisionHandler.PlanTapSequence(untappedLands, cost);

        tapIds.Should().HaveCount(1);
    }

    [Fact]
    public void PlanTapSequence_TwoColorSpell_TapsCorrectCount()
    {
        var mountain = CreateLand("Mountain");
        var forest = CreateLand("Forest");
        var mountain2 = CreateLand("Mountain");
        var untappedLands = new List<GameCard> { mountain, forest, mountain2 };
        var cost = ManaCost.Parse("{1}{R}{G}");

        var tapIds = AiBotDecisionHandler.PlanTapSequence(untappedLands, cost);

        tapIds.Should().HaveCount(3);
    }

    [Fact]
    public void PlanTapSequence_PrefersFixedColorForColoredCost()
    {
        var karplusan = CreateLand("Karplusan Forest");
        var mountain = CreateLand("Mountain");
        var untappedLands = new List<GameCard> { karplusan, mountain };
        var cost = ManaCost.Parse("{R}");

        var tapIds = AiBotDecisionHandler.PlanTapSequence(untappedLands, cost);

        tapIds.Should().HaveCount(1);
        tapIds[0].Should().Be(mountain.Id);
    }

    [Fact]
    public void PlanTapSequence_ReturnsEmpty_WhenCantAfford()
    {
        var forest = CreateLand("Forest");
        var untappedLands = new List<GameCard> { forest };
        var cost = ManaCost.Parse("{R}{R}");

        var tapIds = AiBotDecisionHandler.PlanTapSequence(untappedLands, cost);

        tapIds.Should().BeEmpty();
    }

    [Fact]
    public void PlanTapSequence_GenericCost_UsesLeastFlexibleLands()
    {
        var mountain = CreateLand("Mountain");
        var karplusan = CreateLand("Karplusan Forest");
        var untappedLands = new List<GameCard> { karplusan, mountain };
        var cost = ManaCost.Parse("{1}");

        var tapIds = AiBotDecisionHandler.PlanTapSequence(untappedLands, cost);

        tapIds.Should().HaveCount(1);
        tapIds[0].Should().Be(mountain.Id);
    }

    private static GameCard CreateLand(string name)
    {
        var typeLine = name switch
        {
            "Mountain" => "Basic Land \u2014 Mountain",
            "Forest" => "Basic Land \u2014 Forest",
            "Island" => "Basic Land \u2014 Island",
            "Swamp" => "Basic Land \u2014 Swamp",
            "Plains" => "Basic Land \u2014 Plains",
            _ => "Land"
        };
        return GameCard.Create(name, typeLine, "", null, null, null);
    }
}
