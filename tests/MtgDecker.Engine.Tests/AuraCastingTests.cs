using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class AuraCastingTests
{
    [Fact]
    public async Task Casting_Aura_Prompts_For_Target_And_Attaches()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var forest = GameCard.Create("Forest");
        p1.Battlefield.Add(forest);

        var wildGrowth = GameCard.Create("Wild Growth");
        wildGrowth.ManaCost = ManaCost.Parse("{G}");
        p1.Hand.Add(wildGrowth);
        p1.ManaPool.Add(ManaColor.Green, 1);

        // Queue the card choice: attach to forest
        handler.EnqueueCardChoice(forest.Id);

        var action = GameAction.CastSpell(p1.Id, wildGrowth.Id);
        await engine.ExecuteAction(action);
        await engine.ResolveAllTriggersAsync();

        wildGrowth.AttachedTo.Should().Be(forest.Id);
        p1.Battlefield.Cards.Should().Contain(wildGrowth);
    }

    [Fact]
    public async Task Aura_Falls_Off_When_Target_Leaves_Battlefield()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var forest = GameCard.Create("Forest");
        p1.Battlefield.Add(forest);

        var wildGrowth = new GameCard { Name = "Wild Growth", CardTypes = CardType.Enchantment, AttachedTo = forest.Id };
        p1.Battlefield.Add(wildGrowth);

        // Remove the land
        p1.Battlefield.RemoveById(forest.Id);

        // SBA check should move the aura to graveyard
        await engine.CheckStateBasedActionsAsync(default);

        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Wild Growth");
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Wild Growth");
    }

    [Fact]
    public async Task WildGrowth_Adds_Green_When_Enchanted_Land_Tapped()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var forest = GameCard.Create("Forest");
        p1.Battlefield.Add(forest);

        var wildGrowth = GameCard.Create("Wild Growth");
        wildGrowth.AttachedTo = forest.Id;
        p1.Battlefield.Add(wildGrowth);

        // Tap the forest
        var action = GameAction.TapCard(p1.Id, forest.Id);
        await engine.ExecuteAction(action);

        // Should get 1G from Forest + 1G from Wild Growth = 2G total
        p1.ManaPool.Available[ManaColor.Green].Should().Be(2);
    }

    [Fact]
    public async Task Aura_With_No_Valid_Target_Goes_To_Graveyard()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // No lands on battlefield
        var wildGrowth = GameCard.Create("Wild Growth");
        wildGrowth.ManaCost = ManaCost.Parse("{G}");
        p1.Hand.Add(wildGrowth);
        p1.ManaPool.Add(ManaColor.Green, 1);

        var action = GameAction.CastSpell(p1.Id, wildGrowth.Id);
        await engine.ExecuteAction(action);
        await engine.ResolveAllTriggersAsync();

        p1.Battlefield.Cards.Should().NotContain(wildGrowth);
        p1.Graveyard.Cards.Should().Contain(wildGrowth);
    }

    [Fact]
    public async Task Aura_SBA_Does_Not_Remove_Aura_When_Target_Still_Present()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var forest = GameCard.Create("Forest");
        p1.Battlefield.Add(forest);

        var wildGrowth = new GameCard { Name = "Wild Growth", CardTypes = CardType.Enchantment, AttachedTo = forest.Id };
        p1.Battlefield.Add(wildGrowth);

        // SBA check should NOT move the aura
        await engine.CheckStateBasedActionsAsync(default);

        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Wild Growth");
        p1.Graveyard.Cards.Should().NotContain(c => c.Name == "Wild Growth");
    }

    [Fact]
    public async Task WildGrowth_Does_Not_Trigger_When_Unenchanted_Land_Tapped()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var forest1 = GameCard.Create("Forest");
        var forest2 = GameCard.Create("Forest");
        p1.Battlefield.Add(forest1);
        p1.Battlefield.Add(forest2);

        // Wild Growth attached to forest1 only
        var wildGrowth = GameCard.Create("Wild Growth");
        wildGrowth.AttachedTo = forest1.Id;
        p1.Battlefield.Add(wildGrowth);

        // Tap forest2 (NOT enchanted)
        var action = GameAction.TapCard(p1.Id, forest2.Id);
        await engine.ExecuteAction(action);

        // Should only get 1G from forest2, no bonus
        p1.ManaPool.Available[ManaColor.Green].Should().Be(1);
    }

    [Fact]
    public void AddBonusManaEffect_Adds_Specified_Color()
    {
        var effect = new AddBonusManaEffect(ManaColor.Green);
        effect.Color.Should().Be(ManaColor.Green);
    }

    [Fact]
    public async Task AddBonusManaEffect_Executes_And_Adds_Mana()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var source = new GameCard { Name = "Wild Growth" };
        var ctx = new Engine.Triggers.EffectContext(state, p1, source, handler);

        var effect = new AddBonusManaEffect(ManaColor.Green);
        await effect.Execute(ctx);

        p1.ManaPool.Available[ManaColor.Green].Should().Be(1);
    }

}
