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
/// Tests for Task 12 Batch 2: Final 9 complex cards —
/// Assault/Battery, Ramosian Sergeant, Eternal Dragon, Phyrexian Dreadnought,
/// Decree of Silence, Dystopia, Cleansing Meditation, Wonder, Kirtar's Desire.
/// </summary>
public class ComplexCardsB2Tests
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

    private static StackObject CreateSpell(string name, Guid controllerId, List<TargetInfo> targets)
    {
        var card = GameCard.Create(name);
        return new StackObject(card, controllerId,
            new Dictionary<ManaColor, int>(), targets, 0);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CARD REGISTRATION TESTS
    // ═══════════════════════════════════════════════════════════════════

    #region Registration

    [Theory]
    [InlineData("Assault")]
    [InlineData("Battery")]
    [InlineData("Ramosian Sergeant")]
    [InlineData("Eternal Dragon")]
    [InlineData("Phyrexian Dreadnought")]
    [InlineData("Decree of Silence")]
    [InlineData("Dystopia")]
    [InlineData("Cleansing Meditation")]
    [InlineData("Wonder")]
    [InlineData("Kirtar's Desire")]
    public void Card_IsRegistered(string name)
    {
        CardDefinitions.TryGet(name, out var def).Should().BeTrue();
        def.Should().NotBeNull();
    }

    [Fact]
    public void Assault_HasCorrectProperties()
    {
        CardDefinitions.TryGet("Assault", out var def);
        def!.CardTypes.Should().Be(CardType.Sorcery);
        def.ManaCost!.ConvertedManaCost.Should().Be(1);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Red).WhoseValue.Should().Be(1);
        def.TargetFilter.Should().NotBeNull();
        def.Effect.Should().BeOfType<DamageEffect>();
    }

    [Fact]
    public void Battery_HasCorrectProperties()
    {
        CardDefinitions.TryGet("Battery", out var def);
        def!.CardTypes.Should().Be(CardType.Sorcery);
        def.ManaCost!.ConvertedManaCost.Should().Be(4);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Green).WhoseValue.Should().Be(1);
        def.Effect.Should().BeOfType<BatteryEffect>();
    }

    [Fact]
    public void RamosianSergeant_HasCorrectProperties()
    {
        CardDefinitions.TryGet("Ramosian Sergeant", out var def);
        def!.CardTypes.Should().Be(CardType.Creature);
        def.ManaCost!.ConvertedManaCost.Should().Be(1);
        def.Power.Should().Be(1);
        def.Toughness.Should().Be(1);
        def.Subtypes.Should().Contain("Human").And.Contain("Rebel");
        def.ActivatedAbilities.Should().HaveCount(1);
        def.ActivatedAbilities[0].Cost.TapSelf.Should().BeTrue();
        def.ActivatedAbilities[0].Cost.ManaCost!.ConvertedManaCost.Should().Be(3);
    }

    [Fact]
    public void EternalDragon_HasCorrectProperties()
    {
        CardDefinitions.TryGet("Eternal Dragon", out var def);
        def!.CardTypes.Should().Be(CardType.Creature);
        def.ManaCost!.ConvertedManaCost.Should().Be(7);
        def.Power.Should().Be(5);
        def.Toughness.Should().Be(5);
        def.Subtypes.Should().Contain("Dragon").And.Contain("Spirit");
        def.CyclingCost.Should().NotBeNull();
        def.CyclingCost!.ConvertedManaCost.Should().Be(2);
        def.CyclingReplaceDraw.Should().BeTrue();
        def.CyclingTriggers.Should().HaveCount(1);
    }

    [Fact]
    public void PhyrexianDreadnought_HasCorrectProperties()
    {
        CardDefinitions.TryGet("Phyrexian Dreadnought", out var def);
        def!.CardTypes.Should().HaveFlag(CardType.Artifact);
        def.CardTypes.Should().HaveFlag(CardType.Creature);
        def.ManaCost!.ConvertedManaCost.Should().Be(1);
        def.Power.Should().Be(12);
        def.Toughness.Should().Be(12);
        def.Triggers.Should().HaveCount(1);
        def.Triggers[0].Event.Should().Be(GameEvent.EnterBattlefield);
    }

    [Fact]
    public void DecreeOfSilence_HasCorrectProperties()
    {
        CardDefinitions.TryGet("Decree of Silence", out var def);
        def!.CardTypes.Should().Be(CardType.Enchantment);
        def.ManaCost!.ConvertedManaCost.Should().Be(8);
        def.Triggers.Should().HaveCount(1);
        def.Triggers[0].Condition.Should().Be(TriggerCondition.OpponentCastsAnySpell);
        def.CyclingCost.Should().NotBeNull();
        def.CyclingCost!.ConvertedManaCost.Should().Be(6);
        def.CyclingReplaceDraw.Should().BeTrue();
    }

    [Fact]
    public void Dystopia_HasCorrectProperties()
    {
        CardDefinitions.TryGet("Dystopia", out var def);
        def!.CardTypes.Should().Be(CardType.Enchantment);
        def.ManaCost!.ConvertedManaCost.Should().Be(3);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Black).WhoseValue.Should().Be(2);
        def.Triggers.Should().HaveCount(2);
    }

    [Fact]
    public void CleansingMeditation_HasCorrectProperties()
    {
        CardDefinitions.TryGet("Cleansing Meditation", out var def);
        def!.CardTypes.Should().Be(CardType.Sorcery);
        def.ManaCost!.ConvertedManaCost.Should().Be(3);
        def.Effect.Should().BeOfType<CleansingMeditationEffect>();
    }

    [Fact]
    public void Wonder_HasCorrectProperties()
    {
        CardDefinitions.TryGet("Wonder", out var def);
        def!.CardTypes.Should().Be(CardType.Creature);
        def.ManaCost!.ConvertedManaCost.Should().Be(4);
        def.Power.Should().Be(2);
        def.Toughness.Should().Be(2);
        def.Subtypes.Should().Contain("Incarnation");
        def.GraveyardAbilities.Should().HaveCount(1);
    }

    [Fact]
    public void KirtarsDesire_HasCorrectProperties()
    {
        CardDefinitions.TryGet("Kirtar's Desire", out var def);
        def!.CardTypes.Should().Be(CardType.Enchantment);
        def.ManaCost!.ConvertedManaCost.Should().Be(1);
        def.Subtypes.Should().Contain("Aura");
        def.AuraTarget.Should().Be(AuraTarget.Creature);
        def.DynamicContinuousEffectsFactory.Should().NotBeNull();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    // ASSAULT // BATTERY TESTS
    // ═══════════════════════════════════════════════════════════════════

    #region Assault // Battery

    [Fact]
    public void Assault_Deals2DamageToCreature()
    {
        var (state, p1, p2) = CreateGameState();
        var target = GameCard.Create("Grizzly Bears");
        target.BasePower = 2;
        target.BaseToughness = 2;
        p2.Battlefield.Add(target);

        var spell = CreateSpell("Assault", p1.Id,
            [new TargetInfo(target.Id, p2.Id, ZoneType.Battlefield)]);

        CardDefinitions.TryGet("Assault", out var def);
        def!.Effect!.Resolve(state, spell);

        target.DamageMarked.Should().Be(2);
    }

    [Fact]
    public void Assault_Deals2DamageToPlayer()
    {
        var (state, p1, p2) = CreateGameState();

        var spell = CreateSpell("Assault", p1.Id,
            [new TargetInfo(Guid.Empty, p2.Id, ZoneType.None)]);

        CardDefinitions.TryGet("Assault", out var def);
        def!.Effect!.Resolve(state, spell);

        p2.Life.Should().Be(18); // 20 - 2
    }

    [Fact]
    public void Battery_Creates3x3ElephantToken()
    {
        var (state, p1, _) = CreateGameState();

        var spell = CreateSpell("Battery", p1.Id, []);

        CardDefinitions.TryGet("Battery", out var def);
        def!.Effect!.Resolve(state, spell);

        p1.Battlefield.Cards.Should().ContainSingle(c => c.Name == "Elephant");
        var token = p1.Battlefield.Cards.First(c => c.Name == "Elephant");
        token.Power.Should().Be(3);
        token.Toughness.Should().Be(3);
        token.IsToken.Should().BeTrue();
        token.Subtypes.Should().Contain("Elephant");
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    // RAMOSIAN SERGEANT TESTS
    // ═══════════════════════════════════════════════════════════════════

    #region Ramosian Sergeant

    [Fact]
    public async Task RamosianSergeant_SearchesForRebelCMC2OrLess()
    {
        var (state, _, h1, _) = CreateEngineState();
        var p1 = state.Player1;

        // Put a Rebel with CMC 2 in library
        var rebel = new GameCard
        {
            Name = "Lin Sivvi, Defiant Hero",
            ManaCost = ManaCost.Parse("{1}{W}{W}"),
            CardTypes = CardType.Creature,
            Subtypes = ["Human", "Rebel"],
            BasePower = 1,
            BaseToughness = 3,
        };
        p1.Library.Add(rebel);

        // Create a low-cost rebel
        var lowRebel = new GameCard
        {
            Name = "Defiant Falcon",
            ManaCost = ManaCost.Parse("{1}{W}"),
            CardTypes = CardType.Creature,
            Subtypes = ["Rebel"],
            BasePower = 1,
            BaseToughness = 1,
        };
        p1.Library.Add(lowRebel);

        var effect = new SearchLibraryToBattlefieldEffect("Rebel", maxCmc: 2);
        var sergeant = GameCard.Create("Ramosian Sergeant");

        h1.EnqueueCardChoice(lowRebel.Id); // Choose the CMC 2 rebel

        var context = new EffectContext(state, p1, sergeant, h1);
        await effect.Execute(context);

        // Low rebel should be on battlefield, high rebel still in library
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Defiant Falcon");
        p1.Library.Cards.Should().NotContain(c => c.Name == "Defiant Falcon");
        // The CMC 3 rebel should NOT be offered (filtered out)
    }

    [Fact]
    public async Task SearchLibraryToBattlefieldEffect_FiltersOutHighCMC()
    {
        var (state, _, h1, _) = CreateEngineState();
        var p1 = state.Player1;

        // Only a CMC 3 rebel in library
        var highRebel = new GameCard
        {
            Name = "Lin Sivvi",
            ManaCost = ManaCost.Parse("{1}{W}{W}"),
            CardTypes = CardType.Creature,
            Subtypes = ["Human", "Rebel"],
        };
        p1.Library.Add(highRebel);

        var effect = new SearchLibraryToBattlefieldEffect("Rebel", maxCmc: 2);
        var source = GameCard.Create("Ramosian Sergeant");

        var context = new EffectContext(state, p1, source, h1);
        await effect.Execute(context);

        // No rebel should be found (CMC 3 exceeds max 2)
        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Lin Sivvi");
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    // ETERNAL DRAGON TESTS
    // ═══════════════════════════════════════════════════════════════════

    #region Eternal Dragon

    [Fact]
    public async Task EternalDragon_Plainscycling_SearchesForPlains()
    {
        var (state, _, h1, _) = CreateEngineState();
        var p1 = state.Player1;

        var plains = new GameCard
        {
            Name = "Plains",
            CardTypes = CardType.Land,
            Subtypes = ["Plains"],
        };
        p1.Library.Add(plains);

        var effect = new PlainscyclingEffect("Plains");
        var source = GameCard.Create("Eternal Dragon");

        h1.EnqueueCardChoice(plains.Id);

        var context = new EffectContext(state, p1, source, h1);
        await effect.Execute(context);

        p1.Hand.Cards.Should().Contain(c => c.Name == "Plains");
        p1.Library.Cards.Should().NotContain(c => c.Name == "Plains");
    }

    [Fact]
    public async Task EternalDragon_GraveyardReturn_PaysManaThenReturnsToHand()
    {
        var (state, _, h1, _) = CreateEngineState();
        var p1 = state.Player1;

        var dragon = GameCard.Create("Eternal Dragon");
        p1.Graveyard.Add(dragon);

        // Give player enough mana
        p1.ManaPool.Add(ManaColor.White, 2);
        p1.ManaPool.Add(ManaColor.Colorless, 3);

        h1.EnqueueCardChoice(dragon.Id); // Accept the return

        var cost = ManaCost.Parse("{3}{W}{W}");
        var effect = new ReturnSelfForManaEffect(cost);

        var context = new EffectContext(state, p1, dragon, h1);
        await effect.Execute(context);

        p1.Hand.Cards.Should().Contain(c => c.Name == "Eternal Dragon");
        p1.Graveyard.Cards.Should().NotContain(c => c.Name == "Eternal Dragon");
    }

    [Fact]
    public async Task EternalDragon_GraveyardReturn_FailsWithoutMana()
    {
        var (state, _, h1, _) = CreateEngineState();
        var p1 = state.Player1;

        var dragon = GameCard.Create("Eternal Dragon");
        p1.Graveyard.Add(dragon);

        // No mana available

        var cost = ManaCost.Parse("{3}{W}{W}");
        var effect = new ReturnSelfForManaEffect(cost);

        var context = new EffectContext(state, p1, dragon, h1);
        await effect.Execute(context);

        // Should still be in graveyard
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Eternal Dragon");
        p1.Hand.Cards.Should().NotContain(c => c.Name == "Eternal Dragon");
    }

    [Fact]
    public void EternalDragon_CyclingReplacesDraw()
    {
        CardDefinitions.TryGet("Eternal Dragon", out var def);
        def!.CyclingReplaceDraw.Should().BeTrue();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    // PHYREXIAN DREADNOUGHT TESTS
    // ═══════════════════════════════════════════════════════════════════

    #region Phyrexian Dreadnought

    [Fact]
    public async Task PhyrexianDreadnought_SacrificesItselfWhenNoCreatures()
    {
        var (state, _, h1, _) = CreateEngineState();
        var p1 = state.Player1;

        var dreadnought = GameCard.Create("Phyrexian Dreadnought");
        p1.Battlefield.Add(dreadnought);

        var effect = new DreadnoughtETBEffect();
        var context = new EffectContext(state, p1, dreadnought, h1);
        await effect.Execute(context);

        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Phyrexian Dreadnought");
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Phyrexian Dreadnought");
    }

    [Fact]
    public async Task PhyrexianDreadnought_SacrificesCreaturesWithEnoughPower()
    {
        var (state, _, h1, _) = CreateEngineState();
        var p1 = state.Player1;

        var dreadnought = GameCard.Create("Phyrexian Dreadnought");
        p1.Battlefield.Add(dreadnought);

        // Add creatures with total power >= 12
        var big1 = new GameCard { Name = "Big Creature 1", BasePower = 7, BaseToughness = 7, CardTypes = CardType.Creature };
        var big2 = new GameCard { Name = "Big Creature 2", BasePower = 6, BaseToughness = 6, CardTypes = CardType.Creature };
        p1.Battlefield.Add(big1);
        p1.Battlefield.Add(big2);

        h1.EnqueueCardChoice(big1.Id); // Choose first creature (power 7)
        h1.EnqueueCardChoice(big2.Id); // Choose second creature (power 6, total 13 >= 12)

        var effect = new DreadnoughtETBEffect();
        var context = new EffectContext(state, p1, dreadnought, h1);
        await effect.Execute(context);

        // Dreadnought stays, sacrificed creatures are in graveyard
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Phyrexian Dreadnought");
        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Big Creature 1");
        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Big Creature 2");
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Big Creature 1");
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Big Creature 2");
    }

    [Fact]
    public async Task PhyrexianDreadnought_SacrificesItselfWhenPlayerDeclines()
    {
        var (state, _, h1, _) = CreateEngineState();
        var p1 = state.Player1;

        var dreadnought = GameCard.Create("Phyrexian Dreadnought");
        p1.Battlefield.Add(dreadnought);

        var big1 = new GameCard { Name = "Big Creature", BasePower = 14, BaseToughness = 14, CardTypes = CardType.Creature };
        p1.Battlefield.Add(big1);

        h1.EnqueueCardChoice((Guid?)null); // Player declines to sacrifice

        var effect = new DreadnoughtETBEffect();
        var context = new EffectContext(state, p1, dreadnought, h1);
        await effect.Execute(context);

        // Dreadnought is sacrificed, big creature survives
        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Phyrexian Dreadnought");
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Big Creature");
    }

    [Fact]
    public async Task PhyrexianDreadnought_InsufficientCreaturePower_SacrificesItself()
    {
        var (state, _, h1, _) = CreateEngineState();
        var p1 = state.Player1;

        var dreadnought = GameCard.Create("Phyrexian Dreadnought");
        p1.Battlefield.Add(dreadnought);

        // Only 5 total power from other creatures
        var small = new GameCard { Name = "Small Creature", BasePower = 5, BaseToughness = 5, CardTypes = CardType.Creature };
        p1.Battlefield.Add(small);

        var effect = new DreadnoughtETBEffect();
        var context = new EffectContext(state, p1, dreadnought, h1);
        await effect.Execute(context);

        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Phyrexian Dreadnought");
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Small Creature");
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    // DECREE OF SILENCE TESTS
    // ═══════════════════════════════════════════════════════════════════

    #region Decree of Silence

    [Fact]
    public void DecreeOfSilence_CountersSpellAndAddsDepletionCounter()
    {
        var (state, p1, p2) = CreateGameState();

        var decree = GameCard.Create("Decree of Silence");
        p1.Battlefield.Add(decree);

        // Put an opponent's spell on the stack
        var opponentSpell = GameCard.Create("Lightning Bolt");
        state.StackPush(new StackObject(opponentSpell, p2.Id,
            new Dictionary<ManaColor, int>(),
            [new TargetInfo(Guid.Empty, p1.Id, ZoneType.None)], 0));

        var effect = new DecreeOfSilenceEffect();
        var context = new EffectContext(state, p1, decree, (TestDecisionHandler)p1.DecisionHandler);
        effect.Execute(context);

        // Spell should be countered (removed from stack, in graveyard)
        state.Stack.OfType<StackObject>().Should().NotContain(s => s.Card.Name == "Lightning Bolt");
        p2.Graveyard.Cards.Should().Contain(c => c.Name == "Lightning Bolt");

        // Decree should have 1 depletion counter
        decree.GetCounters(CounterType.Depletion).Should().Be(1);
    }

    [Fact]
    public void DecreeOfSilence_SacrificesAfter3Counters()
    {
        var (state, p1, p2) = CreateGameState();

        var decree = GameCard.Create("Decree of Silence");
        decree.AddCounters(CounterType.Depletion, 2); // Already has 2
        p1.Battlefield.Add(decree);

        // Put an opponent's spell on the stack
        var spell = GameCard.Create("Shock");
        state.StackPush(new StackObject(spell, p2.Id,
            new Dictionary<ManaColor, int>(), [], 0));

        var effect = new DecreeOfSilenceEffect();
        var context = new EffectContext(state, p1, decree, (TestDecisionHandler)p1.DecisionHandler);
        effect.Execute(context);

        // Counter 3 => sacrifice
        decree.GetCounters(CounterType.Depletion).Should().Be(3);
        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Decree of Silence");
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Decree of Silence");
    }

    [Fact]
    public async Task DecreeOfSilence_CyclingCountersSpell_WhenPlayerAccepts()
    {
        var (state, _, h1, _) = CreateEngineState();
        var p1 = state.Player1;
        var p2 = state.Player2;

        // Put an opponent's spell on the stack
        var opponentSpell = GameCard.Create("Counterspell");
        state.StackPush(new StackObject(opponentSpell, p2.Id,
            new Dictionary<ManaColor, int>(), [], 0));

        // Player chooses to counter it
        h1.EnqueueCardChoice(opponentSpell.Id);

        var source = GameCard.Create("Decree of Silence");
        var effect = new CounterTopSpellEffect();
        var context = new EffectContext(state, p1, source, h1);
        await effect.Execute(context);

        state.Stack.OfType<StackObject>().Should().NotContain(s => s.Card.Name == "Counterspell");
        p2.Graveyard.Cards.Should().Contain(c => c.Name == "Counterspell");
    }

    [Fact]
    public async Task DecreeOfSilence_CyclingDoesNotCounter_WhenPlayerDeclines()
    {
        var (state, _, h1, _) = CreateEngineState();
        var p1 = state.Player1;
        var p2 = state.Player2;

        // Put an opponent's spell on the stack
        var opponentSpell = GameCard.Create("Counterspell");
        state.StackPush(new StackObject(opponentSpell, p2.Id,
            new Dictionary<ManaColor, int>(), [], 0));

        // Player declines to counter
        h1.EnqueueCardChoice(null);

        var source = GameCard.Create("Decree of Silence");
        var effect = new CounterTopSpellEffect();
        var context = new EffectContext(state, p1, source, h1);
        await effect.Execute(context);

        // Spell should still be on the stack (not countered)
        state.Stack.OfType<StackObject>().Should().Contain(s => s.Card.Name == "Counterspell");
        p2.Graveyard.Cards.Should().NotContain(c => c.Name == "Counterspell");
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    // DYSTOPIA TESTS
    // ═══════════════════════════════════════════════════════════════════

    #region Dystopia

    [Fact]
    public async Task Dystopia_CumulativeUpkeep_AddsAgeCounter()
    {
        var (state, _, h1, _) = CreateEngineState();
        var p1 = state.Player1;

        var dystopia = GameCard.Create("Dystopia");
        p1.Battlefield.Add(dystopia);

        h1.EnqueueCardChoice(dystopia.Id); // Pay the upkeep

        var effect = new DystopiaUpkeepEffect();
        var context = new EffectContext(state, p1, dystopia, h1);
        await effect.Execute(context);

        dystopia.GetCounters(CounterType.Age).Should().Be(1);
        p1.Life.Should().Be(19); // Paid 1 life
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Dystopia");
    }

    [Fact]
    public async Task Dystopia_CumulativeUpkeep_CostIncreasesEachTurn()
    {
        var (state, _, h1, _) = CreateEngineState();
        var p1 = state.Player1;

        var dystopia = GameCard.Create("Dystopia");
        dystopia.AddCounters(CounterType.Age, 2); // Already 2 age counters
        p1.Battlefield.Add(dystopia);

        h1.EnqueueCardChoice(dystopia.Id); // Pay

        var effect = new DystopiaUpkeepEffect();
        var context = new EffectContext(state, p1, dystopia, h1);
        await effect.Execute(context);

        dystopia.GetCounters(CounterType.Age).Should().Be(3);
        p1.Life.Should().Be(17); // Paid 3 life
    }

    [Fact]
    public async Task Dystopia_CumulativeUpkeep_SacrificesWhenNotPaid()
    {
        var (state, _, h1, _) = CreateEngineState();
        var p1 = state.Player1;

        var dystopia = GameCard.Create("Dystopia");
        p1.Battlefield.Add(dystopia);

        h1.EnqueueCardChoice((Guid?)null); // Decline to pay

        var effect = new DystopiaUpkeepEffect();
        var context = new EffectContext(state, p1, dystopia, h1);
        await effect.Execute(context);

        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Dystopia");
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Dystopia");
    }

    [Fact]
    public async Task Dystopia_SacrificeGreenOrWhitePermanent()
    {
        var (state, _, h1, _) = CreateEngineState();
        var p1 = state.Player1;
        state.ActivePlayer = p1;

        var greenCreature = new GameCard
        {
            Name = "Llanowar Elves",
            ManaCost = ManaCost.Parse("{G}"),
            CardTypes = CardType.Creature,
        };
        p1.Battlefield.Add(greenCreature);

        var source = GameCard.Create("Dystopia");
        h1.EnqueueCardChoice(greenCreature.Id); // Choose to sacrifice

        var effect = new DystopiaSacrificeEffect();
        var context = new EffectContext(state, p1, source, h1);
        await effect.Execute(context);

        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Llanowar Elves");
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Llanowar Elves");
    }

    [Fact]
    public async Task Dystopia_NoGreenOrWhitePermanent_NothingSacrificed()
    {
        var (state, _, h1, _) = CreateEngineState();
        var p1 = state.Player1;
        state.ActivePlayer = p1;

        var redCreature = new GameCard
        {
            Name = "Goblin Guide",
            ManaCost = ManaCost.Parse("{R}"),
            CardTypes = CardType.Creature,
        };
        p1.Battlefield.Add(redCreature);

        var source = GameCard.Create("Dystopia");

        var effect = new DystopiaSacrificeEffect();
        var context = new EffectContext(state, p1, source, h1);
        await effect.Execute(context);

        // Red creature should still be on battlefield
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Goblin Guide");
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    // CLEANSING MEDITATION TESTS
    // ═══════════════════════════════════════════════════════════════════

    #region Cleansing Meditation

    [Fact]
    public void CleansingMeditation_DestroysAllEnchantments()
    {
        var (state, p1, p2) = CreateGameState();

        var ench1 = new GameCard { Name = "Enchantment A", CardTypes = CardType.Enchantment };
        var ench2 = new GameCard { Name = "Enchantment B", CardTypes = CardType.Enchantment };
        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p1.Battlefield.Add(ench1);
        p2.Battlefield.Add(ench2);
        p1.Battlefield.Add(creature);

        var spell = CreateSpell("Cleansing Meditation", p1.Id, []);
        CardDefinitions.TryGet("Cleansing Meditation", out var def);
        def!.Effect!.Resolve(state, spell);

        // Both enchantments destroyed
        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Enchantment A");
        p2.Battlefield.Cards.Should().NotContain(c => c.Name == "Enchantment B");
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Enchantment A");
        p2.Graveyard.Cards.Should().Contain(c => c.Name == "Enchantment B");

        // Non-enchantment survives
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Bear");
    }

    [Fact]
    public void CleansingMeditation_WithThreshold_ReturnsControllerEnchantments()
    {
        var (state, p1, p2) = CreateGameState();

        // Give P1 threshold (7 cards in graveyard)
        for (int i = 0; i < 7; i++)
            p1.Graveyard.Add(new GameCard { Name = $"Junk {i}", CardTypes = CardType.Creature });

        var ench1 = new GameCard { Name = "Enchantment A", CardTypes = CardType.Enchantment };
        var ench2 = new GameCard { Name = "Enchantment B", CardTypes = CardType.Enchantment };
        p1.Battlefield.Add(ench1);
        p2.Battlefield.Add(ench2);

        var spell = CreateSpell("Cleansing Meditation", p1.Id, []);
        CardDefinitions.TryGet("Cleansing Meditation", out var def);
        def!.Effect!.Resolve(state, spell);

        // P1's enchantment should be back on battlefield (threshold return)
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Enchantment A");
        // P2's enchantment stays in graveyard
        p2.Graveyard.Cards.Should().Contain(c => c.Name == "Enchantment B");
        p2.Battlefield.Cards.Should().NotContain(c => c.Name == "Enchantment B");
    }

    [Fact]
    public void CleansingMeditation_WithThreshold_AlsoReturnsGraveyardEnchantments()
    {
        var (state, p1, _) = CreateGameState();

        // Give P1 threshold
        for (int i = 0; i < 7; i++)
            p1.Graveyard.Add(new GameCard { Name = $"Junk {i}", CardTypes = CardType.Creature });

        // An enchantment already in graveyard
        var oldEnch = new GameCard { Name = "Old Enchantment", CardTypes = CardType.Enchantment };
        p1.Graveyard.Add(oldEnch);

        // An enchantment on battlefield
        var currentEnch = new GameCard { Name = "Current Enchantment", CardTypes = CardType.Enchantment };
        p1.Battlefield.Add(currentEnch);

        var spell = CreateSpell("Cleansing Meditation", p1.Id, []);
        CardDefinitions.TryGet("Cleansing Meditation", out var def);
        def!.Effect!.Resolve(state, spell);

        // Both enchantments should be on battlefield now
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Current Enchantment");
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Old Enchantment");
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    // WONDER TESTS
    // ═══════════════════════════════════════════════════════════════════

    #region Wonder

    [Fact]
    public void Wonder_InGraveyard_WithIsland_GrantsFlying()
    {
        var (state, engine, h1, _) = CreateEngineState();
        var p1 = state.Player1;

        // Put Wonder in graveyard
        var wonder = GameCard.Create("Wonder");
        p1.Graveyard.Add(wonder);

        // Control an Island
        var island = GameCard.Create("Island");
        p1.Battlefield.Add(island);

        // A creature on the battlefield
        var creature = new GameCard
        {
            Name = "Grizzly Bears",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        p1.Battlefield.Add(creature);

        engine.RecalculateState();

        creature.ActiveKeywords.Should().Contain(Keyword.Flying);
    }

    [Fact]
    public void Wonder_InGraveyard_WithoutIsland_NoFlying()
    {
        var (state, engine, _, _) = CreateEngineState();
        var p1 = state.Player1;

        var wonder = GameCard.Create("Wonder");
        p1.Graveyard.Add(wonder);

        // No Island on battlefield
        var mountain = GameCard.Create("Mountain");
        p1.Battlefield.Add(mountain);

        var creature = new GameCard
        {
            Name = "Grizzly Bears",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        p1.Battlefield.Add(creature);

        engine.RecalculateState();

        creature.ActiveKeywords.Should().NotContain(Keyword.Flying);
    }

    [Fact]
    public void Wonder_OnBattlefield_DoesNotGrantOthersFlying()
    {
        var (state, engine, _, _) = CreateEngineState();
        var p1 = state.Player1;

        // Wonder on battlefield (not in graveyard)
        var wonder = GameCard.Create("Wonder");
        wonder.TurnEnteredBattlefield = state.TurnNumber;
        p1.Battlefield.Add(wonder);

        var island = GameCard.Create("Island");
        p1.Battlefield.Add(island);

        var creature = new GameCard
        {
            Name = "Grizzly Bears",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        p1.Battlefield.Add(creature);

        engine.RecalculateState();

        // Grizzly Bears should NOT have flying (Wonder is on BF, not in GY)
        // Wonder itself has flying keyword though (intrinsic)
        creature.ActiveKeywords.Should().NotContain(Keyword.Flying);
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    // KIRTAR'S DESIRE TESTS
    // ═══════════════════════════════════════════════════════════════════

    #region Kirtar's Desire

    [Fact]
    public void KirtarsDesire_PreventsAttacking()
    {
        var (state, engine, _, _) = CreateEngineState();
        var p1 = state.Player1;

        var creature = new GameCard
        {
            Name = "Elite Vanguard",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 1,
            TurnEnteredBattlefield = 0, // No summoning sickness
        };
        p1.Battlefield.Add(creature);

        var desire = GameCard.Create("Kirtar's Desire");
        desire.AttachedTo = creature.Id;
        p1.Battlefield.Add(desire);

        engine.RecalculateState();

        // Verify PreventCreatureAttacks effect exists targeting the creature
        var preventEffect = state.ActiveEffects
            .Where(e => e.Type == ContinuousEffectType.PreventCreatureAttacks)
            .ToList();
        preventEffect.Should().HaveCountGreaterThanOrEqualTo(1);
        preventEffect.Should().Contain(e => e.Applies(creature, p1));
    }

    [Fact]
    public void KirtarsDesire_WithThreshold_AlsoPreventsBlocking()
    {
        var (state, engine, _, _) = CreateEngineState();
        var p1 = state.Player1;

        // Give P1 threshold
        for (int i = 0; i < 7; i++)
            p1.Graveyard.Add(new GameCard { Name = $"Junk {i}", CardTypes = CardType.Creature });

        var creature = new GameCard
        {
            Name = "Wall of Blossoms",
            CardTypes = CardType.Creature,
            BasePower = 0,
            BaseToughness = 4,
        };
        p1.Battlefield.Add(creature);

        var desire = GameCard.Create("Kirtar's Desire");
        desire.AttachedTo = creature.Id;
        p1.Battlefield.Add(desire);

        engine.RecalculateState();

        // Should also have PreventCreatureBlocking
        var blockEffect = state.ActiveEffects
            .Where(e => e.Type == ContinuousEffectType.PreventCreatureBlocking)
            .ToList();
        blockEffect.Should().HaveCountGreaterThanOrEqualTo(1);
        blockEffect.Should().Contain(e => e.Applies(creature, p1));
    }

    [Fact]
    public void KirtarsDesire_WithoutThreshold_CanStillBlock()
    {
        var (state, engine, _, _) = CreateEngineState();
        var p1 = state.Player1;

        // Only 3 cards in graveyard (no threshold)
        for (int i = 0; i < 3; i++)
            p1.Graveyard.Add(new GameCard { Name = $"Junk {i}", CardTypes = CardType.Creature });

        var creature = new GameCard
        {
            Name = "Wall of Blossoms",
            CardTypes = CardType.Creature,
            BasePower = 0,
            BaseToughness = 4,
        };
        p1.Battlefield.Add(creature);

        var desire = GameCard.Create("Kirtar's Desire");
        desire.AttachedTo = creature.Id;
        p1.Battlefield.Add(desire);

        engine.RecalculateState();

        // PreventCreatureBlocking should NOT be active (StateCondition fails without threshold)
        var activeBlockEffects = state.ActiveEffects
            .Where(e => e.Type == ContinuousEffectType.PreventCreatureBlocking
                && e.Applies(creature, p1)
                && (e.StateCondition == null || e.StateCondition(state)))
            .ToList();
        activeBlockEffects.Should().BeEmpty();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    // CYCLING REPLACE DRAW TESTS
    // ═══════════════════════════════════════════════════════════════════

    #region CyclingReplaceDraw

    [Fact]
    public void CyclingReplaceDraw_DefaultIsFalse()
    {
        // Normal cycling cards should not replace draw
        CardDefinitions.TryGet("Gempalm Incinerator", out var def);
        def!.CyclingReplaceDraw.Should().BeFalse();
    }

    [Fact]
    public void CyclingReplaceDraw_TrueForPlainscycling()
    {
        CardDefinitions.TryGet("Eternal Dragon", out var def);
        def!.CyclingReplaceDraw.Should().BeTrue();
    }

    [Fact]
    public void CyclingReplaceDraw_TrueForDecreeOfSilence()
    {
        CardDefinitions.TryGet("Decree of Silence", out var def);
        def!.CyclingReplaceDraw.Should().BeTrue();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    // INTEGRATION TESTS
    // ═══════════════════════════════════════════════════════════════════

    #region Integration

    [Fact]
    public void PhyrexianDreadnought_HasTrample()
    {
        var (state, engine, _, _) = CreateEngineState();
        var p1 = state.Player1;

        var dreadnought = GameCard.Create("Phyrexian Dreadnought");
        dreadnought.TurnEnteredBattlefield = state.TurnNumber;
        p1.Battlefield.Add(dreadnought);

        engine.RecalculateState();

        dreadnought.ActiveKeywords.Should().Contain(Keyword.Trample);
    }

    [Fact]
    public void EternalDragon_HasFlying()
    {
        var (state, engine, _, _) = CreateEngineState();
        var p1 = state.Player1;

        var dragon = GameCard.Create("Eternal Dragon");
        dragon.TurnEnteredBattlefield = state.TurnNumber;
        p1.Battlefield.Add(dragon);

        engine.RecalculateState();

        dragon.ActiveKeywords.Should().Contain(Keyword.Flying);
    }

    [Fact]
    public void Wonder_OnBattlefield_HasFlyingItself()
    {
        var (state, engine, _, _) = CreateEngineState();
        var p1 = state.Player1;

        var wonder = GameCard.Create("Wonder");
        wonder.TurnEnteredBattlefield = state.TurnNumber;
        p1.Battlefield.Add(wonder);

        engine.RecalculateState();

        wonder.ActiveKeywords.Should().Contain(Keyword.Flying);
    }

    #endregion
}
