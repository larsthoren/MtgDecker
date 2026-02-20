using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class AdventureIntegrationTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler h1, TestDecisionHandler h2) CreateSetup()
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
        return (engine, state, h1, h2);
    }

    [Fact]
    public async Task FullAdventureFlow_CastAdventure_ThenCastCreatureFromExile()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var borrower = GameCard.Create("Brazen Borrower");
        state.Player1.Hand.Add(borrower);

        // Opponent has a creature to target with Petty Theft
        var oppCreature = new GameCard { Name = "Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        state.Player2.Battlefield.Add(oppCreature);

        // Step 1: Cast adventure (Petty Theft) — costs {1}{U}
        state.Player1.ManaPool.Add(ManaColor.Blue, 1);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 1);
        h1.EnqueueTarget(new TargetInfo(oppCreature.Id, state.Player2.Id, ZoneType.Battlefield));

        await engine.ExecuteAction(GameAction.CastAdventure(state.Player1.Id, borrower.Id));
        state.Stack.Should().HaveCount(1);

        // Resolve adventure
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        await engine.RunPriorityAsync();

        // After resolution: opponent's creature bounced, borrower in exile with IsOnAdventure
        state.Player2.Battlefield.Cards.Should().NotContain(c => c.Id == oppCreature.Id);
        state.Player2.Hand.Cards.Should().Contain(c => c.Id == oppCreature.Id);
        state.Player1.Exile.Cards.Should().Contain(c => c.Id == borrower.Id);
        var exiledBorrower = state.Player1.Exile.Cards.First(c => c.Id == borrower.Id);
        exiledBorrower.IsOnAdventure.Should().BeTrue();

        // Step 2: Cast creature side from exile — costs {1}{U}{U}
        state.Player1.ManaPool.Add(ManaColor.Blue, 2);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 1);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, borrower.Id));
        state.Stack.Should().HaveCount(1);
        state.Player1.Exile.Cards.Should().NotContain(c => c.Id == borrower.Id);

        // Resolve creature
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        await engine.RunPriorityAsync();

        // Borrower is on the battlefield as a 3/1 creature
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == borrower.Id);
        var onBattlefield = state.Player1.Battlefield.Cards.First(c => c.Id == borrower.Id);
        onBattlefield.IsOnAdventure.Should().BeFalse();
        onBattlefield.IsCreature.Should().BeTrue();
        onBattlefield.Power.Should().Be(3);
        onBattlefield.Toughness.Should().Be(1);
    }

    [Fact]
    public async Task BrazenBorrower_PettyTheft_BouncesOpponentPermanent()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var borrower = GameCard.Create("Brazen Borrower");
        state.Player1.Hand.Add(borrower);

        // Opponent has an artifact and a creature
        var artifact = new GameCard { Name = "Sol Ring", CardTypes = CardType.Artifact };
        state.Player2.Battlefield.Add(artifact);

        // Cast Petty Theft targeting the artifact
        state.Player1.ManaPool.Add(ManaColor.Blue, 1);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 1);
        h1.EnqueueTarget(new TargetInfo(artifact.Id, state.Player2.Id, ZoneType.Battlefield));

        await engine.ExecuteAction(GameAction.CastAdventure(state.Player1.Id, borrower.Id));

        // Resolve
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        await engine.RunPriorityAsync();

        // Artifact should be bounced to opponent's hand
        state.Player2.Battlefield.Cards.Should().NotContain(c => c.Id == artifact.Id);
        state.Player2.Hand.Cards.Should().Contain(c => c.Id == artifact.Id);

        // Borrower in exile
        state.Player1.Exile.Cards.Should().Contain(c => c.Id == borrower.Id);
    }
}
