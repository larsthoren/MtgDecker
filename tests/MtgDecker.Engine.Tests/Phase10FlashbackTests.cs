using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class Phase10FlashbackTests
{
    // === CardDefinitions verification ===

    [Theory]
    [InlineData("Call of the Herd", "{3}{G}", 0, false)]
    [InlineData("Deep Analysis", "{1}{U}", 3, false)]
    [InlineData("Ray of Revelation", "{G}", 0, false)]
    [InlineData("Reckless Charge", "{2}{R}", 0, false)]
    public void CardDefinitions_FlashbackCost_Mana(string cardName, string expectedManaCost, int expectedLifeCost, bool expectSacrifice)
    {
        CardDefinitions.TryGet(cardName, out var def).Should().BeTrue($"{cardName} should be registered");
        def!.FlashbackCost.Should().NotBeNull($"{cardName} should have FlashbackCost");
        def.FlashbackCost!.ManaCost.Should().NotBeNull();
        def.FlashbackCost.ManaCost!.ToString().Should().Be(expectedManaCost);
        def.FlashbackCost.LifeCost.Should().Be(expectedLifeCost);
        def.FlashbackCost.SacrificeCreature.Should().Be(expectSacrifice);
    }

    [Fact]
    public void CabalTherapy_Has_SacrificeCreature_FlashbackCost()
    {
        CardDefinitions.TryGet("Cabal Therapy", out var def).Should().BeTrue();
        def!.FlashbackCost.Should().NotBeNull();
        def.FlashbackCost!.ManaCost.Should().BeNull("Cabal Therapy flashback has no mana cost");
        def.FlashbackCost.SacrificeCreature.Should().BeTrue();
        def.FlashbackCost.LifeCost.Should().Be(0);
    }

    [Fact]
    public void QuietSpeculation_Has_No_FlashbackCost()
    {
        CardDefinitions.TryGet("Quiet Speculation", out var def).Should().BeTrue();
        def!.FlashbackCost.Should().BeNull("Quiet Speculation itself does not have flashback");
    }

    // === RecklessChargeEffect tests ===

    [Fact]
    public void RecklessChargeEffect_Gives_Plus3_Plus0_And_Haste()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p1.Battlefield.Add(creature);

        var spellCard = new GameCard { Name = "Reckless Charge", CardTypes = CardType.Sorcery };
        var targets = new List<TargetInfo> { new(creature.Id, p1.Id, ZoneType.Battlefield) };
        var spell = new StackObject(spellCard, p1.Id, new Dictionary<ManaColor, int>(), targets, 0);

        var effect = new RecklessChargeEffect();
        effect.Resolve(state, spell);

        // Should have two continuous effects: +3/+0 and haste
        state.ActiveEffects.Should().HaveCount(2);

        var ptEffect = state.ActiveEffects.First(e => e.Type == ContinuousEffectType.ModifyPowerToughness);
        ptEffect.PowerMod.Should().Be(3);
        ptEffect.ToughnessMod.Should().Be(0);
        ptEffect.UntilEndOfTurn.Should().BeTrue();
        ptEffect.Applies(creature, p1).Should().BeTrue();

        var hasteEffect = state.ActiveEffects.First(e => e.Type == ContinuousEffectType.GrantKeyword);
        hasteEffect.GrantedKeyword.Should().Be(Keyword.Haste);
        hasteEffect.UntilEndOfTurn.Should().BeTrue();
        hasteEffect.Applies(creature, p1).Should().BeTrue();
    }

    // === QuietSpeculationEffect tests ===

    [Fact]
    public async Task QuietSpeculationEffect_Finds_Flashback_Cards_In_Library()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        // Add cards with flashback to library
        var callHerd = GameCard.Create("Call of the Herd");
        var deepAnalysis = GameCard.Create("Deep Analysis");
        var rayRev = GameCard.Create("Ray of Revelation");
        var otherCard = new GameCard { Name = "Forest", CardTypes = CardType.Land };

        p1.Library.Add(callHerd);
        p1.Library.Add(deepAnalysis);
        p1.Library.Add(rayRev);
        p1.Library.Add(otherCard);

        // Enqueue choices: pick all 3 flashback cards
        handler.EnqueueCardChoice(callHerd.Id);
        handler.EnqueueCardChoice(deepAnalysis.Id);
        handler.EnqueueCardChoice(rayRev.Id);

        var spellCard = new GameCard { Name = "Quiet Speculation", CardTypes = CardType.Sorcery };
        var spell = new StackObject(spellCard, p1.Id, new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);

        var effect = new QuietSpeculationEffect();
        await effect.ResolveAsync(state, spell, handler);

        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Call of the Herd");
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Deep Analysis");
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Ray of Revelation");
        p1.Library.Cards.Should().NotContain(c => c.Name == "Call of the Herd");
        p1.Library.Cards.Should().Contain(c => c.Name == "Forest");
    }

    [Fact]
    public async Task QuietSpeculationEffect_Stops_Early_If_Player_Declines()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var callHerd = GameCard.Create("Call of the Herd");
        var deepAnalysis = GameCard.Create("Deep Analysis");
        p1.Library.Add(callHerd);
        p1.Library.Add(deepAnalysis);

        // Choose first, then decline (null)
        handler.EnqueueCardChoice(callHerd.Id);
        handler.EnqueueCardChoice(null);

        var spellCard = new GameCard { Name = "Quiet Speculation", CardTypes = CardType.Sorcery };
        var spell = new StackObject(spellCard, p1.Id, new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);

        var effect = new QuietSpeculationEffect();
        await effect.ResolveAsync(state, spell, handler);

        p1.Graveyard.Cards.Should().HaveCount(1);
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Call of the Herd");
    }

    // === Flashback casting tests ===

    [Fact]
    public async Task Flashback_CastsFromGraveyard_SpellGoesOnStack()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // Set up game phase for sorcery-speed casting
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var callHerd = GameCard.Create("Call of the Herd");
        p1.Graveyard.Add(callHerd);

        // Flashback cost is {3}{G}
        p1.ManaPool.Add(ManaColor.Green, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 3);

        var action = GameAction.Flashback(p1.Id, callHerd.Id);
        await engine.ExecuteAction(action);

        // Card should be on the stack
        state.Stack.Should().ContainSingle();
        var stackObj = state.Stack[0].Should().BeOfType<StackObject>().Subject;
        stackObj.Card.Name.Should().Be("Call of the Herd");
        stackObj.IsFlashback.Should().BeTrue();

        // Card should no longer be in graveyard
        p1.Graveyard.Cards.Should().NotContain(c => c.Name == "Call of the Herd");
    }

    [Fact]
    public async Task Flashback_SpellExiledAfterResolution()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var callHerd = GameCard.Create("Call of the Herd");
        p1.Graveyard.Add(callHerd);

        // Pay flashback cost {3}{G}
        p1.ManaPool.Add(ManaColor.Green, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 3);

        // Cast via flashback
        var action = GameAction.Flashback(p1.Id, callHerd.Id);
        await engine.ExecuteAction(action);

        // Both players pass priority -> stack resolves via RunPriorityAsync
        // After resolution, both pass again -> stack empty -> return
        h1.EnqueueAction(GameAction.Pass(p1.Id));
        h2.EnqueueAction(GameAction.Pass(p2.Id));
        h1.EnqueueAction(GameAction.Pass(p1.Id));
        h2.EnqueueAction(GameAction.Pass(p2.Id));

        await engine.RunPriorityAsync();

        // Spell should be exiled, not in graveyard
        p1.Exile.Cards.Should().Contain(c => c.Name == "Call of the Herd");
        p1.Graveyard.Cards.Should().NotContain(c => c.Name == "Call of the Herd");

        // Token should be on battlefield (Call of the Herd creates a 3/3 Elephant)
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Elephant");
    }

    [Fact]
    public async Task Flashback_DeepAnalysis_PaysLife()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var deepAnalysis = GameCard.Create("Deep Analysis");
        p1.Graveyard.Add(deepAnalysis);

        // Flashback cost is {1}{U}, pay 3 life
        p1.ManaPool.Add(ManaColor.Blue, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 1);

        // Add cards to library for draw
        p1.Library.Add(new GameCard { Name = "Card A" });
        p1.Library.Add(new GameCard { Name = "Card B" });

        int lifeBefore = p1.Life;

        var action = GameAction.Flashback(p1.Id, deepAnalysis.Id);
        await engine.ExecuteAction(action);

        // Life should be reduced by 3
        p1.Life.Should().Be(lifeBefore - 3);

        // Card should be on the stack
        state.Stack.Should().ContainSingle();
        var stackObj = state.Stack[0].Should().BeOfType<StackObject>().Subject;
        stackObj.IsFlashback.Should().BeTrue();
    }

    [Fact]
    public async Task Flashback_CabalTherapy_SacrificeCreature()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var cabalTherapy = GameCard.Create("Cabal Therapy");
        p1.Graveyard.Add(cabalTherapy);

        var goblin = new GameCard { Name = "Goblin Token", CardTypes = CardType.Creature, BasePower = 1, BaseToughness = 1, IsToken = true };
        p1.Battlefield.Add(goblin);

        // Enqueue sacrifice choice
        handler.EnqueueCardChoice(goblin.Id);

        // Enqueue target (player target for Cabal Therapy)
        handler.EnqueueTarget(new TargetInfo(Guid.Empty, p2.Id, ZoneType.None));

        // Enqueue card name choice for Cabal Therapy effect (it calls ChooseCard)
        handler.EnqueueCardChoice(null); // just to pass through effect resolution

        var action = GameAction.Flashback(p1.Id, cabalTherapy.Id);
        await engine.ExecuteAction(action);

        // Goblin should be in graveyard (sacrificed)
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Goblin Token");
        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Goblin Token");

        // Cabal Therapy should be on the stack
        state.Stack.Should().ContainSingle();
        var stackObj = state.Stack[0].Should().BeOfType<StackObject>().Subject;
        stackObj.IsFlashback.Should().BeTrue();
    }

    [Fact]
    public async Task Flashback_NotEnoughMana_Fails()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var callHerd = GameCard.Create("Call of the Herd");
        p1.Graveyard.Add(callHerd);

        // Only 1 green mana, need {3}{G}
        p1.ManaPool.Add(ManaColor.Green, 1);

        var action = GameAction.Flashback(p1.Id, callHerd.Id);
        await engine.ExecuteAction(action);

        // Card should still be in graveyard
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Call of the Herd");
        state.Stack.Should().BeEmpty();
    }

    [Fact]
    public async Task Flashback_NoFlashbackCost_Fails()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        // Duress has no flashback cost
        var duress = GameCard.Create("Duress");
        p1.Graveyard.Add(duress);
        p1.ManaPool.Add(ManaColor.Black, 5);

        var action = GameAction.Flashback(p1.Id, duress.Id);
        await engine.ExecuteAction(action);

        // Card should still be in graveyard
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Duress");
        state.Stack.Should().BeEmpty();
    }

    [Fact]
    public async Task Flashback_Sorcery_CannotCastAtInstantSpeed()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // Not p1's turn, or wrong phase
        state.ActivePlayer = p2;
        state.CurrentPhase = Phase.MainPhase1;

        var callHerd = GameCard.Create("Call of the Herd");
        p1.Graveyard.Add(callHerd);
        p1.ManaPool.Add(ManaColor.Green, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 3);

        var action = GameAction.Flashback(p1.Id, callHerd.Id);
        await engine.ExecuteAction(action);

        // Card should still be in graveyard
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Call of the Herd");
        state.Stack.Should().BeEmpty();
    }

    [Fact]
    public async Task Flashback_RayOfRevelation_Instant_CanCastAnytime()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // Not p1's main phase
        state.ActivePlayer = p2;
        state.CurrentPhase = Phase.Combat;

        var rayRev = GameCard.Create("Ray of Revelation");
        p1.Graveyard.Add(rayRev);

        // Put an enchantment on the battlefield as a target
        var enchantment = new GameCard { Name = "Seal of Cleansing", CardTypes = CardType.Enchantment };
        p2.Battlefield.Add(enchantment);

        // Flashback cost is {G}
        p1.ManaPool.Add(ManaColor.Green, 1);

        // Enqueue target
        handler.EnqueueTarget(new TargetInfo(enchantment.Id, p2.Id, ZoneType.Battlefield));

        var action = GameAction.Flashback(p1.Id, rayRev.Id);
        await engine.ExecuteAction(action);

        // Card should be on the stack (instant-speed flashback is fine)
        state.Stack.Should().ContainSingle();
        var stackObj = state.Stack[0].Should().BeOfType<StackObject>().Subject;
        stackObj.IsFlashback.Should().BeTrue();
    }

    [Fact]
    public async Task Flashback_CabalTherapy_NoCreatureToSacrifice_Fails()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var cabalTherapy = GameCard.Create("Cabal Therapy");
        p1.Graveyard.Add(cabalTherapy);

        // No creatures on battlefield to sacrifice

        var action = GameAction.Flashback(p1.Id, cabalTherapy.Id);
        await engine.ExecuteAction(action);

        // Card should still be in graveyard
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Cabal Therapy");
        state.Stack.Should().BeEmpty();
    }

    [Fact]
    public async Task Flashback_Fizzle_SpellExiled_NotGraveyard()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var rayRev = GameCard.Create("Ray of Revelation");
        p1.Graveyard.Add(rayRev);

        // Put a target enchantment on the battlefield
        var enchantment = new GameCard { Name = "Seal of Cleansing", CardTypes = CardType.Enchantment };
        p2.Battlefield.Add(enchantment);

        // Flashback cost is {G}
        p1.ManaPool.Add(ManaColor.Green, 1);

        // Enqueue target
        h1.EnqueueTarget(new TargetInfo(enchantment.Id, p2.Id, ZoneType.Battlefield));

        var action = GameAction.Flashback(p1.Id, rayRev.Id);
        await engine.ExecuteAction(action);

        // Remove the enchantment before resolution (target becomes illegal)
        p2.Battlefield.RemoveById(enchantment.Id);

        // Both players pass -> resolve (fizzle) -> both pass again -> done
        h1.EnqueueAction(GameAction.Pass(p1.Id));
        h2.EnqueueAction(GameAction.Pass(p2.Id));
        h1.EnqueueAction(GameAction.Pass(p1.Id));
        h2.EnqueueAction(GameAction.Pass(p2.Id));

        await engine.RunPriorityAsync();

        // Even on fizzle, flashback spell goes to exile
        p1.Exile.Cards.Should().Contain(c => c.Name == "Ray of Revelation");
        p1.Graveyard.Cards.Should().NotContain(c => c.Name == "Ray of Revelation");
    }

    [Fact]
    public void GameAction_Flashback_Factory_Creates_Correct_Action()
    {
        var playerId = Guid.NewGuid();
        var cardId = Guid.NewGuid();

        var action = GameAction.Flashback(playerId, cardId);

        action.Type.Should().Be(ActionType.Flashback);
        action.PlayerId.Should().Be(playerId);
        action.CardId.Should().Be(cardId);
        action.SourceZone.Should().Be(ZoneType.Graveyard);
    }

    [Fact]
    public async Task Flashback_RecklessCharge_TargetsCreature()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var recklessCharge = GameCard.Create("Reckless Charge");
        p1.Graveyard.Add(recklessCharge);

        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p1.Battlefield.Add(creature);

        // Flashback cost is {2}{R}
        p1.ManaPool.Add(ManaColor.Red, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 2);

        // Enqueue target
        handler.EnqueueTarget(new TargetInfo(creature.Id, p1.Id, ZoneType.Battlefield));

        var action = GameAction.Flashback(p1.Id, recklessCharge.Id);
        await engine.ExecuteAction(action);

        // Spell should be on the stack
        state.Stack.Should().ContainSingle();
        var stackObj = state.Stack[0].Should().BeOfType<StackObject>().Subject;
        stackObj.IsFlashback.Should().BeTrue();
        stackObj.Card.Name.Should().Be("Reckless Charge");
    }
}
