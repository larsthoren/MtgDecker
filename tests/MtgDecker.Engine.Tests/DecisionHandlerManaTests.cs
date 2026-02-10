using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class DecisionHandlerManaTests
{
    [Fact]
    public async Task InteractiveHandler_ChooseManaColor_WaitsForSubmit()
    {
        var handler = new InteractiveDecisionHandler();
        var options = new List<ManaColor> { ManaColor.Red, ManaColor.Green };

        var task = handler.ChooseManaColor(options);

        task.IsCompleted.Should().BeFalse();
        handler.IsWaitingForManaColor.Should().BeTrue();
    }

    [Fact]
    public async Task InteractiveHandler_SubmitManaColor_CompletesTask()
    {
        var handler = new InteractiveDecisionHandler();
        var options = new List<ManaColor> { ManaColor.Red, ManaColor.Green };

        var task = handler.ChooseManaColor(options);
        handler.SubmitManaColor(ManaColor.Green);

        var result = await task;
        result.Should().Be(ManaColor.Green);
        handler.IsWaitingForManaColor.Should().BeFalse();
    }

    [Fact]
    public async Task InteractiveHandler_ChooseGenericPayment_WaitsForSubmit()
    {
        var handler = new InteractiveDecisionHandler();
        var available = new Dictionary<ManaColor, int>
        {
            { ManaColor.Red, 2 },
            { ManaColor.Green, 1 }
        };

        var task = handler.ChooseGenericPayment(2, available);

        task.IsCompleted.Should().BeFalse();
        handler.IsWaitingForGenericPayment.Should().BeTrue();
    }

    [Fact]
    public async Task InteractiveHandler_SubmitGenericPayment_CompletesTask()
    {
        var handler = new InteractiveDecisionHandler();
        var available = new Dictionary<ManaColor, int>
        {
            { ManaColor.Red, 2 },
            { ManaColor.Green, 1 }
        };
        var payment = new Dictionary<ManaColor, int>
        {
            { ManaColor.Red, 1 },
            { ManaColor.Green, 1 }
        };

        var task = handler.ChooseGenericPayment(2, available);
        handler.SubmitGenericPayment(payment);

        var result = await task;
        result.Should().BeEquivalentTo(payment);
        handler.IsWaitingForGenericPayment.Should().BeFalse();
    }

    [Fact]
    public async Task TestHandler_ChooseManaColor_ReturnsEnqueued()
    {
        var handler = new TestDecisionHandler();
        handler.EnqueueManaColor(ManaColor.Blue);
        var options = new List<ManaColor> { ManaColor.Red, ManaColor.Blue };

        var result = await handler.ChooseManaColor(options);

        result.Should().Be(ManaColor.Blue);
    }

    [Fact]
    public async Task TestHandler_ChooseManaColor_DefaultsToFirstOption()
    {
        var handler = new TestDecisionHandler();
        var options = new List<ManaColor> { ManaColor.Black, ManaColor.White };

        var result = await handler.ChooseManaColor(options);

        result.Should().Be(ManaColor.Black);
    }

    [Fact]
    public async Task TestHandler_ChooseGenericPayment_ReturnsEnqueued()
    {
        var handler = new TestDecisionHandler();
        var payment = new Dictionary<ManaColor, int>
        {
            { ManaColor.Red, 2 }
        };
        handler.EnqueueGenericPayment(payment);
        var available = new Dictionary<ManaColor, int>
        {
            { ManaColor.Red, 3 },
            { ManaColor.Green, 1 }
        };

        var result = await handler.ChooseGenericPayment(2, available);

        result.Should().BeEquivalentTo(payment);
    }
}
