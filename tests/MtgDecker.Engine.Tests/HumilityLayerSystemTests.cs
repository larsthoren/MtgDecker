using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class HumilityLayerSystemTests
{
    [Fact]
    public void EffectLayer_HasExpectedValues()
    {
        ((int)EffectLayer.Layer4_TypeChanging).Should().Be(4);
        ((int)EffectLayer.Layer6_AbilityAddRemove).Should().Be(6);
        ((int)EffectLayer.Layer7a_CDA).Should().Be(70);
        ((int)EffectLayer.Layer7b_SetPT).Should().Be(71);
        ((int)EffectLayer.Layer7c_ModifyPT).Should().Be(72);
    }

    [Fact]
    public void ContinuousEffectType_HasNewValues()
    {
        var setPT = ContinuousEffectType.SetBasePowerToughness;
        var removeAbilities = ContinuousEffectType.RemoveAbilities;
        setPT.Should().NotBe(ContinuousEffectType.ModifyPowerToughness);
        removeAbilities.Should().NotBe(ContinuousEffectType.ModifyPowerToughness);
    }

    [Fact]
    public void ContinuousEffect_SupportsLayerAndTimestampFields()
    {
        var effect = new ContinuousEffect(
            Guid.NewGuid(),
            ContinuousEffectType.SetBasePowerToughness,
            (_, _) => true,
            Layer: EffectLayer.Layer7b_SetPT,
            Timestamp: 42,
            SetPower: 1,
            SetToughness: 1);

        effect.Layer.Should().Be(EffectLayer.Layer7b_SetPT);
        effect.Timestamp.Should().Be(42);
        effect.SetPower.Should().Be(1);
        effect.SetToughness.Should().Be(1);
    }

    [Fact]
    public void GameCard_HasAbilitiesRemovedField()
    {
        var card = new GameCard();
        card.AbilitiesRemoved.Should().BeFalse();
        card.AbilitiesRemoved = true;
        card.AbilitiesRemoved.Should().BeTrue();
    }

    [Fact]
    public void GameState_HasNextEffectTimestamp()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.NextEffectTimestamp.Should().Be(1);
        state.NextEffectTimestamp++;
        state.NextEffectTimestamp.Should().Be(2);
    }

    [Theory]
    [InlineData("Goblin King", ContinuousEffectType.ModifyPowerToughness, EffectLayer.Layer7c_ModifyPT)]
    [InlineData("Goblin King", ContinuousEffectType.GrantKeyword, EffectLayer.Layer6_AbilityAddRemove)]
    [InlineData("Goblin Warchief", ContinuousEffectType.GrantKeyword, EffectLayer.Layer6_AbilityAddRemove)]
    [InlineData("Deranged Hermit", ContinuousEffectType.ModifyPowerToughness, EffectLayer.Layer7c_ModifyPT)]
    [InlineData("Opalescence", ContinuousEffectType.BecomeCreature, EffectLayer.Layer4_TypeChanging)]
    [InlineData("Goblin Guide", ContinuousEffectType.GrantKeyword, EffectLayer.Layer6_AbilityAddRemove)]
    [InlineData("Nimble Mongoose", ContinuousEffectType.GrantKeyword, EffectLayer.Layer6_AbilityAddRemove)]
    [InlineData("Nimble Mongoose", ContinuousEffectType.ModifyPowerToughness, EffectLayer.Layer7c_ModifyPT)]
    [InlineData("Argothian Enchantress", ContinuousEffectType.GrantKeyword, EffectLayer.Layer6_AbilityAddRemove)]
    [InlineData("Sterling Grove", ContinuousEffectType.GrantKeyword, EffectLayer.Layer6_AbilityAddRemove)]
    public void CardDefinition_ContinuousEffects_HaveCorrectLayer(string cardName, ContinuousEffectType effectType, EffectLayer expectedLayer)
    {
        CardDefinitions.TryGet(cardName, out var def).Should().BeTrue($"{cardName} should exist");
        var matching = def!.ContinuousEffects.Where(e => e.Type == effectType).ToList();
        matching.Should().NotBeEmpty($"{cardName} should have {effectType} effect");
        matching.First().Layer.Should().Be(expectedLayer, $"{cardName}'s {effectType} should be in {expectedLayer}");
    }

    [Fact]
    public void CardDefinition_NonLayeredEffects_HaveNullLayer()
    {
        // Exploration's ExtraLandDrop effect should not have a layer
        CardDefinitions.TryGet("Exploration", out var def).Should().BeTrue();
        def!.ContinuousEffects.Should().ContainSingle();
        def.ContinuousEffects[0].Layer.Should().BeNull();

        // Solitary Confinement's effects should not have layers
        CardDefinitions.TryGet("Solitary Confinement", out var solDef).Should().BeTrue();
        solDef!.ContinuousEffects.Should().AllSatisfy(e => e.Layer.Should().BeNull());
    }

    [Fact]
    public void CardDefinition_GraveyardAbilities_HaveCorrectLayer()
    {
        CardDefinitions.TryGet("Anger", out var def).Should().BeTrue();
        def!.GraveyardAbilities.Should().ContainSingle();
        def.GraveyardAbilities[0].Layer.Should().Be(EffectLayer.Layer6_AbilityAddRemove);
    }

    [Fact]
    public void RecalculateState_AssignsTimestamps_InBattlefieldOrder()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // Add two lords — first added should have lower timestamp
        var king = GameCard.Create("Goblin King");
        var hermit = GameCard.Create("Deranged Hermit");
        p1.Battlefield.Add(king);
        p1.Battlefield.Add(hermit);

        engine.RecalculateState();

        var kingEffects = state.ActiveEffects.Where(e => e.SourceId == king.Id).ToList();
        var hermitEffects = state.ActiveEffects.Where(e => e.SourceId == hermit.Id).ToList();

        kingEffects.Should().NotBeEmpty();
        hermitEffects.Should().NotBeEmpty();

        // King was added first — should have lower timestamps
        kingEffects.Max(e => e.Timestamp).Should().BeLessThan(hermitEffects.Min(e => e.Timestamp));
    }

    [Fact]
    public void RecalculateState_ResetsNextEffectTimestamp_EachCall()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var king = GameCard.Create("Goblin King");
        p1.Battlefield.Add(king);

        engine.RecalculateState();
        var firstCallTimestamps = state.ActiveEffects.Select(e => e.Timestamp).ToList();

        engine.RecalculateState();
        var secondCallTimestamps = state.ActiveEffects.Select(e => e.Timestamp).ToList();

        // Timestamps should be identical across calls (counter resets)
        firstCallTimestamps.Should().BeEquivalentTo(secondCallTimestamps);
    }

    [Fact]
    public void Humility_MakesAllCreatures_1_1()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var humility = GameCard.Create("Humility");
        p1.Battlefield.Add(humility);

        var ball = GameCard.Create("Ball Lightning");
        var king = GameCard.Create("Goblin King");
        var baloth = GameCard.Create("Ravenous Baloth");
        p1.Battlefield.Add(ball);
        p1.Battlefield.Add(king);
        p2.Battlefield.Add(baloth);

        engine.RecalculateState();

        ball.Power.Should().Be(1);
        ball.Toughness.Should().Be(1);
        king.Power.Should().Be(1);
        king.Toughness.Should().Be(1);
        baloth.Power.Should().Be(1);
        baloth.Toughness.Should().Be(1);
    }

    [Fact]
    public void Humility_RemovesKeywords()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var humility = GameCard.Create("Humility");
        p1.Battlefield.Add(humility);

        var ball = GameCard.Create("Ball Lightning");
        var specter = GameCard.Create("Hypnotic Specter");
        p1.Battlefield.Add(ball);
        p1.Battlefield.Add(specter);

        engine.RecalculateState();

        ball.ActiveKeywords.Should().BeEmpty("Humility removes all abilities");
        specter.ActiveKeywords.Should().BeEmpty("Humility removes all abilities");
    }

    [Fact]
    public void Humility_SuppressesLordPTBuffs()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var humility = GameCard.Create("Humility");
        var king = GameCard.Create("Goblin King");
        var lackey = GameCard.Create("Goblin Lackey");
        p1.Battlefield.Add(humility);
        p1.Battlefield.Add(king);
        p1.Battlefield.Add(lackey);

        engine.RecalculateState();

        king.Power.Should().Be(1);
        king.Toughness.Should().Be(1);
        lackey.Power.Should().Be(1);
        lackey.Toughness.Should().Be(1);
    }

    [Fact]
    public void Humility_SuppressesLordKeywordGrants()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var humility = GameCard.Create("Humility");
        var king = GameCard.Create("Goblin King");
        var lackey = GameCard.Create("Goblin Lackey");
        p1.Battlefield.Add(humility);
        p1.Battlefield.Add(king);
        p1.Battlefield.Add(lackey);

        engine.RecalculateState();

        lackey.ActiveKeywords.Should().NotContain(Keyword.Mountainwalk);
    }

    [Fact]
    public void Humility_SuppressesCDA_Terravore()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var humility = GameCard.Create("Humility");
        var terravore = GameCard.Create("Terravore");
        p1.Battlefield.Add(humility);
        p1.Battlefield.Add(terravore);

        p1.Graveyard.Add(new GameCard { Name = "Forest", CardTypes = CardType.Land });
        p1.Graveyard.Add(new GameCard { Name = "Mountain", CardTypes = CardType.Land });
        p1.Graveyard.Add(new GameCard { Name = "Island", CardTypes = CardType.Land });

        engine.RecalculateState();

        terravore.Power.Should().Be(1);
        terravore.Toughness.Should().Be(1);
    }

    [Fact]
    public void Humility_NonCreatureEffectsStillWork()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var humility = GameCard.Create("Humility");
        var exploration = GameCard.Create("Exploration");
        p1.Battlefield.Add(humility);
        p1.Battlefield.Add(exploration);

        engine.RecalculateState();

        p1.MaxLandDrops.Should().Be(2);
    }

    [Fact]
    public void Humility_EnchantmentKeywordsStillWork()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var humility = GameCard.Create("Humility");
        var grove = GameCard.Create("Sterling Grove");
        var enchantress = GameCard.Create("Enchantress's Presence");
        p1.Battlefield.Add(humility);
        p1.Battlefield.Add(grove);
        p1.Battlefield.Add(enchantress);

        engine.RecalculateState();

        enchantress.ActiveKeywords.Should().Contain(Keyword.Shroud);
    }

    [Fact]
    public void Humility_GraveyardHaste_StillWorks()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var humility = GameCard.Create("Humility");
        p1.Battlefield.Add(humility);

        var anger = GameCard.Create("Anger");
        p1.Graveyard.Add(anger);
        var mountain = GameCard.Create("Mountain");
        p1.Battlefield.Add(mountain);

        var creature = GameCard.Create("Goblin Lackey");
        p1.Battlefield.Add(creature);

        engine.RecalculateState();

        creature.ActiveKeywords.Should().Contain(Keyword.Haste);
    }

    [Fact]
    public void Humility_PumpSpellStillWorks()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var humility = GameCard.Create("Humility");
        p1.Battlefield.Add(humility);

        var creature = GameCard.Create("Goblin Lackey");
        p1.Battlefield.Add(creature);

        // Simulate a +3/+3 pump effect (like Giant Growth — UntilEndOfTurn)
        state.ActiveEffects.Add(new ContinuousEffect(
            Guid.Empty,
            ContinuousEffectType.ModifyPowerToughness,
            (card, _) => card.Id == creature.Id,
            PowerMod: 3, ToughnessMod: 3,
            UntilEndOfTurn: true,
            Layer: EffectLayer.Layer7c_ModifyPT));

        engine.RecalculateState();

        // Base 1/1 (from Humility) + 3/3 pump = 4/4
        creature.Power.Should().Be(4);
        creature.Toughness.Should().Be(4);
    }

    [Fact]
    public void Humility_SuppressesBoardTriggers()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var humility = GameCard.Create("Humility");
        p1.Battlefield.Add(humility);

        var eidolon = GameCard.Create("Eidolon of the Great Revel");
        p1.Battlefield.Add(eidolon);

        engine.RecalculateState();

        var bolt = GameCard.Create("Lightning Bolt");
        var triggers = engine.CollectBoardTriggersForTest(GameEvent.SpellCast, bolt, p1);

        triggers.Should().BeEmpty("Eidolon has AbilitiesRemoved — its triggers are suppressed");
    }

    [Fact]
    public async Task Humility_SuppressesAttackTriggers()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var humility = GameCard.Create("Humility");
        p1.Battlefield.Add(humility);

        var guide = GameCard.Create("Goblin Guide");
        p1.Battlefield.Add(guide);
        state.ActivePlayer = p1;

        engine.RecalculateState();

        await engine.QueueAttackTriggersOnStackAsync(guide);

        state.StackCount.Should().Be(0, "Goblin Guide's attack trigger is suppressed by Humility");
    }

    [Fact]
    public async Task Humility_SuppressesEchoTriggers()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var humility = GameCard.Create("Humility");
        p1.Battlefield.Add(humility);

        var acolyte = GameCard.Create("Multani's Acolyte");
        acolyte.EchoPaid = false;
        p1.Battlefield.Add(acolyte);
        state.ActivePlayer = p1;

        engine.RecalculateState();

        await engine.QueueEchoTriggersOnStackAsync();

        state.StackCount.Should().Be(0, "Echo trigger is suppressed — creature lost all abilities");
    }

    [Fact]
    public async Task Humility_SuppressesSelfETBTriggers()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var humility = GameCard.Create("Humility");
        p1.Battlefield.Add(humility);

        var rager = GameCard.Create("Phyrexian Rager");
        p1.Battlefield.Add(rager);

        engine.RecalculateState();

        await engine.QueueSelfTriggersOnStackAsync(GameEvent.EnterBattlefield, rager, p1);

        state.StackCount.Should().Be(0, "ETB trigger is suppressed — creature has AbilitiesRemoved");
    }
}
