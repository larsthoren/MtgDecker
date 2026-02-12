using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class OpponentCostModTests
{
    [Fact]
    public void ContinuousEffect_CostAppliesToOpponent_Defaults_False()
    {
        var effect = new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyCost,
            (_, _) => true, CostMod: 2);
        effect.CostAppliesToOpponent.Should().BeFalse();
    }

    [Fact]
    public void ContinuousEffect_ExcludeSelf_Defaults_False()
    {
        var effect = new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyCost,
            (_, _) => true, CostMod: 2);
        effect.ExcludeSelf.Should().BeFalse();
    }

    [Fact]
    public async Task AuraOfSilence_Taxes_Opponent_Enchantments()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P1 has Aura of Silence on battlefield
        var aura = GameCard.Create("Aura of Silence");
        p1.Battlefield.Add(aura);
        engine.RecalculateState();

        // P2 tries to cast a {1}{G} enchantment — should cost {3}{G} instead
        var enchantment = new GameCard
        {
            Name = "Test Enchantment",
            CardTypes = CardType.Enchantment,
            ManaCost = ManaCost.Parse("{1}{G}")
        };
        p2.Hand.Add(enchantment);

        // Give P2 exactly {1}{G} — NOT enough (needs {3}{G})
        p2.ManaPool.Add(ManaColor.Green, 1);
        p2.ManaPool.Add(ManaColor.Colorless, 1);

        state.ActivePlayer = p2;
        var action = GameAction.PlayCard(p2.Id, enchantment.Id);
        await engine.ExecuteAction(action);

        // Card should still be in hand (not enough mana)
        p2.Hand.Cards.Should().Contain(c => c.Name == "Test Enchantment");
    }

    [Fact]
    public async Task AuraOfSilence_Taxes_Opponent_Artifacts()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P1 has Aura of Silence on battlefield
        var aura = GameCard.Create("Aura of Silence");
        p1.Battlefield.Add(aura);
        engine.RecalculateState();

        // P2 tries to cast a {2} artifact — should cost {4} instead
        var artifact = new GameCard
        {
            Name = "Test Artifact",
            CardTypes = CardType.Artifact,
            ManaCost = ManaCost.Parse("{2}")
        };
        p2.Hand.Add(artifact);

        // Give P2 exactly {2} — NOT enough (needs {4})
        p2.ManaPool.Add(ManaColor.Colorless, 2);

        state.ActivePlayer = p2;
        var action = GameAction.PlayCard(p2.Id, artifact.Id);
        await engine.ExecuteAction(action);

        // Card should still be in hand (not enough mana)
        p2.Hand.Cards.Should().Contain(c => c.Name == "Test Artifact");
    }

    [Fact]
    public async Task AuraOfSilence_Does_Not_Tax_Own_Enchantments()
    {
        var handler1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P1 has Aura of Silence
        var aura = GameCard.Create("Aura of Silence");
        p1.Battlefield.Add(aura);
        engine.RecalculateState();

        // P1 casts own enchantment — should NOT be taxed
        var enchantment = new GameCard
        {
            Name = "Test Enchantment",
            CardTypes = CardType.Enchantment,
            ManaCost = ManaCost.Parse("{1}{G}")
        };
        p1.Hand.Add(enchantment);
        p1.ManaPool.Add(ManaColor.Green, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 1);

        state.ActivePlayer = p1;
        var action = GameAction.PlayCard(p1.Id, enchantment.Id);
        await engine.ExecuteAction(action);

        // Card should be on battlefield (enough mana without tax)
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Test Enchantment");
    }

    [Fact]
    public async Task AuraOfSilence_Does_Not_Tax_Opponent_Creatures()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P1 has Aura of Silence on battlefield
        var aura = GameCard.Create("Aura of Silence");
        p1.Battlefield.Add(aura);
        engine.RecalculateState();

        // P2 casts a creature — should NOT be taxed
        var creature = new GameCard
        {
            Name = "Test Creature",
            CardTypes = CardType.Creature,
            ManaCost = ManaCost.Parse("{1}{G}"),
            BasePower = 2,
            BaseToughness = 2
        };
        p2.Hand.Add(creature);
        p2.ManaPool.Add(ManaColor.Green, 1);
        p2.ManaPool.Add(ManaColor.Colorless, 1);

        state.ActivePlayer = p2;
        var action = GameAction.PlayCard(p2.Id, creature.Id);
        await engine.ExecuteAction(action);

        // Card should be on battlefield (creature is not taxed)
        p2.Battlefield.Cards.Should().Contain(c => c.Name == "Test Creature");
    }

    [Fact]
    public async Task AuraOfSilence_Opponent_Can_Cast_With_Enough_Mana()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P1 has Aura of Silence on battlefield
        var aura = GameCard.Create("Aura of Silence");
        p1.Battlefield.Add(aura);
        engine.RecalculateState();

        // P2 casts a {1}{G} enchantment with {3}{G} mana — should succeed
        var enchantment = new GameCard
        {
            Name = "Test Enchantment",
            CardTypes = CardType.Enchantment,
            ManaCost = ManaCost.Parse("{1}{G}")
        };
        p2.Hand.Add(enchantment);
        p2.ManaPool.Add(ManaColor.Green, 1);
        p2.ManaPool.Add(ManaColor.Colorless, 3);

        state.ActivePlayer = p2;
        var action = GameAction.PlayCard(p2.Id, enchantment.Id);
        await engine.ExecuteAction(action);

        // Card should be on battlefield (enough mana with tax)
        p2.Battlefield.Cards.Should().Contain(c => c.Name == "Test Enchantment");
    }
}
