using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
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
    [InlineData("Anger")]
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

        def!.ActivatedAbility.Should().NotBeNull();
        var effect = def.ActivatedAbility!.Effect as DealDamageEffect;
        effect.Should().NotBeNull();
        effect!.Amount.Should().Be(2,
            because: "Grim Lavamancer deals 2 damage, not 1");
    }

    [Fact]
    public void GrimLavamancer_CostsRedMana()
    {
        CardDefinitions.TryGet("Grim Lavamancer", out var def);

        def!.ActivatedAbility.Should().NotBeNull();
        def.ActivatedAbility!.Cost.ManaCost.Should().NotBeNull(
            because: "Grim Lavamancer costs {R} to activate");
        def.ActivatedAbility.Cost.ManaCost!.ColorRequirements.Should()
            .ContainKey(ManaColor.Red);
    }

    // === Card audit: Goblin Tinkerer fix ===

    [Fact]
    public void GoblinTinkerer_CostsRedMana()
    {
        CardDefinitions.TryGet("Goblin Tinkerer", out var def);

        def!.ActivatedAbility.Should().NotBeNull();
        def.ActivatedAbility!.Cost.ManaCost.Should().NotBeNull(
            because: "Goblin Tinkerer costs {R} to activate");
        def.ActivatedAbility.Cost.ManaCost!.ColorRequirements.Should()
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
}
