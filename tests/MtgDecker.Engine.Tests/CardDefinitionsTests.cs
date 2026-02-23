using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

// ReSharper disable InconsistentNaming

namespace MtgDecker.Engine.Tests;

public class CardDefinitionsTests
{
    [Fact]
    public void TryGet_KnownCreature_ReturnsDefinition()
    {
        var result = CardDefinitions.TryGet("Goblin Lackey", out var def);

        result.Should().BeTrue();
        def.Should().NotBeNull();
    }

    [Fact]
    public void TryGet_KnownLand_ReturnsDefinition()
    {
        var result = CardDefinitions.TryGet("Mountain", out var def);

        result.Should().BeTrue();
        def.Should().NotBeNull();
    }

    [Fact]
    public void TryGet_UnknownCard_ReturnsFalse()
    {
        var result = CardDefinitions.TryGet("Nonexistent Card XYZ", out var def);

        result.Should().BeFalse();
        def.Should().BeNull();
    }

    [Fact]
    public void GoblinLackey_HasCorrectCost()
    {
        CardDefinitions.TryGet("Goblin Lackey", out var def);

        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(1);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Red).WhoseValue.Should().Be(1);
        def.ManaCost.GenericCost.Should().Be(0);
    }

    [Fact]
    public void Mountain_HasFixedManaAbility()
    {
        CardDefinitions.TryGet("Mountain", out var def);

        def!.ManaAbility.Should().NotBeNull();
        def.ManaAbility!.Type.Should().Be(ManaAbilityType.Fixed);
        def.ManaAbility.FixedColor.Should().Be(ManaColor.Red);
    }

    [Fact]
    public void KarplusanForest_HasChoiceManaAbility()
    {
        CardDefinitions.TryGet("Karplusan Forest", out var def);

        def!.ManaAbility.Should().NotBeNull();
        def.ManaAbility!.Type.Should().Be(ManaAbilityType.Choice);
        def.ManaAbility.ChoiceColors.Should().BeEquivalentTo(
            new[] { ManaColor.Colorless, ManaColor.Red, ManaColor.Green });
    }

    [Fact]
    public void SiegeGangCommander_HasCorrectCostAndStats()
    {
        CardDefinitions.TryGet("Siege-Gang Commander", out var def);

        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(5);
        def.ManaCost.GenericCost.Should().Be(3);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Red).WhoseValue.Should().Be(2);
        def.Power.Should().Be(2);
        def.Toughness.Should().Be(2);
    }

    [Fact]
    public void ArgothianEnchantress_IsCreatureOnly()
    {
        CardDefinitions.TryGet("Argothian Enchantress", out var def);

        def!.CardTypes.Should().HaveFlag(CardType.Creature);
        def.CardTypes.Should().NotHaveFlag(CardType.Enchantment,
            "Argothian Enchantress is Creature — Human Druid, not an enchantment creature");
    }

    [Fact]
    public void AllStarterDeckCards_HaveDefinitions()
    {
        var allCardNames = new[]
        {
            // Goblins deck
            "Goblin Lackey", "Goblin Matron", "Goblin Piledriver", "Goblin Ringleader",
            "Goblin Warchief", "Mogg Fanatic", "Gempalm Incinerator", "Siege-Gang Commander",
            "Goblin King", "Goblin Pyromancer", "Goblin Sharpshooter", "Goblin Tinkerer",
            "Skirk Prospector", "Naturalize", "Mountain", "Forest", "Karplusan Forest",
            "Wooded Foothills", "Rishadan Port", "Wasteland",
            // Enchantress deck
            "Argothian Enchantress", "Swords to Plowshares", "Replenish",
            "Enchantress's Presence", "Wild Growth", "Exploration", "Mirri's Guile",
            "Opalescence", "Parallax Wave", "Sterling Grove", "Aura of Silence",
            "Seal of Cleansing", "Solitary Confinement", "Sylvan Library",
            "Plains", "Brushland", "Windswept Heath", "Serra's Sanctum",
            // Burn deck
            "Lightning Bolt", "Chain Lightning", "Lava Spike", "Rift Bolt",
            "Fireblast", "Goblin Guide", "Monastery Swiftspear",
            "Eidolon of the Great Revel", "Searing Blood", "Flame Rift",
            // UR Delver deck
            "Brainstorm", "Ponder", "Preordain", "Counterspell", "Daze",
            "Force of Will", "Delver of Secrets", "Murktide Regent",
            "Dragon's Rage Channeler", "Island", "Volcanic Island",
            "Scalding Tarn", "Mystic Sanctuary"
        };

        foreach (var name in allCardNames)
        {
            CardDefinitions.TryGet(name, out var def).Should().BeTrue(
                because: $"'{name}' should be registered in CardDefinitions");
        }
    }

    [Theory]
    [InlineData("Mountain")]
    [InlineData("Forest")]
    [InlineData("Plains")]
    public void Lands_HaveNoManaCost(string landName)
    {
        CardDefinitions.TryGet(landName, out var def);

        def!.ManaCost.Should().BeNull();
    }

    [Theory]
    [InlineData("Naturalize")]
    [InlineData("Swords to Plowshares")]
    public void Instants_HaveCorrectType(string cardName)
    {
        CardDefinitions.TryGet(cardName, out var def);

        def!.CardTypes.Should().HaveFlag(CardType.Instant);
    }

    // === Burn deck tests ===

    [Theory]
    [InlineData("Lightning Bolt")]
    [InlineData("Chain Lightning")]
    [InlineData("Lava Spike")]
    [InlineData("Rift Bolt")]
    [InlineData("Fireblast")]
    [InlineData("Goblin Guide")]
    [InlineData("Monastery Swiftspear")]
    [InlineData("Eidolon of the Great Revel")]
    [InlineData("Searing Blood")]
    [InlineData("Flame Rift")]
    public void BurnDeckCard_IsRegistered(string cardName)
    {
        CardDefinitions.TryGet(cardName, out var def).Should().BeTrue();
        def.Should().NotBeNull();
    }

    [Fact]
    public void LightningBolt_HasCorrectEffect()
    {
        CardDefinitions.TryGet("Lightning Bolt", out var def);
        def!.Effect.Should().BeOfType<DamageEffect>();
        ((DamageEffect)def.Effect!).Amount.Should().Be(3);
    }

    [Fact]
    public void LavaSpike_CanOnlyTargetPlayers()
    {
        CardDefinitions.TryGet("Lava Spike", out var def);
        var effect = (DamageEffect)def!.Effect!;
        effect.CanTargetPlayer.Should().BeTrue();
        effect.CanTargetCreature.Should().BeFalse();
    }

    // === UR Delver deck tests ===

    [Theory]
    [InlineData("Brainstorm")]
    [InlineData("Ponder")]
    [InlineData("Preordain")]
    [InlineData("Counterspell")]
    [InlineData("Daze")]
    [InlineData("Force of Will")]
    [InlineData("Delver of Secrets")]
    [InlineData("Murktide Regent")]
    [InlineData("Dragon's Rage Channeler")]
    [InlineData("Volcanic Island")]
    public void DelverDeckCard_IsRegistered(string cardName)
    {
        CardDefinitions.TryGet(cardName, out var def).Should().BeTrue();
        def.Should().NotBeNull();
    }

    [Fact]
    public void Counterspell_HasSpellTargetAndCounterEffect()
    {
        CardDefinitions.TryGet("Counterspell", out var def);
        def!.Effect.Should().BeOfType<CounterSpellEffect>();
        def.TargetFilter.Should().NotBeNull();
    }

    [Fact]
    public void Brainstorm_HasBrainstormEffect_NoTarget()
    {
        CardDefinitions.TryGet("Brainstorm", out var def);
        def!.Effect.Should().BeOfType<BrainstormEffect>();
        def.TargetFilter.Should().BeNull();
    }

    [Fact]
    public void Island_HasIslandSubtype()
    {
        CardDefinitions.TryGet("Island", out var def);

        def!.Subtypes.Should().Contain("Island",
            because: "Island has the Island land subtype for fetchland interactions");
    }

    [Fact]
    public void VolcanicIsland_HasDualSubtypes()
    {
        CardDefinitions.TryGet("Volcanic Island", out var def);

        def!.Subtypes.Should().Contain("Island");
        def.Subtypes.Should().Contain("Mountain");
    }

    [Theory]
    [InlineData("Caves of Koilos")]
    [InlineData("Llanowar Wastes")]
    [InlineData("Battlefield Forge")]
    [InlineData("Adarkar Wastes")]
    public void PainLand_HasPainColors(string cardName)
    {
        CardDefinitions.TryGet(cardName, out var def);

        def!.ManaAbility.Should().NotBeNull();
        def.ManaAbility!.PainColors.Should().NotBeNull(
            because: $"{cardName} should deal damage when tapping for colored mana");
        def.ManaAbility.PainColors!.Count.Should().BeGreaterThan(0);
    }

    // === Card audit: lands with missing abilities ===

    [Fact]
    public void RishadanPort_HasColorlessManaAbility()
    {
        CardDefinitions.TryGet("Rishadan Port", out var def);

        def!.ManaAbility.Should().NotBeNull(
            because: "Rishadan Port taps for {C}");
        def.ManaAbility!.FixedColor.Should().Be(ManaColor.Colorless);
    }

    [Fact]
    public void Wasteland_HasColorlessManaAbility()
    {
        CardDefinitions.TryGet("Wasteland", out var def);

        def!.ManaAbility.Should().NotBeNull(
            because: "Wasteland taps for {C}");
        def.ManaAbility!.FixedColor.Should().Be(ManaColor.Colorless);
    }

    [Fact]
    public void ScaldingTarn_HasFetchAbility()
    {
        CardDefinitions.TryGet("Scalding Tarn", out var def);

        def!.FetchAbility.Should().NotBeNull(
            because: "Scalding Tarn fetches Island or Mountain");
        def.FetchAbility!.SearchTypes.Should().BeEquivalentTo(
            new[] { "Island", "Mountain" });
    }

    // === Card audit: missing Haste keywords ===

    [Theory]
    [InlineData("Goblin Guide")]
    [InlineData("Goblin Ringleader")]
    [InlineData("Monastery Swiftspear")]
    public void Card_HasHaste(string cardName)
    {
        CardDefinitions.TryGet(cardName, out var def);

        def!.ContinuousEffects.Should().Contain(e =>
            e.Type == ContinuousEffectType.GrantKeyword
            && e.GrantedKeyword == Keyword.Haste,
            because: $"{cardName} should have haste");
    }

    // === Card audit: Exalted Angel Lifelink + Wall of Blossoms Defender ===

    [Fact]
    public void ExaltedAngel_HasLifelink()
    {
        CardDefinitions.TryGet("Exalted Angel", out var def);

        def!.ContinuousEffects.Should().Contain(e =>
            e.Type == ContinuousEffectType.GrantKeyword
            && e.GrantedKeyword == Keyword.Lifelink,
            because: "Exalted Angel has lifelink");
    }

    [Fact]
    public void WallOfBlossoms_HasDefender()
    {
        CardDefinitions.TryGet("Wall of Blossoms", out var def);

        def!.ContinuousEffects.Should().Contain(e =>
            e.Type == ContinuousEffectType.GrantKeyword
            && e.GrantedKeyword == Keyword.Defender,
            because: "Wall of Blossoms has defender");
    }

    // === Card audit: Grim Lavamancer fix ===

    [Fact]
    public void GrimLavamancer_Deals2Damage()
    {
        CardDefinitions.TryGet("Grim Lavamancer", out var def);

        def!.ActivatedAbilities.Should().NotBeEmpty();
        var effect = def.ActivatedAbilities[0].Effect as DealDamageEffect;
        effect.Should().NotBeNull();
        effect!.Amount.Should().Be(2,
            because: "Grim Lavamancer deals 2 damage, not 1");
    }

    [Fact]
    public void GrimLavamancer_CostsRedMana()
    {
        CardDefinitions.TryGet("Grim Lavamancer", out var def);

        def!.ActivatedAbilities.Should().NotBeEmpty();
        def.ActivatedAbilities[0].Cost.ManaCost.Should().NotBeNull(
            because: "Grim Lavamancer costs {R} to activate");
        def.ActivatedAbilities[0].Cost.ManaCost!.ColorRequirements.Should()
            .ContainKey(ManaColor.Red);
    }

    // === Card audit: Goblin Tinkerer fix ===

    [Fact]
    public void GoblinTinkerer_CostsRedMana()
    {
        CardDefinitions.TryGet("Goblin Tinkerer", out var def);

        def!.ActivatedAbilities.Should().NotBeEmpty();
        def.ActivatedAbilities[0].Cost.ManaCost.Should().NotBeNull(
            because: "Goblin Tinkerer costs {R} to activate");
        def.ActivatedAbilities[0].Cost.ManaCost!.ColorRequirements.Should()
            .ContainKey(ManaColor.Red);
    }

    // === Card audit Phase 2: correct card selection effects ===

    [Fact]
    public void Impulse_HasImpulseEffect()
    {
        CardDefinitions.TryGet("Impulse", out var def);

        def!.Effect.Should().BeOfType<ImpulseEffect>(
            because: "Impulse looks at top 4, picks 1, not just draw 1");
    }

    [Fact]
    public void FactOrFiction_HasFactOrFictionEffect()
    {
        CardDefinitions.TryGet("Fact or Fiction", out var def);

        def!.Effect.Should().BeOfType<FactOrFictionEffect>(
            because: "Fact or Fiction reveals 5, opponent splits, caster picks pile");
    }

    [Fact]
    public void SkeletalScrying_HasSkeletalScryingEffect()
    {
        CardDefinitions.TryGet("Skeletal Scrying", out var def);

        def!.Effect.Should().BeOfType<SkeletalScryingEffect>(
            because: "Skeletal Scrying exiles from graveyard, draws X, loses X life");
    }

    // === Card audit Phase 3: triggers and activated abilities ===

    [Theory]
    [InlineData("Eidolon of the Great Revel")]
    [InlineData("Ball Lightning")]
    [InlineData("Goblin Guide")]
    [InlineData("Squee, Goblin Nabob")]
    [InlineData("Plague Spitter")]
    public void Phase3Card_HasTriggers(string cardName)
    {
        CardDefinitions.TryGet(cardName, out var def);
        def!.Triggers.Should().NotBeEmpty(
            because: $"{cardName} should have at least one trigger");
    }

    [Theory]
    [InlineData("Nantuko Shade")]
    [InlineData("Ravenous Baloth")]
    [InlineData("Zuran Orb")]
    [InlineData("Withered Wretch")]
    [InlineData("Dust Bowl")]
    [InlineData("Mother of Runes")]
    public void Phase3Card_HasActivatedAbility(string cardName)
    {
        CardDefinitions.TryGet(cardName, out var def);
        def!.ActivatedAbilities.Should().NotBeEmpty(
            because: $"{cardName} should have an activated ability");
    }

    // === Card audit Phase 4: conditional counters + correct discard ===

    [Fact]
    public void ManaLeak_HasConditionalCounterEffect()
    {
        CardDefinitions.TryGet("Mana Leak", out var def);
        var effect = def!.Effect as ConditionalCounterEffect;
        effect.Should().NotBeNull(
            because: "Mana Leak should use ConditionalCounterEffect");
        effect!.GenericCost.Should().Be(3);
    }

    [Fact]
    public void Absorb_HasCounterAndGainLifeEffect()
    {
        CardDefinitions.TryGet("Absorb", out var def);
        def!.Effect.Should().BeOfType<CounterAndGainLifeEffect>(
            because: "Absorb counters and gains 3 life");
    }

    [Fact]
    public void Duress_HasDuressEffect()
    {
        CardDefinitions.TryGet("Duress", out var def);
        def!.Effect.Should().BeOfType<DuressEffect>(
            because: "Duress reveals hand and lets caster choose noncreature/nonland");
    }

    [Fact]
    public void CabalTherapy_HasCabalTherapyEffect()
    {
        CardDefinitions.TryGet("Cabal Therapy", out var def);
        def!.Effect.Should().BeOfType<CabalTherapyEffect>(
            because: "Cabal Therapy names a card and discards all copies");
    }

    [Fact]
    public void GerrardsVerdict_HasGerrardVerdictEffect()
    {
        CardDefinitions.TryGet("Gerrard's Verdict", out var def);
        def!.Effect.Should().BeOfType<GerrardVerdictEffect>(
            because: "Gerrard's Verdict target discards 2, caster gains 3 life per land");
    }

    // === Card audit: mana cost corrections ===

    [Fact]
    public void GempalmIncinerator_HasCorrectCost()
    {
        CardDefinitions.TryGet("Gempalm Incinerator", out var def);

        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(3);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Red).WhoseValue.Should().Be(1);
        def.ManaCost.GenericCost.Should().Be(2);
    }

    [Fact]
    public void RiftBolt_HasCorrectCost()
    {
        CardDefinitions.TryGet("Rift Bolt", out var def);

        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(3);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Red).WhoseValue.Should().Be(1);
        def.ManaCost.GenericCost.Should().Be(2);
    }

    [Fact]
    public void ShowAndTell_HasCorrectCost()
    {
        CardDefinitions.TryGet("Show and Tell", out var def);

        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(3);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Blue).WhoseValue.Should().Be(1);
        def.ManaCost.GenericCost.Should().Be(2);
    }

    [Fact]
    public void SkeletalScrying_HasCorrectCost()
    {
        CardDefinitions.TryGet("Skeletal Scrying", out var def);

        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(1);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Black).WhoseValue.Should().Be(1);
        def.ManaCost.GenericCost.Should().Be(0);
    }

    // === Card audit: P/T fix ===

    [Fact]
    public void GoblinTinkerer_HasCorrectPT()
    {
        CardDefinitions.TryGet("Goblin Tinkerer", out var def);
        def!.Power.Should().Be(1);
        def!.Toughness.Should().Be(2,
            because: "Goblin Tinkerer is a 1/2, not 1/1");
    }

    // === Card audit: subtype fixes ===

    [Theory]
    [InlineData("Goblin Piledriver", new[] { "Goblin", "Warrior" })]
    [InlineData("Goblin Warchief", new[] { "Goblin", "Warrior" })]
    [InlineData("Goblin Pyromancer", new[] { "Goblin", "Wizard" })]
    [InlineData("Goblin Guide", new[] { "Goblin", "Scout" })]
    [InlineData("Quirion Ranger", new[] { "Elf", "Ranger" })]
    [InlineData("Bane of the Living", new[] { "Insect" })]
    [InlineData("Plague Spitter", new[] { "Phyrexian", "Horror" })]
    [InlineData("Phyrexian Rager", new[] { "Phyrexian", "Horror" })]
    [InlineData("Jackal Pup", new[] { "Jackal" })]
    [InlineData("Masticore", new[] { "Masticore" })]
    [InlineData("Nantuko Vigilante", new[] { "Insect", "Druid", "Mutant" })]
    public void Card_HasCorrectSubtypes(string cardName, string[] expectedSubtypes)
    {
        CardDefinitions.TryGet(cardName, out var def);
        def.Should().NotBeNull(because: $"'{cardName}' should be registered");
        def!.Subtypes.Should().BeEquivalentTo(expectedSubtypes,
            because: $"{cardName} should have subtypes [{string.Join(", ", expectedSubtypes)}]");
    }

    // === Card audit: missing keywords for 7 cards ===

    [Theory]
    [InlineData("Anger")]
    [InlineData("Terravore")]
    [InlineData("Murktide Regent")]
    [InlineData("Wall of Roots")]
    public void Card_HasKeyword(string cardName)
    {
        CardDefinitions.TryGet(cardName, out var def);
        def!.ContinuousEffects.Should().NotBeEmpty(
            because: $"{cardName} should have keyword-granting continuous effects");
    }

    [Fact]
    public void Anger_HasHasteOnCreature()
    {
        CardDefinitions.TryGet("Anger", out var def);
        def!.ContinuousEffects.Should().Contain(e =>
            e.Type == ContinuousEffectType.GrantKeyword && e.GrantedKeyword == Keyword.Haste);
    }

    [Fact]
    public void Terravore_HasTrample()
    {
        CardDefinitions.TryGet("Terravore", out var def);
        def!.ContinuousEffects.Should().Contain(e =>
            e.Type == ContinuousEffectType.GrantKeyword && e.GrantedKeyword == Keyword.Trample);
    }

    [Fact]
    public void MurktideRegent_HasFlying()
    {
        CardDefinitions.TryGet("Murktide Regent", out var def);
        def!.ContinuousEffects.Should().Contain(e =>
            e.Type == ContinuousEffectType.GrantKeyword && e.GrantedKeyword == Keyword.Flying);
    }

    [Fact]
    public void WallOfRoots_HasDefender()
    {
        CardDefinitions.TryGet("Wall of Roots", out var def);
        def!.ContinuousEffects.Should().Contain(e =>
            e.Type == ContinuousEffectType.GrantKeyword && e.GrantedKeyword == Keyword.Defender);
    }

    [Fact]
    public void Emrakul_CannotBeCountered()
    {
        CardDefinitions.TryGet("Emrakul, the Aeons Torn", out var def);
        def!.CannotBeCountered.Should().BeTrue();
    }

    [Fact]
    public void FaerieConclave_BecomeCreatureHasFlying()
    {
        CardDefinitions.TryGet("Faerie Conclave", out var def);
        var effect = def!.ActivatedAbilities[0].Effect as BecomeCreatureEffect;
        effect.Should().NotBeNull();
        effect!.Keywords.Should().Contain(Keyword.Flying);
    }

    [Fact]
    public void TreetopVillage_BecomeCreatureHasTrample()
    {
        CardDefinitions.TryGet("Treetop Village", out var def);
        var effect = def!.ActivatedAbilities[0].Effect as BecomeCreatureEffect;
        effect.Should().NotBeNull();
        effect!.Keywords.Should().Contain(Keyword.Trample);
    }

    // === Card audit Batch 4a: simple data fixes ===

    [Fact]
    public void GoblinKing_PumpExcludesSelf()
    {
        CardDefinitions.TryGet("Goblin King", out var def);
        var pumpEffect = def!.ContinuousEffects.First(e => e.Type == ContinuousEffectType.ModifyPowerToughness);
        pumpEffect.ExcludeSelf.Should().BeTrue();
    }

    [Fact]
    public void RayOfRevelation_TargetsEnchantmentOnly()
    {
        CardDefinitions.TryGet("Ray of Revelation", out var def);
        def!.TargetFilter.Should().NotBeNull();
        // Create a mock card that is an artifact but not enchantment — should NOT be legal
        var artifactCard = GameCard.Create("Test Artifact", "Artifact", null, "{2}", null, null);
        def.TargetFilter!.IsLegal(artifactCard, ZoneType.Battlefield).Should().BeFalse(
            because: "Ray of Revelation should only target enchantments, not artifacts");
        // Create a mock card that is an enchantment — should be legal
        var enchantmentCard = GameCard.Create("Test Enchantment", "Enchantment", null, "{2}", null, null);
        def.TargetFilter!.IsLegal(enchantmentCard, ZoneType.Battlefield).Should().BeTrue(
            because: "Ray of Revelation should target enchantments");
    }

    [Fact]
    public void GoblinTinkerer_UsesTapSelf()
    {
        CardDefinitions.TryGet("Goblin Tinkerer", out var def);
        def!.ActivatedAbilities[0].Cost.TapSelf.Should().BeTrue();
        def.ActivatedAbilities[0].Cost.SacrificeSelf.Should().BeFalse();
    }

    [Fact]
    public void KnightOfStromgald_NoStaticFirstStrike()
    {
        CardDefinitions.TryGet("Knight of Stromgald", out var def);
        def!.ContinuousEffects.Should().NotContain(e =>
            e.Type == ContinuousEffectType.GrantKeyword && e.GrantedKeyword == Keyword.FirstStrike);
    }

    [Fact]
    public void KnightOfStromgald_PumpCostsBB()
    {
        CardDefinitions.TryGet("Knight of Stromgald", out var def);
        def!.ActivatedAbilities[0].Cost.ManaCost!.ColorRequirements[ManaColor.Black].Should().Be(2);
    }

    [Fact]
    public void Daze_IsSoftCounter()
    {
        CardDefinitions.TryGet("Daze", out var def);
        def!.Effect.Should().BeOfType<ConditionalCounterEffect>();
    }

    // === Card audit Batch 4b: PyromancerEffect ===

    [Fact]
    public void GoblinPyromancer_HasPyromancerEffect()
    {
        CardDefinitions.TryGet("Goblin Pyromancer", out var def);
        def!.Triggers.Should().Contain(t => t.Effect is PyromancerEffect);
    }

    // === Card audit Batch 4c: Sterling Grove search ===

    [Fact]
    public void SterlingGrove_SearchPutsOnTopOfLibrary()
    {
        CardDefinitions.TryGet("Sterling Grove", out var def);
        def!.ActivatedAbilities[0].Effect.Should().BeOfType<SearchLibraryToTopEffect>();
    }

    // === Card audit Batch 4d: Yavimaya Granger ===

    [Fact]
    public void YavimayaGranger_SearchesBasicLandToBattlefield()
    {
        CardDefinitions.TryGet("Yavimaya Granger", out var def);
        def!.Triggers.Should().ContainSingle();
        def.Triggers[0].Effect.Should().BeOfType<SearchLandToBattlefieldEffect>();
    }

    // === Card audit Batch 4e: Gempalm Incinerator + Priest of Titania counting ===

    [Fact]
    public void GempalmIncinerator_HasCyclingTrigger()
    {
        CardDefinitions.TryGet("Gempalm Incinerator", out var def);
        def!.CyclingTriggers.Should().ContainSingle();
        def.CyclingTriggers[0].Effect.Should().BeOfType<GempalmIncineratorEffect>();
    }

    // === Card audit Batch 4f: Dust Bowl ===

    [Fact]
    public void DustBowl_SacrificesLandNotSelf()
    {
        CardDefinitions.TryGet("Dust Bowl", out var def);
        def!.ActivatedAbilities[0].Cost.SacrificeSelf.Should().BeFalse();
        def.ActivatedAbilities[0].Cost.SacrificeCardType.Should().Be(CardType.Land);
    }

    // === Card audit Batch 4g: Volcanic Spray fixes ===

    [Fact]
    public void VolcanicSpray_HasFlashback()
    {
        CardDefinitions.TryGet("Volcanic Spray", out var def);
        def!.FlashbackCost.Should().NotBeNull();
    }

    [Fact]
    public void VolcanicSpray_UsesDamageNonflyingEffect()
    {
        CardDefinitions.TryGet("Volcanic Spray", out var def);
        def!.Effect.Should().BeOfType<DamageNonflyingCreaturesAndPlayersEffect>();
    }

    // === Card audit Batch 4h: Bottomless Pit fixes ===

    [Fact]
    public void BottomlessPit_UsesAnyUpkeepTrigger()
    {
        CardDefinitions.TryGet("Bottomless Pit", out var def);
        def!.Triggers[0].Condition.Should().Be(TriggerCondition.AnyUpkeep);
    }

    [Fact]
    public void BottomlessPit_UsesActivePlayerDiscardsRandom()
    {
        CardDefinitions.TryGet("Bottomless Pit", out var def);
        def!.Triggers[0].Effect.Should().BeOfType<ActivePlayerDiscardsRandomEffect>();
    }

    // === Card audit Batch 4i: Prohibit CMC check ===

    [Fact]
    public void Prohibit_UsesCmcCheckCounter()
    {
        CardDefinitions.TryGet("Prohibit", out var def);
        def!.Effect.Should().BeOfType<CmcCheckCounterEffect>();
        ((CmcCheckCounterEffect)def.Effect!).MaxCmc.Should().Be(2);
    }

    // === Card audit Batch 5a: Grim Lavamancer graveyard exile cost ===

    [Fact]
    public void GrimLavamancer_RequiresGraveyardExile()
    {
        CardDefinitions.TryGet("Grim Lavamancer", out var def);
        def!.ActivatedAbilities[0].Cost.ExileFromGraveyardCount.Should().Be(2);
    }

    // === Card audit Batch 5b: Barbarian Ring + Cabal Pit self-damage on mana tap ===

    [Fact]
    public void BarbarianRing_ManaAbilityDealsSelfDamage()
    {
        CardDefinitions.TryGet("Barbarian Ring", out var def);
        def!.ManaAbility!.SelfDamage.Should().Be(1);
    }

    [Fact]
    public void CabalPit_ManaAbilityDealsSelfDamage()
    {
        CardDefinitions.TryGet("Cabal Pit", out var def);
        def!.ManaAbility!.SelfDamage.Should().Be(1);
    }

    // === Card audit Batch 5c: Goblin Sharpshooter DoesNotUntap ===

    [Fact]
    public void GoblinSharpshooter_HasDoesNotUntap()
    {
        CardDefinitions.TryGet("Goblin Sharpshooter", out var def);
        def!.ContinuousEffects.Should().Contain(e =>
            e.Type == ContinuousEffectType.GrantKeyword && e.GrantedKeyword == Keyword.DoesNotUntap);
    }

    // === Card audit Batch 5d: Sulfuric Vortex life-gain prevention ===

    [Fact]
    public void SulfuricVortex_PreventsLifeGain()
    {
        CardDefinitions.TryGet("Sulfuric Vortex", out var def);
        def!.ContinuousEffects.Should().Contain(e =>
            e.Type == ContinuousEffectType.PreventLifeGain);
    }

    // === Card audit Batch 5e: Parallax Wave fading upkeep ===

    [Fact]
    public void ParallaxWave_HasFadingUpkeepTrigger()
    {
        CardDefinitions.TryGet("Parallax Wave", out var def);
        def!.Triggers.Should().Contain(t => t.Event == GameEvent.Upkeep);
    }

    [Fact]
    public void ParallaxWave_StillHasLeavesBattlefieldTrigger()
    {
        CardDefinitions.TryGet("Parallax Wave", out var def);
        def!.Triggers.Should().Contain(t => t.Event == GameEvent.LeavesBattlefield);
    }

    // === Card audit Batch 5f: Mother of Runes targets own creatures only ===

    [Fact]
    public void MotherOfRunes_TargetsOwnCreaturesOnly()
    {
        CardDefinitions.TryGet("Mother of Runes", out var def);
        def!.ActivatedAbilities[0].TargetOwnOnly.Should().BeTrue();
    }

    // === Card audit Batch 5g: Withered Wretch exiles from any graveyard ===

    [Fact]
    public void WitheredWretch_ExilesFromAnyGraveyard()
    {
        CardDefinitions.TryGet("Withered Wretch", out var def);
        def!.ActivatedAbilities[0].Effect.Should().BeOfType<ExileFromAnyGraveyardEffect>();
    }

    // === Card audit Batch 5h: Searing Blood delayed death trigger ===

    [Fact]
    public void SearingBlood_HasSearingBloodEffect()
    {
        CardDefinitions.TryGet("Searing Blood", out var def);
        def!.Effect.Should().BeOfType<SearingBloodEffect>();
    }

    // === Card audit Batch 5i: Mystic Sanctuary conditional enters-tapped + ETB ===

    [Fact]
    public void MysticSanctuary_HasConditionalEntersTapped()
    {
        CardDefinitions.TryGet("Mystic Sanctuary", out var def);
        def!.ConditionalEntersTapped.Should().NotBeNull();
    }

    [Fact]
    public void MysticSanctuary_HasETBTrigger()
    {
        CardDefinitions.TryGet("Mystic Sanctuary", out var def);
        def!.Triggers.Should().NotBeEmpty();
    }

    [Fact]
    public void MysticSanctuary_HasIslandSubtype()
    {
        CardDefinitions.TryGet("Mystic Sanctuary", out var def);
        def!.Subtypes.Should().Contain("Island");
    }
}
