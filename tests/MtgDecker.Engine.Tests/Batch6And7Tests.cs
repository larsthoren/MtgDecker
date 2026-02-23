using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

/// <summary>
/// Tests for Batch 6 (unimplemented card shells) and Batch 7 (land fixes).
/// </summary>
public class Batch6And7Tests
{
    private (GameState state, Player p1, Player p2, TestDecisionHandler h1, TestDecisionHandler h2) CreateState()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);

        for (int i = 0; i < 40; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}" });
            p2.Library.Add(new GameCard { Name = $"Card{i}" });
        }

        var state = new GameState(p1, p2);
        return (state, p1, p2, h1, h2);
    }

    // ===================== Batch 6a: Funeral Pyre =====================

    [Fact]
    public void FuneralPyre_HasEffect_InCardDefinitions()
    {
        CardDefinitions.TryGet("Funeral Pyre", out var def).Should().BeTrue();
        def!.Effect.Should().NotBeNull();
        def.Effect.Should().BeOfType<FuneralPyreEffect>();
    }

    [Fact]
    public async Task FuneralPyre_ExilesCardAndCreatesToken()
    {
        var (state, p1, p2, h1, _) = CreateState();

        var graveyardCard = new GameCard { Name = "Goblin Lackey" };
        p2.Graveyard.Add(graveyardCard);

        h1.EnqueueCardChoice(graveyardCard.Id);

        var spellCard = new GameCard { Name = "Funeral Pyre" };
        var spell = new StackObject(spellCard, p1.Id, new(), new(), 0);
        var effect = new FuneralPyreEffect();

        await effect.ResolveAsync(state, spell, h1);

        // Card should be exiled from P2's graveyard
        p2.Graveyard.Cards.Should().NotContain(c => c.Name == "Goblin Lackey");
        p2.Exile.Cards.Should().Contain(c => c.Name == "Goblin Lackey");

        // P2 (the card's owner) should get a 1/1 Spirit token
        p2.Battlefield.Cards.Should().Contain(c => c.Name == "Spirit" && c.IsToken);
        var token = p2.Battlefield.Cards.First(c => c.Name == "Spirit");
        token.BasePower.Should().Be(1);
        token.BaseToughness.Should().Be(1);
        token.Subtypes.Should().Contain("Spirit");
    }

    [Fact]
    public async Task FuneralPyre_EmptyGraveyard_DoesNothing()
    {
        var (state, p1, p2, h1, _) = CreateState();

        var spellCard = new GameCard { Name = "Funeral Pyre" };
        var spell = new StackObject(spellCard, p1.Id, new(), new(), 0);
        var effect = new FuneralPyreEffect();

        await effect.ResolveAsync(state, spell, h1);

        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Spirit");
        p2.Battlefield.Cards.Should().NotContain(c => c.Name == "Spirit");
    }

    [Fact]
    public async Task FuneralPyre_OwnerGetsToken_NotCaster()
    {
        var (state, p1, p2, h1, _) = CreateState();

        // Card is in P1's graveyard, P1 is also the caster
        var graveyardCard = new GameCard { Name = "Some Card" };
        p1.Graveyard.Add(graveyardCard);

        h1.EnqueueCardChoice(graveyardCard.Id);

        var spellCard = new GameCard { Name = "Funeral Pyre" };
        var spell = new StackObject(spellCard, p1.Id, new(), new(), 0);
        var effect = new FuneralPyreEffect();

        await effect.ResolveAsync(state, spell, h1);

        // P1 owns the exiled card, so P1 gets the token
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Spirit" && c.IsToken);
    }

    // ===================== Batch 6b: Surgical Extraction =====================

    [Fact]
    public void SurgicalExtraction_HasEffect_InCardDefinitions()
    {
        CardDefinitions.TryGet("Surgical Extraction", out var def).Should().BeTrue();
        def!.Effect.Should().NotBeNull();
        def.Effect.Should().BeOfType<SurgicalExtractionEffect>();
    }

    [Fact]
    public async Task SurgicalExtraction_ExilesAllCopies()
    {
        var (state, p1, p2, h1, _) = CreateState();

        // Put target card in graveyard
        var grave1 = new GameCard { Name = "Lightning Bolt" };
        p2.Graveyard.Add(grave1);

        // Put copies in other zones
        var grave2 = new GameCard { Name = "Lightning Bolt" };
        p2.Graveyard.Add(grave2);
        var hand1 = new GameCard { Name = "Lightning Bolt" };
        p2.Hand.Add(hand1);
        var lib1 = new GameCard { Name = "Lightning Bolt" };
        p2.Library.Add(lib1);

        h1.EnqueueCardChoice(grave1.Id);

        var spellCard = new GameCard { Name = "Surgical Extraction" };
        var spell = new StackObject(spellCard, p1.Id, new(), new(), 0);
        var effect = new SurgicalExtractionEffect();

        await effect.ResolveAsync(state, spell, h1);

        // All copies should be exiled
        p2.Graveyard.Cards.Should().NotContain(c => c.Name == "Lightning Bolt");
        p2.Hand.Cards.Should().NotContain(c => c.Name == "Lightning Bolt");
        p2.Library.Cards.Should().NotContain(c => c.Name == "Lightning Bolt");
        p2.Exile.Cards.Where(c => c.Name == "Lightning Bolt").Should().HaveCount(4);
    }

    [Fact]
    public async Task SurgicalExtraction_SkipsBasicLands()
    {
        var (state, p1, p2, h1, _) = CreateState();

        var basicLand = GameCard.Create("Mountain", "Basic Land — Mountain");
        p2.Graveyard.Add(basicLand);

        // No non-basic-land cards in graveyard, so nothing should happen
        var spellCard = new GameCard { Name = "Surgical Extraction" };
        var spell = new StackObject(spellCard, p1.Id, new(), new(), 0);
        var effect = new SurgicalExtractionEffect();

        await effect.ResolveAsync(state, spell, h1);

        // Basic land should still be in graveyard (not a valid target)
        p2.Graveyard.Cards.Should().Contain(c => c.Name == "Mountain");
    }

    // ===================== Batch 6c: Grafdigger's Cage =====================

    [Fact]
    public void GrafdiggersCage_HasPreventCastFromGraveyard()
    {
        CardDefinitions.TryGet("Grafdigger's Cage", out var def).Should().BeTrue();
        def!.ContinuousEffects.Should().NotBeNull();
        def.ContinuousEffects!.Should().ContainSingle(e =>
            e.Type == ContinuousEffectType.PreventCastFromGraveyard);
    }

    [Fact]
    public void ContinuousEffectType_HasPreventCastFromGraveyard()
    {
        Enum.IsDefined(typeof(ContinuousEffectType), ContinuousEffectType.PreventCastFromGraveyard)
            .Should().BeTrue();
    }

    // ===================== Batch 6d: Powder Keg =====================

    [Fact]
    public void PowderKeg_HasUpkeepTriggerAndActivatedAbility()
    {
        CardDefinitions.TryGet("Powder Keg", out var def).Should().BeTrue();
        def!.Triggers.Should().ContainSingle(t => t.Event == GameEvent.Upkeep);
        def.ActivatedAbilities.Should().NotBeEmpty();
        def.ActivatedAbilities[0].Cost.TapSelf.Should().BeTrue();
        def.ActivatedAbilities[0].Cost.SacrificeSelf.Should().BeTrue();
    }

    [Fact]
    public async Task PowderKeg_UpkeepEffect_AddsFuseCounter()
    {
        var (state, p1, _, h1, _) = CreateState();

        var powderKeg = new GameCard { Name = "Powder Keg" };
        p1.Battlefield.Add(powderKeg);

        // Choose to add a fuse counter
        h1.EnqueueCardChoice(powderKeg.Id);

        var context = new EffectContext(state, p1, powderKeg, h1);
        var effect = new PowderKegUpkeepEffect();
        await effect.Execute(context);

        powderKeg.GetCounters(CounterType.Fuse).Should().Be(1);
    }

    [Fact]
    public async Task PowderKeg_UpkeepEffect_DeclineAddingCounter()
    {
        var (state, p1, _, h1, _) = CreateState();

        var powderKeg = new GameCard { Name = "Powder Keg" };
        p1.Battlefield.Add(powderKeg);

        // Decline adding a fuse counter (optional=true, so null means decline)
        h1.EnqueueCardChoice(null);

        var context = new EffectContext(state, p1, powderKeg, h1);
        var effect = new PowderKegUpkeepEffect();
        await effect.Execute(context);

        powderKeg.GetCounters(CounterType.Fuse).Should().Be(0);
    }

    [Fact]
    public async Task PowderKeg_DestroyEffect_DestroysMatchingCMC()
    {
        var (state, p1, p2, h1, _) = CreateState();

        var powderKeg = new GameCard { Name = "Powder Keg" };
        powderKeg.AddCounters(CounterType.Fuse, 2);
        p1.Battlefield.Add(powderKeg);

        // Creature with CMC 2
        var creature = new GameCard
        {
            Name = "Bear",
            CardTypes = CardType.Creature,
            ManaCost = ManaCost.Parse("{1}{G}"),
        };
        p2.Battlefield.Add(creature);

        // Artifact with CMC 2
        var artifact = new GameCard
        {
            Name = "Some Artifact",
            CardTypes = CardType.Artifact,
            ManaCost = ManaCost.Parse("{2}"),
        };
        p1.Battlefield.Add(artifact);

        // Creature with CMC 3 (should survive)
        var bigCreature = new GameCard
        {
            Name = "Big Creature",
            CardTypes = CardType.Creature,
            ManaCost = ManaCost.Parse("{2}{G}"),
        };
        p2.Battlefield.Add(bigCreature);

        var context = new EffectContext(state, p1, powderKeg, h1);
        var effect = new PowderKegDestroyEffect();
        await effect.Execute(context);

        // CMC 2 creatures and artifacts should be destroyed
        p2.Battlefield.Cards.Should().NotContain(c => c.Name == "Bear");
        p2.Graveyard.Cards.Should().Contain(c => c.Name == "Bear");
        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Some Artifact");
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Some Artifact");

        // CMC 3 creature should survive
        p2.Battlefield.Cards.Should().Contain(c => c.Name == "Big Creature");
    }

    [Fact]
    public void CounterType_HasFuse()
    {
        Enum.IsDefined(typeof(CounterType), CounterType.Fuse).Should().BeTrue();
    }

    // ===================== Batch 6e: Phyrexian Furnace =====================

    [Fact]
    public void PhyrexianFurnace_HasActivatedAbility()
    {
        CardDefinitions.TryGet("Phyrexian Furnace", out var def).Should().BeTrue();
        def!.ActivatedAbilities.Should().NotBeEmpty();
        def.ActivatedAbilities[0].Cost.SacrificeSelf.Should().BeTrue();
        def.ActivatedAbilities[0].Cost.ManaCost.Should().NotBeNull();
    }

    [Fact]
    public async Task PhyrexianFurnace_ExilesCardAndDraws()
    {
        var (state, p1, p2, h1, _) = CreateState();

        var furnace = new GameCard { Name = "Phyrexian Furnace" };
        p1.Battlefield.Add(furnace);

        var graveyardCard = new GameCard { Name = "Dead Thing" };
        p2.Graveyard.Add(graveyardCard);

        h1.EnqueueCardChoice(graveyardCard.Id);

        var initialHandCount = p1.Hand.Count;

        var context = new EffectContext(state, p1, furnace, h1);
        var effect = new PhyrexianFurnaceEffect();
        await effect.Execute(context);

        // Card should be exiled
        p2.Graveyard.Cards.Should().NotContain(c => c.Name == "Dead Thing");
        p1.Exile.Cards.Should().Contain(c => c.Name == "Dead Thing");

        // Controller should draw a card
        p1.Hand.Count.Should().Be(initialHandCount + 1);
    }

    [Fact]
    public async Task PhyrexianFurnace_EmptyGraveyards_StillDraws()
    {
        var (state, p1, p2, h1, _) = CreateState();

        var furnace = new GameCard { Name = "Phyrexian Furnace" };
        p1.Battlefield.Add(furnace);

        var initialHandCount = p1.Hand.Count;
        var initialLibraryCount = p1.Library.Count;

        var context = new EffectContext(state, p1, furnace, h1);
        var effect = new PhyrexianFurnaceEffect();
        await effect.Execute(context);

        // Still draws a card even with empty graveyards
        p1.Hand.Count.Should().Be(initialHandCount + 1);
        p1.Library.Count.Should().Be(initialLibraryCount - 1);
    }

    // ===================== Batch 7: Undercity Sewers =====================

    [Fact]
    public void UndercitySewers_HasIslandSwampSubtypes()
    {
        CardDefinitions.TryGet("Undercity Sewers", out var def).Should().BeTrue();
        def!.Subtypes.Should().NotBeNull();
        def.Subtypes.Should().Contain("Island");
        def.Subtypes.Should().Contain("Swamp");
    }

    // ===================== Batch 7: Tainted Field =====================

    [Fact]
    public void TaintedField_HasColorlessManaOption()
    {
        CardDefinitions.TryGet("Tainted Field", out var def).Should().BeTrue();
        def!.ManaAbility.Should().NotBeNull();
        // Should produce Colorless, White, or Black
        def.ManaAbility!.ChoiceColors.Should().Contain(ManaColor.Colorless);
        def.ManaAbility.ChoiceColors.Should().Contain(ManaColor.White);
        def.ManaAbility.ChoiceColors.Should().Contain(ManaColor.Black);
    }

    // ===================== Batch 7: Darigaaz's Caldera =====================

    [Fact]
    public void DarigaazsCaldera_HasBounceLandETB()
    {
        CardDefinitions.TryGet("Darigaaz's Caldera", out var def).Should().BeTrue();
        def!.EntersTapped.Should().BeFalse("Lair lands don't enter tapped, they bounce a land");
        def.Triggers.Should().ContainSingle(t =>
            t.Event == GameEvent.EnterBattlefield && t.Condition == TriggerCondition.Self);
    }

    [Fact]
    public async Task BounceLandETB_ReturnsLandToHand()
    {
        var (state, p1, _, h1, _) = CreateState();

        var caldera = new GameCard { Name = "Darigaaz's Caldera", CardTypes = CardType.Land };
        p1.Battlefield.Add(caldera);

        var mountain = new GameCard { Name = "Mountain", CardTypes = CardType.Land };
        p1.Battlefield.Add(mountain);

        h1.EnqueueCardChoice(mountain.Id);

        var context = new EffectContext(state, p1, caldera, h1);
        var effect = new BounceLandETBEffect();
        await effect.Execute(context);

        // Mountain should be returned to hand
        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Mountain");
        p1.Hand.Cards.Should().Contain(c => c.Name == "Mountain");

        // Caldera should remain on battlefield
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Darigaaz's Caldera");
    }

    [Fact]
    public async Task BounceLandETB_NoOtherLands_SacrificesSelf()
    {
        var (state, p1, _, h1, _) = CreateState();

        var caldera = new GameCard { Name = "Darigaaz's Caldera", CardTypes = CardType.Land };
        p1.Battlefield.Add(caldera);

        // No other lands on battlefield

        var context = new EffectContext(state, p1, caldera, h1);
        var effect = new BounceLandETBEffect();
        await effect.Execute(context);

        // Should sacrifice itself
        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Darigaaz's Caldera");
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Darigaaz's Caldera");
    }

    // ===================== Batch 7: Treva's Ruins =====================

    [Fact]
    public void TrevasRuins_HasBounceLandETB()
    {
        CardDefinitions.TryGet("Treva's Ruins", out var def).Should().BeTrue();
        def!.EntersTapped.Should().BeFalse("Lair lands don't enter tapped, they bounce a land");
        def.Triggers.Should().ContainSingle(t =>
            t.Event == GameEvent.EnterBattlefield && t.Condition == TriggerCondition.Self);
    }
}
