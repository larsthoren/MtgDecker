using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class ReplenishTests
{
    [Fact]
    public void Replenish_Has_SpellEffect()
    {
        CardDefinitions.TryGet("Replenish", out var def).Should().BeTrue();
        def!.Effect.Should().BeOfType<ReplenishEffect>();
    }

    [Fact]
    public void ReplenishEffect_Returns_Enchantments_From_Graveyard()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.TurnNumber = 5;

        var enchantment1 = new GameCard { Name = "Enchantment A", CardTypes = CardType.Enchantment };
        var enchantment2 = new GameCard { Name = "Enchantment B", CardTypes = CardType.Enchantment };
        var creature = new GameCard { Name = "Creature", CardTypes = CardType.Creature };
        p1.Graveyard.Add(enchantment1);
        p1.Graveyard.Add(enchantment2);
        p1.Graveyard.Add(creature);

        var replenish = new GameCard { Name = "Replenish" };
        var spell = new StackObject(replenish, p1.Id, new(), new(), 1);

        var effect = new ReplenishEffect();
        effect.Resolve(state, spell);

        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Enchantment A");
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Enchantment B");
        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Creature");
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Creature");
    }

    [Fact]
    public void ReplenishEffect_Skips_Auras_Without_Targets()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var aura = new GameCard { Name = "Wild Growth", CardTypes = CardType.Enchantment, Subtypes = ["Aura"] };
        var enchantment = new GameCard { Name = "Normal Enchantment", CardTypes = CardType.Enchantment };
        p1.Graveyard.Add(aura);
        p1.Graveyard.Add(enchantment);

        var replenish = new GameCard { Name = "Replenish" };
        var spell = new StackObject(replenish, p1.Id, new(), new(), 1);

        var effect = new ReplenishEffect();
        effect.Resolve(state, spell);

        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Normal Enchantment");
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Wild Growth");
    }
}
