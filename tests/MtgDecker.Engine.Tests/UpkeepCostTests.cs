using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class UpkeepCostTests
{
    private static (GameEngine engine, GameState state, Player p1, Player p2, TestDecisionHandler handler1) Setup()
    {
        var handler1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, p1, p2, handler1);
    }

    [Fact]
    public async Task Discards_Card_To_Keep_Enchantment()
    {
        var (engine, state, p1, _, handler1) = Setup();

        var enchantment = new GameCard { Name = "Solitary Confinement", CardTypes = CardType.Enchantment };
        p1.Battlefield.Add(enchantment);

        var handCard = new GameCard { Name = "Forest", CardTypes = CardType.Land };
        p1.Hand.Add(handCard);

        // Player chooses to discard the card
        handler1.EnqueueCardChoice(handCard.Id);

        var effect = new UpkeepCostEffect();
        var context = new EffectContext(state, p1, enchantment, handler1);
        await effect.Execute(context);

        // Enchantment should still be on battlefield
        p1.Battlefield.Cards.Should().Contain(c => c.Id == enchantment.Id);
        // Hand card should be in graveyard
        p1.Hand.Cards.Should().NotContain(c => c.Id == handCard.Id);
        p1.Graveyard.Cards.Should().Contain(c => c.Id == handCard.Id);
        state.GameLog.Should().Contain(l => l.Contains("discards") && l.Contains("Forest"));
    }

    [Fact]
    public async Task Sacrifices_When_No_Cards_In_Hand()
    {
        var (engine, state, p1, _, handler1) = Setup();

        var enchantment = new GameCard { Name = "Solitary Confinement", CardTypes = CardType.Enchantment };
        p1.Battlefield.Add(enchantment);

        // Hand is empty — no cards to discard

        var effect = new UpkeepCostEffect();
        var context = new EffectContext(state, p1, enchantment, handler1);
        await effect.Execute(context);

        // Enchantment should be sacrificed (moved to graveyard)
        p1.Battlefield.Cards.Should().NotContain(c => c.Id == enchantment.Id);
        p1.Graveyard.Cards.Should().Contain(c => c.Id == enchantment.Id);
        state.GameLog.Should().Contain(l => l.Contains("sacrificed"));
    }

    [Fact]
    public async Task Sacrifices_When_Player_Declines_Discard()
    {
        var (engine, state, p1, _, handler1) = Setup();

        var enchantment = new GameCard { Name = "Solitary Confinement", CardTypes = CardType.Enchantment };
        p1.Battlefield.Add(enchantment);

        var handCard = new GameCard { Name = "Forest", CardTypes = CardType.Land };
        p1.Hand.Add(handCard);

        // Player declines to discard (null choice)
        handler1.EnqueueCardChoice(null);

        var effect = new UpkeepCostEffect();
        var context = new EffectContext(state, p1, enchantment, handler1);
        await effect.Execute(context);

        // Enchantment should be sacrificed
        p1.Battlefield.Cards.Should().NotContain(c => c.Id == enchantment.Id);
        p1.Graveyard.Cards.Should().Contain(c => c.Id == enchantment.Id);
        // Hand card should still be in hand
        p1.Hand.Cards.Should().Contain(c => c.Id == handCard.Id);
        state.GameLog.Should().Contain(l => l.Contains("sacrificed"));
    }
}
