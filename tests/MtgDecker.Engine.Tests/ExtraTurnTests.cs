using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class ExtraTurnTests
{
    private static void AddLibraryCards(Player player, int count)
    {
        for (int i = 0; i < count; i++)
            player.Library.Add(new GameCard { Name = $"Filler {player.Name} {i}", CardTypes = CardType.Creature });
    }

    [Fact]
    public async Task ExtraTurn_SamePlayerTakesNextTurn()
    {
        var (state, h1, h2) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);

        // Add enough library cards so draw phase doesn't fail
        AddLibraryCards(state.Player1, 10);
        AddLibraryCards(state.Player2, 10);

        state.ActivePlayer = state.Player1;
        state.TurnNumber = 2; // Not first turn (so draw step is not skipped)

        // Queue an extra turn for Player1
        state.ExtraTurns.Enqueue(state.Player1.Id);

        // Run a turn for Player1 -- at the end, extra turn should keep Player1 active
        await engine.RunTurnAsync();

        state.ActivePlayer.Should().Be(state.Player1);
    }

    [Fact]
    public async Task ExtraTurn_AfterExtraTurn_NormalRotationResumes()
    {
        var (state, h1, h2) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);

        AddLibraryCards(state.Player1, 20);
        AddLibraryCards(state.Player2, 20);

        state.ActivePlayer = state.Player1;
        state.TurnNumber = 2;

        // Queue exactly one extra turn for Player1
        state.ExtraTurns.Enqueue(state.Player1.Id);

        // Turn 1: Player1's normal turn ends, extra turn queued -> Player1 stays active
        await engine.RunTurnAsync();
        state.ActivePlayer.Should().Be(state.Player1, "extra turn keeps Player1 active");

        // Turn 2: Player1's extra turn ends, no more extra turns -> Player2 becomes active
        await engine.RunTurnAsync();
        state.ActivePlayer.Should().Be(state.Player2, "normal rotation resumes after extra turn");
    }

    [Fact]
    public async Task ExtraTurnEffect_EnqueuesControllerIdInExtraTurns()
    {
        var (state, _, _) = TestHelper.CreateStateWithHandlers();
        var effect = new ExtraTurnEffect();

        var source = new GameCard { Name = "Time Walk", CardTypes = CardType.Sorcery };
        var context = new EffectContext(state, state.Player1, source, state.Player1.DecisionHandler);

        await effect.Execute(context);

        state.ExtraTurns.Should().HaveCount(1);
        state.ExtraTurns.Peek().Should().Be(state.Player1.Id);
    }
}
