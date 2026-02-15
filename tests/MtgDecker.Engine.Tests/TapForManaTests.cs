using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class TapForManaTests
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
    public async Task TapBasicLand_AddsFixedManaToPool()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
        state.Player1.Battlefield.Add(mountain);

        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, mountain.Id));

        state.Player1.ManaPool[ManaColor.Red].Should().Be(1);
    }

    [Fact]
    public async Task TapBasicLand_SetsTappedTrue()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
        state.Player1.Battlefield.Add(mountain);

        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, mountain.Id));

        mountain.IsTapped.Should().BeTrue();
    }

    [Fact]
    public async Task TapPainLand_PromptsForChoice()
    {
        var (engine, state, handler) = CreateSetup();
        await engine.StartGameAsync();

        var karplusan = GameCard.Create("Karplusan Forest", "Land");
        state.Player1.Battlefield.Add(karplusan);

        handler.EnqueueManaColor(ManaColor.Green);

        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, karplusan.Id));

        state.Player1.ManaPool[ManaColor.Green].Should().Be(1);
    }

    [Fact]
    public async Task TapPainLand_AddsChosenColorToPool()
    {
        var (engine, state, handler) = CreateSetup();
        await engine.StartGameAsync();

        var karplusan = GameCard.Create("Karplusan Forest", "Land");
        state.Player1.Battlefield.Add(karplusan);

        handler.EnqueueManaColor(ManaColor.Red);

        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, karplusan.Id));

        state.Player1.ManaPool[ManaColor.Red].Should().Be(1);
    }

    [Fact]
    public async Task TapCardWithNoManaAbility_DoesNotAddMana()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var goblin = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        state.Player1.Battlefield.Add(goblin);

        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, goblin.Id));

        state.Player1.ManaPool.Total.Should().Be(0);
    }

    [Fact]
    public async Task TapCardWithNoManaAbility_StillSetsTapped()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var goblin = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        state.Player1.Battlefield.Add(goblin);

        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, goblin.Id));

        goblin.IsTapped.Should().BeTrue();
    }

    [Fact]
    public async Task TapCard_WithNoRegistryEntry_WorksAsBefore()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var unknownCard = new GameCard { Name = "Unknown Widget" };
        state.Player1.Battlefield.Add(unknownCard);

        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, unknownCard.Id));

        unknownCard.IsTapped.Should().BeTrue();
        state.Player1.ManaPool.Total.Should().Be(0);
    }

    [Fact]
    public async Task TapPainLand_ColoredChoice_Deals1Damage()
    {
        var (engine, state, handler) = CreateSetup();
        await engine.StartGameAsync();

        var brushland = GameCard.Create("Brushland", "Land");
        state.Player1.Battlefield.Add(brushland);

        handler.EnqueueManaColor(ManaColor.Green);

        var initialLife = state.Player1.Life;
        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, brushland.Id));

        state.Player1.ManaPool[ManaColor.Green].Should().Be(1);
        state.Player1.Life.Should().Be(initialLife - 1, "painland should deal 1 damage when tapped for colored mana");
    }

    [Fact]
    public async Task TapPainLand_ColorlessChoice_NoDamage()
    {
        var (engine, state, handler) = CreateSetup();
        await engine.StartGameAsync();

        var brushland = GameCard.Create("Brushland", "Land");
        state.Player1.Battlefield.Add(brushland);

        handler.EnqueueManaColor(ManaColor.Colorless);

        var initialLife = state.Player1.Life;
        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, brushland.Id));

        state.Player1.ManaPool[ManaColor.Colorless].Should().Be(1);
        state.Player1.Life.Should().Be(initialLife, "painland should not deal damage when tapped for colorless mana");
    }

    [Fact]
    public async Task TapCityOfBrass_DealsOneDamage()
    {
        var (engine, state, handler) = CreateSetup();
        await engine.StartGameAsync();

        var city = GameCard.Create("City of Brass", "Land");
        state.Player1.Battlefield.Add(city);

        handler.EnqueueManaColor(ManaColor.Blue);

        var initialLife = state.Player1.Life;
        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, city.Id));

        state.Player1.ManaPool[ManaColor.Blue].Should().Be(1);
        state.Player1.Life.Should().Be(initialLife - 1, "City of Brass should deal 1 damage when tapped for any color");
    }

    [Fact]
    public async Task TapAlreadyTappedCard_DoesNothing()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
        mountain.IsTapped = true;
        state.Player1.Battlefield.Add(mountain);

        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, mountain.Id));

        state.Player1.ManaPool.Total.Should().Be(0);
    }
}
