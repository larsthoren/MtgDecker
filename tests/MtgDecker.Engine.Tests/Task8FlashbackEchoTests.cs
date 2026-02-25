using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class Task8FlashbackEchoTests
{
    // =====================================================================
    // Roar of the Wurm — CardDefinition verification
    // =====================================================================

    [Fact]
    public void RoarOfTheWurm_IsRegistered_WithCorrectProperties()
    {
        CardDefinitions.TryGet("Roar of the Wurm", out var def).Should().BeTrue();
        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ToString().Should().Be("{6}{G}");
        def.CardTypes.Should().Be(CardType.Sorcery);
        def.Effect.Should().BeOfType<CreateTokenSpellEffect>();
    }

    [Fact]
    public void RoarOfTheWurm_HasFlashbackCost()
    {
        CardDefinitions.TryGet("Roar of the Wurm", out var def).Should().BeTrue();
        def!.FlashbackCost.Should().NotBeNull();
        def.FlashbackCost!.ManaCost.Should().NotBeNull();
        def.FlashbackCost.ManaCost!.ToString().Should().Be("{3}{G}");
    }

    [Fact]
    public void RoarOfTheWurm_Effect_Creates6x6WurmToken()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var spellCard = new GameCard { Name = "Roar of the Wurm", CardTypes = CardType.Sorcery };
        var spell = new StackObject(spellCard, p1.Id, new Dictionary<ManaColor, int>(), [], 0);

        var effect = new CreateTokenSpellEffect("Wurm", 6, 6, CardType.Creature, ["Wurm"]);
        effect.Resolve(state, spell);

        p1.Battlefield.Cards.Should().ContainSingle(c => c.Name == "Wurm" && c.IsToken);
        var token = p1.Battlefield.Cards.First(c => c.Name == "Wurm");
        token.Power.Should().Be(6);
        token.Toughness.Should().Be(6);
        token.IsCreature.Should().BeTrue();
        token.Subtypes.Should().Contain("Wurm");
    }

    // =====================================================================
    // Krosan Reclamation — CardDefinition verification
    // =====================================================================

    [Fact]
    public void KrosanReclamation_IsRegistered_WithCorrectProperties()
    {
        CardDefinitions.TryGet("Krosan Reclamation", out var def).Should().BeTrue();
        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ToString().Should().Be("{1}{G}");
        def.CardTypes.Should().Be(CardType.Instant);
        def.FlashbackCost.Should().NotBeNull();
        def.FlashbackCost!.ManaCost!.ToString().Should().Be("{1}{G}");
        def.TargetFilter.Should().NotBeNull();
        def.Effect.Should().BeOfType<KrosanReclamationEffect>();
    }

    [Fact]
    public async Task KrosanReclamation_ShufflesCardsFromGraveyardIntoLibrary()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        // Put some cards in p2's graveyard
        var card1 = new GameCard { Name = "Lightning Bolt", CardTypes = CardType.Instant };
        var card2 = new GameCard { Name = "Counterspell", CardTypes = CardType.Instant };
        var card3 = new GameCard { Name = "Giant Growth", CardTypes = CardType.Instant };
        p2.Graveyard.Add(card1);
        p2.Graveyard.Add(card2);
        p2.Graveyard.Add(card3);

        var initialLibraryCount = p2.Library.Count;

        // Enqueue choices: pick card1, then card2
        handler.EnqueueCardChoice(card1.Id);
        handler.EnqueueCardChoice(card2.Id);

        var spellCard = new GameCard { Name = "Krosan Reclamation", CardTypes = CardType.Instant };
        var targets = new List<TargetInfo> { new(Guid.Empty, p2.Id, ZoneType.None) };
        var spell = new StackObject(spellCard, p1.Id, new Dictionary<ManaColor, int>(), targets, 0);

        var effect = new KrosanReclamationEffect();
        await effect.ResolveAsync(state, spell, handler);

        // 2 cards moved from graveyard to library
        p2.Graveyard.Cards.Should().HaveCount(1);
        p2.Graveyard.Cards.Should().Contain(c => c.Name == "Giant Growth");
        p2.Library.Count.Should().Be(initialLibraryCount + 2);
    }

    [Fact]
    public async Task KrosanReclamation_PlayerCanChooseJustOneCard()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var card1 = new GameCard { Name = "Lightning Bolt", CardTypes = CardType.Instant };
        var card2 = new GameCard { Name = "Counterspell", CardTypes = CardType.Instant };
        p2.Graveyard.Add(card1);
        p2.Graveyard.Add(card2);

        // Choose one, then skip
        handler.EnqueueCardChoice(card1.Id);
        handler.EnqueueCardChoice(null); // skip second choice

        var spellCard = new GameCard { Name = "Krosan Reclamation", CardTypes = CardType.Instant };
        var targets = new List<TargetInfo> { new(Guid.Empty, p2.Id, ZoneType.None) };
        var spell = new StackObject(spellCard, p1.Id, new Dictionary<ManaColor, int>(), targets, 0);

        var effect = new KrosanReclamationEffect();
        await effect.ResolveAsync(state, spell, handler);

        p2.Graveyard.Cards.Should().HaveCount(1);
        p2.Graveyard.Cards.Should().Contain(c => c.Name == "Counterspell");
    }

    [Fact]
    public async Task KrosanReclamation_EmptyGraveyard_DoesNothing()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var spellCard = new GameCard { Name = "Krosan Reclamation", CardTypes = CardType.Instant };
        var targets = new List<TargetInfo> { new(Guid.Empty, p2.Id, ZoneType.None) };
        var spell = new StackObject(spellCard, p1.Id, new Dictionary<ManaColor, int>(), targets, 0);

        var effect = new KrosanReclamationEffect();
        await effect.ResolveAsync(state, spell, handler);

        p2.Graveyard.Cards.Should().BeEmpty();
    }

    // =====================================================================
    // Flash of Insight — CardDefinition verification
    // =====================================================================

    [Fact]
    public void FlashOfInsight_IsRegistered_WithCorrectProperties()
    {
        CardDefinitions.TryGet("Flash of Insight", out var def).Should().BeTrue();
        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ToString().Should().Be("{1}{U}");
        def.CardTypes.Should().Be(CardType.Instant);
        def.Effect.Should().BeOfType<FlashOfInsightEffect>();
    }

    [Fact]
    public void FlashOfInsight_HasFlashbackCost_WithBlueCardExile()
    {
        CardDefinitions.TryGet("Flash of Insight", out var def).Should().BeTrue();
        def!.FlashbackCost.Should().NotBeNull();
        def.FlashbackCost!.ManaCost.Should().NotBeNull();
        def.FlashbackCost.ManaCost!.ToString().Should().Be("{1}{U}");
        def.FlashbackCost.ExileBlueCardsFromGraveyard.Should().Be(1);
    }

    [Fact]
    public async Task FlashOfInsight_UsesRemainingManaForX_NormalCast()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        // Add 3 mana remaining (X=3)
        p1.ManaPool.Add(ManaColor.Blue, 2);
        p1.ManaPool.Add(ManaColor.Colorless, 1);

        // Library has 5 cards
        var libCard1 = new GameCard { Name = "Card A" };
        var libCard2 = new GameCard { Name = "Card B" };
        var libCard3 = new GameCard { Name = "Card C" };
        var libCard4 = new GameCard { Name = "Card D" };
        var libCard5 = new GameCard { Name = "Card E" };
        p1.Library.Add(libCard5);
        p1.Library.Add(libCard4);
        p1.Library.Add(libCard3);
        p1.Library.Add(libCard2);
        p1.Library.Add(libCard1);

        // Choose card B
        handler.EnqueueCardChoice(libCard2.Id);

        var spellCard = new GameCard { Name = "Flash of Insight", CardTypes = CardType.Instant };
        var spell = new StackObject(spellCard, p1.Id, new Dictionary<ManaColor, int>(), [], 0);

        var effect = new FlashOfInsightEffect();
        await effect.ResolveAsync(state, spell, handler);

        // Card B should be in hand
        p1.Hand.Cards.Should().Contain(c => c.Name == "Card B");
        // Remaining mana drained
        p1.ManaPool.Total.Should().Be(0);
        // Other 2 cards go to bottom
        p1.Library.Count.Should().Be(4); // 5 - 3 looked at + 2 put back on bottom
    }

    [Fact]
    public async Task FlashOfInsight_UsesXValueForFlashback()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        // Library has 4 cards
        var libCard1 = new GameCard { Name = "Card A" };
        var libCard2 = new GameCard { Name = "Card B" };
        var libCard3 = new GameCard { Name = "Card C" };
        var libCard4 = new GameCard { Name = "Card D" };
        p1.Library.Add(libCard4);
        p1.Library.Add(libCard3);
        p1.Library.Add(libCard2);
        p1.Library.Add(libCard1);

        // Choose card A
        handler.EnqueueCardChoice(libCard1.Id);

        var spellCard = new GameCard { Name = "Flash of Insight", CardTypes = CardType.Instant };
        var spell = new StackObject(spellCard, p1.Id, new Dictionary<ManaColor, int>(), [], 0)
        {
            IsFlashback = true,
            XValue = 2, // Exiled 2 blue cards
        };

        var effect = new FlashOfInsightEffect();
        await effect.ResolveAsync(state, spell, handler);

        // Card A should be in hand
        p1.Hand.Cards.Should().Contain(c => c.Name == "Card A");
        // 2 looked at - 1 kept = 1 on bottom
        p1.Library.Count.Should().Be(3); // 4 - 2 + 1 back
    }

    [Fact]
    public async Task FlashOfInsight_XZero_DoesNothing()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        // No remaining mana = X=0
        var spellCard = new GameCard { Name = "Flash of Insight", CardTypes = CardType.Instant };
        var spell = new StackObject(spellCard, p1.Id, new Dictionary<ManaColor, int>(), [], 0);

        var effect = new FlashOfInsightEffect();
        await effect.ResolveAsync(state, spell, handler);

        p1.Hand.Cards.Should().BeEmpty();
    }

    // =====================================================================
    // Radiant's Dragoons — CardDefinition verification
    // =====================================================================

    [Fact]
    public void RadiantsDragoons_IsRegistered_WithCorrectProperties()
    {
        CardDefinitions.TryGet("Radiant's Dragoons", out var def).Should().BeTrue();
        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ToString().Should().Be("{3}{W}");
        def.CardTypes.Should().Be(CardType.Creature);
        def.Power.Should().Be(2);
        def.Toughness.Should().Be(5);
        def.Subtypes.Should().BeEquivalentTo(["Human", "Soldier"]);
    }

    [Fact]
    public void RadiantsDragoons_HasEchoCost()
    {
        CardDefinitions.TryGet("Radiant's Dragoons", out var def).Should().BeTrue();
        def!.EchoCost.Should().NotBeNull();
        def.EchoCost!.ToString().Should().Be("{3}{W}");
    }

    [Fact]
    public void RadiantsDragoons_HasETBGainLifeTrigger()
    {
        CardDefinitions.TryGet("Radiant's Dragoons", out var def).Should().BeTrue();
        def!.Triggers.Should().HaveCount(1);
        def.Triggers[0].Event.Should().Be(GameEvent.EnterBattlefield);
        def.Triggers[0].Condition.Should().Be(TriggerCondition.Self);
        def.Triggers[0].Effect.Should().BeOfType<MtgDecker.Engine.Triggers.Effects.GainLifeEffect>();
        ((MtgDecker.Engine.Triggers.Effects.GainLifeEffect)def.Triggers[0].Effect).Amount.Should().Be(5);
    }

    [Fact]
    public async Task RadiantsDragoons_ETB_Gains5Life()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var dragoons = GameCard.Create("Radiant's Dragoons");
        p1.Battlefield.Add(dragoons);

        var effect = new MtgDecker.Engine.Triggers.Effects.GainLifeEffect(5);
        var context = new EffectContext(state, p1, dragoons, handler);
        await effect.Execute(context);

        p1.Life.Should().Be(25); // 20 + 5
    }

    [Fact]
    public async Task RadiantsDragoons_EchoPaid_StaysOnBattlefield()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var dragoons = GameCard.Create("Radiant's Dragoons");
        dragoons.EchoPaid = false;
        p1.Battlefield.Add(dragoons);

        // Give mana to pay echo {3}{W}
        p1.ManaPool.Add(ManaColor.White, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 3);

        // Choose to pay
        handler.EnqueueCardChoice(dragoons.Id);

        var echoCost = ManaCost.Parse("{3}{W}");
        var echoEffect = new EchoEffect(echoCost);
        var context = new EffectContext(state, p1, dragoons, handler);
        await echoEffect.Execute(context);

        dragoons.EchoPaid.Should().BeTrue();
        p1.Battlefield.Cards.Should().Contain(c => c.Id == dragoons.Id);
        p1.ManaPool.Total.Should().Be(0);
    }

    [Fact]
    public async Task RadiantsDragoons_EchoNotPaid_Sacrificed()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var dragoons = GameCard.Create("Radiant's Dragoons");
        dragoons.EchoPaid = false;
        p1.Battlefield.Add(dragoons);

        // No mana available
        var echoCost = ManaCost.Parse("{3}{W}");
        var echoEffect = new EchoEffect(echoCost);
        var context = new EffectContext(state, p1, dragoons, handler);
        await echoEffect.Execute(context);

        dragoons.EchoPaid.Should().BeFalse();
        p1.Battlefield.Cards.Should().NotContain(c => c.Id == dragoons.Id);
        p1.Graveyard.Cards.Should().Contain(c => c.Id == dragoons.Id);
    }

    // =====================================================================
    // Attunement — CardDefinition verification
    // =====================================================================

    [Fact]
    public void Attunement_IsRegistered_WithCorrectProperties()
    {
        CardDefinitions.TryGet("Attunement", out var def).Should().BeTrue();
        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ToString().Should().Be("{2}{U}");
        def.CardTypes.Should().Be(CardType.Enchantment);
    }

    [Fact]
    public void Attunement_HasActivatedAbility_WithReturnSelfToHand()
    {
        CardDefinitions.TryGet("Attunement", out var def).Should().BeTrue();
        def!.ActivatedAbilities.Should().HaveCount(1);
        def.ActivatedAbilities[0].Cost.ReturnSelfToHand.Should().BeTrue();
        def.ActivatedAbilities[0].Effect.Should().BeOfType<AttunementEffect>();
    }

    [Fact]
    public async Task AttunementEffect_Draws3ThenDiscards4()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        // Give player some cards in hand to have enough to discard
        var handCard1 = new GameCard { Name = "Hand Card 1" };
        var handCard2 = new GameCard { Name = "Hand Card 2" };
        p1.Hand.Add(handCard1);
        p1.Hand.Add(handCard2);

        // Add cards to library to draw
        var libCard1 = new GameCard { Name = "Lib Card 1" };
        var libCard2 = new GameCard { Name = "Lib Card 2" };
        var libCard3 = new GameCard { Name = "Lib Card 3" };
        p1.Library.Add(libCard3);
        p1.Library.Add(libCard2);
        p1.Library.Add(libCard1);

        var attunement = new GameCard { Name = "Attunement", CardTypes = CardType.Enchantment };

        // After drawing 3, hand has 5 cards. Discard 4.
        // The test handler default picks first N cards to discard
        var effect = new AttunementEffect();
        var context = new EffectContext(state, p1, attunement, handler);
        await effect.Execute(context);

        // Drew 3, had 2 = 5 in hand, discarded 4 = 1 in hand
        p1.Hand.Cards.Should().HaveCount(1);
        p1.Graveyard.Cards.Should().HaveCount(4);
    }

    [Fact]
    public async Task AttunementEffect_NotEnoughCardsToDiscard4_DiscardsAll()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        // No cards in hand, only 3 in library
        var libCard1 = new GameCard { Name = "Lib Card 1" };
        var libCard2 = new GameCard { Name = "Lib Card 2" };
        var libCard3 = new GameCard { Name = "Lib Card 3" };
        p1.Library.Add(libCard3);
        p1.Library.Add(libCard2);
        p1.Library.Add(libCard1);

        var attunement = new GameCard { Name = "Attunement", CardTypes = CardType.Enchantment };

        // After drawing 3, hand has 3 cards. Min(4, 3) = 3 to discard
        var effect = new AttunementEffect();
        var context = new EffectContext(state, p1, attunement, handler);
        await effect.Execute(context);

        // Drew 3, discarded 3 (min of 4 and hand size 3) = 0 in hand
        p1.Hand.Cards.Should().HaveCount(0);
        p1.Graveyard.Cards.Should().HaveCount(3);
    }

    [Fact]
    public async Task AttunementEffect_EmptyLibrary_GameOver()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        // Empty library
        var attunement = new GameCard { Name = "Attunement", CardTypes = CardType.Enchantment };
        var effect = new AttunementEffect();
        var context = new EffectContext(state, p1, attunement, handler);
        await effect.Execute(context);

        state.IsGameOver.Should().BeTrue();
        state.Winner.Should().Be(p2.Name);
    }

    // =====================================================================
    // FlashbackCost — ExileBlueCardsFromGraveyard field
    // =====================================================================

    [Fact]
    public void FlashbackCost_ExileBlueCardsFromGraveyard_DefaultsToZero()
    {
        var cost = new FlashbackCost(ManaCost.Parse("{1}{U}"));
        cost.ExileBlueCardsFromGraveyard.Should().Be(0);
    }

    [Fact]
    public void FlashbackCost_ExileBlueCardsFromGraveyard_CanBeSet()
    {
        var cost = new FlashbackCost(ManaCost.Parse("{1}{U}"), ExileBlueCardsFromGraveyard: 1);
        cost.ExileBlueCardsFromGraveyard.Should().Be(1);
    }

    // =====================================================================
    // ActivatedAbilityCost — ReturnSelfToHand field
    // =====================================================================

    [Fact]
    public void ActivatedAbilityCost_ReturnSelfToHand_DefaultsToFalse()
    {
        var cost = new ActivatedAbilityCost();
        cost.ReturnSelfToHand.Should().BeFalse();
    }

    [Fact]
    public void ActivatedAbilityCost_ReturnSelfToHand_CanBeSet()
    {
        var cost = new ActivatedAbilityCost(ReturnSelfToHand: true);
        cost.ReturnSelfToHand.Should().BeTrue();
    }

    // =====================================================================
    // StackObject — XValue property
    // =====================================================================

    [Fact]
    public void StackObject_XValue_DefaultsToNull()
    {
        var card = new GameCard { Name = "Test" };
        var spell = new StackObject(card, Guid.NewGuid(), new Dictionary<ManaColor, int>(), [], 0);
        spell.XValue.Should().BeNull();
    }

    [Fact]
    public void StackObject_XValue_CanBeSet()
    {
        var card = new GameCard { Name = "Test" };
        var spell = new StackObject(card, Guid.NewGuid(), new Dictionary<ManaColor, int>(), [], 0)
        {
            XValue = 5,
        };
        spell.XValue.Should().Be(5);
    }

    // =====================================================================
    // ActivateAbilityHandler — ReturnSelfToHand integration
    // =====================================================================

    [Fact]
    public async Task ActivateAbility_ReturnSelfToHand_MovesCardToHand()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.ActivePlayer = p1;
        var engine = new GameEngine(state);

        var attunement = GameCard.Create("Attunement");
        p1.Battlefield.Add(attunement);

        // Give player cards in library to draw
        for (int i = 0; i < 10; i++)
            p1.Library.Add(new GameCard { Name = $"Card {i}" });

        // Give initial hand cards so discard works (need 4 total in hand after drawing 3)
        var existing1 = new GameCard { Name = "Existing 1" };
        var existing2 = new GameCard { Name = "Existing 2" };
        p1.Hand.Add(existing1);
        p1.Hand.Add(existing2);

        var action = GameAction.ActivateAbility(p1.Id, attunement.Id);
        handler.EnqueueAction(action);
        handler.EnqueueAction(GameAction.Pass(p1.Id));

        await engine.ExecuteAction(action, ct: CancellationToken.None);

        // Attunement should now be in hand (returned as cost)
        p1.Hand.Cards.Should().Contain(c => c.Name == "Attunement");
        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Attunement");
    }
}
