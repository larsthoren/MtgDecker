using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests;

public class BloodMoonTests
{
    [Fact]
    public void BloodMoon_NonbasicLandBecomesRed()
    {
        var (state, _, _) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);

        // Add a nonbasic land
        var ancientTomb = new GameCard
        {
            Name = "Ancient Tomb",
            CardTypes = CardType.Land,
            BaseManaAbility = ManaAbility.FixedMultiple(ManaColor.Colorless, 2, selfDamage: 2),
            ManaAbility = ManaAbility.FixedMultiple(ManaColor.Colorless, 2, selfDamage: 2),
        };
        state.Player1.Battlefield.Add(ancientTomb);

        // Add Blood Moon
        var bloodMoon = new GameCard
        {
            Name = "Blood Moon",
            CardTypes = CardType.Enchantment,
        };
        CardDefinitions.TryGet("Blood Moon", out var def);
        state.Player1.Battlefield.Add(bloodMoon);

        // Apply Blood Moon's continuous effects
        foreach (var ce in def!.ContinuousEffects)
        {
            state.ActiveEffects.Add(ce with { SourceId = bloodMoon.Id });
        }

        engine.RecalculateState();

        // Ancient Tomb should now produce Red mana only
        ancientTomb.ManaAbility.Should().NotBeNull();
        ancientTomb.ManaAbility!.FixedColor.Should().Be(ManaColor.Red);
    }

    [Fact]
    public void BloodMoon_BasicLandUnaffected()
    {
        var (state, _, _) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);

        var island = GameCard.Create("Island", "Basic Land \u2014 Island");
        island.BaseManaAbility = ManaAbility.Fixed(ManaColor.Blue);
        island.ManaAbility = ManaAbility.Fixed(ManaColor.Blue);
        state.Player1.Battlefield.Add(island);

        var bloodMoon = new GameCard
        {
            Name = "Blood Moon",
            CardTypes = CardType.Enchantment,
        };
        CardDefinitions.TryGet("Blood Moon", out var def);
        state.Player1.Battlefield.Add(bloodMoon);

        foreach (var ce in def!.ContinuousEffects)
        {
            state.ActiveEffects.Add(ce with { SourceId = bloodMoon.Id });
        }

        engine.RecalculateState();

        // Basic Island should still produce Blue
        island.ManaAbility.Should().NotBeNull();
        island.ManaAbility!.FixedColor.Should().Be(ManaColor.Blue);
    }

    [Fact]
    public void BloodMoon_MultipleNonbasicLands_AllBecomeRed()
    {
        var (state, _, _) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);

        var cityOfTraitors = new GameCard
        {
            Name = "City of Traitors",
            CardTypes = CardType.Land,
            BaseManaAbility = ManaAbility.FixedMultiple(ManaColor.Colorless, 2),
            ManaAbility = ManaAbility.FixedMultiple(ManaColor.Colorless, 2),
        };
        state.Player1.Battlefield.Add(cityOfTraitors);

        var volcanicIsland = new GameCard
        {
            Name = "Volcanic Island",
            CardTypes = CardType.Land,
            BaseManaAbility = ManaAbility.Choice(ManaColor.Blue, ManaColor.Red),
            ManaAbility = ManaAbility.Choice(ManaColor.Blue, ManaColor.Red),
        };
        state.Player1.Battlefield.Add(volcanicIsland);

        var bloodMoon = new GameCard
        {
            Name = "Blood Moon",
            CardTypes = CardType.Enchantment,
        };
        CardDefinitions.TryGet("Blood Moon", out var def);
        state.Player1.Battlefield.Add(bloodMoon);

        foreach (var ce in def!.ContinuousEffects)
        {
            state.ActiveEffects.Add(ce with { SourceId = bloodMoon.Id });
        }

        engine.RecalculateState();

        cityOfTraitors.ManaAbility!.FixedColor.Should().Be(ManaColor.Red);
        volcanicIsland.ManaAbility!.FixedColor.Should().Be(ManaColor.Red);
    }

    [Fact]
    public void BloodMoon_Removed_LandsRevertToOriginal()
    {
        var (state, _, _) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);

        var tomb = new GameCard
        {
            Name = "Ancient Tomb",
            CardTypes = CardType.Land,
            BaseManaAbility = ManaAbility.FixedMultiple(ManaColor.Colorless, 2, selfDamage: 2),
            ManaAbility = ManaAbility.FixedMultiple(ManaColor.Colorless, 2, selfDamage: 2),
        };
        state.Player1.Battlefield.Add(tomb);

        var bloodMoon = new GameCard
        {
            Name = "Blood Moon",
            CardTypes = CardType.Enchantment,
        };
        CardDefinitions.TryGet("Blood Moon", out var def);
        state.Player1.Battlefield.Add(bloodMoon);

        foreach (var ce in def!.ContinuousEffects)
        {
            state.ActiveEffects.Add(ce with { SourceId = bloodMoon.Id });
        }

        engine.RecalculateState();
        tomb.ManaAbility!.FixedColor.Should().Be(ManaColor.Red);

        // Remove Blood Moon from battlefield (simulating destruction/bounce)
        state.Player1.Battlefield.RemoveById(bloodMoon.Id);
        engine.RecalculateState();

        // Tomb should revert to colorless
        tomb.ManaAbility!.FixedColor.Should().Be(ManaColor.Colorless);
    }
}
