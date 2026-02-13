using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class SummoningSicknessTests
{
    private (GameEngine engine, GameState state) CreateSetup()
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
        return (engine, state);
    }

    [Fact]
    public async Task TapCard_CreatureWithSummoningSickness_IsRejected()
    {
        var (engine, state) = CreateSetup();
        await engine.StartGameAsync();

        var creature = GameCard.Create("Grizzly Bears", "Creature — Bear");
        state.Player1.Battlefield.Add(creature);
        creature.TurnEnteredBattlefield = state.TurnNumber; // entered this turn

        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, creature.Id));

        creature.IsTapped.Should().BeFalse();
        state.GameLog.Should().Contain(l => l.Contains("summoning sickness"));
    }

    [Fact]
    public async Task TapCard_LandPlayedThisTurn_IsAllowed()
    {
        var (engine, state) = CreateSetup();
        await engine.StartGameAsync();

        var land = GameCard.Create("Forest", "Basic Land — Forest");
        land.ManaAbility = ManaAbility.Fixed(ManaColor.Green);
        state.Player1.Battlefield.Add(land);
        land.TurnEnteredBattlefield = state.TurnNumber; // entered this turn — but it's a land, exempt

        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, land.Id));

        land.IsTapped.Should().BeTrue();
    }

    [Fact]
    public async Task TapCard_CreatureFromPreviousTurn_IsAllowed()
    {
        var (engine, state) = CreateSetup();
        await engine.StartGameAsync();

        var creature = GameCard.Create("Grizzly Bears", "Creature — Bear");
        state.Player1.Battlefield.Add(creature);
        creature.TurnEnteredBattlefield = state.TurnNumber - 1; // entered last turn — no sickness

        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, creature.Id));

        creature.IsTapped.Should().BeTrue();
    }

    [Fact]
    public async Task TapCard_CreatureWithHaste_IgnoresSummoningSickness()
    {
        var (engine, state) = CreateSetup();
        await engine.StartGameAsync();

        var creature = GameCard.Create("Goblin Guide", "Creature — Goblin Scout");
        creature.ActiveKeywords.Add(Keyword.Haste);
        state.Player1.Battlefield.Add(creature);
        creature.TurnEnteredBattlefield = state.TurnNumber; // entered this turn — but has haste

        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, creature.Id));

        creature.IsTapped.Should().BeTrue();
    }

    [Fact]
    public async Task TapCard_ArtifactPlayedThisTurn_IsAllowed()
    {
        var (engine, state) = CreateSetup();
        await engine.StartGameAsync();

        var artifact = GameCard.Create("Sol Ring", "Artifact");
        artifact.ManaAbility = ManaAbility.Fixed(ManaColor.Colorless);
        state.Player1.Battlefield.Add(artifact);
        artifact.TurnEnteredBattlefield = state.TurnNumber;

        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, artifact.Id));

        artifact.IsTapped.Should().BeTrue(); // Non-creature, exempt from summoning sickness
    }
}
