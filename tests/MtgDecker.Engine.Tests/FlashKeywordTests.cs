using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class FlashKeywordTests : IDisposable
{
    private const string FlashCreatureName = "Flash Test Creature";
    private const string NonFlashCreatureName = "NonFlash Test Creature";

    public FlashKeywordTests()
    {
        // Register a test creature with HasFlash = true
        CardDefinitions.Register(new CardDefinition(
            ManaCost: ManaCost.Parse("{1}{B}"),
            ManaAbility: null,
            Power: 1,
            Toughness: 1,
            CardTypes: CardType.Creature
        ) { Name = FlashCreatureName, HasFlash = true });

        // Register a test creature without HasFlash
        CardDefinitions.Register(new CardDefinition(
            ManaCost: ManaCost.Parse("{1}{B}"),
            ManaAbility: null,
            Power: 2,
            Toughness: 2,
            CardTypes: CardType.Creature
        ) { Name = NonFlashCreatureName });
    }

    public void Dispose()
    {
        CardDefinitions.Unregister(FlashCreatureName);
        CardDefinitions.Unregister(NonFlashCreatureName);
    }

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
    public async Task FlashCreature_CanBeCastByNonActivePlayer()
    {
        var (engine, state, _, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        // P1 is active player — P2 (non-active) casts a Flash creature

        var card = GameCard.Create(FlashCreatureName);
        state.Player2.Hand.Add(card);
        state.Player2.ManaPool.Add(ManaColor.Black, 1);
        state.Player2.ManaPool.Add(ManaColor.Colorless, 1);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player2.Id, card.Id));

        state.Stack.Should().HaveCount(1,
            "Flash creature should be castable during opponent's turn");
        state.Stack[0].ControllerId.Should().Be(state.Player2.Id);
    }

    [Fact]
    public async Task FlashCreature_CanBeCastDuringCombat()
    {
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.Combat;

        var card = GameCard.Create(FlashCreatureName);
        state.Player1.Hand.Add(card);
        state.Player1.ManaPool.Add(ManaColor.Black, 1);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 1);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, card.Id));

        state.Stack.Should().HaveCount(1,
            "Flash creature should be castable during combat");
    }

    [Fact]
    public async Task NonFlashCreature_CannotBeCastByNonActivePlayer()
    {
        var (engine, state, _, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        // P1 is active player — P2 (non-active) tries to cast a normal creature

        var card = GameCard.Create(NonFlashCreatureName);
        state.Player2.Hand.Add(card);
        state.Player2.ManaPool.Add(ManaColor.Black, 1);
        state.Player2.ManaPool.Add(ManaColor.Colorless, 1);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player2.Id, card.Id));

        state.Stack.Should().BeEmpty(
            "non-Flash creature should not be castable during opponent's turn");
        state.Player2.Hand.Cards.Should().Contain(c => c.Id == card.Id);
    }

    [Fact]
    public async Task NonFlashCreature_CannotBeCastDuringCombat()
    {
        var (engine, state, _, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.Combat;

        var card = GameCard.Create(NonFlashCreatureName);
        state.Player1.Hand.Add(card);
        state.Player1.ManaPool.Add(ManaColor.Black, 1);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 1);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, card.Id));

        state.Stack.Should().BeEmpty(
            "non-Flash creature should not be castable during combat");
        state.Player1.Hand.Cards.Should().Contain(c => c.Id == card.Id);
    }

    [Fact]
    public async Task FlashCreature_CanBeCastDuringOwnMainPhase()
    {
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var card = GameCard.Create(FlashCreatureName);
        state.Player1.Hand.Add(card);
        state.Player1.ManaPool.Add(ManaColor.Black, 1);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 1);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, card.Id));

        state.Stack.Should().HaveCount(1,
            "Flash creature should be castable during own main phase");
    }
}
