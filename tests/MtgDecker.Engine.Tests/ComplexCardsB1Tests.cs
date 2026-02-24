using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

// ReSharper disable InconsistentNaming

namespace MtgDecker.Engine.Tests;

/// <summary>
/// Tests for Task 12 Batch 1: Overload, Orim's Chant, Rancor, Stifle,
/// Brain Freeze, River Boa, Circle of Protection: Red/Black.
/// </summary>
public class ComplexCardsB1Tests
{
    private static (GameState state, Player p1, Player p2) CreateGameState()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Alice", h1);
        var p2 = new Player(Guid.NewGuid(), "Bob", h2);
        var state = new GameState(p1, p2);
        return (state, p1, p2);
    }

    private static (GameState state, GameEngine engine, TestDecisionHandler h1, TestDecisionHandler h2)
        CreateEngineState()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (state, engine, h1, h2);
    }

    private static StackObject CreateSpell(string name, Guid controllerId, List<TargetInfo> targets,
        bool isKicked = false)
    {
        var card = GameCard.Create(name);
        return new StackObject(card, controllerId,
            new Dictionary<ManaColor, int>(), targets, 0)
        { IsKicked = isKicked };
    }

    // ═══════════════════════════════════════════════════════════════════
    // CARD REGISTRATION TESTS
    // ═══════════════════════════════════════════════════════════════════

    #region Registration

    [Theory]
    [InlineData("Overload")]
    [InlineData("Orim's Chant")]
    [InlineData("Rancor")]
    [InlineData("Stifle")]
    [InlineData("Brain Freeze")]
    [InlineData("River Boa")]
    [InlineData("Circle of Protection: Red")]
    [InlineData("Circle of Protection: Black")]
    public void Card_IsRegistered(string name)
    {
        CardDefinitions.TryGet(name, out var def).Should().BeTrue();
        def.Should().NotBeNull();
    }

    [Fact]
    public void Overload_HasCorrectProperties()
    {
        CardDefinitions.TryGet("Overload", out var def);
        def!.CardTypes.Should().Be(CardType.Instant);
        def.ManaCost!.ConvertedManaCost.Should().Be(1);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Red).WhoseValue.Should().Be(1);
        def.KickerCost.Should().NotBeNull();
        def.KickerCost!.ConvertedManaCost.Should().Be(2);
        def.TargetFilter.Should().NotBeNull();
        def.Effect.Should().BeOfType<OverloadEffect>();
    }

    [Fact]
    public void OrimsChant_HasCorrectProperties()
    {
        CardDefinitions.TryGet("Orim's Chant", out var def);
        def!.CardTypes.Should().Be(CardType.Instant);
        def.ManaCost!.ConvertedManaCost.Should().Be(1);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.White).WhoseValue.Should().Be(1);
        def.KickerCost.Should().NotBeNull();
        def.KickerCost!.ConvertedManaCost.Should().Be(1);
        def.KickerCost.ColorRequirements.Should().ContainKey(ManaColor.White);
        def.TargetFilter.Should().NotBeNull();
        def.Effect.Should().BeOfType<OrimsChantEffect>();
    }

    [Fact]
    public void Rancor_HasCorrectProperties()
    {
        CardDefinitions.TryGet("Rancor", out var def);
        def!.CardTypes.Should().Be(CardType.Enchantment);
        def.ManaCost!.ConvertedManaCost.Should().Be(1);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Green);
        def.Subtypes.Should().Contain("Aura");
        def.AuraTarget.Should().Be(AuraTarget.Creature);
        def.DynamicContinuousEffectsFactory.Should().NotBeNull();
        def.Triggers.Should().HaveCount(1);
        def.Triggers[0].Condition.Should().Be(TriggerCondition.SelfLeavesBattlefield);
    }

    [Fact]
    public void Stifle_HasCorrectProperties()
    {
        CardDefinitions.TryGet("Stifle", out var def);
        def!.CardTypes.Should().Be(CardType.Instant);
        def.ManaCost!.ConvertedManaCost.Should().Be(1);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Blue);
        def.Effect.Should().BeOfType<StifleEffect>();
    }

    [Fact]
    public void BrainFreeze_HasCorrectProperties()
    {
        CardDefinitions.TryGet("Brain Freeze", out var def);
        def!.CardTypes.Should().Be(CardType.Instant);
        def.ManaCost!.ConvertedManaCost.Should().Be(2);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Blue);
        def.HasStorm.Should().BeTrue();
        def.TargetFilter.Should().NotBeNull();
        def.Effect.Should().BeOfType<BrainFreezeEffect>();
    }

    [Fact]
    public void RiverBoa_HasCorrectProperties()
    {
        CardDefinitions.TryGet("River Boa", out var def);
        def!.CardTypes.Should().Be(CardType.Creature);
        def.ManaCost!.ConvertedManaCost.Should().Be(2);
        def.Power.Should().Be(2);
        def.Toughness.Should().Be(1);
        def.Subtypes.Should().Contain("Snake");
        def.ContinuousEffects.Should().NotBeEmpty();
        def.ActivatedAbilities.Should().HaveCount(1);
    }

    [Fact]
    public void CircleOfProtectionRed_HasCorrectProperties()
    {
        CardDefinitions.TryGet("Circle of Protection: Red", out var def);
        def!.CardTypes.Should().Be(CardType.Enchantment);
        def.ManaCost!.ConvertedManaCost.Should().Be(2);
        def.ActivatedAbilities.Should().HaveCount(1);
        def.ActivatedAbilities[0].Cost.ManaCost!.ConvertedManaCost.Should().Be(1);
    }

    [Fact]
    public void CircleOfProtectionBlack_HasCorrectProperties()
    {
        CardDefinitions.TryGet("Circle of Protection: Black", out var def);
        def!.CardTypes.Should().Be(CardType.Enchantment);
        def.ManaCost!.ConvertedManaCost.Should().Be(2);
        def.ActivatedAbilities.Should().HaveCount(1);
        def.ActivatedAbilities[0].Cost.ManaCost!.ConvertedManaCost.Should().Be(1);
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    // OVERLOAD EFFECT TESTS
    // ═══════════════════════════════════════════════════════════════════

    #region Overload

    [Fact]
    public void Overload_DestroysArtifact_WithMV2OrLess()
    {
        var (state, p1, p2) = CreateGameState();
        var artifact = GameCard.Create("Sol Ring", "Artifact", null, "{1}", null, null);
        p2.Battlefield.Add(artifact);

        var target = new TargetInfo(artifact.Id, p2.Id, ZoneType.Battlefield);
        var spell = CreateSpell("Overload", p1.Id, [target]);

        new OverloadEffect().Resolve(state, spell);

        p2.Battlefield.Cards.Should().NotContain(c => c.Id == artifact.Id);
        p2.Graveyard.Cards.Should().Contain(c => c.Id == artifact.Id);
    }

    [Fact]
    public void Overload_CannotDestroy_ArtifactWithMV3_WhenNotKicked()
    {
        var (state, p1, p2) = CreateGameState();
        var artifact = GameCard.Create("Phyrexian Revoker", "Artifact Creature", null, "{3}", null, null);
        p2.Battlefield.Add(artifact);

        var target = new TargetInfo(artifact.Id, p2.Id, ZoneType.Battlefield);
        var spell = CreateSpell("Overload", p1.Id, [target], isKicked: false);

        new OverloadEffect().Resolve(state, spell);

        p2.Battlefield.Cards.Should().Contain(c => c.Id == artifact.Id);
        p2.Graveyard.Cards.Should().NotContain(c => c.Id == artifact.Id);
        state.GameLog.Should().Contain(l => l.Contains("can't destroy"));
    }

    [Fact]
    public void Overload_Kicked_DestroysArtifactWithMV5OrLess()
    {
        var (state, p1, p2) = CreateGameState();
        var artifact = GameCard.Create("Gilded Lotus", "Artifact", null, "{5}", null, null);
        p2.Battlefield.Add(artifact);

        var target = new TargetInfo(artifact.Id, p2.Id, ZoneType.Battlefield);
        var spell = CreateSpell("Overload", p1.Id, [target], isKicked: true);

        new OverloadEffect().Resolve(state, spell);

        p2.Battlefield.Cards.Should().NotContain(c => c.Id == artifact.Id);
        p2.Graveyard.Cards.Should().Contain(c => c.Id == artifact.Id);
    }

    [Fact]
    public void Overload_Kicked_CannotDestroy_ArtifactWithMV6()
    {
        var (state, p1, p2) = CreateGameState();
        var artifact = GameCard.Create("Wurmcoil Engine", "Artifact Creature", null, "{6}", null, null);
        p2.Battlefield.Add(artifact);

        var target = new TargetInfo(artifact.Id, p2.Id, ZoneType.Battlefield);
        var spell = CreateSpell("Overload", p1.Id, [target], isKicked: true);

        new OverloadEffect().Resolve(state, spell);

        p2.Battlefield.Cards.Should().Contain(c => c.Id == artifact.Id);
        state.GameLog.Should().Contain(l => l.Contains("can't destroy"));
    }

    [Fact]
    public void Overload_Fizzles_WhenTargetGone()
    {
        var (state, p1, p2) = CreateGameState();
        var missingArtifactId = Guid.NewGuid();

        var target = new TargetInfo(missingArtifactId, p2.Id, ZoneType.Battlefield);
        var spell = CreateSpell("Overload", p1.Id, [target]);

        new OverloadEffect().Resolve(state, spell);

        state.GameLog.Should().Contain(l => l.Contains("fizzles"));
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    // ORIM'S CHANT EFFECT TESTS
    // ═══════════════════════════════════════════════════════════════════

    #region Orim's Chant

    [Fact]
    public void OrimsChant_PreventsCasting()
    {
        var (state, p1, p2) = CreateGameState();

        var target = new TargetInfo(Guid.Empty, p2.Id, ZoneType.None);
        var spell = CreateSpell("Orim's Chant", p1.Id, [target], isKicked: false);

        new OrimsChantEffect().Resolve(state, spell);

        state.ActiveEffects.Should().Contain(e =>
            e.Type == ContinuousEffectType.PreventSpellCasting);
        state.GameLog.Should().Contain(l => l.Contains("can't cast spells"));
    }

    [Fact]
    public void OrimsChant_NotKicked_DoesNotPreventAttacks()
    {
        var (state, p1, p2) = CreateGameState();

        var target = new TargetInfo(Guid.Empty, p2.Id, ZoneType.None);
        var spell = CreateSpell("Orim's Chant", p1.Id, [target], isKicked: false);

        new OrimsChantEffect().Resolve(state, spell);

        state.ActiveEffects.Should().NotContain(e =>
            e.Type == ContinuousEffectType.PreventCreatureAttacks);
    }

    [Fact]
    public void OrimsChant_Kicked_PreventsAttacks()
    {
        var (state, p1, p2) = CreateGameState();

        var target = new TargetInfo(Guid.Empty, p2.Id, ZoneType.None);
        var spell = CreateSpell("Orim's Chant", p1.Id, [target], isKicked: true);

        new OrimsChantEffect().Resolve(state, spell);

        state.ActiveEffects.Should().Contain(e =>
            e.Type == ContinuousEffectType.PreventCreatureAttacks);
        state.GameLog.Should().Contain(l => l.Contains("can't attack"));
    }

    [Fact]
    public void OrimsChant_PreventCastingAppliesToCorrectPlayer()
    {
        var (state, p1, p2) = CreateGameState();

        var target = new TargetInfo(Guid.Empty, p2.Id, ZoneType.None);
        var spell = CreateSpell("Orim's Chant", p1.Id, [target]);

        new OrimsChantEffect().Resolve(state, spell);

        var effect = state.ActiveEffects.First(e => e.Type == ContinuousEffectType.PreventSpellCasting);
        // Effect should apply to p2 (targeted) but not p1
        effect.Applies(new GameCard(), p2).Should().BeTrue();
        effect.Applies(new GameCard(), p1).Should().BeFalse();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    // RANCOR TESTS
    // ═══════════════════════════════════════════════════════════════════

    #region Rancor

    [Fact]
    public void Rancor_DynamicEffects_GrantPowerAndTrample()
    {
        CardDefinitions.TryGet("Rancor", out var def);
        var rancor = GameCard.Create("Rancor");
        var creature = GameCard.Create("Grizzly Bears", "Creature — Bear", null, null, "2", "2");
        rancor.AttachedTo = creature.Id;

        var effects = def!.DynamicContinuousEffectsFactory!(rancor);

        effects.Should().HaveCount(2);
        var ptEffect = effects.First(e => e.Type == ContinuousEffectType.ModifyPowerToughness);
        ptEffect.PowerMod.Should().Be(2);
        ptEffect.ToughnessMod.Should().Be(0);
        ptEffect.Applies(creature, null!).Should().BeTrue();

        var keywordEffect = effects.First(e => e.Type == ContinuousEffectType.GrantKeyword);
        keywordEffect.GrantedKeyword.Should().Be(Keyword.Trample);
        keywordEffect.Applies(creature, null!).Should().BeTrue();
    }

    [Fact]
    public void Rancor_DynamicEffects_ReturnEmpty_WhenNotAttached()
    {
        CardDefinitions.TryGet("Rancor", out var def);
        var rancor = GameCard.Create("Rancor");

        var effects = def!.DynamicContinuousEffectsFactory!(rancor);

        effects.Should().BeEmpty();
    }

    [Fact]
    public async Task Rancor_ReturnEffect_MovesFromGraveyardToHand()
    {
        var (state, p1, p2) = CreateGameState();
        var rancor = GameCard.Create("Rancor");
        p1.Graveyard.Add(rancor);

        var context = new EffectContext(state, p1, rancor, p1.DecisionHandler);
        var effect = new RancorReturnEffect();

        await effect.Execute(context);

        p1.Graveyard.Cards.Should().NotContain(c => c.Id == rancor.Id);
        p1.Hand.Cards.Should().Contain(c => c.Id == rancor.Id);
    }

    [Fact]
    public async Task Rancor_ReturnEffect_DoesNothing_WhenNotInGraveyard()
    {
        var (state, p1, p2) = CreateGameState();
        var rancor = GameCard.Create("Rancor");
        // rancor is not in the graveyard (e.g., exiled)

        var context = new EffectContext(state, p1, rancor, p1.DecisionHandler);
        var effect = new RancorReturnEffect();

        await effect.Execute(context);

        p1.Hand.Cards.Should().NotContain(c => c.Id == rancor.Id);
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    // STIFLE EFFECT TESTS
    // ═══════════════════════════════════════════════════════════════════

    #region Stifle

    [Fact]
    public void Stifle_RemovesTriggeredAbilityFromStack()
    {
        var (state, p1, p2) = CreateGameState();
        var sourceCard = GameCard.Create("Siege-Gang Commander");
        var trigger = new TriggeredAbilityStackObject(
            sourceCard, p2.Id, new RancorReturnEffect()); // any effect
        state.StackPush(trigger);

        var spell = CreateSpell("Stifle", p1.Id, []);
        new StifleEffect().Resolve(state, spell);

        state.Stack.OfType<TriggeredAbilityStackObject>().Should().BeEmpty();
        state.GameLog.Should().Contain(l => l.Contains("counters") && l.Contains("triggered ability"));
    }

    [Fact]
    public void Stifle_Fizzles_WhenNoTriggeredAbilityOnStack()
    {
        var (state, p1, p2) = CreateGameState();

        var spell = CreateSpell("Stifle", p1.Id, []);
        new StifleEffect().Resolve(state, spell);

        state.GameLog.Should().Contain(l => l.Contains("fizzles"));
    }

    [Fact]
    public void Stifle_OnlyRemovesFirstTriggeredAbility()
    {
        var (state, p1, p2) = CreateGameState();
        var source1 = GameCard.Create("Siege-Gang Commander");
        var source2 = GameCard.Create("Goblin Matron");
        var trigger1 = new TriggeredAbilityStackObject(source1, p2.Id, new RancorReturnEffect());
        var trigger2 = new TriggeredAbilityStackObject(source2, p2.Id, new RancorReturnEffect());
        state.StackPush(trigger1);
        state.StackPush(trigger2);

        var spell = CreateSpell("Stifle", p1.Id, []);
        new StifleEffect().Resolve(state, spell);

        // Should remove one, leave one
        state.Stack.OfType<TriggeredAbilityStackObject>().Should().HaveCount(1);
    }

    [Fact]
    public void Stifle_LeavesRegularSpellsAlone()
    {
        var (state, p1, p2) = CreateGameState();
        var creatureSpell = new StackObject(GameCard.Create("Grizzly Bears"), p2.Id,
            new Dictionary<ManaColor, int>(), [], 0);
        state.StackPush(creatureSpell);

        var spell = CreateSpell("Stifle", p1.Id, []);
        new StifleEffect().Resolve(state, spell);

        // The regular spell should remain on the stack
        state.Stack.OfType<StackObject>().Should().HaveCount(1);
        state.GameLog.Should().Contain(l => l.Contains("fizzles"));
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    // BRAIN FREEZE EFFECT TESTS
    // ═══════════════════════════════════════════════════════════════════

    #region Brain Freeze

    [Fact]
    public void BrainFreeze_Mills3Cards()
    {
        var (state, p1, p2) = CreateGameState();
        state.SpellsCastThisTurn = 1; // Just Brain Freeze itself

        for (int i = 0; i < 10; i++)
            p2.Library.AddToTop(GameCard.Create($"Card{i}"));

        var target = new TargetInfo(Guid.Empty, p2.Id, ZoneType.None);
        var spell = CreateSpell("Brain Freeze", p1.Id, [target]);

        new BrainFreezeEffect().Resolve(state, spell);

        p2.Graveyard.Cards.Should().HaveCount(3);
        p2.Library.Count.Should().Be(7);
    }

    [Fact]
    public void BrainFreeze_Storm_MillsExtraCopies()
    {
        var (state, p1, p2) = CreateGameState();
        state.SpellsCastThisTurn = 4; // 3 spells before BF + BF itself

        for (int i = 0; i < 20; i++)
            p2.Library.AddToTop(GameCard.Create($"Card{i}"));

        var target = new TargetInfo(Guid.Empty, p2.Id, ZoneType.None);
        var spell = CreateSpell("Brain Freeze", p1.Id, [target]);

        new BrainFreezeEffect().Resolve(state, spell);

        // Storm count = 4 - 1 = 3, total resolutions = 4, total milled = 12
        p2.Graveyard.Cards.Should().HaveCount(12);
        p2.Library.Count.Should().Be(8);
    }

    [Fact]
    public void BrainFreeze_Storm_LogsStormCount()
    {
        var (state, p1, p2) = CreateGameState();
        state.SpellsCastThisTurn = 3; // 2 before + BF itself

        for (int i = 0; i < 20; i++)
            p2.Library.AddToTop(GameCard.Create($"Card{i}"));

        var target = new TargetInfo(Guid.Empty, p2.Id, ZoneType.None);
        var spell = CreateSpell("Brain Freeze", p1.Id, [target]);

        new BrainFreezeEffect().Resolve(state, spell);

        state.GameLog.Should().Contain(l => l.Contains("Storm count: 2"));
    }

    [Fact]
    public void BrainFreeze_CausesDeckOut_WhenLibraryEmpty()
    {
        var (state, p1, p2) = CreateGameState();
        state.SpellsCastThisTurn = 1;

        // Only 2 cards in library, need to mill 3
        p2.Library.AddToTop(GameCard.Create("Card1"));
        p2.Library.AddToTop(GameCard.Create("Card2"));

        var target = new TargetInfo(Guid.Empty, p2.Id, ZoneType.None);
        var spell = CreateSpell("Brain Freeze", p1.Id, [target]);

        new BrainFreezeEffect().Resolve(state, spell);

        state.IsGameOver.Should().BeTrue();
        state.Winner.Should().Be(p1.Name);
    }

    [Fact]
    public void BrainFreeze_NoStorm_WhenOnlySpellCast()
    {
        var (state, p1, p2) = CreateGameState();
        state.SpellsCastThisTurn = 1; // Only Brain Freeze

        for (int i = 0; i < 10; i++)
            p2.Library.AddToTop(GameCard.Create($"Card{i}"));

        var target = new TargetInfo(Guid.Empty, p2.Id, ZoneType.None);
        var spell = CreateSpell("Brain Freeze", p1.Id, [target]);

        new BrainFreezeEffect().Resolve(state, spell);

        // No storm copies, just base 3 mills
        p2.Graveyard.Cards.Should().HaveCount(3);
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    // REGENERATION TESTS
    // ═══════════════════════════════════════════════════════════════════

    #region Regeneration

    [Fact]
    public async Task RegenerateEffect_IncrementsShields()
    {
        var (state, p1, p2) = CreateGameState();
        var creature = GameCard.Create("River Boa");
        creature.RegenerationShields.Should().Be(0);

        var context = new EffectContext(state, p1, creature, p1.DecisionHandler);
        var effect = new RegenerateEffect();

        await effect.Execute(context);

        creature.RegenerationShields.Should().Be(1);
    }

    [Fact]
    public async Task RegenerateEffect_StacksShields()
    {
        var (state, p1, p2) = CreateGameState();
        var creature = GameCard.Create("River Boa");

        var context = new EffectContext(state, p1, creature, p1.DecisionHandler);
        var effect = new RegenerateEffect();

        await effect.Execute(context);
        await effect.Execute(context);

        creature.RegenerationShields.Should().Be(2);
    }

    [Fact]
    public void GameCard_RegenerationShields_DefaultsToZero()
    {
        var card = GameCard.Create("Test Creature");
        card.RegenerationShields.Should().Be(0);
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    // CIRCLE OF PROTECTION TESTS
    // ═══════════════════════════════════════════════════════════════════

    #region Circle of Protection

    [Fact]
    public async Task CoPPreventDamageEffect_AddsDamagePreventionShield()
    {
        var (state, p1, p2) = CreateGameState();
        var cop = GameCard.Create("Circle of Protection: Red");

        var context = new EffectContext(state, p1, cop, p1.DecisionHandler);
        var effect = new CoPPreventDamageEffect(ManaColor.Red);

        await effect.Execute(context);

        p1.DamagePreventionShields.Should().HaveCount(1);
        p1.DamagePreventionShields[0].Color.Should().Be(ManaColor.Red);
    }

    [Fact]
    public async Task CoPPreventDamageEffect_LogsColor()
    {
        var (state, p1, p2) = CreateGameState();
        var cop = GameCard.Create("Circle of Protection: Black");

        var context = new EffectContext(state, p1, cop, p1.DecisionHandler);
        var effect = new CoPPreventDamageEffect(ManaColor.Black);

        await effect.Execute(context);

        state.GameLog.Should().Contain(l => l.Contains("Black"));
    }

    [Fact]
    public async Task CoPPreventDamageEffect_IsColorSpecific()
    {
        var (state, p1, p2) = CreateGameState();
        var cop = GameCard.Create("Circle of Protection: Red");

        var context = new EffectContext(state, p1, cop, p1.DecisionHandler);
        var effect = new CoPPreventDamageEffect(ManaColor.Red);

        await effect.Execute(context);

        // Shield is for Red only
        p1.DamagePreventionShields[0].Color.Should().Be(ManaColor.Red);
    }

    [Fact]
    public void CoPPreventDamageShield_IsConsumedOnUse()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // Add a Red shield
        p1.DamagePreventionShields.Add(new DamagePreventionShield(ManaColor.Red));

        // Red creature attacks
        var redCreature = new GameCard { Name = "Goblin", Power = 3, Toughness = 1, CardTypes = CardType.Creature };
        redCreature.Colors.Add(ManaColor.Red);

        var consumed = engine.TryConsumeColorDamageShield(p1, redCreature);
        consumed.Should().BeTrue();
        p1.DamagePreventionShields.Should().BeEmpty();
    }

    [Fact]
    public void CoPPreventDamageShield_DoesNotPreventWrongColor()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // Add a Red shield
        p1.DamagePreventionShields.Add(new DamagePreventionShield(ManaColor.Red));

        // Blue creature attacks — shield should NOT be consumed
        var blueCreature = new GameCard { Name = "Phantom", Power = 2, Toughness = 2, CardTypes = CardType.Creature };
        blueCreature.Colors.Add(ManaColor.Blue);

        var consumed = engine.TryConsumeColorDamageShield(p1, blueCreature);
        consumed.Should().BeFalse();
        p1.DamagePreventionShields.Should().HaveCount(1);
    }

    [Fact]
    public void CoPPreventDamageShield_ClearedAtEndOfTurn()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        p1.DamagePreventionShields.Add(new DamagePreventionShield(ManaColor.Red));

        engine.StripEndOfTurnEffects();

        p1.DamagePreventionShields.Should().BeEmpty();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    // KICKER MECHANIC TESTS
    // ═══════════════════════════════════════════════════════════════════

    #region Kicker

    [Fact]
    public void StackObject_IsKicked_DefaultsFalse()
    {
        var card = GameCard.Create("Overload");
        var spell = new StackObject(card, Guid.NewGuid(),
            new Dictionary<ManaColor, int>(), [], 0);
        spell.IsKicked.Should().BeFalse();
    }

    [Fact]
    public void StackObject_IsKicked_CanBeSetViaInitializer()
    {
        var card = GameCard.Create("Overload");
        var spell = new StackObject(card, Guid.NewGuid(),
            new Dictionary<ManaColor, int>(), [], 0) { IsKicked = true };
        spell.IsKicked.Should().BeTrue();
    }

    [Fact]
    public void CardDefinition_KickerCost_ExistsOnOverload()
    {
        CardDefinitions.TryGet("Overload", out var def);
        def!.KickerCost.Should().NotBeNull();
        def.KickerCost!.GenericCost.Should().Be(2);
    }

    [Fact]
    public void CardDefinition_KickerCost_ExistsOnOrimsChant()
    {
        CardDefinitions.TryGet("Orim's Chant", out var def);
        def!.KickerCost.Should().NotBeNull();
        def.KickerCost!.ColorRequirements.Should().ContainKey(ManaColor.White);
    }

    [Fact]
    public async Task Kicker_TryPayKickerAsync_ReturnsFalse_WhenNoKicker()
    {
        var (state, engine, h1, h2) = CreateEngineState();
        var card = GameCard.Create("Lightning Bolt");
        var player = state.Player1;

        var result = await engine.TryPayKickerAsync(card, player, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Kicker_TryPayKickerAsync_ReturnsFalse_WhenCantAfford()
    {
        var (state, engine, h1, h2) = CreateEngineState();
        var card = GameCard.Create("Overload"); // KickerCost = {2}
        var player = state.Player1;
        // Player has no mana

        var result = await engine.TryPayKickerAsync(card, player, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Kicker_TryPayKickerAsync_ReturnsTrue_WhenPlayerAccepts()
    {
        var (state, engine, h1, h2) = CreateEngineState();
        var card = GameCard.Create("Overload"); // KickerCost = {2}
        var player = state.Player1;
        player.ManaPool.Add(ManaColor.Colorless, 3); // Enough to pay {2} kicker

        // Default TestDecisionHandler.ChooseCard returns the first option (accepts kicker)

        var result = await engine.TryPayKickerAsync(card, player, CancellationToken.None);

        result.Should().BeTrue();
        player.ManaPool.Total.Should().Be(1); // 3 - 2 = 1 remaining
    }

    [Fact]
    public async Task Kicker_TryPayKickerAsync_ReturnsFalse_WhenPlayerDeclines()
    {
        var (state, engine, h1, h2) = CreateEngineState();
        var card = GameCard.Create("Overload"); // KickerCost = {2}
        var player = state.Player1;
        player.ManaPool.Add(ManaColor.Colorless, 3);

        // Enqueue null to decline kicker
        h1.EnqueueCardChoice(null);

        var result = await engine.TryPayKickerAsync(card, player, CancellationToken.None);

        result.Should().BeFalse();
        player.ManaPool.Total.Should().Be(3); // No mana deducted
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    // STORM COUNTER TESTS
    // ═══════════════════════════════════════════════════════════════════

    #region Storm Counter

    [Fact]
    public void SpellsCastThisTurn_DefaultsToZero()
    {
        var (state, p1, p2) = CreateGameState();
        state.SpellsCastThisTurn.Should().Be(0);
    }

    [Fact]
    public void HasStorm_BrainFreezeOnly()
    {
        CardDefinitions.TryGet("Brain Freeze", out var def);
        def!.HasStorm.Should().BeTrue();

        // Other cards should not have storm
        CardDefinitions.TryGet("Overload", out var overload);
        overload!.HasStorm.Should().BeFalse();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    // RIVER BOA ISLANDWALK TESTS
    // ═══════════════════════════════════════════════════════════════════

    #region River Boa Islandwalk

    [Fact]
    public void RiverBoa_HasIslandwalkEffect()
    {
        CardDefinitions.TryGet("River Boa", out var def);
        def!.ContinuousEffects.Should().Contain(e =>
            e.Type == ContinuousEffectType.GrantKeyword &&
            e.GrantedKeyword == Keyword.Islandwalk);
    }

    [Fact]
    public void RiverBoa_HasRegenerateAbility()
    {
        CardDefinitions.TryGet("River Boa", out var def);
        def!.ActivatedAbilities.Should().HaveCount(1);
        def.ActivatedAbilities[0].Effect.Should().BeOfType<RegenerateEffect>();
        def.ActivatedAbilities[0].Cost.ManaCost!.ColorRequirements
            .Should().ContainKey(ManaColor.Green).WhoseValue.Should().Be(1);
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    // PREVENT CREATURE ATTACKS TESTS
    // ═══════════════════════════════════════════════════════════════════

    #region PreventCreatureAttacks

    [Fact]
    public void ContinuousEffectType_HasPreventCreatureAttacks()
    {
        var value = ContinuousEffectType.PreventCreatureAttacks;
        value.Should().NotBe(default(ContinuousEffectType));
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    // GAME CARD PROPERTY TESTS
    // ═══════════════════════════════════════════════════════════════════

    #region GameCard Properties

    [Fact]
    public void GameCard_WasKicked_DefaultsFalse()
    {
        var card = GameCard.Create("Test");
        card.WasKicked.Should().BeFalse();
    }

    [Fact]
    public void GameCard_WasKicked_CanBeSet()
    {
        var card = GameCard.Create("Test");
        card.WasKicked = true;
        card.WasKicked.Should().BeTrue();
    }

    #endregion
}
