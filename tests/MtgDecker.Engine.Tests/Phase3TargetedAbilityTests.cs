using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class Phase3TargetedAbilityTests
{
    // === Withered Wretch ===

    [Fact]
    public void WitheredWretch_HasExileGraveyardAbility()
    {
        CardDefinitions.TryGet("Withered Wretch", out var def);

        def!.ActivatedAbility.Should().NotBeNull();
        def.ActivatedAbility!.Cost.ManaCost.Should().NotBeNull();
        def.ActivatedAbility.Cost.ManaCost!.GenericCost.Should().Be(1);
        def.ActivatedAbility.Effect.Should().BeOfType<ExileFromAnyGraveyardEffect>();
    }

    [Fact]
    public async Task ExileFromOpponentGraveyard_ExilesChosenCard()
    {
        var h1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var target = new GameCard { Name = "Bolt in GY" };
        p2.Graveyard.Add(target);
        p2.Graveyard.Add(new GameCard { Name = "Other Card" });

        h1.EnqueueCardChoice(target.Id);

        var wretch = new GameCard { Name = "Withered Wretch" };
        var context = new EffectContext(state, p1, wretch, h1);

        var effect = new ExileFromOpponentGraveyardEffect();
        await effect.Execute(context);

        p2.Graveyard.Cards.Should().NotContain(c => c.Name == "Bolt in GY");
        p2.Exile.Cards.Should().Contain(c => c.Name == "Bolt in GY");
        p2.Graveyard.Count.Should().Be(1, "other card stays");
    }

    [Fact]
    public async Task ExileFromOpponentGraveyard_EmptyGraveyard_DoesNothing()
    {
        var h1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var wretch = new GameCard { Name = "Withered Wretch" };
        var context = new EffectContext(state, p1, wretch, h1);

        var effect = new ExileFromOpponentGraveyardEffect();
        await effect.Execute(context); // should not throw

        p2.Exile.Count.Should().Be(0);
    }

    // === Dust Bowl ===

    [Fact]
    public void DustBowl_HasDestroyNonbasicLandAbility()
    {
        CardDefinitions.TryGet("Dust Bowl", out var def);

        def!.ActivatedAbility.Should().NotBeNull();
        def.ActivatedAbility!.Cost.TapSelf.Should().BeTrue();
        def.ActivatedAbility.Cost.SacrificeSelf.Should().BeFalse("Dust Bowl sacrifices a land, not itself");
        def.ActivatedAbility.Cost.SacrificeCardType.Should().Be(CardType.Land);
        def.ActivatedAbility.Cost.ManaCost.Should().NotBeNull();
        def.ActivatedAbility.Cost.ManaCost!.GenericCost.Should().Be(3);
        def.ActivatedAbility.Effect.Should().BeOfType<DestroyTargetEffect>();
        def.ActivatedAbility.TargetFilter.Should().NotBeNull();
    }

    [Fact]
    public void DustBowl_TargetFilter_MatchesNonbasicLands()
    {
        CardDefinitions.TryGet("Dust Bowl", out var def);
        var filter = def!.ActivatedAbility!.TargetFilter!;

        var nonbasic = new GameCard { Name = "Rishadan Port", CardTypes = CardType.Land };
        var basic = new GameCard { Name = "Mountain", CardTypes = CardType.Land };
        var creature = new GameCard { Name = "Goblin", CardTypes = CardType.Creature };

        filter(nonbasic).Should().BeTrue("nonbasic land should be valid target");
        filter(basic).Should().BeFalse("basic land should not be valid target");
        filter(creature).Should().BeFalse("non-land should not be valid target");
    }

    [Fact]
    public void DustBowl_TargetFilter_RejectsAllFiveBasicLands()
    {
        CardDefinitions.TryGet("Dust Bowl", out var def);
        var filter = def!.ActivatedAbility!.TargetFilter!;

        foreach (var name in new[] { "Plains", "Island", "Swamp", "Mountain", "Forest" })
        {
            var land = new GameCard { Name = name, CardTypes = CardType.Land };
            filter(land).Should().BeFalse($"{name} is a basic land");
        }
    }

    [Fact]
    public void DustBowl_StillHasColorlessManaAbility()
    {
        CardDefinitions.TryGet("Dust Bowl", out var def);

        def!.ManaAbility.Should().NotBeNull("Dust Bowl also taps for colorless");
        def.ManaAbility!.FixedColor.Should().Be(ManaColor.Colorless);
    }
}
