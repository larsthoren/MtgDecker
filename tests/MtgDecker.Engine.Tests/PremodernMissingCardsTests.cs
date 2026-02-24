using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using TriggerEffects = MtgDecker.Engine.Triggers.Effects;

// ReSharper disable InconsistentNaming

namespace MtgDecker.Engine.Tests;

public class PremodernMissingCardsTests
{
    private GameState CreateState()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        return new GameState(p1, p2);
    }

    // ─── Task 1: Lands + Vanilla Creatures ────────────────────────────────

    #region Yavimaya Coast

    [Fact]
    public void YavimayaCoast_IsRegistered()
    {
        CardDefinitions.TryGet("Yavimaya Coast", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Land);
    }

    [Fact]
    public void YavimayaCoast_HasPainChoiceManaAbility()
    {
        CardDefinitions.TryGet("Yavimaya Coast", out var def);

        def!.ManaAbility.Should().NotBeNull();
        def.ManaAbility!.Type.Should().Be(ManaAbilityType.Choice);
        def.ManaAbility.ChoiceColors.Should().BeEquivalentTo(
            new[] { ManaColor.Colorless, ManaColor.Green, ManaColor.Blue });
    }

    [Fact]
    public void YavimayaCoast_PainColors_AreGreenAndBlue()
    {
        CardDefinitions.TryGet("Yavimaya Coast", out var def);

        def!.ManaAbility!.PainColors.Should().BeEquivalentTo(
            new[] { ManaColor.Green, ManaColor.Blue });
    }

    [Fact]
    public void YavimayaCoast_NoManaCost()
    {
        CardDefinitions.TryGet("Yavimaya Coast", out var def);

        def!.ManaCost.Should().BeNull();
    }

    #endregion

    #region Savannah Lions

    [Fact]
    public void SavannahLions_IsRegistered()
    {
        CardDefinitions.TryGet("Savannah Lions", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Creature);
    }

    [Fact]
    public void SavannahLions_HasCorrectStats()
    {
        CardDefinitions.TryGet("Savannah Lions", out var def);

        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(1);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.White).WhoseValue.Should().Be(1);
        def.Power.Should().Be(2);
        def.Toughness.Should().Be(1);
    }

    [Fact]
    public void SavannahLions_HasCatSubtype()
    {
        CardDefinitions.TryGet("Savannah Lions", out var def);

        def!.Subtypes.Should().Contain("Cat");
    }

    [Fact]
    public void SavannahLions_IsVanilla()
    {
        CardDefinitions.TryGet("Savannah Lions", out var def);

        def!.Effect.Should().BeNull();
        def.TargetFilter.Should().BeNull();
        def.Triggers.Should().BeEmpty();
        def.ActivatedAbilities.Should().BeEmpty();
    }

    #endregion

    // ─── Task 2: Simple Removal Spells ────────────────────────────────────

    #region Red Elemental Blast

    [Fact]
    public void RedElementalBlast_IsRegistered()
    {
        CardDefinitions.TryGet("Red Elemental Blast", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Instant);
        def.ManaCost!.ConvertedManaCost.Should().Be(1);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Red);
    }

    [Fact]
    public void RedElementalBlast_UsesPyroblastEffect()
    {
        CardDefinitions.TryGet("Red Elemental Blast", out var def);
        def!.Effect.Should().BeOfType<PyroblastEffect>();
    }

    [Fact]
    public void RedElementalBlast_CountersBlueSpell()
    {
        var state = CreateState();
        var blueSpell = new GameCard
        {
            Name = "Counterspell",
            ManaCost = ManaCost.Parse("{U}{U}"),
            CardTypes = CardType.Instant,
        };
        var blueStackObj = new StackObject(blueSpell, state.Player2.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);
        state.StackPush(blueStackObj);

        var reb = GameCard.Create("Red Elemental Blast");
        var spell = new StackObject(reb, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(blueSpell.Id, state.Player2.Id, ZoneType.Stack) }, 1);

        new PyroblastEffect().Resolve(state, spell);

        state.Stack.OfType<StackObject>().Should().NotContain(so => so.Card.Id == blueSpell.Id);
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Id == blueSpell.Id);
    }

    #endregion

    #region Blue Elemental Blast

    [Fact]
    public void BlueElementalBlast_IsRegistered()
    {
        CardDefinitions.TryGet("Blue Elemental Blast", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Instant);
        def.ManaCost!.ConvertedManaCost.Should().Be(1);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Blue);
    }

    [Fact]
    public void BlueElementalBlast_UsesBlueElementalBlastEffect()
    {
        CardDefinitions.TryGet("Blue Elemental Blast", out var def);
        def!.Effect.Should().BeOfType<BlueElementalBlastEffect>();
    }

    [Fact]
    public void BlueElementalBlastEffect_CountersRedSpell()
    {
        var state = CreateState();
        var redSpell = new GameCard
        {
            Name = "Lightning Bolt",
            ManaCost = ManaCost.Parse("{R}"),
            CardTypes = CardType.Instant,
        };
        var redStackObj = new StackObject(redSpell, state.Player2.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);
        state.StackPush(redStackObj);

        var beb = GameCard.Create("Blue Elemental Blast");
        var spell = new StackObject(beb, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(redSpell.Id, state.Player2.Id, ZoneType.Stack) }, 1);

        new BlueElementalBlastEffect().Resolve(state, spell);

        state.Stack.OfType<StackObject>().Should().NotContain(so => so.Card.Id == redSpell.Id);
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Id == redSpell.Id);
    }

    [Fact]
    public void BlueElementalBlastEffect_DestroysRedPermanent()
    {
        var state = CreateState();
        var redCreature = new GameCard
        {
            Name = "Goblin Guide",
            ManaCost = ManaCost.Parse("{R}"),
            CardTypes = CardType.Creature,
        };
        state.Player2.Battlefield.Add(redCreature);

        var beb = GameCard.Create("Blue Elemental Blast");
        var spell = new StackObject(beb, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(redCreature.Id, state.Player2.Id, ZoneType.Battlefield) }, 0);

        new BlueElementalBlastEffect().Resolve(state, spell);

        state.Player2.Battlefield.Cards.Should().NotContain(c => c.Id == redCreature.Id);
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Id == redCreature.Id);
    }

    [Fact]
    public void BlueElementalBlastEffect_FizzlesOnNonRedSpell()
    {
        var state = CreateState();
        var greenSpell = new GameCard
        {
            Name = "Naturalize",
            ManaCost = ManaCost.Parse("{1}{G}"),
            CardTypes = CardType.Instant,
        };
        var greenStackObj = new StackObject(greenSpell, state.Player2.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);
        state.StackPush(greenStackObj);

        var beb = GameCard.Create("Blue Elemental Blast");
        var spell = new StackObject(beb, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(greenSpell.Id, state.Player2.Id, ZoneType.Stack) }, 1);

        new BlueElementalBlastEffect().Resolve(state, spell);

        // Green spell should still be on the stack
        state.Stack.OfType<StackObject>().Should().Contain(so => so.Card.Id == greenSpell.Id);
    }

    [Fact]
    public void BlueElementalBlastEffect_FizzlesOnNonRedPermanent()
    {
        var state = CreateState();
        var whiteCreature = new GameCard
        {
            Name = "Savannah Lions",
            ManaCost = ManaCost.Parse("{W}"),
            CardTypes = CardType.Creature,
        };
        state.Player2.Battlefield.Add(whiteCreature);

        var beb = GameCard.Create("Blue Elemental Blast");
        var spell = new StackObject(beb, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(whiteCreature.Id, state.Player2.Id, ZoneType.Battlefield) }, 0);

        new BlueElementalBlastEffect().Resolve(state, spell);

        // White creature should still be on the battlefield
        state.Player2.Battlefield.Cards.Should().Contain(c => c.Id == whiteCreature.Id);
    }

    #endregion

    #region Hydroblast

    [Fact]
    public void Hydroblast_IsRegistered()
    {
        CardDefinitions.TryGet("Hydroblast", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Instant);
        def.ManaCost!.ConvertedManaCost.Should().Be(1);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Blue);
    }

    [Fact]
    public void Hydroblast_UsesBlueElementalBlastEffect()
    {
        CardDefinitions.TryGet("Hydroblast", out var def);
        def!.Effect.Should().BeOfType<BlueElementalBlastEffect>();
    }

    #endregion

    #region Annul

    [Fact]
    public void Annul_IsRegistered()
    {
        CardDefinitions.TryGet("Annul", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Instant);
        def.ManaCost!.ConvertedManaCost.Should().Be(1);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Blue);
    }

    [Fact]
    public void Annul_UsesCounterSpellEffect()
    {
        CardDefinitions.TryGet("Annul", out var def);
        def!.Effect.Should().BeOfType<CounterSpellEffect>();
    }

    [Fact]
    public void Annul_TargetFilter_AcceptsArtifactSpell()
    {
        CardDefinitions.TryGet("Annul", out var def);
        var artifact = new GameCard { Name = "Cursed Scroll", CardTypes = CardType.Artifact };
        def!.TargetFilter!.IsLegal(artifact, ZoneType.Stack).Should().BeTrue();
    }

    [Fact]
    public void Annul_TargetFilter_AcceptsEnchantmentSpell()
    {
        CardDefinitions.TryGet("Annul", out var def);
        var enchantment = new GameCard { Name = "Wild Growth", CardTypes = CardType.Enchantment };
        def!.TargetFilter!.IsLegal(enchantment, ZoneType.Stack).Should().BeTrue();
    }

    [Fact]
    public void Annul_TargetFilter_RejectsCreatureSpell()
    {
        CardDefinitions.TryGet("Annul", out var def);
        var creature = new GameCard { Name = "Goblin Lackey", CardTypes = CardType.Creature };
        def!.TargetFilter!.IsLegal(creature, ZoneType.Stack).Should().BeFalse();
    }

    [Fact]
    public void Annul_TargetFilter_RejectsInstantSpell()
    {
        CardDefinitions.TryGet("Annul", out var def);
        var instant = new GameCard { Name = "Lightning Bolt", CardTypes = CardType.Instant };
        def!.TargetFilter!.IsLegal(instant, ZoneType.Stack).Should().BeFalse();
    }

    [Fact]
    public void Annul_CountersArtifactSpell()
    {
        var state = CreateState();
        var artifactSpell = new GameCard
        {
            Name = "Cursed Scroll",
            ManaCost = ManaCost.Parse("{1}"),
            CardTypes = CardType.Artifact,
        };
        var artifactStackObj = new StackObject(artifactSpell, state.Player2.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);
        state.StackPush(artifactStackObj);

        var annul = GameCard.Create("Annul");
        var spell = new StackObject(annul, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(artifactSpell.Id, state.Player2.Id, ZoneType.Stack) }, 1);

        new CounterSpellEffect().Resolve(state, spell);

        state.Stack.OfType<StackObject>().Should().NotContain(so => so.Card.Id == artifactSpell.Id);
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Id == artifactSpell.Id);
    }

    #endregion

    #region Erase

    [Fact]
    public void Erase_IsRegistered()
    {
        CardDefinitions.TryGet("Erase", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Instant);
        def.ManaCost!.ConvertedManaCost.Should().Be(1);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.White);
    }

    [Fact]
    public void Erase_UsesExileEnchantmentEffect()
    {
        CardDefinitions.TryGet("Erase", out var def);
        def!.Effect.Should().BeOfType<ExileEnchantmentEffect>();
    }

    [Fact]
    public void Erase_TargetFilter_AcceptsEnchantment()
    {
        CardDefinitions.TryGet("Erase", out var def);
        var enchantment = new GameCard { Name = "Sulfuric Vortex", CardTypes = CardType.Enchantment };
        def!.TargetFilter!.IsLegal(enchantment, ZoneType.Battlefield).Should().BeTrue();
    }

    [Fact]
    public void Erase_TargetFilter_RejectsCreature()
    {
        CardDefinitions.TryGet("Erase", out var def);
        var creature = new GameCard { Name = "Goblin Lackey", CardTypes = CardType.Creature };
        def!.TargetFilter!.IsLegal(creature, ZoneType.Battlefield).Should().BeFalse();
    }

    [Fact]
    public void ExileEnchantmentEffect_ExilesEnchantment()
    {
        var state = CreateState();
        var enchantment = new GameCard
        {
            Name = "Sulfuric Vortex",
            CardTypes = CardType.Enchantment,
        };
        state.Player2.Battlefield.Add(enchantment);

        var erase = GameCard.Create("Erase");
        var spell = new StackObject(erase, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(enchantment.Id, state.Player2.Id, ZoneType.Battlefield) }, 0);

        new ExileEnchantmentEffect().Resolve(state, spell);

        state.Player2.Battlefield.Cards.Should().NotContain(c => c.Id == enchantment.Id);
        state.Player2.Exile.Cards.Should().Contain(c => c.Id == enchantment.Id);
        state.Player2.Graveyard.Cards.Should().NotContain(c => c.Id == enchantment.Id);
    }

    [Fact]
    public void ExileEnchantmentEffect_TargetGone_NoEffect()
    {
        var state = CreateState();
        var enchantment = new GameCard { Name = "Wild Growth", CardTypes = CardType.Enchantment };
        // Don't add to battlefield

        var erase = GameCard.Create("Erase");
        var spell = new StackObject(erase, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(enchantment.Id, state.Player2.Id, ZoneType.Battlefield) }, 0);

        new ExileEnchantmentEffect().Resolve(state, spell);

        state.Player2.Exile.Cards.Should().BeEmpty();
    }

    #endregion

    #region Perish

    [Fact]
    public void Perish_IsRegistered()
    {
        CardDefinitions.TryGet("Perish", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Sorcery);
        def.ManaCost!.ConvertedManaCost.Should().Be(3);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Black);
    }

    [Fact]
    public void Perish_UsesDestroyAllByColorEffect_GreenCreature()
    {
        CardDefinitions.TryGet("Perish", out var def);
        def!.Effect.Should().BeOfType<DestroyAllByColorEffect>();
        var effect = (DestroyAllByColorEffect)def.Effect!;
        effect.Color.Should().Be(ManaColor.Green);
        effect.CardTypeFilter.Should().Be(CardType.Creature);
    }

    [Fact]
    public void DestroyAllByColorEffect_DestroysGreenCreatures()
    {
        var state = CreateState();
        var greenCreature = new GameCard
        {
            Name = "Llanowar Elves",
            ManaCost = ManaCost.Parse("{G}"),
            CardTypes = CardType.Creature,
        };
        var redCreature = new GameCard
        {
            Name = "Goblin Guide",
            ManaCost = ManaCost.Parse("{R}"),
            CardTypes = CardType.Creature,
        };
        var greenEnchantment = new GameCard
        {
            Name = "Exploration",
            ManaCost = ManaCost.Parse("{G}"),
            CardTypes = CardType.Enchantment,
        };
        state.Player1.Battlefield.Add(greenCreature);
        state.Player2.Battlefield.Add(redCreature);
        state.Player1.Battlefield.Add(greenEnchantment);

        var perish = GameCard.Create("Perish");
        var spell = new StackObject(perish, state.Player2.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        new DestroyAllByColorEffect(ManaColor.Green, CardType.Creature).Resolve(state, spell);

        // Green creature should be destroyed
        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Id == greenCreature.Id);
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Id == greenCreature.Id);
        // Red creature should survive
        state.Player2.Battlefield.Cards.Should().Contain(c => c.Id == redCreature.Id);
        // Green enchantment should survive (Perish only destroys creatures)
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == greenEnchantment.Id);
    }

    [Fact]
    public void DestroyAllByColorEffect_DestroysOnBothSides()
    {
        var state = CreateState();
        var p1Green = new GameCard
        {
            Name = "Llanowar Elves",
            ManaCost = ManaCost.Parse("{G}"),
            CardTypes = CardType.Creature,
        };
        var p2Green = new GameCard
        {
            Name = "Wall of Blossoms",
            ManaCost = ManaCost.Parse("{1}{G}"),
            CardTypes = CardType.Creature,
        };
        state.Player1.Battlefield.Add(p1Green);
        state.Player2.Battlefield.Add(p2Green);

        var perish = GameCard.Create("Perish");
        var spell = new StackObject(perish, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        new DestroyAllByColorEffect(ManaColor.Green, CardType.Creature).Resolve(state, spell);

        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Id == p1Green.Id);
        state.Player2.Battlefield.Cards.Should().NotContain(c => c.Id == p2Green.Id);
    }

    #endregion

    #region Anarchy

    [Fact]
    public void Anarchy_IsRegistered()
    {
        CardDefinitions.TryGet("Anarchy", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Sorcery);
        def.ManaCost!.ConvertedManaCost.Should().Be(4);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Red).WhoseValue.Should().Be(2);
        def.ManaCost.GenericCost.Should().Be(2);
    }

    [Fact]
    public void Anarchy_DestroysAllWhitePermanents()
    {
        var state = CreateState();
        var whiteCreature = new GameCard
        {
            Name = "Savannah Lions",
            ManaCost = ManaCost.Parse("{W}"),
            CardTypes = CardType.Creature,
        };
        var whiteEnchantment = new GameCard
        {
            Name = "Opalescence",
            ManaCost = ManaCost.Parse("{2}{W}{W}"),
            CardTypes = CardType.Enchantment,
        };
        var redCreature = new GameCard
        {
            Name = "Goblin Guide",
            ManaCost = ManaCost.Parse("{R}"),
            CardTypes = CardType.Creature,
        };
        state.Player2.Battlefield.Add(whiteCreature);
        state.Player2.Battlefield.Add(whiteEnchantment);
        state.Player2.Battlefield.Add(redCreature);

        var anarchy = GameCard.Create("Anarchy");
        var spell = new StackObject(anarchy, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        new DestroyAllByColorEffect(ManaColor.White).Resolve(state, spell);

        // White permanents should be destroyed
        state.Player2.Battlefield.Cards.Should().NotContain(c => c.Id == whiteCreature.Id);
        state.Player2.Battlefield.Cards.Should().NotContain(c => c.Id == whiteEnchantment.Id);
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Id == whiteCreature.Id);
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Id == whiteEnchantment.Id);
        // Red creature should survive
        state.Player2.Battlefield.Cards.Should().Contain(c => c.Id == redCreature.Id);
    }

    [Fact]
    public void Anarchy_UsesDestroyAllByColorEffect_White_NoTypeFilter()
    {
        CardDefinitions.TryGet("Anarchy", out var def);
        def!.Effect.Should().BeOfType<DestroyAllByColorEffect>();
        var effect = (DestroyAllByColorEffect)def.Effect!;
        effect.Color.Should().Be(ManaColor.White);
        effect.CardTypeFilter.Should().BeNull();
    }

    #endregion

    #region Simoon

    [Fact]
    public void Simoon_IsRegistered()
    {
        CardDefinitions.TryGet("Simoon", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Instant);
        def.ManaCost!.ConvertedManaCost.Should().Be(2);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Red).WhoseValue.Should().Be(1);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Green).WhoseValue.Should().Be(1);
    }

    [Fact]
    public void Simoon_UsesDamageOpponentCreaturesEffect()
    {
        CardDefinitions.TryGet("Simoon", out var def);
        def!.Effect.Should().BeOfType<DamageOpponentCreaturesEffect>();
        var effect = (DamageOpponentCreaturesEffect)def.Effect!;
        effect.Amount.Should().Be(1);
    }

    [Fact]
    public void DamageOpponentCreaturesEffect_DamagesOnlyOpponentCreatures()
    {
        var state = CreateState();
        var myCreature = new GameCard
        {
            Name = "My Creature",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        var oppCreature1 = new GameCard
        {
            Name = "Opp Creature 1",
            CardTypes = CardType.Creature,
            BasePower = 1,
            BaseToughness = 1,
        };
        var oppCreature2 = new GameCard
        {
            Name = "Opp Creature 2",
            CardTypes = CardType.Creature,
            BasePower = 3,
            BaseToughness = 3,
        };
        var oppLand = new GameCard
        {
            Name = "Mountain",
            CardTypes = CardType.Land,
        };
        state.Player1.Battlefield.Add(myCreature);
        state.Player2.Battlefield.Add(oppCreature1);
        state.Player2.Battlefield.Add(oppCreature2);
        state.Player2.Battlefield.Add(oppLand);

        var simoon = GameCard.Create("Simoon");
        var spell = new StackObject(simoon, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        new DamageOpponentCreaturesEffect(1).Resolve(state, spell);

        // My creature should be unaffected
        myCreature.DamageMarked.Should().Be(0);
        // Opponent's creatures should each take 1 damage
        oppCreature1.DamageMarked.Should().Be(1);
        oppCreature2.DamageMarked.Should().Be(1);
    }

    #endregion

    #region Crumble

    [Fact]
    public void Crumble_IsRegistered()
    {
        CardDefinitions.TryGet("Crumble", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Instant);
        def.ManaCost!.ConvertedManaCost.Should().Be(1);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Green);
    }

    [Fact]
    public void Crumble_UsesCrumbleEffect()
    {
        CardDefinitions.TryGet("Crumble", out var def);
        def!.Effect.Should().BeOfType<CrumbleEffect>();
    }

    [Fact]
    public void Crumble_TargetFilter_AcceptsArtifact()
    {
        CardDefinitions.TryGet("Crumble", out var def);
        var artifact = new GameCard { Name = "Cursed Scroll", CardTypes = CardType.Artifact };
        def!.TargetFilter!.IsLegal(artifact, ZoneType.Battlefield).Should().BeTrue();
    }

    [Fact]
    public void Crumble_TargetFilter_RejectsCreature()
    {
        CardDefinitions.TryGet("Crumble", out var def);
        var creature = new GameCard { Name = "Goblin Lackey", CardTypes = CardType.Creature };
        def!.TargetFilter!.IsLegal(creature, ZoneType.Battlefield).Should().BeFalse();
    }

    [Fact]
    public void CrumbleEffect_DestroysArtifact_GainsLifeEqualToManaValue()
    {
        var state = CreateState();
        var artifact = new GameCard
        {
            Name = "Cursed Scroll",
            ManaCost = ManaCost.Parse("{1}"),
            CardTypes = CardType.Artifact,
        };
        state.Player2.Battlefield.Add(artifact);

        var crumble = GameCard.Create("Crumble");
        var spell = new StackObject(crumble, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(artifact.Id, state.Player2.Id, ZoneType.Battlefield) }, 0);

        new CrumbleEffect().Resolve(state, spell);

        state.Player2.Battlefield.Cards.Should().NotContain(c => c.Id == artifact.Id);
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Id == artifact.Id);
        // Controller gains life = mana value (1)
        state.Player2.Life.Should().Be(21);
    }

    [Fact]
    public void CrumbleEffect_HighCostArtifact_GainsMoreLife()
    {
        var state = CreateState();
        var artifact = new GameCard
        {
            Name = "Masticore",
            ManaCost = ManaCost.Parse("{4}"),
            CardTypes = CardType.Artifact | CardType.Creature,
        };
        state.Player2.Battlefield.Add(artifact);

        var crumble = GameCard.Create("Crumble");
        var spell = new StackObject(crumble, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(artifact.Id, state.Player2.Id, ZoneType.Battlefield) }, 0);

        new CrumbleEffect().Resolve(state, spell);

        state.Player2.Life.Should().Be(24); // 20 + 4 mana value
    }

    [Fact]
    public void CrumbleEffect_ZeroCostArtifact_NoLifeGain()
    {
        var state = CreateState();
        var artifact = new GameCard
        {
            Name = "Zuran Orb",
            ManaCost = ManaCost.Parse("{0}"),
            CardTypes = CardType.Artifact,
        };
        state.Player2.Battlefield.Add(artifact);

        var crumble = GameCard.Create("Crumble");
        var spell = new StackObject(crumble, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(artifact.Id, state.Player2.Id, ZoneType.Battlefield) }, 0);

        new CrumbleEffect().Resolve(state, spell);

        state.Player2.Battlefield.Cards.Should().NotContain(c => c.Id == artifact.Id);
        state.Player2.Life.Should().Be(20); // No life gain for CMC 0
    }

    [Fact]
    public void CrumbleEffect_TargetGone_NoEffect()
    {
        var state = CreateState();
        var artifact = new GameCard { Name = "Cursed Scroll", CardTypes = CardType.Artifact };
        // Don't add to battlefield

        var crumble = GameCard.Create("Crumble");
        var spell = new StackObject(crumble, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(artifact.Id, state.Player2.Id, ZoneType.Battlefield) }, 0);

        new CrumbleEffect().Resolve(state, spell);

        state.Player2.Life.Should().Be(20);
    }

    #endregion

    #region Tranquil Domain

    [Fact]
    public void TranquilDomain_IsRegistered()
    {
        CardDefinitions.TryGet("Tranquil Domain", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Instant);
        def.ManaCost!.ConvertedManaCost.Should().Be(2);
        def.ManaCost.GenericCost.Should().Be(1);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Green);
    }

    [Fact]
    public void TranquilDomain_UsesDestroyAllNonAuraEnchantmentsEffect()
    {
        CardDefinitions.TryGet("Tranquil Domain", out var def);
        def!.Effect.Should().BeOfType<DestroyAllNonAuraEnchantmentsEffect>();
    }

    [Fact]
    public void DestroyAllNonAuraEnchantmentsEffect_DestroysNonAuraEnchantments()
    {
        var state = CreateState();
        var enchantment1 = new GameCard
        {
            Name = "Sulfuric Vortex",
            CardTypes = CardType.Enchantment,
            Subtypes = [],
        };
        var enchantment2 = new GameCard
        {
            Name = "Phyrexian Arena",
            CardTypes = CardType.Enchantment,
            Subtypes = [],
        };
        var aura = new GameCard
        {
            Name = "Wild Growth",
            CardTypes = CardType.Enchantment,
            Subtypes = ["Aura"],
        };
        var creature = new GameCard
        {
            Name = "Goblin Lackey",
            CardTypes = CardType.Creature,
        };
        state.Player1.Battlefield.Add(enchantment1);
        state.Player2.Battlefield.Add(enchantment2);
        state.Player1.Battlefield.Add(aura);
        state.Player2.Battlefield.Add(creature);

        var tranquil = GameCard.Create("Tranquil Domain");
        var spell = new StackObject(tranquil, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        new DestroyAllNonAuraEnchantmentsEffect().Resolve(state, spell);

        // Non-Aura enchantments destroyed on both sides
        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Id == enchantment1.Id);
        state.Player2.Battlefield.Cards.Should().NotContain(c => c.Id == enchantment2.Id);
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Id == enchantment1.Id);
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Id == enchantment2.Id);
        // Aura survives
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == aura.Id);
        // Creature unaffected
        state.Player2.Battlefield.Cards.Should().Contain(c => c.Id == creature.Id);
    }

    [Fact]
    public void DestroyAllNonAuraEnchantmentsEffect_NothingToDo_NoError()
    {
        var state = CreateState();
        var creature = new GameCard { Name = "Goblin Guide", CardTypes = CardType.Creature };
        state.Player1.Battlefield.Add(creature);

        var tranquil = GameCard.Create("Tranquil Domain");
        var spell = new StackObject(tranquil, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        var act = () => new DestroyAllNonAuraEnchantmentsEffect().Resolve(state, spell);
        act.Should().NotThrow();
    }

    #endregion

    #region GameCard.Create integration tests

    [Fact]
    public void GameCard_Create_YavimayaCoast_HasManaAbility()
    {
        var card = GameCard.Create("Yavimaya Coast");
        card.ManaAbility.Should().NotBeNull();
        card.ManaAbility!.Type.Should().Be(ManaAbilityType.Choice);
        card.CardTypes.Should().Be(CardType.Land);
    }

    [Fact]
    public void GameCard_Create_SavannahLions_HasCorrectStats()
    {
        var card = GameCard.Create("Savannah Lions");
        card.ManaCost.Should().NotBeNull();
        card.ManaCost!.ConvertedManaCost.Should().Be(1);
        card.Power.Should().Be(2);
        card.Toughness.Should().Be(1);
        card.IsCreature.Should().BeTrue();
        card.Subtypes.Should().Contain("Cat");
    }

    #endregion

    // ─── Task 5: Creatures with Activated Abilities ─────────────────────

    private (GameEngine engine, GameState state, Player p1, Player p2, TestDecisionHandler h1, TestDecisionHandler h2) CreateEngineSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);

        // Add library cards to prevent deck-out
        for (int i = 0; i < 20; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}" });
            p2.Library.Add(new GameCard { Name = $"Card{i}" });
        }

        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, p1, p2, h1, h2);
    }

    #region True Believer

    [Fact]
    public void TrueBeliever_IsRegistered()
    {
        CardDefinitions.TryGet("True Believer", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Creature);
        def.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(2);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.White).WhoseValue.Should().Be(2);
        def.Power.Should().Be(2);
        def.Toughness.Should().Be(2);
    }

    [Fact]
    public void TrueBeliever_HasSubtypes()
    {
        CardDefinitions.TryGet("True Believer", out var def);
        def!.Subtypes.Should().BeEquivalentTo(new[] { "Human", "Cleric" });
    }

    [Fact]
    public void TrueBeliever_GrantsPlayerShroud()
    {
        CardDefinitions.TryGet("True Believer", out var def);
        def!.ContinuousEffects.Should().ContainSingle();
        def.ContinuousEffects[0].Type.Should().Be(ContinuousEffectType.GrantPlayerShroud);
    }

    [Fact]
    public void TrueBeliever_HasNoActivatedAbilities()
    {
        CardDefinitions.TryGet("True Believer", out var def);
        def!.ActivatedAbilities.Should().BeEmpty();
    }

    #endregion

    #region Nova Cleric

    [Fact]
    public void NovaCleric_IsRegistered()
    {
        CardDefinitions.TryGet("Nova Cleric", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Creature);
        def.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(1);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.White).WhoseValue.Should().Be(1);
        def.Power.Should().Be(1);
        def.Toughness.Should().Be(2);
    }

    [Fact]
    public void NovaCleric_HasSubtypes()
    {
        CardDefinitions.TryGet("Nova Cleric", out var def);
        def!.Subtypes.Should().BeEquivalentTo(new[] { "Human", "Cleric" });
    }

    [Fact]
    public void NovaCleric_HasActivatedAbility()
    {
        CardDefinitions.TryGet("Nova Cleric", out var def);
        def!.ActivatedAbilities.Should().ContainSingle();

        var ability = def.ActivatedAbilities[0];
        ability.Cost.TapSelf.Should().BeTrue();
        ability.Cost.SacrificeSelf.Should().BeTrue();
        ability.Cost.ManaCost.Should().NotBeNull();
        ability.Cost.ManaCost!.ConvertedManaCost.Should().Be(3);
        ability.Cost.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.White);
        ability.Effect.Should().BeOfType<TriggerEffects.DestroyAllEnchantmentsEffect>();
    }

    [Fact]
    public async Task NovaCleric_Activation_DestroysAllEnchantments()
    {
        var (engine, state, p1, p2, h1, h2) = CreateEngineSetup();

        var cleric = GameCard.Create("Nova Cleric");
        p1.Battlefield.Add(cleric);
        p1.ManaPool.Add(ManaColor.White, 3);

        var ench1 = new GameCard { Name = "Crusade", CardTypes = CardType.Enchantment };
        var ench2 = new GameCard { Name = "Worship", CardTypes = CardType.Enchantment };
        p1.Battlefield.Add(ench1);
        p2.Battlefield.Add(ench2);

        var nonEnch = new GameCard { Name = "Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p2.Battlefield.Add(nonEnch);

        var action = GameAction.ActivateAbility(p1.Id, cleric.Id);
        await engine.ExecuteAction(action);
        await engine.ResolveAllTriggersAsync();

        // Cleric should be sacrificed
        p1.Battlefield.Cards.Should().NotContain(c => c.Id == cleric.Id);
        p1.Graveyard.Cards.Should().Contain(c => c.Id == cleric.Id);

        // Both enchantments destroyed
        p1.Battlefield.Cards.Should().NotContain(c => c.Id == ench1.Id);
        p2.Battlefield.Cards.Should().NotContain(c => c.Id == ench2.Id);
        p1.Graveyard.Cards.Should().Contain(c => c.Id == ench1.Id);
        p2.Graveyard.Cards.Should().Contain(c => c.Id == ench2.Id);

        // Non-enchantment should survive
        p2.Battlefield.Cards.Should().Contain(c => c.Id == nonEnch.Id);
    }

    #endregion

    #region Thornscape Apprentice

    [Fact]
    public void ThornscapeApprentice_IsRegistered()
    {
        CardDefinitions.TryGet("Thornscape Apprentice", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Creature);
        def.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(1);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Green).WhoseValue.Should().Be(1);
        def.Power.Should().Be(1);
        def.Toughness.Should().Be(1);
    }

    [Fact]
    public void ThornscapeApprentice_HasSubtypes()
    {
        CardDefinitions.TryGet("Thornscape Apprentice", out var def);
        def!.Subtypes.Should().BeEquivalentTo(new[] { "Human", "Wizard" });
    }

    [Fact]
    public void ThornscapeApprentice_HasExactlyTwoAbilities()
    {
        CardDefinitions.TryGet("Thornscape Apprentice", out var def);
        def!.ActivatedAbilities.Should().HaveCount(2);
    }

    [Fact]
    public void ThornscapeApprentice_FirstAbility_GrantsFirstStrike()
    {
        CardDefinitions.TryGet("Thornscape Apprentice", out var def);
        var ability = def!.ActivatedAbilities[0];

        ability.Cost.TapSelf.Should().BeTrue();
        ability.Cost.ManaCost.Should().NotBeNull();
        ability.Cost.ManaCost!.ColorRequirements.Should().ContainKey(ManaColor.Red);
        ability.Effect.Should().BeOfType<TriggerEffects.GrantFirstStrikeEffect>();
        ability.TargetFilter.Should().NotBeNull();
    }

    [Fact]
    public void ThornscapeApprentice_SecondAbility_TapsTarget()
    {
        CardDefinitions.TryGet("Thornscape Apprentice", out var def);
        var ability = def!.ActivatedAbilities[1];

        ability.Cost.TapSelf.Should().BeTrue();
        ability.Cost.ManaCost.Should().NotBeNull();
        ability.Cost.ManaCost!.ColorRequirements.Should().ContainKey(ManaColor.White);
        ability.Effect.Should().BeOfType<TriggerEffects.TapTargetEffect>();
        ability.TargetFilter.Should().NotBeNull();
    }

    [Fact]
    public async Task ThornscapeApprentice_FirstAbility_GrantsFirstStrikeToTarget()
    {
        var (engine, state, p1, p2, h1, h2) = CreateEngineSetup();

        var apprentice = GameCard.Create("Thornscape Apprentice");
        p1.Battlefield.Add(apprentice);
        p1.ManaPool.Add(ManaColor.Red, 1);

        var target = new GameCard { Name = "Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p1.Battlefield.Add(target);

        var action = GameAction.ActivateAbility(p1.Id, apprentice.Id, targetId: target.Id, abilityIndex: 0);
        await engine.ExecuteAction(action);
        await engine.ResolveAllTriggersAsync();

        // Recalculate state to apply continuous effects
        engine.RecalculateState();

        apprentice.IsTapped.Should().BeTrue();
        target.ActiveKeywords.Should().Contain(Keyword.FirstStrike);
    }

    [Fact]
    public async Task ThornscapeApprentice_SecondAbility_TapsTargetCreature()
    {
        var (engine, state, p1, p2, h1, h2) = CreateEngineSetup();

        var apprentice = GameCard.Create("Thornscape Apprentice");
        p1.Battlefield.Add(apprentice);
        p1.ManaPool.Add(ManaColor.White, 1);

        var target = new GameCard { Name = "Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p2.Battlefield.Add(target);

        var action = GameAction.ActivateAbility(p1.Id, apprentice.Id, targetId: target.Id, abilityIndex: 1);
        await engine.ExecuteAction(action);
        await engine.ResolveAllTriggersAsync();

        apprentice.IsTapped.Should().BeTrue();
        target.IsTapped.Should().BeTrue();
    }

    #endregion

    #region Waterfront Bouncer

    [Fact]
    public void WaterfrontBouncer_IsRegistered()
    {
        CardDefinitions.TryGet("Waterfront Bouncer", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Creature);
        def.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(2);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Blue).WhoseValue.Should().Be(1);
        def.Power.Should().Be(1);
        def.Toughness.Should().Be(1);
    }

    [Fact]
    public void WaterfrontBouncer_HasSubtypes()
    {
        CardDefinitions.TryGet("Waterfront Bouncer", out var def);
        def!.Subtypes.Should().BeEquivalentTo(new[] { "Merfolk", "Spellshaper" });
    }

    [Fact]
    public void WaterfrontBouncer_HasActivatedAbility()
    {
        CardDefinitions.TryGet("Waterfront Bouncer", out var def);
        def!.ActivatedAbilities.Should().ContainSingle();

        var ability = def.ActivatedAbilities[0];
        ability.Cost.TapSelf.Should().BeTrue();
        ability.Cost.ManaCost.Should().NotBeNull();
        ability.Cost.ManaCost!.ColorRequirements.Should().ContainKey(ManaColor.Blue);
        ability.Cost.DiscardAny.Should().BeTrue();
        ability.Effect.Should().BeOfType<TriggerEffects.BounceTargetCreatureEffect>();
        ability.TargetFilter.Should().NotBeNull();
    }

    [Fact]
    public async Task WaterfrontBouncer_Activation_BouncesTargetCreature()
    {
        var (engine, state, p1, p2, h1, h2) = CreateEngineSetup();

        var bouncer = GameCard.Create("Waterfront Bouncer");
        p1.Battlefield.Add(bouncer);
        p1.ManaPool.Add(ManaColor.Blue, 1);

        var discardCard = new GameCard { Name = "Forest", CardTypes = CardType.Land };
        p1.Hand.Add(discardCard);

        var target = new GameCard { Name = "Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p2.Battlefield.Add(target);

        h1.EnqueueCardChoice(discardCard.Id); // Choose card to discard

        var action = GameAction.ActivateAbility(p1.Id, bouncer.Id, targetId: target.Id);
        await engine.ExecuteAction(action);
        await engine.ResolveAllTriggersAsync();

        // Bouncer should be tapped
        bouncer.IsTapped.Should().BeTrue();

        // Discard card should be in graveyard
        p1.Hand.Cards.Should().NotContain(c => c.Id == discardCard.Id);
        p1.Graveyard.Cards.Should().Contain(c => c.Id == discardCard.Id);

        // Target creature should be returned to owner's hand
        p2.Battlefield.Cards.Should().NotContain(c => c.Id == target.Id);
        p2.Hand.Cards.Should().Contain(c => c.Id == target.Id);
    }

    [Fact]
    public async Task WaterfrontBouncer_NoCardsInHand_CannotActivate()
    {
        var (engine, state, p1, p2, h1, h2) = CreateEngineSetup();

        var bouncer = GameCard.Create("Waterfront Bouncer");
        p1.Battlefield.Add(bouncer);
        p1.ManaPool.Add(ManaColor.Blue, 1);

        var target = new GameCard { Name = "Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p2.Battlefield.Add(target);

        var action = GameAction.ActivateAbility(p1.Id, bouncer.Id, targetId: target.Id);
        await engine.ExecuteAction(action);

        // Bouncer should NOT be tapped (activation failed)
        bouncer.IsTapped.Should().BeFalse();

        // Target should still be on battlefield
        p2.Battlefield.Cards.Should().Contain(c => c.Id == target.Id);
    }

    #endregion

    #region Wild Mongrel

    [Fact]
    public void WildMongrel_IsRegistered()
    {
        CardDefinitions.TryGet("Wild Mongrel", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Creature);
        def.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(2);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Green).WhoseValue.Should().Be(1);
        def.Power.Should().Be(2);
        def.Toughness.Should().Be(2);
    }

    [Fact]
    public void WildMongrel_HasSubtypes()
    {
        CardDefinitions.TryGet("Wild Mongrel", out var def);
        def!.Subtypes.Should().BeEquivalentTo(new[] { "Dog" });
    }

    [Fact]
    public void WildMongrel_HasDiscardPumpAbility()
    {
        CardDefinitions.TryGet("Wild Mongrel", out var def);
        def!.ActivatedAbilities.Should().ContainSingle();

        var ability = def.ActivatedAbilities[0];
        ability.Cost.DiscardAny.Should().BeTrue();
        ability.Cost.TapSelf.Should().BeFalse();
        ability.Cost.ManaCost.Should().BeNull();
        ability.Effect.Should().BeOfType<TriggerEffects.PumpSelfEffect>();
    }

    [Fact]
    public async Task WildMongrel_Activation_PumpsAndDiscards()
    {
        var (engine, state, p1, p2, h1, h2) = CreateEngineSetup();

        var mongrel = GameCard.Create("Wild Mongrel");
        p1.Battlefield.Add(mongrel);

        var discardCard = new GameCard { Name = "Forest", CardTypes = CardType.Land };
        p1.Hand.Add(discardCard);

        h1.EnqueueCardChoice(discardCard.Id);

        var action = GameAction.ActivateAbility(p1.Id, mongrel.Id);
        await engine.ExecuteAction(action);
        await engine.ResolveAllTriggersAsync();
        engine.RecalculateState();

        // Card should be discarded
        p1.Hand.Cards.Should().NotContain(c => c.Id == discardCard.Id);
        p1.Graveyard.Cards.Should().Contain(c => c.Id == discardCard.Id);

        // Mongrel should be pumped +1/+1
        mongrel.Power.Should().Be(3);
        mongrel.Toughness.Should().Be(3);
    }

    [Fact]
    public async Task WildMongrel_MultipleActivations_StacksPump()
    {
        var (engine, state, p1, p2, h1, h2) = CreateEngineSetup();

        var mongrel = GameCard.Create("Wild Mongrel");
        p1.Battlefield.Add(mongrel);

        var card1 = new GameCard { Name = "Forest", CardTypes = CardType.Land };
        var card2 = new GameCard { Name = "Mountain", CardTypes = CardType.Land };
        p1.Hand.Add(card1);
        p1.Hand.Add(card2);

        h1.EnqueueCardChoice(card1.Id);
        var action1 = GameAction.ActivateAbility(p1.Id, mongrel.Id);
        await engine.ExecuteAction(action1);
        await engine.ResolveAllTriggersAsync();

        h1.EnqueueCardChoice(card2.Id);
        var action2 = GameAction.ActivateAbility(p1.Id, mongrel.Id);
        await engine.ExecuteAction(action2);
        await engine.ResolveAllTriggersAsync();
        engine.RecalculateState();

        // Should be +2/+2 from two activations
        mongrel.Power.Should().Be(4);
        mongrel.Toughness.Should().Be(4);
    }

    [Fact]
    public async Task WildMongrel_NoTapRequired()
    {
        var (engine, state, p1, p2, h1, h2) = CreateEngineSetup();

        var mongrel = GameCard.Create("Wild Mongrel");
        p1.Battlefield.Add(mongrel);

        var card = new GameCard { Name = "Forest", CardTypes = CardType.Land };
        p1.Hand.Add(card);

        h1.EnqueueCardChoice(card.Id);

        var action = GameAction.ActivateAbility(p1.Id, mongrel.Id);
        await engine.ExecuteAction(action);
        await engine.ResolveAllTriggersAsync();

        // Mongrel should NOT be tapped (no tap cost)
        mongrel.IsTapped.Should().BeFalse();
    }

    #endregion

    #region Aquamoeba

    [Fact]
    public void Aquamoeba_IsRegistered()
    {
        CardDefinitions.TryGet("Aquamoeba", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Creature);
        def.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(2);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Blue).WhoseValue.Should().Be(1);
        def.Power.Should().Be(1);
        def.Toughness.Should().Be(3);
    }

    [Fact]
    public void Aquamoeba_HasSubtypes()
    {
        CardDefinitions.TryGet("Aquamoeba", out var def);
        def!.Subtypes.Should().BeEquivalentTo(new[] { "Elemental", "Beast" });
    }

    [Fact]
    public void Aquamoeba_HasSwapAbility()
    {
        CardDefinitions.TryGet("Aquamoeba", out var def);
        def!.ActivatedAbilities.Should().ContainSingle();

        var ability = def.ActivatedAbilities[0];
        ability.Cost.DiscardAny.Should().BeTrue();
        ability.Cost.TapSelf.Should().BeFalse();
        ability.Cost.ManaCost.Should().BeNull();
        ability.Effect.Should().BeOfType<TriggerEffects.SwapPowerToughnessEffect>();
    }

    [Fact]
    public async Task Aquamoeba_Activation_SwapsPowerToughness()
    {
        var (engine, state, p1, p2, h1, h2) = CreateEngineSetup();

        var aquamoeba = GameCard.Create("Aquamoeba");
        p1.Battlefield.Add(aquamoeba);

        var discardCard = new GameCard { Name = "Island", CardTypes = CardType.Land };
        p1.Hand.Add(discardCard);

        h1.EnqueueCardChoice(discardCard.Id);

        var action = GameAction.ActivateAbility(p1.Id, aquamoeba.Id);
        await engine.ExecuteAction(action);
        await engine.ResolveAllTriggersAsync();
        engine.RecalculateState();

        // After swap: 1/3 becomes 3/1
        aquamoeba.Power.Should().Be(3);
        aquamoeba.Toughness.Should().Be(1);
    }

    [Fact]
    public async Task Aquamoeba_NoHandCards_CannotActivate()
    {
        var (engine, state, p1, p2, h1, h2) = CreateEngineSetup();

        var aquamoeba = GameCard.Create("Aquamoeba");
        p1.Battlefield.Add(aquamoeba);

        var action = GameAction.ActivateAbility(p1.Id, aquamoeba.Id);
        await engine.ExecuteAction(action);

        // Stats should remain unchanged (activation failed)
        aquamoeba.Power.Should().Be(1);
        aquamoeba.Toughness.Should().Be(3);
    }

    #endregion

    #region Flametongue Kavu

    [Fact]
    public void FlametongueKavu_IsRegistered()
    {
        CardDefinitions.TryGet("Flametongue Kavu", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Creature);
        def.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(4);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Red).WhoseValue.Should().Be(1);
        def.Power.Should().Be(4);
        def.Toughness.Should().Be(2);
    }

    [Fact]
    public void FlametongueKavu_HasSubtypes()
    {
        CardDefinitions.TryGet("Flametongue Kavu", out var def);
        def!.Subtypes.Should().BeEquivalentTo(new[] { "Kavu" });
    }

    [Fact]
    public void FlametongueKavu_HasETBTrigger()
    {
        CardDefinitions.TryGet("Flametongue Kavu", out var def);
        def!.Triggers.Should().ContainSingle();
        def.Triggers[0].Event.Should().Be(GameEvent.EnterBattlefield);
        def.Triggers[0].Condition.Should().Be(global::MtgDecker.Engine.Triggers.TriggerCondition.Self);
        def.Triggers[0].Effect.Should().BeOfType<TriggerEffects.DealDamageToTargetCreatureEffect>();
    }

    [Fact]
    public async Task FlametongueKavu_ETB_Deals4DamageToTargetCreature()
    {
        var (engine, state, p1, p2, h1, h2) = CreateEngineSetup();

        var target = new GameCard { Name = "Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 5 };
        p2.Battlefield.Add(target);

        // Enqueue target choice for the ETB effect
        h1.EnqueueTarget(new TargetInfo(target.Id, p2.Id, ZoneType.Battlefield));

        var ftk = GameCard.Create("Flametongue Kavu");
        p1.Battlefield.Add(ftk);
        ftk.TurnEnteredBattlefield = state.TurnNumber;

        await engine.QueueSelfTriggersOnStackAsync(GameEvent.EnterBattlefield, ftk, p1);
        await engine.ResolveAllTriggersAsync();

        // Target should have 4 damage
        target.DamageMarked.Should().Be(4);
    }

    [Fact]
    public async Task FlametongueKavu_ETB_NoOtherCreatures_TargetsItself()
    {
        var (engine, state, p1, p2, h1, h2) = CreateEngineSetup();

        var ftk = GameCard.Create("Flametongue Kavu");
        p1.Battlefield.Add(ftk);
        ftk.TurnEnteredBattlefield = state.TurnNumber;

        // No target enqueued — default behavior picks first eligible target (FTK itself)
        await engine.QueueSelfTriggersOnStackAsync(GameEvent.EnterBattlefield, ftk, p1);
        await engine.ResolveAllTriggersAsync();

        // FTK is the only creature, so it must target itself (4 damage >= 2 toughness -> dies)
        state.GameLog.Should().Contain(l => l.Contains("Flametongue Kavu deals 4 damage to Flametongue Kavu"));
        state.GameLog.Should().Contain(l => l.Contains("Flametongue Kavu dies"));
        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Flametongue Kavu");
    }

    [Fact]
    public async Task FlametongueKavu_ETB_CanTargetOwnCreature()
    {
        var (engine, state, p1, p2, h1, h2) = CreateEngineSetup();

        var ownCreature = new GameCard { Name = "Goblin", CardTypes = CardType.Creature, BasePower = 1, BaseToughness = 1 };
        p1.Battlefield.Add(ownCreature);

        h1.EnqueueTarget(new TargetInfo(ownCreature.Id, p1.Id, ZoneType.Battlefield));

        var ftk = GameCard.Create("Flametongue Kavu");
        p1.Battlefield.Add(ftk);
        ftk.TurnEnteredBattlefield = state.TurnNumber;

        await engine.QueueSelfTriggersOnStackAsync(GameEvent.EnterBattlefield, ftk, p1);
        await engine.ResolveAllTriggersAsync();

        // Own creature took 4 damage (1/1 creature -> dies from SBA lethal damage)
        state.GameLog.Should().Contain(l => l.Contains("Flametongue Kavu deals 4 damage to Goblin"));
        state.GameLog.Should().Contain(l => l.Contains("Goblin dies"));
        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Goblin");
        // FTK should still be alive on the battlefield
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Flametongue Kavu");
    }

    #endregion

    #region GameCard.Create tests for Task 5

    [Fact]
    public void GameCard_Create_TrueBeliever_HasCorrectStats()
    {
        var card = GameCard.Create("True Believer");
        card.Power.Should().Be(2);
        card.Toughness.Should().Be(2);
        card.IsCreature.Should().BeTrue();
        card.Subtypes.Should().BeEquivalentTo(new[] { "Human", "Cleric" });
    }

    [Fact]
    public void GameCard_Create_WildMongrel_HasCorrectStats()
    {
        var card = GameCard.Create("Wild Mongrel");
        card.Power.Should().Be(2);
        card.Toughness.Should().Be(2);
        card.IsCreature.Should().BeTrue();
        card.Subtypes.Should().BeEquivalentTo(new[] { "Dog" });
    }

    [Fact]
    public void GameCard_Create_Aquamoeba_HasCorrectStats()
    {
        var card = GameCard.Create("Aquamoeba");
        card.Power.Should().Be(1);
        card.Toughness.Should().Be(3);
        card.IsCreature.Should().BeTrue();
        card.Subtypes.Should().BeEquivalentTo(new[] { "Elemental", "Beast" });
    }

    [Fact]
    public void GameCard_Create_FlametongueKavu_HasCorrectStats()
    {
        var card = GameCard.Create("Flametongue Kavu");
        card.Power.Should().Be(4);
        card.Toughness.Should().Be(2);
        card.IsCreature.Should().BeTrue();
        card.Subtypes.Should().BeEquivalentTo(new[] { "Kavu" });
    }

    [Fact]
    public void GameCard_Create_WaterfrontBouncer_HasCorrectStats()
    {
        var card = GameCard.Create("Waterfront Bouncer");
        card.Power.Should().Be(1);
        card.Toughness.Should().Be(1);
        card.IsCreature.Should().BeTrue();
        card.Subtypes.Should().BeEquivalentTo(new[] { "Merfolk", "Spellshaper" });
    }

    [Fact]
    public void GameCard_Create_ThornscapeApprentice_HasCorrectStats()
    {
        var card = GameCard.Create("Thornscape Apprentice");
        card.Power.Should().Be(1);
        card.Toughness.Should().Be(1);
        card.IsCreature.Should().BeTrue();
        card.Subtypes.Should().BeEquivalentTo(new[] { "Human", "Wizard" });
    }

    [Fact]
    public void GameCard_Create_NovaCleric_HasCorrectStats()
    {
        var card = GameCard.Create("Nova Cleric");
        card.Power.Should().Be(1);
        card.Toughness.Should().Be(2);
        card.IsCreature.Should().BeTrue();
        card.Subtypes.Should().BeEquivalentTo(new[] { "Human", "Cleric" });
    }

    #endregion
}
