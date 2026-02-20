using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class DimirTempoCardsTests
{
    // === Card Registration Tests ===

    [Fact]
    public void PollutedDelta_IsRegistered_WithFetchAbility()
    {
        CardDefinitions.TryGet("Polluted Delta", out var def).Should().BeTrue();
        def.Should().NotBeNull();
        def!.FetchAbility.Should().NotBeNull();
        def.FetchAbility!.SearchTypes.Should().Contain("Island").And.Contain("Swamp");
        def.CardTypes.Should().Be(CardType.Land);
    }

    [Fact]
    public void UndergroundSea_IsRegistered_WithDualMana()
    {
        CardDefinitions.TryGet("Underground Sea", out var def).Should().BeTrue();
        def.Should().NotBeNull();
        def!.ManaAbility.Should().NotBeNull();
        def.ManaAbility!.Type.Should().Be(ManaAbilityType.Choice);
        def.Subtypes.Should().Contain("Island").And.Contain("Swamp");
        def.CardTypes.Should().Be(CardType.Land);
    }

    [Fact]
    public void MistyRainforest_IsRegistered_WithFetchAbility()
    {
        CardDefinitions.TryGet("Misty Rainforest", out var def).Should().BeTrue();
        def.Should().NotBeNull();
        def!.FetchAbility.Should().NotBeNull();
        def.FetchAbility!.SearchTypes.Should().Contain("Forest").And.Contain("Island");
        def.CardTypes.Should().Be(CardType.Land);
    }

    [Fact]
    public void UndercitySewers_IsRegistered_EntersTapped()
    {
        CardDefinitions.TryGet("Undercity Sewers", out var def).Should().BeTrue();
        def.Should().NotBeNull();
        def!.EntersTapped.Should().BeTrue();
        def.ManaAbility.Should().NotBeNull();
        def.ManaAbility!.Type.Should().Be(ManaAbilityType.Choice);
        def.Triggers.Should().ContainSingle(t =>
            t.Event == GameEvent.EnterBattlefield
            && t.Condition == TriggerCondition.Self
            && t.Effect is SurveilEffect);
    }

    [Fact]
    public void Thoughtseize_IsRegistered_AsBlackSorcery()
    {
        CardDefinitions.TryGet("Thoughtseize", out var def).Should().BeTrue();
        def.Should().NotBeNull();
        def!.CardTypes.Should().Be(CardType.Sorcery);
        def.ManaCost.Should().NotBeNull();
        def.ManaCost!.ColorRequirements.Should().ContainKey(ManaColor.Black);
        def.ManaCost.ConvertedManaCost.Should().Be(1);
        def.Effect.Should().BeOfType<ThoughtseizeEffect>();
    }

    [Fact]
    public void FatalPush_IsRegistered_AsBlackInstant()
    {
        CardDefinitions.TryGet("Fatal Push", out var def).Should().BeTrue();
        def.Should().NotBeNull();
        def!.CardTypes.Should().Be(CardType.Instant);
        def.ManaCost.Should().NotBeNull();
        def.ManaCost!.ColorRequirements.Should().ContainKey(ManaColor.Black);
        def.ManaCost.ConvertedManaCost.Should().Be(1);
        def.Effect.Should().BeOfType<FatalPushEffect>();
    }

    // === ThoughtseizeEffect Tests ===

    private TestDecisionHandler _h1 = null!;
    private TestDecisionHandler _h2 = null!;
    private Player _p1 = null!;
    private Player _p2 = null!;
    private GameState _state = null!;

    private void Setup()
    {
        _h1 = new TestDecisionHandler();
        _h2 = new TestDecisionHandler();
        _p1 = new Player(Guid.NewGuid(), "P1", _h1);
        _p2 = new Player(Guid.NewGuid(), "P2", _h2);
        _state = new GameState(_p1, _p2);
    }

    private StackObject CreateThoughtseizeSpell()
    {
        var card = new GameCard { Name = "Thoughtseize" };
        return new StackObject(card, _p1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(Guid.Empty, _p2.Id, ZoneType.None) }, 0);
    }

    private StackObject CreateFatalPushSpell(GameCard target, Guid ownerId)
    {
        var card = new GameCard { Name = "Fatal Push" };
        return new StackObject(card, _p1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(target.Id, ownerId, ZoneType.Battlefield) }, 0);
    }

    [Fact]
    public async Task ThoughtseizeEffect_DiscardsNonlandCard()
    {
        // Arrange: opponent has a creature and a land in hand
        Setup();
        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature };
        var land = new GameCard { Name = "Swamp", CardTypes = CardType.Land };
        _p2.Hand.Add(creature);
        _p2.Hand.Add(land);

        // Caster picks the creature (any nonland card is eligible)
        _h1.EnqueueCardChoice(creature.Id);

        var spell = CreateThoughtseizeSpell();
        var effect = new ThoughtseizeEffect();

        // Act
        await effect.ResolveAsync(_state, spell, _h1);

        // Assert: creature discarded, land still in hand, caster lost 2 life
        _p2.Hand.Cards.Should().NotContain(c => c.Id == creature.Id);
        _p2.Graveyard.Cards.Should().Contain(c => c.Id == creature.Id);
        _p2.Hand.Cards.Should().Contain(c => c.Id == land.Id);
        _p1.Life.Should().Be(18); // Lost 2 life
    }

    [Fact]
    public async Task ThoughtseizeEffect_CanChooseCreatures_UnlikeDuress()
    {
        // Arrange: unlike Duress, Thoughtseize can target creatures (nonland)
        Setup();
        var creature = new GameCard { Name = "Tarmogoyf", CardTypes = CardType.Creature };
        var instant = new GameCard { Name = "Lightning Bolt", CardTypes = CardType.Instant };
        _p2.Hand.Add(creature);
        _p2.Hand.Add(instant);

        // Caster picks the creature
        _h1.EnqueueCardChoice(creature.Id);

        var spell = CreateThoughtseizeSpell();
        var effect = new ThoughtseizeEffect();

        // Act
        await effect.ResolveAsync(_state, spell, _h1);

        // Assert
        _p2.Hand.Cards.Should().NotContain(c => c.Id == creature.Id);
        _p2.Graveyard.Cards.Should().Contain(c => c.Id == creature.Id);
        _p1.Life.Should().Be(18);
    }

    [Fact]
    public async Task ThoughtseizeEffect_FiltersOutLandsOnly()
    {
        // Arrange: lands are the only excluded card type
        Setup();
        var land = new GameCard { Name = "Forest", CardTypes = CardType.Land };
        var enchantment = new GameCard { Name = "Wild Growth", CardTypes = CardType.Enchantment };
        _p2.Hand.Add(land);
        _p2.Hand.Add(enchantment);

        // Caster picks the enchantment (only eligible card)
        _h1.EnqueueCardChoice(enchantment.Id);

        var spell = CreateThoughtseizeSpell();
        var effect = new ThoughtseizeEffect();

        // Act
        await effect.ResolveAsync(_state, spell, _h1);

        // Assert
        _p2.Hand.Cards.Should().NotContain(c => c.Id == enchantment.Id);
        _p2.Graveyard.Cards.Should().Contain(c => c.Id == enchantment.Id);
        _p2.Hand.Cards.Should().Contain(c => c.Id == land.Id);
        _p1.Life.Should().Be(18);
    }

    [Fact]
    public async Task ThoughtseizeEffect_StillLosesLife_WhenNoEligibleCards()
    {
        // Arrange: opponent has only lands
        Setup();
        var land1 = new GameCard { Name = "Swamp", CardTypes = CardType.Land };
        var land2 = new GameCard { Name = "Island", CardTypes = CardType.Land };
        _p2.Hand.Add(land1);
        _p2.Hand.Add(land2);

        var spell = CreateThoughtseizeSpell();
        var effect = new ThoughtseizeEffect();

        // Act
        await effect.ResolveAsync(_state, spell, _h1);

        // Assert: no cards discarded, but caster still loses 2 life
        _p2.Hand.Count.Should().Be(2);
        _p2.Graveyard.Count.Should().Be(0);
        _p1.Life.Should().Be(18);
    }

    // === FatalPushEffect Tests ===

    [Fact]
    public void FatalPushEffect_DestroysCreatureCmc2OrLess()
    {
        // Arrange: creature with CMC 2 on battlefield
        Setup();

        // Register a test creature with CMC 2 in CardDefinitions
        var testDef = new CardDefinition(ManaCost.Parse("{1}{G}"), null, 2, 2, CardType.Creature)
        { Name = "TestBear" };
        CardDefinitions.Register(testDef);

        try
        {
            var creature = new GameCard { Name = "TestBear", CardTypes = CardType.Creature };
            _p2.Battlefield.Add(creature);

            var spell = CreateFatalPushSpell(creature, _p2.Id);
            var effect = new FatalPushEffect();

            // Act
            effect.Resolve(_state, spell);

            // Assert: creature destroyed
            _p2.Battlefield.Cards.Should().NotContain(c => c.Id == creature.Id);
            _p2.Graveyard.Cards.Should().Contain(c => c.Id == creature.Id);
        }
        finally
        {
            CardDefinitions.Unregister("TestBear");
        }
    }

    [Fact]
    public void FatalPushEffect_FailsOnCreatureCmc3_WithoutRevolt()
    {
        // Arrange: creature with CMC 3, no revolt
        Setup();

        var testDef = new CardDefinition(ManaCost.Parse("{2}{G}"), null, 3, 3, CardType.Creature)
        { Name = "TestElk" };
        CardDefinitions.Register(testDef);

        try
        {
            var creature = new GameCard { Name = "TestElk", CardTypes = CardType.Creature };
            _p2.Battlefield.Add(creature);

            var spell = CreateFatalPushSpell(creature, _p2.Id);
            var effect = new FatalPushEffect();

            // Act
            effect.Resolve(_state, spell);

            // Assert: creature NOT destroyed (CMC 3 > max 2 without revolt)
            _p2.Battlefield.Cards.Should().Contain(c => c.Id == creature.Id);
            _p2.Graveyard.Cards.Should().NotContain(c => c.Id == creature.Id);
        }
        finally
        {
            CardDefinitions.Unregister("TestElk");
        }
    }

    [Fact]
    public void FatalPushEffect_DestroysCreatureCmc4_WithRevolt()
    {
        // Arrange: creature with CMC 4, revolt active
        Setup();

        var testDef = new CardDefinition(ManaCost.Parse("{3}{G}"), null, 4, 4, CardType.Creature)
        { Name = "TestRhino" };
        CardDefinitions.Register(testDef);

        try
        {
            var creature = new GameCard { Name = "TestRhino", CardTypes = CardType.Creature };
            _p2.Battlefield.Add(creature);

            // Enable revolt for caster
            _p1.PermanentLeftBattlefieldThisTurn = true;

            var spell = CreateFatalPushSpell(creature, _p2.Id);
            var effect = new FatalPushEffect();

            // Act
            effect.Resolve(_state, spell);

            // Assert: creature destroyed (CMC 4 <= max 4 with revolt)
            _p2.Battlefield.Cards.Should().NotContain(c => c.Id == creature.Id);
            _p2.Graveyard.Cards.Should().Contain(c => c.Id == creature.Id);
        }
        finally
        {
            CardDefinitions.Unregister("TestRhino");
        }
    }

    [Fact]
    public void FatalPushEffect_FailsOnCreatureCmc5_WithRevolt()
    {
        // Arrange: creature with CMC 5, revolt active (still too high)
        Setup();

        var testDef = new CardDefinition(ManaCost.Parse("{3}{G}{G}"), null, 5, 5, CardType.Creature)
        { Name = "TestWurm" };
        CardDefinitions.Register(testDef);

        try
        {
            var creature = new GameCard { Name = "TestWurm", CardTypes = CardType.Creature };
            _p2.Battlefield.Add(creature);

            _p1.PermanentLeftBattlefieldThisTurn = true;

            var spell = CreateFatalPushSpell(creature, _p2.Id);
            var effect = new FatalPushEffect();

            // Act
            effect.Resolve(_state, spell);

            // Assert: creature NOT destroyed (CMC 5 > max 4 even with revolt)
            _p2.Battlefield.Cards.Should().Contain(c => c.Id == creature.Id);
            _p2.Graveyard.Cards.Should().NotContain(c => c.Id == creature.Id);
        }
        finally
        {
            CardDefinitions.Unregister("TestWurm");
        }
    }

    [Fact]
    public void FatalPushEffect_TokensAreSentToGraveyard_ThenCeaseToExist()
    {
        // Arrange: token creature with no CMC on battlefield
        Setup();
        var token = new GameCard { Name = "Goblin", CardTypes = CardType.Creature, IsToken = true };
        _p2.Battlefield.Add(token);

        var spell = CreateFatalPushSpell(token, _p2.Id);
        var effect = new FatalPushEffect();

        // Act
        effect.Resolve(_state, spell);

        // Assert: token removed from battlefield, not added to graveyard (IsToken)
        _p2.Battlefield.Cards.Should().NotContain(c => c.Id == token.Id);
        _p2.Graveyard.Cards.Should().NotContain(c => c.Id == token.Id);
    }
}
