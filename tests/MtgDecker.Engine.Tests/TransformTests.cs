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
}
