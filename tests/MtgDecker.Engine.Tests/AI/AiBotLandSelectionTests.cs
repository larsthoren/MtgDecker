using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.AI;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests.AI;

public class AiBotLandSelectionTests
{
    [Fact]
    public void ChooseLandToPlay_PrefersBasicOverCityOfTraitors()
    {
        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain", "", null, null, null);
        var city = GameCard.Create("City of Traitors", "Land", "", null, null, null);
        var bolt = GameCard.Create("Lightning Bolt", "Instant", "", "{R}", null, null);

        var hand = new List<GameCard> { city, mountain, bolt };

        var result = AiBotDecisionHandler.ChooseLandToPlay(hand, neededColors: new HashSet<ManaColor> { ManaColor.Red });

        result.Should().NotBeNull();
        result!.Name.Should().Be("Mountain");
    }

    [Fact]
    public void ChooseLandToPlay_PrefersColorMatchingBasic()
    {
        var forest = GameCard.Create("Forest", "Basic Land — Forest", "", null, null, null);
        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain", "", null, null, null);
        var bolt = GameCard.Create("Lightning Bolt", "Instant", "", "{R}", null, null);

        var hand = new List<GameCard> { forest, mountain, bolt };

        var result = AiBotDecisionHandler.ChooseLandToPlay(hand, neededColors: new HashSet<ManaColor> { ManaColor.Red });

        result!.Name.Should().Be("Mountain");
    }

    [Fact]
    public void ChooseLandToPlay_PrefersDualOverNonMatchingBasic()
    {
        var forest = GameCard.Create("Forest", "Basic Land — Forest", "", null, null, null);
        var karplusan = GameCard.Create("Karplusan Forest", "Land", "", null, null, null);
        var bolt = GameCard.Create("Lightning Bolt", "Instant", "", "{R}", null, null);

        var hand = new List<GameCard> { forest, karplusan, bolt };

        // Karplusan Forest produces Red (needed) — Forest doesn't
        var result = AiBotDecisionHandler.ChooseLandToPlay(hand, neededColors: new HashSet<ManaColor> { ManaColor.Red });

        result!.Name.Should().Be("Karplusan Forest");
    }

    [Fact]
    public void ChooseLandToPlay_CityOfTraitors_OnlyWhenNoOtherLands()
    {
        var city = GameCard.Create("City of Traitors", "Land", "", null, null, null);
        var bolt = GameCard.Create("Lightning Bolt", "Instant", "", "{R}", null, null);

        var hand = new List<GameCard> { city, bolt };

        var result = AiBotDecisionHandler.ChooseLandToPlay(hand, neededColors: new HashSet<ManaColor> { ManaColor.Red });

        result!.Name.Should().Be("City of Traitors");
    }

    [Fact]
    public void ChooseLandToPlay_PrefersBasicOverAncientTomb()
    {
        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain", "", null, null, null);
        var tomb = GameCard.Create("Ancient Tomb", "Land", "", null, null, null);
        var bolt = GameCard.Create("Lightning Bolt", "Instant", "", "{R}", null, null);

        var hand = new List<GameCard> { tomb, mountain, bolt };

        var result = AiBotDecisionHandler.ChooseLandToPlay(hand, neededColors: new HashSet<ManaColor> { ManaColor.Red });

        result!.Name.Should().Be("Mountain");
    }

    [Fact]
    public void ChooseLandToPlay_ReturnsNullIfNoLands()
    {
        var bolt = GameCard.Create("Lightning Bolt", "Instant", "", "{R}", null, null);

        var hand = new List<GameCard> { bolt };

        var result = AiBotDecisionHandler.ChooseLandToPlay(hand, neededColors: new HashSet<ManaColor> { ManaColor.Red });

        result.Should().BeNull();
    }
}
