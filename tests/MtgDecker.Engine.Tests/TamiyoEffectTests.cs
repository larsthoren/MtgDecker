using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class TamiyoEffectTests
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
        var source = new GameCard { Name = "Tamiyo, Seasoned Scholar" };
        var context = new EffectContext(state, p1, source, h1);
        return (context, p1, p2, state, h1, h2);
    }

    #endregion

    #region TamiyoDefenseEffect (+2)

    [Fact]
    public async Task TamiyoDefenseEffect_CreatesMinusOneEffect()
    {
        var (context, controller, opponent, state, _, _) = CreateTwoPlayerContext();

        var effect = new TamiyoDefenseEffect();
        await effect.Execute(context);

        state.ActiveEffects.Should().HaveCount(1);
        var active = state.ActiveEffects[0];
        active.Type.Should().Be(ContinuousEffectType.ModifyPowerToughness);
        active.PowerMod.Should().Be(-1);
        active.ToughnessMod.Should().Be(0);
    }

    [Fact]
    public async Task TamiyoDefenseEffect_ExpiresAfterTwoTurns()
    {
        var (context, controller, opponent, state, _, _) = CreateTwoPlayerContext();
        state.TurnNumber = 5;

        var effect = new TamiyoDefenseEffect();
        await effect.Execute(context);

        var active = state.ActiveEffects[0];
        active.ExpiresOnTurnNumber.Should().Be(7, "effect should expire in TurnNumber + 2");
    }

    [Fact]
    public async Task TamiyoDefenseEffect_AppliesToOpponentCreatures()
    {
        var (context, controller, opponent, state, _, _) = CreateTwoPlayerContext();

        var effect = new TamiyoDefenseEffect();
        await effect.Execute(context);

        var active = state.ActiveEffects[0];

        var opponentCreature = new GameCard
        {
            Name = "Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };

        active.Applies(opponentCreature, opponent).Should().BeTrue(
            "effect should apply to opponent's creatures");
    }

    [Fact]
    public async Task TamiyoDefenseEffect_DoesNotApplyToControllerCreatures()
    {
        var (context, controller, opponent, state, _, _) = CreateTwoPlayerContext();

        var effect = new TamiyoDefenseEffect();
        await effect.Execute(context);

        var active = state.ActiveEffects[0];

        var ownCreature = new GameCard
        {
            Name = "Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };

        active.Applies(ownCreature, controller).Should().BeFalse(
            "effect should not apply to controller's own creatures");
    }

    [Fact]
    public async Task TamiyoDefenseEffect_DoesNotApplyToNonCreatures()
    {
        var (context, controller, opponent, state, _, _) = CreateTwoPlayerContext();

        var effect = new TamiyoDefenseEffect();
        await effect.Execute(context);

        var active = state.ActiveEffects[0];

        var artifact = new GameCard
        {
            Name = "Sol Ring",
            CardTypes = CardType.Artifact,
        };

        active.Applies(artifact, opponent).Should().BeFalse(
            "effect should not apply to non-creatures");
    }

    [Fact]
    public async Task TamiyoDefenseEffect_Logs()
    {
        var (context, controller, opponent, state, _, _) = CreateTwoPlayerContext();

        var effect = new TamiyoDefenseEffect();
        await effect.Execute(context);

        state.GameLog.Should().Contain(l => l.Contains("-1/-0"));
    }

    #endregion

    #region TamiyoRecoverEffect (-3)

    [Fact]
    public async Task TamiyoRecoverEffect_ReturnsInstantFromGraveyard()
    {
        var (context, controller, opponent, state, handler, _) = CreateTwoPlayerContext();

        var instant = new GameCard
        {
            Name = "Lightning Bolt",
            CardTypes = CardType.Instant,
        };
        controller.Graveyard.Add(instant);

        handler.EnqueueCardChoice(instant.Id);

        var effect = new TamiyoRecoverEffect();
        await effect.Execute(context);

        controller.Hand.Cards.Should().Contain(c => c.Name == "Lightning Bolt");
        controller.Graveyard.Cards.Should().NotContain(c => c.Name == "Lightning Bolt");
    }

    [Fact]
    public async Task TamiyoRecoverEffect_ReturnsSorceryFromGraveyard()
    {
        var (context, controller, opponent, state, handler, _) = CreateTwoPlayerContext();

        var sorcery = new GameCard
        {
            Name = "Ponder",
            CardTypes = CardType.Sorcery,
        };
        controller.Graveyard.Add(sorcery);

        handler.EnqueueCardChoice(sorcery.Id);

        var effect = new TamiyoRecoverEffect();
        await effect.Execute(context);

        controller.Hand.Cards.Should().Contain(c => c.Name == "Ponder");
        controller.Graveyard.Cards.Should().NotContain(c => c.Name == "Ponder");
    }

    [Fact]
    public async Task TamiyoRecoverEffect_IgnoresCreaturesInGraveyard()
    {
        var (context, controller, opponent, state, handler, _) = CreateTwoPlayerContext();

        var creature = new GameCard
        {
            Name = "Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        controller.Graveyard.Add(creature);

        // No eligible cards, so effect should do nothing
        var effect = new TamiyoRecoverEffect();
        await effect.Execute(context);

        controller.Graveyard.Cards.Should().Contain(c => c.Name == "Bear");
        controller.Hand.Cards.Should().BeEmpty();
        state.GameLog.Should().Contain(l => l.Contains("No instant or sorcery"));
    }

    [Fact]
    public async Task TamiyoRecoverEffect_EmptyGraveyard_DoesNothing()
    {
        var (context, controller, opponent, state, handler, _) = CreateTwoPlayerContext();

        var effect = new TamiyoRecoverEffect();
        await effect.Execute(context);

        controller.Hand.Cards.Should().BeEmpty();
        state.GameLog.Should().Contain(l => l.Contains("No instant or sorcery"));
    }

    [Fact]
    public async Task TamiyoRecoverEffect_DeclineChoice_DoesNothing()
    {
        var (context, controller, opponent, state, handler, _) = CreateTwoPlayerContext();

        var instant = new GameCard
        {
            Name = "Counterspell",
            CardTypes = CardType.Instant,
        };
        controller.Graveyard.Add(instant);

        handler.EnqueueCardChoice(null); // decline

        var effect = new TamiyoRecoverEffect();
        await effect.Execute(context);

        controller.Hand.Cards.Should().BeEmpty();
        controller.Graveyard.Cards.Should().Contain(c => c.Name == "Counterspell");
    }

    [Fact]
    public async Task TamiyoRecoverEffect_Logs()
    {
        var (context, controller, opponent, state, handler, _) = CreateTwoPlayerContext();

        var instant = new GameCard
        {
            Name = "Brainstorm",
            CardTypes = CardType.Instant,
        };
        controller.Graveyard.Add(instant);

        handler.EnqueueCardChoice(instant.Id);

        var effect = new TamiyoRecoverEffect();
        await effect.Execute(context);

        state.GameLog.Should().Contain(l => l.Contains("Brainstorm") && l.Contains("returned"));
    }

    #endregion

    #region TamiyoUltimateEffect (-7)

    [Fact]
    public async Task TamiyoUltimateEffect_DrawsHalfLibraryRoundedUp()
    {
        var (context, controller, opponent, state, _, _) = CreateTwoPlayerContext();

        // Put 7 cards in library — should draw ceil(7/2) = 4
        for (int i = 0; i < 7; i++)
            controller.Library.AddToTop(new GameCard { Name = $"Card {i}" });

        var effect = new TamiyoUltimateEffect();
        await effect.Execute(context);

        controller.Hand.Cards.Should().HaveCount(4);
        controller.Library.Count.Should().Be(3);
    }

    [Fact]
    public async Task TamiyoUltimateEffect_DrawsHalfLibraryRoundedUp_EvenNumber()
    {
        var (context, controller, opponent, state, _, _) = CreateTwoPlayerContext();

        // Put 10 cards in library — should draw ceil(10/2) = 5
        for (int i = 0; i < 10; i++)
            controller.Library.AddToTop(new GameCard { Name = $"Card {i}" });

        var effect = new TamiyoUltimateEffect();
        await effect.Execute(context);

        controller.Hand.Cards.Should().HaveCount(5);
        controller.Library.Count.Should().Be(5);
    }

    [Fact]
    public async Task TamiyoUltimateEffect_EmptyLibrary_DrawsNothing()
    {
        var (context, controller, opponent, state, _, _) = CreateTwoPlayerContext();

        // Library is empty
        var effect = new TamiyoUltimateEffect();
        await effect.Execute(context);

        controller.Hand.Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task TamiyoUltimateEffect_OneCardLibrary_DrawsOne()
    {
        var (context, controller, opponent, state, _, _) = CreateTwoPlayerContext();

        controller.Library.AddToTop(new GameCard { Name = "Last Card" });

        var effect = new TamiyoUltimateEffect();
        await effect.Execute(context);

        controller.Hand.Cards.Should().HaveCount(1);
        controller.Library.Count.Should().Be(0);
    }

    [Fact]
    public async Task TamiyoUltimateEffect_CreatesEmblem()
    {
        var (context, controller, opponent, state, _, _) = CreateTwoPlayerContext();

        // Put some cards so the draw part works
        for (int i = 0; i < 4; i++)
            controller.Library.AddToTop(new GameCard { Name = $"Card {i}" });

        var effect = new TamiyoUltimateEffect();
        await effect.Execute(context);

        controller.Emblems.Should().HaveCount(1);
        controller.Emblems[0].Description.Should().Contain("no maximum hand size");
    }

    [Fact]
    public async Task TamiyoUltimateEffect_IncrementsDrawsThisTurn()
    {
        var (context, controller, opponent, state, _, _) = CreateTwoPlayerContext();

        for (int i = 0; i < 6; i++)
            controller.Library.AddToTop(new GameCard { Name = $"Card {i}" });

        controller.DrawsThisTurn = 0;

        var effect = new TamiyoUltimateEffect();
        await effect.Execute(context);

        // ceil(6/2) = 3 draws
        controller.DrawsThisTurn.Should().Be(3);
    }

    [Fact]
    public async Task TamiyoUltimateEffect_Logs()
    {
        var (context, controller, opponent, state, _, _) = CreateTwoPlayerContext();

        for (int i = 0; i < 4; i++)
            controller.Library.AddToTop(new GameCard { Name = $"Card {i}" });

        var effect = new TamiyoUltimateEffect();
        await effect.Execute(context);

        state.GameLog.Should().Contain(l => l.Contains("draws") && l.Contains("2"));
        state.GameLog.Should().Contain(l => l.Contains("emblem"));
    }

    #endregion
}
