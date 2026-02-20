using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class TransformTests
{
    [Fact]
    public void GameCard_IsTransformed_DefaultsFalse()
    {
        var card = new GameCard { Name = "Delver of Secrets" };

        card.IsTransformed.Should().BeFalse();
    }

    [Fact]
    public void GameCard_BackFaceDefinition_DefaultsNull()
    {
        var card = new GameCard { Name = "Delver of Secrets" };

        card.BackFaceDefinition.Should().BeNull();
    }

    [Fact]
    public void GameCard_WhenTransformed_NameFromBackFace()
    {
        var backFace = new CardDefinition(
            ManaCost: null,
            ManaAbility: null,
            Power: 3,
            Toughness: 2,
            CardTypes: CardType.Creature)
        {
            Name = "Insectile Aberration",
        };

        var card = new GameCard
        {
            Name = "Delver of Secrets",
            TypeLine = "Creature — Human Wizard",
            BackFaceDefinition = backFace,
        };

        card.IsTransformed = true;

        card.Name.Should().Be("Insectile Aberration");
    }

    [Fact]
    public void GameCard_WhenTransformed_CardTypesFromBackFace()
    {
        var backFace = new CardDefinition(
            ManaCost: null,
            ManaAbility: null,
            Power: null,
            Toughness: null,
            CardTypes: CardType.Land)
        {
            Name = "Search for Azcanta",
        };

        var card = new GameCard
        {
            Name = "Search for Azcanta",
            TypeLine = "Legendary Enchantment",
            CardTypes = CardType.Enchantment,
            BackFaceDefinition = backFace,
        };

        card.IsTransformed = true;

        card.CardTypes.Should().Be(CardType.Land);
    }

    [Fact]
    public void GameCard_WhenTransformed_PowerToughnessFromBackFace()
    {
        var backFace = new CardDefinition(
            ManaCost: null,
            ManaAbility: null,
            Power: 3,
            Toughness: 2,
            CardTypes: CardType.Creature)
        {
            Name = "Insectile Aberration",
        };

        var card = new GameCard
        {
            Name = "Delver of Secrets",
            TypeLine = "Creature — Human Wizard",
            BasePower = 1,
            BaseToughness = 1,
            BackFaceDefinition = backFace,
        };

        card.IsTransformed = true;

        card.BasePower.Should().Be(3);
        card.BaseToughness.Should().Be(2);
    }

    [Fact]
    public void GameCard_WhenNotTransformed_UsesOwnProperties()
    {
        var backFace = new CardDefinition(
            ManaCost: null,
            ManaAbility: null,
            Power: 3,
            Toughness: 2,
            CardTypes: CardType.Creature)
        {
            Name = "Insectile Aberration",
        };

        var card = new GameCard
        {
            Name = "Delver of Secrets",
            TypeLine = "Creature — Human Wizard",
            BasePower = 1,
            BaseToughness = 1,
            CardTypes = CardType.Creature,
            BackFaceDefinition = backFace,
        };

        // Not transformed — should use front face values
        card.Name.Should().Be("Delver of Secrets");
        card.CardTypes.Should().Be(CardType.Creature);
        card.BasePower.Should().Be(1);
        card.BaseToughness.Should().Be(1);
    }

    [Fact]
    public void GameCard_FrontName_AlwaysReturnsFrontFaceName()
    {
        var backFace = new CardDefinition(
            ManaCost: null,
            ManaAbility: null,
            Power: 3,
            Toughness: 2,
            CardTypes: CardType.Creature)
        {
            Name = "Insectile Aberration",
        };

        var card = new GameCard
        {
            Name = "Delver of Secrets",
            BackFaceDefinition = backFace,
        };

        // Before transform
        card.FrontName.Should().Be("Delver of Secrets");

        // After transform — FrontName still returns front
        card.IsTransformed = true;
        card.FrontName.Should().Be("Delver of Secrets");
        card.Name.Should().Be("Insectile Aberration");
    }

    [Fact]
    public void GameCard_WhenTransformed_WithoutBackFace_UsesOwnProperties()
    {
        var card = new GameCard
        {
            Name = "Some Card",
            BasePower = 2,
            BaseToughness = 3,
            CardTypes = CardType.Creature,
        };

        // Setting IsTransformed without BackFaceDefinition shouldn't break anything
        card.IsTransformed = true;

        card.Name.Should().Be("Some Card");
        card.BasePower.Should().Be(2);
        card.BaseToughness.Should().Be(3);
        card.CardTypes.Should().Be(CardType.Creature);
    }

    [Fact]
    public void GameCardCreate_WithTransformDefinition_SetsBackFaceDefinition()
    {
        var backFace = new CardDefinition(null, null, 5, 5, CardType.Planeswalker)
        { Name = "Test Back Face", StartingLoyalty = 3 };
        CardDefinitions.Register(new CardDefinition(null, null, 0, 3, CardType.Creature)
        { Name = "Test Transform Card", TransformInto = backFace });
        try
        {
            var card = GameCard.Create("Test Transform Card");
            card.BackFaceDefinition.Should().NotBeNull();
            card.BackFaceDefinition!.Name.Should().Be("Test Back Face");
        }
        finally
        {
            CardDefinitions.Unregister("Test Transform Card");
        }
    }

    [Fact]
    public async Task TransformExileReturnEffect_TransformsCard()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var backFace = new CardDefinition(null, null, null, null, CardType.Planeswalker)
        { Name = "PW Back", StartingLoyalty = 3 };
        var card = new GameCard
        {
            Name = "Creature Front",
            CardTypes = CardType.Creature,
            BasePower = 0,
            BaseToughness = 3,
            BackFaceDefinition = backFace,
        };
        p1.Battlefield.Add(card);

        var effect = new TransformExileReturnEffect();
        var context = new EffectContext(state, p1, card, handler);
        await effect.Execute(context);

        p1.Battlefield.Cards.Should().Contain(c => c.Id == card.Id);
        card.IsTransformed.Should().BeTrue();
        card.IsPlaneswalker.Should().BeTrue();
    }

    [Fact]
    public async Task TransformExileReturnEffect_AddsLoyaltyCounters()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var backFace = new CardDefinition(null, null, null, null, CardType.Planeswalker)
        { Name = "PW Back", StartingLoyalty = 2 };
        var card = new GameCard
        {
            Name = "Creature Front",
            CardTypes = CardType.Creature,
            BackFaceDefinition = backFace,
        };
        p1.Battlefield.Add(card);

        var effect = new TransformExileReturnEffect();
        var context = new EffectContext(state, p1, card, handler);
        await effect.Execute(context);

        card.Loyalty.Should().Be(2);
    }

    [Fact]
    public async Task TransformExileReturnEffect_NoLoyaltyCounters_WhenNotPlaneswalker()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var backFace = new CardDefinition(null, null, 3, 2, CardType.Creature)
        { Name = "Creature Back" };
        var card = new GameCard
        {
            Name = "Creature Front",
            CardTypes = CardType.Creature,
            BasePower = 1,
            BaseToughness = 1,
            BackFaceDefinition = backFace,
        };
        p1.Battlefield.Add(card);

        var effect = new TransformExileReturnEffect();
        var context = new EffectContext(state, p1, card, handler);
        await effect.Execute(context);

        card.IsTransformed.Should().BeTrue();
        card.Loyalty.Should().Be(0);
        card.IsCreature.Should().BeTrue();
    }

    // --- Task 6: TriggerCondition.ThirdDrawInTurn ---

    [Fact]
    public void ThirdDrawTrigger_FiresOnThirdDraw()
    {
        // Setup: card with trigger GameEvent.DrawCard + TriggerCondition.ThirdDrawInTurn
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var backFace = new CardDefinition(null, null, 3, 3, CardType.Creature)
        { Name = "Transformed Side" };
        var card = new GameCard
        {
            Name = "Front Side",
            CardTypes = CardType.Creature,
            BasePower = 0,
            BaseToughness = 3,
            BackFaceDefinition = backFace,
            Triggers = [new Trigger(GameEvent.DrawCard, TriggerCondition.ThirdDrawInTurn, new TransformExileReturnEffect())]
        };
        p1.Battlefield.Add(card);

        // Put 3 cards in library for p1 to draw
        for (int i = 0; i < 3; i++)
            p1.Library.Add(new GameCard { Name = $"Card {i}" });

        // Draw 3 cards — should fire the trigger and push to stack
        engine.DrawCards(p1, 3);

        // The trigger should have been pushed to the stack
        state.StackCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ThirdDrawTrigger_DoesNotFireBeforeThirdDraw()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var backFace = new CardDefinition(null, null, 3, 3, CardType.Creature)
        { Name = "Transformed Side" };
        var card = new GameCard
        {
            Name = "Front Side",
            CardTypes = CardType.Creature,
            BasePower = 0,
            BaseToughness = 3,
            BackFaceDefinition = backFace,
            Triggers = [new Trigger(GameEvent.DrawCard, TriggerCondition.ThirdDrawInTurn, new TransformExileReturnEffect())]
        };
        p1.Battlefield.Add(card);

        // Put 2 cards in library for p1 to draw
        for (int i = 0; i < 2; i++)
            p1.Library.Add(new GameCard { Name = $"Card {i}" });

        // Draw only 2 cards — should NOT fire the trigger
        engine.DrawCards(p1, 2);

        // No trigger on the stack
        state.StackCount.Should().Be(0);
    }

    [Fact]
    public void ThirdDrawTrigger_OnlyFiresForController()
    {
        // The trigger should only fire when the CONTROLLER of the card draws the third card,
        // not when the opponent does
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var backFace = new CardDefinition(null, null, 3, 3, CardType.Creature)
        { Name = "Transformed Side" };
        var card = new GameCard
        {
            Name = "Front Side",
            CardTypes = CardType.Creature,
            BasePower = 0,
            BaseToughness = 3,
            BackFaceDefinition = backFace,
            Triggers = [new Trigger(GameEvent.DrawCard, TriggerCondition.ThirdDrawInTurn, new TransformExileReturnEffect())]
        };
        // Card is on P1's battlefield
        p1.Battlefield.Add(card);

        // Put 3 cards in P2's library and have P2 draw 3
        for (int i = 0; i < 3; i++)
            p2.Library.Add(new GameCard { Name = $"Card {i}" });

        engine.DrawCards(p2, 3);

        // Trigger should NOT fire — P2 drew, but the card is controlled by P1
        state.StackCount.Should().Be(0);
    }

    // --- Task 7: ExpiresOnTurnNumber for ContinuousEffect ---

    [Fact]
    public void ContinuousEffect_ExpiresOnTurnNumber_RemovedAtTurnStart()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        state.TurnNumber = 1;

        var effect = new ContinuousEffect(
            Guid.NewGuid(), ContinuousEffectType.ModifyPowerToughness,
            (card, _) => card.IsCreature,
            PowerMod: -1,
            ExpiresOnTurnNumber: 3,
            Layer: EffectLayer.Layer7c_ModifyPT);
        state.ActiveEffects.Add(effect);

        // Turn 2 — effect should still be there
        state.TurnNumber = 2;
        engine.RemoveExpiredEffects();
        state.ActiveEffects.Should().Contain(effect);

        // Turn 3 — effect should be removed
        state.TurnNumber = 3;
        engine.RemoveExpiredEffects();
        state.ActiveEffects.Should().NotContain(effect);
    }

    [Fact]
    public void ContinuousEffect_ExpiresOnTurnNumber_NotRemovedBeforeExpiry()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        state.TurnNumber = 1;

        var effect = new ContinuousEffect(
            Guid.NewGuid(), ContinuousEffectType.ModifyPowerToughness,
            (card, _) => card.IsCreature,
            PowerMod: +2,
            ExpiresOnTurnNumber: 5,
            Layer: EffectLayer.Layer7c_ModifyPT);
        state.ActiveEffects.Add(effect);

        // Turn 4 — effect should still be there
        state.TurnNumber = 4;
        engine.RemoveExpiredEffects();
        state.ActiveEffects.Should().Contain(effect);
    }

    [Fact]
    public void ContinuousEffect_WithoutExpiresOnTurnNumber_NeverExpires()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var effect = new ContinuousEffect(
            Guid.NewGuid(), ContinuousEffectType.ModifyPowerToughness,
            (card, _) => card.IsCreature,
            PowerMod: -1,
            Layer: EffectLayer.Layer7c_ModifyPT);
        state.ActiveEffects.Add(effect);

        // Even at turn 100, effect without ExpiresOnTurnNumber should persist
        state.TurnNumber = 100;
        engine.RemoveExpiredEffects();
        state.ActiveEffects.Should().Contain(effect);
    }

    [Fact]
    public void ContinuousEffect_ExpiresOnTurnNumber_SurvivedByRecalculateState()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        state.TurnNumber = 1;

        var effect = new ContinuousEffect(
            Guid.NewGuid(), ContinuousEffectType.ModifyPowerToughness,
            (card, _) => card.IsCreature,
            PowerMod: -1,
            ExpiresOnTurnNumber: 5,
            Layer: EffectLayer.Layer7c_ModifyPT);
        state.ActiveEffects.Add(effect);

        // RecalculateState should preserve ExpiresOnTurnNumber effects (like UntilEndOfTurn)
        engine.RecalculateState();

        state.ActiveEffects.Should().ContainSingle(e => e.ExpiresOnTurnNumber == 5);
    }
}
