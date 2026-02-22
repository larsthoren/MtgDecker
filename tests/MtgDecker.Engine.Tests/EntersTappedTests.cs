using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class EntersTappedTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler handler) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);

        for (int i = 0; i < 40; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}" });
            p2.Library.Add(new GameCard { Name = $"Card{i}" });
        }

        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, h1);
    }

    [Fact]
    public async Task PlayLand_CoastalTower_EntersTapped()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var tower = GameCard.Create("Coastal Tower", "Land");
        state.Player1.Hand.Add(tower);

        await engine.ExecuteAction(GameAction.PlayLand(state.Player1.Id, tower.Id));

        state.Player1.Battlefield.Cards.Should().Contain(c => c.Name == "Coastal Tower");
        tower.IsTapped.Should().BeTrue("Coastal Tower should enter the battlefield tapped");
    }

    [Fact]
    public async Task PlayLand_Mountain_DoesNotEnterTapped()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
        state.Player1.Hand.Add(mountain);

        await engine.ExecuteAction(GameAction.PlayLand(state.Player1.Id, mountain.Id));

        mountain.IsTapped.Should().BeFalse("basic lands should not enter tapped");
    }

    [Fact]
    public async Task PlayLand_TreetopVillage_EntersTapped()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var treetopVillage = GameCard.Create("Treetop Village", "Land");
        state.Player1.Hand.Add(treetopVillage);

        await engine.ExecuteAction(GameAction.PlayLand(state.Player1.Id, treetopVillage.Id));

        treetopVillage.IsTapped.Should().BeTrue("Treetop Village should enter the battlefield tapped");
    }

    [Fact]
    public async Task SpellResolution_EntersTappedCreature_DoesNotApply()
    {
        // EntersTapped only applies to lands in the current implementation
        // Creatures entering via spell resolution should not be affected
        var (engine, state, handler) = CreateSetup();
        await engine.StartGameAsync();

        // A normal creature should not enter tapped
        var goblin = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        state.Player1.Hand.Add(goblin);
        state.Player1.ManaPool.Add(ManaColor.Red);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, goblin.Id));
        await engine.ResolveAllTriggersAsync();

        goblin.IsTapped.Should().BeFalse("creatures should not enter tapped");
    }

    [Theory]
    [InlineData("Coastal Tower")]
    [InlineData("Treetop Village")]
    [InlineData("Faerie Conclave")]
    [InlineData("Spawning Pool")]
    [InlineData("Darigaaz's Caldera")]
    [InlineData("Treva's Ruins")]
    public void CardDefinition_EntersTapped_IsSet(string cardName)
    {
        CardDefinitions.TryGet(cardName, out var def).Should().BeTrue();
        def!.EntersTapped.Should().BeTrue($"{cardName} should be marked as enters-tapped");
    }

    [Theory]
    [InlineData("Mountain")]
    [InlineData("Forest")]
    [InlineData("Karplusan Forest")]
    [InlineData("City of Brass")]
    public void CardDefinition_NormalLand_DoesNotEnterTapped(string cardName)
    {
        CardDefinitions.TryGet(cardName, out var def).Should().BeTrue();
        def!.EntersTapped.Should().BeFalse($"{cardName} should not be marked as enters-tapped");
    }
}
