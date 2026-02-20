using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class InvestigateTests
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
    public async Task InvestigateEffect_CreatesClueToken()
    {
        // Arrange
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var source = new GameCard { Name = "TestSource" };
        state.Player1.Battlefield.Add(source);

        var effect = new InvestigateEffect();
        var context = new EffectContext(state, state.Player1, source, h1);

        // Act
        await effect.Execute(context);

        // Assert
        var clue = state.Player1.Battlefield.Cards.FirstOrDefault(c => c.Name == "Clue");
        clue.Should().NotBeNull("InvestigateEffect should create a Clue token on the battlefield");
        clue!.IsToken.Should().BeTrue("Clue should be a token");
    }

    [Fact]
    public async Task InvestigateEffect_ClueIsArtifact()
    {
        // Arrange
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();

        var source = new GameCard { Name = "TestSource" };
        state.Player1.Battlefield.Add(source);

        var effect = new InvestigateEffect();
        var context = new EffectContext(state, state.Player1, source, h1);

        // Act
        await effect.Execute(context);

        // Assert
        var clue = state.Player1.Battlefield.Cards.First(c => c.Name == "Clue");
        clue.CardTypes.HasFlag(CardType.Artifact).Should().BeTrue("Clue should be an Artifact");
    }

    [Fact]
    public async Task InvestigateEffect_ClueHasActivatedAbility()
    {
        // Arrange
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();

        var source = new GameCard { Name = "TestSource" };
        state.Player1.Battlefield.Add(source);

        var effect = new InvestigateEffect();
        var context = new EffectContext(state, state.Player1, source, h1);

        // Act
        await effect.Execute(context);

        // Assert
        var clue = state.Player1.Battlefield.Cards.First(c => c.Name == "Clue");
        clue.TokenActivatedAbility.Should().NotBeNull("Clue should have an activated ability (sacrifice + draw)");
        clue.TokenActivatedAbility!.Cost.SacrificeSelf.Should().BeTrue("Clue ability should sacrifice itself");
        clue.TokenActivatedAbility!.Cost.ManaCost.Should().NotBeNull("Clue ability should cost {2}");
        clue.TokenActivatedAbility!.Cost.ManaCost!.GenericCost.Should().Be(2);
    }

    [Fact]
    public async Task ClueToken_SacrificeAndDraw_WithMana()
    {
        // Arrange
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        // Create a Clue token via InvestigateEffect
        var source = new GameCard { Name = "TestSource" };
        state.Player1.Battlefield.Add(source);
        var effect = new InvestigateEffect();
        var context = new EffectContext(state, state.Player1, source, h1);
        await effect.Execute(context);

        var clue = state.Player1.Battlefield.Cards.First(c => c.Name == "Clue");
        var handSizeBefore = state.Player1.Hand.Count;

        // Add {2} mana to pay for Clue ability
        state.Player1.ManaPool.Add(ManaColor.Colorless, 2);

        // Act: activate the Clue's ability
        await engine.ExecuteAction(GameAction.ActivateAbility(state.Player1.Id, clue.Id));

        // Resolve the stack (ability goes on stack per MTG rules)
        await engine.ResolveAllTriggersAsync();

        // Assert
        state.Player1.Battlefield.Cards.Any(c => c.Id == clue.Id).Should().BeFalse(
            "Clue token should be sacrificed and removed from battlefield");
        state.Player1.Hand.Count.Should().Be(handSizeBefore + 1,
            "Player should have drawn a card from Clue sacrifice");
    }

    [Fact]
    public async Task ClueToken_CannotSacrifice_WithoutMana()
    {
        // Arrange
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        // Create a Clue token via InvestigateEffect
        var source = new GameCard { Name = "TestSource" };
        state.Player1.Battlefield.Add(source);
        var effect = new InvestigateEffect();
        var context = new EffectContext(state, state.Player1, source, h1);
        await effect.Execute(context);

        var clue = state.Player1.Battlefield.Cards.First(c => c.Name == "Clue");
        var handSizeBefore = state.Player1.Hand.Count;

        // No mana added — pool is empty

        // Act: attempt to activate the Clue's ability
        await engine.ExecuteAction(GameAction.ActivateAbility(state.Player1.Id, clue.Id));

        // Assert
        state.Player1.Battlefield.Cards.Any(c => c.Id == clue.Id).Should().BeTrue(
            "Clue token should still be on the battlefield when mana is insufficient");
        state.Player1.Hand.Count.Should().Be(handSizeBefore,
            "Player should not have drawn a card");
    }
}
