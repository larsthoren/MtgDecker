using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class InteractiveDecisionHandlerTests
{
    private readonly GameState _state;
    private readonly Guid _playerId = Guid.NewGuid();

    public InteractiveDecisionHandlerTests()
    {
        var p1 = new Player(Guid.NewGuid(), "Alice", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "Bob", new TestDecisionHandler());
        _state = new GameState(p1, p2);
    }

    [Fact]
    public async Task GetAction_WaitsUntilSubmitAction()
    {
        var handler = new InteractiveDecisionHandler();
        var actionTask = handler.GetAction(_state, _playerId);

        actionTask.IsCompleted.Should().BeFalse();

        var pass = GameAction.Pass(_playerId);
        handler.SubmitAction(pass);

        var result = await actionTask;
        result.Type.Should().Be(ActionType.PassPriority);
    }

    [Fact]
    public async Task GetMulliganDecision_WaitsUntilSubmitMulligan()
    {
        var handler = new InteractiveDecisionHandler();
        var hand = new List<GameCard> { new() { Name = "Forest" } };
        var task = handler.GetMulliganDecision(hand, 0);

        task.IsCompleted.Should().BeFalse();

        handler.SubmitMulliganDecision(MulliganDecision.Keep);

        var result = await task;
        result.Should().Be(MulliganDecision.Keep);
    }

    [Fact]
    public async Task ChooseCardsToBottom_WaitsUntilSubmitBottomCards()
    {
        var handler = new InteractiveDecisionHandler();
        var hand = new List<GameCard>
        {
            new() { Name = "Forest" },
            new() { Name = "Bear" }
        };
        var task = handler.ChooseCardsToBottom(hand, 1);

        task.IsCompleted.Should().BeFalse();

        var selected = new List<GameCard> { hand[0] };
        await handler.SubmitBottomCardsAsync(selected);

        var result = await task;
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Forest");
    }

    [Fact]
    public void IsWaitingForAction_TrueWhileAwaiting()
    {
        var handler = new InteractiveDecisionHandler();
        handler.IsWaitingForAction.Should().BeFalse();

        _ = handler.GetAction(_state, _playerId);

        handler.IsWaitingForAction.Should().BeTrue();
    }

    [Fact]
    public async Task IsWaitingForAction_FalseAfterSubmit()
    {
        var handler = new InteractiveDecisionHandler();
        var task = handler.GetAction(_state, _playerId);

        handler.SubmitAction(GameAction.Pass(_playerId));
        await task;

        handler.IsWaitingForAction.Should().BeFalse();
    }

    [Fact]
    public void IsWaitingForMulligan_TrueWhileAwaiting()
    {
        var handler = new InteractiveDecisionHandler();
        _ = handler.GetMulliganDecision(new List<GameCard>(), 0);

        handler.IsWaitingForMulligan.Should().BeTrue();
    }

    [Fact]
    public async Task Cancellation_CancelsWaitingAction()
    {
        var handler = new InteractiveDecisionHandler();
        using var cts = new CancellationTokenSource();
        var task = handler.GetAction(_state, _playerId, cts.Token);

        cts.Cancel();

        Func<Task> act = async () => await task;
        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public async Task ChooseGenericPayment_AutoPays_FromLargestPoolFirst()
    {
        var handler = new InteractiveDecisionHandler();
        var available = new Dictionary<ManaColor, int>
        {
            { ManaColor.Red, 1 },
            { ManaColor.Green, 3 }
        };

        var result = await handler.ChooseGenericPayment(2, available);

        result[ManaColor.Green].Should().Be(2);
        result.Should().NotContainKey(ManaColor.Red);
    }

    [Fact]
    public async Task ChooseGenericPayment_AutoPays_SplitsAcrossColors()
    {
        var handler = new InteractiveDecisionHandler();
        var available = new Dictionary<ManaColor, int>
        {
            { ManaColor.Red, 1 },
            { ManaColor.Green, 1 }
        };

        var result = await handler.ChooseGenericPayment(2, available);

        result.Values.Sum().Should().Be(2);
    }

    [Fact]
    public async Task ChooseGenericPayment_DoesNotBlockOnTaskCompletionSource()
    {
        var handler = new InteractiveDecisionHandler();
        var available = new Dictionary<ManaColor, int>
        {
            { ManaColor.Red, 2 }
        };

        var task = handler.ChooseGenericPayment(1, available);
        task.IsCompleted.Should().BeTrue();

        var result = await task;
        result[ManaColor.Red].Should().Be(1);
    }

    [Fact]
    public async Task ChooseGenericPayment_IsNotWaitingAfterCall()
    {
        var handler = new InteractiveDecisionHandler();
        var available = new Dictionary<ManaColor, int>
        {
            { ManaColor.Red, 2 }
        };

        await handler.ChooseGenericPayment(1, available);

        handler.IsWaitingForGenericPayment.Should().BeFalse();
    }

    [Fact]
    public async Task ChooseManaColor_ExposesManaColorOptions()
    {
        var handler = new InteractiveDecisionHandler();
        var options = new List<ManaColor> { ManaColor.Red, ManaColor.Green };

        // Start the choice (will block until resolved)
        var task = handler.ChooseManaColor(options);

        handler.IsWaitingForManaColor.Should().BeTrue();
        handler.ManaColorOptions.Should().BeEquivalentTo(options);

        // Resolve it
        handler.SubmitManaColor(ManaColor.Red);
        var result = await task;
        result.Should().Be(ManaColor.Red);
    }

    [Fact]
    public async Task ChooseManaColor_ClearsOptionsAfterSubmission()
    {
        var handler = new InteractiveDecisionHandler();
        var options = new List<ManaColor> { ManaColor.Red, ManaColor.Green };

        var task = handler.ChooseManaColor(options);
        handler.SubmitManaColor(ManaColor.Green);
        await task;

        handler.ManaColorOptions.Should().BeNull();
    }
}
