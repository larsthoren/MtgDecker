using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class Phase12AngerTests
{
    [Fact]
    public void Anger_HasGraveyardAbilities()
    {
        CardDefinitions.TryGet("Anger", out var def).Should().BeTrue();
        def!.GraveyardAbilities.Should().ContainSingle();
        def.GraveyardAbilities[0].Type.Should().Be(ContinuousEffectType.GrantKeyword);
        def.GraveyardAbilities[0].GrantedKeyword.Should().Be(Keyword.Haste);
    }

    [Fact]
    public void Anger_NoLongerHasBattlefieldContinuousEffects()
    {
        CardDefinitions.TryGet("Anger", out var def).Should().BeTrue();
        def!.ContinuousEffects.Should().BeEmpty();
    }

    [Fact]
    public void Anger_InGraveyard_WithMountain_GrantsHaste()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // Anger in P1's graveyard
        var anger = GameCard.Create("Anger");
        p1.Graveyard.Add(anger);

        // Mountain on P1's battlefield
        var mountain = new GameCard { Name = "Mountain", CardTypes = CardType.Land };
        p1.Battlefield.Add(mountain);

        // A creature on P1's battlefield
        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p1.Battlefield.Add(creature);

        engine.RecalculateState();

        creature.ActiveKeywords.Should().Contain(Keyword.Haste);
    }

    [Fact]
    public void Anger_InGraveyard_NoMountain_NoHaste()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // Anger in graveyard but NO Mountain
        var anger = GameCard.Create("Anger");
        p1.Graveyard.Add(anger);

        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p1.Battlefield.Add(creature);

        engine.RecalculateState();

        creature.ActiveKeywords.Should().NotContain(Keyword.Haste);
    }

    [Fact]
    public void Anger_InGraveyard_WithDualLandMountain_GrantsHaste()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var anger = GameCard.Create("Anger");
        p1.Graveyard.Add(anger);

        // Dual land with Mountain subtype (e.g. Taiga)
        var taiga = new GameCard { Name = "Taiga", CardTypes = CardType.Land, Subtypes = ["Mountain", "Forest"] };
        p1.Battlefield.Add(taiga);

        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p1.Battlefield.Add(creature);

        engine.RecalculateState();

        creature.ActiveKeywords.Should().Contain(Keyword.Haste);
    }

    [Fact]
    public void Anger_OnlyAffectsOwnersCreatures()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P1 has Anger in graveyard + Mountain
        var anger = GameCard.Create("Anger");
        p1.Graveyard.Add(anger);
        p1.Battlefield.Add(new GameCard { Name = "Mountain", CardTypes = CardType.Land });

        var p1Creature = new GameCard { Name = "P1 Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p1.Battlefield.Add(p1Creature);

        var p2Creature = new GameCard { Name = "P2 Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p2.Battlefield.Add(p2Creature);

        engine.RecalculateState();

        p1Creature.ActiveKeywords.Should().Contain(Keyword.Haste);
        p2Creature.ActiveKeywords.Should().NotContain(Keyword.Haste);
    }

    [Fact]
    public void Anger_OnBattlefield_DoesNotGrantHaste()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // Anger ON battlefield (not in graveyard)
        var anger = GameCard.Create("Anger");
        p1.Battlefield.Add(anger);
        p1.Battlefield.Add(new GameCard { Name = "Mountain", CardTypes = CardType.Land });

        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p1.Battlefield.Add(creature);

        engine.RecalculateState();

        // Anger on battlefield should NOT grant haste to other creatures
        creature.ActiveKeywords.Should().NotContain(Keyword.Haste);
    }

    [Fact]
    public void Anger_InGraveyard_WithMountain_GrantsHasteToAllOwnCreatures()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var anger = GameCard.Create("Anger");
        p1.Graveyard.Add(anger);
        p1.Battlefield.Add(new GameCard { Name = "Mountain", CardTypes = CardType.Land });

        var creature1 = new GameCard { Name = "Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        var creature2 = new GameCard { Name = "Elf", CardTypes = CardType.Creature, BasePower = 1, BaseToughness = 1 };
        p1.Battlefield.Add(creature1);
        p1.Battlefield.Add(creature2);

        engine.RecalculateState();

        creature1.ActiveKeywords.Should().Contain(Keyword.Haste);
        creature2.ActiveKeywords.Should().Contain(Keyword.Haste);
    }

    [Fact]
    public void Anger_OpponentHasMountainButNotAnger_NoHaste()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P1 has Anger in graveyard but NO Mountain
        var anger = GameCard.Create("Anger");
        p1.Graveyard.Add(anger);

        // P2 has a Mountain but NOT Anger in graveyard
        p2.Battlefield.Add(new GameCard { Name = "Mountain", CardTypes = CardType.Land });

        var p1Creature = new GameCard { Name = "P1 Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p1.Battlefield.Add(p1Creature);

        var p2Creature = new GameCard { Name = "P2 Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p2.Battlefield.Add(p2Creature);

        engine.RecalculateState();

        // P1 has Anger in graveyard but no Mountain on P1's battlefield => no haste for P1
        p1Creature.ActiveKeywords.Should().NotContain(Keyword.Haste);
        // P2 doesn't have Anger in graveyard => no haste for P2
        p2Creature.ActiveKeywords.Should().NotContain(Keyword.Haste);
    }
}
