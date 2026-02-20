using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class KaitoAbilityEffectsTests
{
    #region Helpers

    private static (EffectContext context, Player controller, Player opponent, GameState state,
        TestDecisionHandler controllerHandler, TestDecisionHandler opponentHandler) CreateTwoPlayerContext()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Controller", h1);
        var p2 = new Player(Guid.NewGuid(), "Opponent", h2);
        var state = new GameState(p1, p2);
        var source = new GameCard { Name = "Kaito, Bane of Nightmares" };
        var context = new EffectContext(state, p1, source, h1);
        return (context, p1, p2, state, h1, h2);
    }

    #endregion

    #region TapAndStunEffect

    [Fact]
    public async Task TapAndStun_TapsTargetCreature()
    {
        var (context, controller, opponent, state, handler, _) = CreateTwoPlayerContext();

        var creature = new GameCard
        {
            Name = "Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        opponent.Battlefield.Add(creature);

        handler.EnqueueCardChoice(creature.Id);

        var effect = new TapAndStunEffect();
        await effect.Execute(context);

        creature.IsTapped.Should().BeTrue();
    }

    [Fact]
    public async Task TapAndStun_PutsTwoStunCountersOnTarget()
    {
        var (context, controller, opponent, state, handler, _) = CreateTwoPlayerContext();

        var creature = new GameCard
        {
            Name = "Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        opponent.Battlefield.Add(creature);

        handler.EnqueueCardChoice(creature.Id);

        var effect = new TapAndStunEffect();
        await effect.Execute(context);

        creature.GetCounters(CounterType.Stun).Should().Be(2);
    }

    [Fact]
    public async Task TapAndStun_CanTargetOwnCreature()
    {
        var (context, controller, opponent, state, handler, _) = CreateTwoPlayerContext();

        var creature = new GameCard
        {
            Name = "Own Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        controller.Battlefield.Add(creature);

        handler.EnqueueCardChoice(creature.Id);

        var effect = new TapAndStunEffect();
        await effect.Execute(context);

        creature.IsTapped.Should().BeTrue();
        creature.GetCounters(CounterType.Stun).Should().Be(2);
    }

    [Fact]
    public async Task TapAndStun_FiltersOutShroudCreatures()
    {
        var (context, controller, opponent, state, handler, _) = CreateTwoPlayerContext();

        var shroudCreature = new GameCard
        {
            Name = "Shroud Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        shroudCreature.ActiveKeywords.Add(Keyword.Shroud);
        opponent.Battlefield.Add(shroudCreature);

        // No valid targets — choose null
        handler.EnqueueCardChoice(null);

        var effect = new TapAndStunEffect();
        await effect.Execute(context);

        shroudCreature.IsTapped.Should().BeFalse("shroud creatures should not be targetable");
        shroudCreature.GetCounters(CounterType.Stun).Should().Be(0);
    }

    [Fact]
    public async Task TapAndStun_FiltersOutOpponentHexproof_AllowsOwnHexproof()
    {
        var (context, controller, opponent, state, handler, _) = CreateTwoPlayerContext();

        // Opponent's hexproof creature — should NOT be targetable
        var oppHexproof = new GameCard
        {
            Name = "Opponent Hexproof Bear",
            CardTypes = CardType.Creature,
            BasePower = 3,
            BaseToughness = 3,
        };
        oppHexproof.ActiveKeywords.Add(Keyword.Hexproof);
        opponent.Battlefield.Add(oppHexproof);

        // Controller's hexproof creature — should be targetable
        var ownHexproof = new GameCard
        {
            Name = "Own Hexproof Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        ownHexproof.ActiveKeywords.Add(Keyword.Hexproof);
        controller.Battlefield.Add(ownHexproof);

        handler.EnqueueCardChoice(ownHexproof.Id);

        var effect = new TapAndStunEffect();
        await effect.Execute(context);

        // Own hexproof creature was targeted
        ownHexproof.IsTapped.Should().BeTrue();
        ownHexproof.GetCounters(CounterType.Stun).Should().Be(2);

        // Opponent's hexproof creature was NOT targeted
        oppHexproof.IsTapped.Should().BeFalse();
        oppHexproof.GetCounters(CounterType.Stun).Should().Be(0);
    }

    [Fact]
    public async Task TapAndStun_NoTargets_DoesNothing()
    {
        var (context, controller, opponent, state, handler, _) = CreateTwoPlayerContext();

        // No creatures on any battlefield

        var effect = new TapAndStunEffect();
        await effect.Execute(context);

        state.GameLog.Should().Contain(l => l.Contains("No legal targets"));
    }

    [Fact]
    public async Task TapAndStun_OptionalTargetDecline_DoesNothing()
    {
        var (context, controller, opponent, state, handler, _) = CreateTwoPlayerContext();

        var creature = new GameCard
        {
            Name = "Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        opponent.Battlefield.Add(creature);

        // Decline target
        handler.EnqueueCardChoice(null);

        var effect = new TapAndStunEffect();
        await effect.Execute(context);

        creature.IsTapped.Should().BeFalse("no target was selected");
        creature.GetCounters(CounterType.Stun).Should().Be(0);
    }

    [Fact]
    public async Task TapAndStun_Logs()
    {
        var (context, controller, opponent, state, handler, _) = CreateTwoPlayerContext();

        var creature = new GameCard
        {
            Name = "Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        opponent.Battlefield.Add(creature);

        handler.EnqueueCardChoice(creature.Id);

        var effect = new TapAndStunEffect();
        await effect.Execute(context);

        state.GameLog.Should().Contain(l => l.Contains("Bear") && l.Contains("tapped"));
        state.GameLog.Should().Contain(l => l.Contains("Bear") && l.Contains("stun counter"));
    }

    #endregion

    #region SurveilAndDrawEffect

    [Fact]
    public async Task SurveilAndDraw_SurveisTwo()
    {
        var (context, controller, opponent, state, handler, _) = CreateTwoPlayerContext();

        var card1 = new GameCard { Name = "Card A" };
        var card2 = new GameCard { Name = "Card B" };
        var drawCard = new GameCard { Name = "Draw Card" };

        // Library: bottom-to-top: drawCard, card2, card1
        controller.Library.AddToTop(drawCard);
        controller.Library.AddToTop(card2);
        controller.Library.AddToTop(card1);

        // Surveil: put card1 to graveyard, keep card2
        handler.EnqueueCardChoice(card1.Id);
        handler.EnqueueCardChoice(null);

        // No opponent lost life — no draw
        var effect = new SurveilAndDrawEffect();
        await effect.Execute(context);

        controller.Graveyard.Cards.Should().Contain(c => c.Name == "Card A");
        controller.Library.Cards.Should().Contain(c => c.Name == "Card B");
    }

    [Fact]
    public async Task SurveilAndDraw_DrawsForEachOpponentWhoLostLife()
    {
        var (context, controller, opponent, state, handler, _) = CreateTwoPlayerContext();

        var card1 = new GameCard { Name = "Surveil1" };
        var card2 = new GameCard { Name = "Surveil2" };
        var drawCard = new GameCard { Name = "Draw Me" };

        controller.Library.AddToTop(drawCard);
        controller.Library.AddToTop(card2);
        controller.Library.AddToTop(card1);

        // Both surveil cards go to graveyard
        handler.EnqueueCardChoice(card1.Id);
        handler.EnqueueCardChoice(card2.Id);

        // Opponent lost life this turn
        opponent.LifeLostThisTurn = 3;

        var effect = new SurveilAndDrawEffect();
        await effect.Execute(context);

        // Should draw 1 card (1 opponent lost life)
        controller.Hand.Cards.Should().Contain(c => c.Name == "Draw Me");
    }

    [Fact]
    public async Task SurveilAndDraw_NoOpponentLostLife_NoDraws()
    {
        var (context, controller, opponent, state, handler, _) = CreateTwoPlayerContext();

        var card1 = new GameCard { Name = "Surveil1" };
        var card2 = new GameCard { Name = "Surveil2" };
        var extraCard = new GameCard { Name = "No Draw" };

        controller.Library.AddToTop(extraCard);
        controller.Library.AddToTop(card2);
        controller.Library.AddToTop(card1);

        handler.EnqueueCardChoice(null);
        handler.EnqueueCardChoice(null);

        // Opponent did NOT lose life
        opponent.LifeLostThisTurn = 0;

        var effect = new SurveilAndDrawEffect();
        await effect.Execute(context);

        // No draws should happen
        controller.Hand.Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task SurveilAndDraw_EmptyLibrary_NoSurveilNoDraws()
    {
        var (context, controller, opponent, state, handler, _) = CreateTwoPlayerContext();

        // Library is empty
        opponent.LifeLostThisTurn = 5;

        var effect = new SurveilAndDrawEffect();
        await effect.Execute(context);

        controller.Hand.Cards.Should().BeEmpty();
        controller.Graveyard.Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task SurveilAndDraw_DrawFromEmptyLibraryAfterSurveil_DrawsNothing()
    {
        var (context, controller, opponent, state, handler, _) = CreateTwoPlayerContext();

        // Only 2 cards in library (surveil will look at them both)
        var card1 = new GameCard { Name = "Surveil1" };
        var card2 = new GameCard { Name = "Surveil2" };
        controller.Library.AddToTop(card2);
        controller.Library.AddToTop(card1);

        // Both go to graveyard during surveil
        handler.EnqueueCardChoice(card1.Id);
        handler.EnqueueCardChoice(card2.Id);

        opponent.LifeLostThisTurn = 1;

        var effect = new SurveilAndDrawEffect();
        await effect.Execute(context);

        // Surveil sent both to graveyard, library is empty, so draw draws nothing
        controller.Hand.Cards.Should().BeEmpty();
        controller.Graveyard.Cards.Should().HaveCount(2);
    }

    [Fact]
    public async Task SurveilAndDraw_LogsSurveilAndDraw()
    {
        var (context, controller, opponent, state, handler, _) = CreateTwoPlayerContext();

        var card1 = new GameCard { Name = "Card A" };
        var drawCard = new GameCard { Name = "Draw Card" };
        controller.Library.AddToTop(drawCard);
        controller.Library.AddToTop(card1);

        handler.EnqueueCardChoice(null); // keep card1 on top
        // surveil 2 but only 2 cards, second card is drawCard — already peeked by surveil
        // Actually: surveil peeks top 2: [card1, drawCard], then asks about each
        handler.EnqueueCardChoice(null); // keep drawCard on top

        opponent.LifeLostThisTurn = 2;

        var effect = new SurveilAndDrawEffect();
        await effect.Execute(context);

        state.GameLog.Should().Contain(l => l.Contains("surveil"));
        state.GameLog.Should().Contain(l => l.Contains("draws"));
    }

    #endregion

    #region CreateNinjaEmblemEffect

    [Fact]
    public async Task CreateNinjaEmblem_AddsEmblemToController()
    {
        var (context, controller, opponent, state, handler, _) = CreateTwoPlayerContext();

        var effect = new CreateNinjaEmblemEffect();
        await effect.Execute(context);

        controller.Emblems.Should().HaveCount(1);
        controller.Emblems[0].Description.Should().Contain("Ninja");
    }

    [Fact]
    public async Task CreateNinjaEmblem_HasModifyPowerToughnessEffect()
    {
        var (context, controller, opponent, state, handler, _) = CreateTwoPlayerContext();

        var effect = new CreateNinjaEmblemEffect();
        await effect.Execute(context);

        var emblem = controller.Emblems[0];
        emblem.Effect.Type.Should().Be(ContinuousEffectType.ModifyPowerToughness);
        emblem.Effect.PowerMod.Should().Be(1);
        emblem.Effect.ToughnessMod.Should().Be(1);
        emblem.Effect.ControllerOnly.Should().BeTrue();
    }

    [Fact]
    public async Task CreateNinjaEmblem_AppliesOnlyToNinjas()
    {
        var (context, controller, opponent, state, handler, _) = CreateTwoPlayerContext();
        var engine = new GameEngine(state);

        var ninja = new GameCard
        {
            Name = "Test Ninja",
            CardTypes = CardType.Creature,
            BasePower = 1,
            BaseToughness = 1,
            Subtypes = ["Ninja"],
        };
        controller.Battlefield.Add(ninja);

        var nonNinja = new GameCard
        {
            Name = "Grizzly Bears",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
            Subtypes = ["Bear"],
        };
        controller.Battlefield.Add(nonNinja);

        var effect = new CreateNinjaEmblemEffect();
        await effect.Execute(context);

        engine.RecalculateState();

        ninja.Power.Should().Be(2, "ninja should get +1/+1");
        ninja.Toughness.Should().Be(2, "ninja should get +1/+1");
        nonNinja.Power.Should().Be(2, "non-ninja should not be affected");
        nonNinja.Toughness.Should().Be(2, "non-ninja should not be affected");
    }

    [Fact]
    public async Task CreateNinjaEmblem_DoesNotAffectOpponentNinjas()
    {
        var (context, controller, opponent, state, handler, _) = CreateTwoPlayerContext();
        var engine = new GameEngine(state);

        var opponentNinja = new GameCard
        {
            Name = "Opponent Ninja",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
            Subtypes = ["Ninja"],
        };
        opponent.Battlefield.Add(opponentNinja);

        var effect = new CreateNinjaEmblemEffect();
        await effect.Execute(context);

        engine.RecalculateState();

        opponentNinja.Power.Should().Be(2, "opponent's ninjas should not get the buff");
        opponentNinja.Toughness.Should().Be(2, "opponent's ninjas should not get the buff");
    }

    [Fact]
    public async Task CreateNinjaEmblem_MultipleEmblems_Stack()
    {
        var (context, controller, opponent, state, handler, _) = CreateTwoPlayerContext();
        var engine = new GameEngine(state);

        var ninja = new GameCard
        {
            Name = "Ninja Boi",
            CardTypes = CardType.Creature,
            BasePower = 1,
            BaseToughness = 1,
            Subtypes = ["Ninja"],
        };
        controller.Battlefield.Add(ninja);

        var effect = new CreateNinjaEmblemEffect();
        await effect.Execute(context);
        await effect.Execute(context); // activate +1 twice

        engine.RecalculateState();

        ninja.Power.Should().Be(3, "two emblems should give +2/+2 total");
        ninja.Toughness.Should().Be(3, "two emblems should give +2/+2 total");
    }

    [Fact]
    public async Task CreateNinjaEmblem_Logs()
    {
        var (context, controller, opponent, state, handler, _) = CreateTwoPlayerContext();

        var effect = new CreateNinjaEmblemEffect();
        await effect.Execute(context);

        state.GameLog.Should().Contain(l => l.Contains("emblem") && l.Contains("Ninja"));
    }

    #endregion
}
