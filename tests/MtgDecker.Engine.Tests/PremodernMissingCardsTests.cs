using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

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
}
