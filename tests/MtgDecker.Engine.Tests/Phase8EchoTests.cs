using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class Phase8EchoTests
{
    [Theory]
    [InlineData("Multani's Acolyte", "{G}{G}")]
    [InlineData("Deranged Hermit", "{3}{G}{G}")]
    [InlineData("Yavimaya Granger", "{2}{G}")]
    public void CardDefinition_EchoCost_IsSet(string cardName, string expectedEchoCost)
    {
        CardDefinitions.TryGet(cardName, out var def).Should().BeTrue();
        def!.EchoCost.Should().NotBeNull();
        def.EchoCost!.ToString().Should().Be(expectedEchoCost);
    }

    [Fact]
    public void GameCard_WithEcho_HasEchoPaidFalse()
    {
        var card = GameCard.Create("Multani's Acolyte");
        card.EchoPaid.Should().BeFalse();
    }

    [Fact]
    public void GameCard_WithoutEcho_HasEchoPaidTrue()
    {
        var card = GameCard.Create("Goblin Lackey");
        card.EchoPaid.Should().BeTrue();
    }

    [Fact]
    public async Task EchoEffect_PaysMana_SetsEchoPaidTrue()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var acolyte = GameCard.Create("Multani's Acolyte");
        acolyte.EchoPaid = false;
        p1.Battlefield.Add(acolyte);

        // Give player enough mana to pay {G}{G}
        p1.ManaPool.Add(ManaColor.Green, 2);

        // Enqueue card choice: choose the card (meaning "pay")
        handler.EnqueueCardChoice(acolyte.Id);

        var echoCost = ManaCost.Parse("{G}{G}");
        var effect = new EchoEffect(echoCost);
        var context = new EffectContext(state, p1, acolyte, handler);
        await effect.Execute(context);

        acolyte.EchoPaid.Should().BeTrue();
        p1.Battlefield.Cards.Should().Contain(c => c.Id == acolyte.Id);
        // Mana should have been spent
        p1.ManaPool.Total.Should().Be(0);
    }

    [Fact]
    public async Task EchoEffect_NoMana_SacrificesCreature()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var acolyte = GameCard.Create("Multani's Acolyte");
        acolyte.EchoPaid = false;
        p1.Battlefield.Add(acolyte);

        // No mana available
        var echoCost = ManaCost.Parse("{G}{G}");
        var effect = new EchoEffect(echoCost);
        var context = new EffectContext(state, p1, acolyte, handler);
        await effect.Execute(context);

        acolyte.EchoPaid.Should().BeFalse();
        p1.Battlefield.Cards.Should().NotContain(c => c.Id == acolyte.Id);
        p1.Graveyard.Cards.Should().Contain(c => c.Id == acolyte.Id);
    }

    [Fact]
    public async Task EchoEffect_PlayerDeclines_SacrificesCreature()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var acolyte = GameCard.Create("Multani's Acolyte");
        acolyte.EchoPaid = false;
        p1.Battlefield.Add(acolyte);

        // Give player enough mana
        p1.ManaPool.Add(ManaColor.Green, 2);

        // Enqueue null card choice: player declines to pay
        handler.EnqueueCardChoice(null);

        var echoCost = ManaCost.Parse("{G}{G}");
        var effect = new EchoEffect(echoCost);
        var context = new EffectContext(state, p1, acolyte, handler);
        await effect.Execute(context);

        p1.Battlefield.Cards.Should().NotContain(c => c.Id == acolyte.Id);
        p1.Graveyard.Cards.Should().Contain(c => c.Id == acolyte.Id);
    }

    [Fact]
    public async Task EchoEffect_CardNotOnBattlefield_DoesNothing()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var acolyte = GameCard.Create("Multani's Acolyte");
        acolyte.EchoPaid = false;
        // NOT added to battlefield

        p1.ManaPool.Add(ManaColor.Green, 2);

        var echoCost = ManaCost.Parse("{G}{G}");
        var effect = new EchoEffect(echoCost);
        var context = new EffectContext(state, p1, acolyte, handler);
        await effect.Execute(context);

        // Nothing should happen — card is not on battlefield
        p1.Battlefield.Cards.Should().BeEmpty();
        p1.Graveyard.Cards.Should().BeEmpty();
        p1.ManaPool.Total.Should().Be(2);
    }

    [Fact]
    public void DerangedHermit_HasSquirrelLordEffect()
    {
        CardDefinitions.TryGet("Deranged Hermit", out var def).Should().BeTrue();
        def!.ContinuousEffects.Should().ContainSingle();

        var lordEffect = def.ContinuousEffects[0];
        lordEffect.Type.Should().Be(ContinuousEffectType.ModifyPowerToughness);
        lordEffect.PowerMod.Should().Be(1);
        lordEffect.ToughnessMod.Should().Be(1);

        // Verify filter targets Squirrels
        var squirrelCard = new GameCard { Name = "Squirrel Token", CardTypes = CardType.Creature };
        squirrelCard.GetType().GetProperty("Subtypes")!.SetValue(squirrelCard, new List<string> { "Squirrel" }.AsReadOnly());
        // Use a dummy player for the Applies check
        var handler = new TestDecisionHandler();
        var player = new Player(Guid.NewGuid(), "P1", handler);
        lordEffect.Applies(squirrelCard, player).Should().BeTrue();

        // Non-squirrel should not match
        var goblinCard = new GameCard { Name = "Goblin", CardTypes = CardType.Creature };
        lordEffect.Applies(goblinCard, player).Should().BeFalse();
    }

    [Fact]
    public async Task Echo_TriggeredDuringUpkeep()
    {
        // Integration test: play echo creature, advance to next upkeep, verify echo trigger on stack
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // Put an echo creature directly on P1's battlefield with EchoPaid = false
        var acolyte = GameCard.Create("Multani's Acolyte");
        acolyte.EchoPaid.Should().BeFalse(); // sanity check
        p1.Battlefield.Add(acolyte);

        // Set up state for P1's upkeep
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.Upkeep;

        // Queue echo triggers
        await engine.QueueEchoTriggersOnStackAsync();

        // Verify echo trigger went on the stack
        state.StackCount.Should().BeGreaterThan(0);
        var stackItem = state.Stack[0];
        stackItem.Should().BeOfType<TriggeredAbilityStackObject>();
        var triggered = (TriggeredAbilityStackObject)stackItem;
        triggered.Effect.Should().BeOfType<EchoEffect>();
        triggered.Source.Name.Should().Be("Multani's Acolyte");
    }
}
