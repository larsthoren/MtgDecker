using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using NSubstitute;
using MtgDecker.Application.Behaviors;

namespace MtgDecker.Application.Tests.Behaviors;

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_NoValidators_CallsNext()
    {
        var validators = Enumerable.Empty<IValidator<TestRequest>>();
        var behavior = new ValidationBehavior<TestRequest, TestResponse>(validators);
        var next = Substitute.For<RequestHandlerDelegate<TestResponse>>();
        var expected = new TestResponse();
        next(Arg.Any<CancellationToken>()).Returns(expected);

        var result = await behavior.Handle(new TestRequest(), next, CancellationToken.None);

        result.Should().Be(expected);
        await next.Received(1)(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidRequest_CallsNext()
    {
        var validator = Substitute.For<IValidator<TestRequest>>();
        validator.ValidateAsync(Arg.Any<ValidationContext<TestRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        var behavior = new ValidationBehavior<TestRequest, TestResponse>(new[] { validator });
        var next = Substitute.For<RequestHandlerDelegate<TestResponse>>();
        var expected = new TestResponse();
        next(Arg.Any<CancellationToken>()).Returns(expected);

        var result = await behavior.Handle(new TestRequest(), next, CancellationToken.None);

        result.Should().Be(expected);
        await next.Received(1)(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidRequest_ThrowsValidationException()
    {
        var validator = Substitute.For<IValidator<TestRequest>>();
        validator.ValidateAsync(Arg.Any<ValidationContext<TestRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new[] { new ValidationFailure("Prop", "Error") }));
        var behavior = new ValidationBehavior<TestRequest, TestResponse>(new[] { validator });
        var next = Substitute.For<RequestHandlerDelegate<TestResponse>>();

        var act = () => behavior.Handle(new TestRequest(), next, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        await next.DidNotReceive()(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MultipleValidators_AllAreInvoked()
    {
        var validator1 = Substitute.For<IValidator<TestRequest>>();
        validator1.ValidateAsync(Arg.Any<ValidationContext<TestRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        var validator2 = Substitute.For<IValidator<TestRequest>>();
        validator2.ValidateAsync(Arg.Any<ValidationContext<TestRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        var behavior = new ValidationBehavior<TestRequest, TestResponse>(new[] { validator1, validator2 });
        var next = Substitute.For<RequestHandlerDelegate<TestResponse>>();
        next(Arg.Any<CancellationToken>()).Returns(new TestResponse());

        await behavior.Handle(new TestRequest(), next, CancellationToken.None);

        await validator1.Received(1).ValidateAsync(Arg.Any<ValidationContext<TestRequest>>(), Arg.Any<CancellationToken>());
        await validator2.Received(1).ValidateAsync(Arg.Any<ValidationContext<TestRequest>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MultipleValidators_OneInvalid_ThrowsWithAllFailures()
    {
        var validator1 = Substitute.For<IValidator<TestRequest>>();
        validator1.ValidateAsync(Arg.Any<ValidationContext<TestRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new[] { new ValidationFailure("Prop1", "Error1") }));
        var validator2 = Substitute.For<IValidator<TestRequest>>();
        validator2.ValidateAsync(Arg.Any<ValidationContext<TestRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new[] { new ValidationFailure("Prop2", "Error2") }));
        var behavior = new ValidationBehavior<TestRequest, TestResponse>(new[] { validator1, validator2 });
        var next = Substitute.For<RequestHandlerDelegate<TestResponse>>();

        var act = () => behavior.Handle(new TestRequest(), next, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().HaveCount(2);
        await next.DidNotReceive()(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsResponseFromNext()
    {
        var validators = Enumerable.Empty<IValidator<TestRequest>>();
        var behavior = new ValidationBehavior<TestRequest, TestResponse>(validators);
        var expected = new TestResponse();
        var next = Substitute.For<RequestHandlerDelegate<TestResponse>>();
        next(Arg.Any<CancellationToken>()).Returns(expected);

        var result = await behavior.Handle(new TestRequest(), next, CancellationToken.None);

        result.Should().BeSameAs(expected);
    }

    public record TestRequest : IRequest<TestResponse>;
    public record TestResponse;
}
