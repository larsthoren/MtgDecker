using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class Phase11MechanicsTests
{
    // ========== Prowess Tests ==========

    [Fact]
    public void MonasterySwiftspear_HasHaste()
    {
        CardDefinitions.TryGet("Monastery Swiftspear", out var def);

        def.Should().NotBeNull();
        def!.ContinuousEffects.Should().Contain(e =>
            e.Type == ContinuousEffectType.GrantKeyword
            && e.GrantedKeyword == Keyword.Haste);
    }

    [Fact]
    public void MonasterySwiftspear_HasProwessTrigger()
    {
        CardDefinitions.TryGet("Monastery Swiftspear", out var def);

        def.Should().NotBeNull();
        var trigger = def!.Triggers.FirstOrDefault(t =>
            t.Event == GameEvent.SpellCast
            && t.Condition == TriggerCondition.ControllerCastsNoncreature);

        trigger.Should().NotBeNull("Monastery Swiftspear should have a prowess trigger");
        trigger!.Effect.Should().BeOfType<ProwessEffect>();
    }

    [Fact]
    public async Task Prowess_Triggers_OnNoncreatureSpell()
    {
        var h1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var swiftspear = GameCard.Create("Monastery Swiftspear", "Creature — Human Monk");
        p1.Battlefield.Add(swiftspear);

        // Cast an instant (noncreature spell)
        var bolt = new GameCard { Name = "Bolt", ManaCost = ManaCost.Parse("{R}"), CardTypes = CardType.Instant };
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, bolt);

        state.StackCount.Should().Be(1, "prowess should trigger on noncreature spell cast");
    }

    [Fact]
    public async Task Prowess_DoesNotTrigger_OnCreatureSpell()
    {
        var h1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var swiftspear = GameCard.Create("Monastery Swiftspear", "Creature — Human Monk");
        p1.Battlefield.Add(swiftspear);

        // Cast a creature spell
        var creature = new GameCard { Name = "Bear", ManaCost = ManaCost.Parse("{1}{G}"), CardTypes = CardType.Creature };
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, creature);

        state.StackCount.Should().Be(0, "prowess should NOT trigger on creature spell cast");
    }

    [Fact]
    public async Task Prowess_MultipleCasts_StackBuffs()
    {
        var h1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var swiftspear = GameCard.Create("Monastery Swiftspear", "Creature — Human Monk");
        p1.Battlefield.Add(swiftspear);

        // Cast two noncreature spells
        var bolt1 = new GameCard { Name = "Bolt1", ManaCost = ManaCost.Parse("{R}"), CardTypes = CardType.Instant };
        var bolt2 = new GameCard { Name = "Bolt2", ManaCost = ManaCost.Parse("{R}"), CardTypes = CardType.Instant };

        await engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, bolt1);
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, bolt2);

        state.StackCount.Should().Be(2, "prowess should trigger for each noncreature spell");

        // Resolve both triggers
        await engine.ResolveAllTriggersAsync();

        // Should have 2 separate +1/+1 effects
        var pumpEffects = state.ActiveEffects
            .Where(e => e.Type == ContinuousEffectType.ModifyPowerToughness
                && e.SourceId == swiftspear.Id
                && e.UntilEndOfTurn)
            .ToList();

        pumpEffects.Should().HaveCount(2, "two prowess triggers should create two +1/+1 effects");
    }

    [Fact]
    public async Task ProwessEffect_AddsUntilEndOfTurn()
    {
        var h1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var card = GameCard.Create("Monastery Swiftspear", "Creature — Human Monk");
        p1.Battlefield.Add(card);

        var effect = new ProwessEffect();
        var context = new EffectContext(state, p1, card, h1);
        await effect.Execute(context);

        state.ActiveEffects.Should().ContainSingle()
            .Which.Should().Match<ContinuousEffect>(e =>
                e.SourceId == card.Id
                && e.Type == ContinuousEffectType.ModifyPowerToughness
                && e.PowerMod == 1
                && e.ToughnessMod == 1
                && e.UntilEndOfTurn == true);
    }

    [Fact]
    public async Task Prowess_Triggers_OnSorcery()
    {
        var h1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var swiftspear = GameCard.Create("Monastery Swiftspear", "Creature — Human Monk");
        p1.Battlefield.Add(swiftspear);

        // Cast a sorcery (noncreature spell)
        var sorcery = new GameCard { Name = "Ponder", ManaCost = ManaCost.Parse("{U}"), CardTypes = CardType.Sorcery };
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, sorcery);

        state.StackCount.Should().Be(1, "prowess should trigger on sorcery cast");
    }

    [Fact]
    public async Task Prowess_Triggers_OnEnchantment()
    {
        var h1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var swiftspear = GameCard.Create("Monastery Swiftspear", "Creature — Human Monk");
        p1.Battlefield.Add(swiftspear);

        // Cast an enchantment (noncreature spell)
        var enchantment = new GameCard { Name = "TestEnchantment", ManaCost = ManaCost.Parse("{1}{G}"), CardTypes = CardType.Enchantment };
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, enchantment);

        state.StackCount.Should().Be(1, "prowess should trigger on enchantment cast");
    }

    [Fact]
    public async Task Prowess_DoesNotTrigger_OnOpponentCast()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // Swiftspear is on P1's side
        var swiftspear = GameCard.Create("Monastery Swiftspear", "Creature — Human Monk");
        p1.Battlefield.Add(swiftspear);

        // P2 is active player and casts a spell
        state.ActivePlayer = p2;

        var bolt = new GameCard { Name = "Bolt", ManaCost = ManaCost.Parse("{R}"), CardTypes = CardType.Instant };
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, bolt);

        state.StackCount.Should().Be(0, "prowess should NOT trigger when opponent casts a spell");
    }

    // ========== Knight of Stromgald Tests ==========

    [Fact]
    public void KnightOfStromgald_NoStaticFirstStrike()
    {
        CardDefinitions.TryGet("Knight of Stromgald", out var def);

        def.Should().NotBeNull();
        def!.ContinuousEffects.Should().NotContain(e =>
            e.Type == ContinuousEffectType.GrantKeyword
            && e.GrantedKeyword == Keyword.FirstStrike,
            because: "Knight of Stromgald gains first strike via activated ability, not statically");
    }

    [Fact]
    public void KnightOfStromgald_HasProtectionFromWhite()
    {
        CardDefinitions.TryGet("Knight of Stromgald", out var def);

        def.Should().NotBeNull();
        def!.ContinuousEffects.Should().Contain(e =>
            e.Type == ContinuousEffectType.GrantKeyword
            && e.GrantedKeyword == Keyword.ProtectionFromWhite);
    }

    [Fact]
    public void KnightOfStromgald_HasPumpAbility()
    {
        CardDefinitions.TryGet("Knight of Stromgald", out var def);

        def.Should().NotBeNull();
        def!.ActivatedAbilities.Should().NotBeEmpty();
        def!.ActivatedAbilities[0].Cost.ManaCost.Should().NotBeNull();
        def!.ActivatedAbilities[0].Cost.ManaCost!.ToString().Should().Be("{B}{B}");
        def!.ActivatedAbilities[0].Effect.Should().BeOfType<PumpSelfEffect>();
    }

    [Fact]
    public async Task KnightOfStromgald_PumpAbility_GivesPlusOneZero()
    {
        var h1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var knight = GameCard.Create("Knight of Stromgald", "Creature — Human Knight");
        p1.Battlefield.Add(knight);

        var effect = new PumpSelfEffect(1, 0);
        var context = new EffectContext(state, p1, knight, h1);
        await effect.Execute(context);

        state.ActiveEffects.Should().ContainSingle()
            .Which.Should().Match<ContinuousEffect>(e =>
                e.SourceId == knight.Id
                && e.Type == ContinuousEffectType.ModifyPowerToughness
                && e.PowerMod == 1
                && e.ToughnessMod == 0
                && e.UntilEndOfTurn == true);
    }

    [Fact]
    public void KnightOfStromgald_BasePowerToughness()
    {
        CardDefinitions.TryGet("Knight of Stromgald", out var def);

        def.Should().NotBeNull();
        def!.Power.Should().Be(2);
        def!.Toughness.Should().Be(1);
    }

    // ========== Goblin Piledriver Tests ==========

    [Fact]
    public void GoblinPiledriver_HasProtectionFromBlue()
    {
        CardDefinitions.TryGet("Goblin Piledriver", out var def);

        def.Should().NotBeNull();
        def!.ContinuousEffects.Should().Contain(e =>
            e.Type == ContinuousEffectType.GrantKeyword
            && e.GrantedKeyword == Keyword.ProtectionFromBlue);
    }

    [Fact]
    public void GoblinPiledriver_StillHasAttackTrigger()
    {
        CardDefinitions.TryGet("Goblin Piledriver", out var def);

        def.Should().NotBeNull();
        var trigger = def!.Triggers.FirstOrDefault(t =>
            t.Event == GameEvent.BeginCombat
            && t.Condition == TriggerCondition.SelfAttacks);

        trigger.Should().NotBeNull("Goblin Piledriver should still have its attack trigger");
        trigger!.Effect.Should().BeOfType<PiledriverPumpEffect>();
    }
}
