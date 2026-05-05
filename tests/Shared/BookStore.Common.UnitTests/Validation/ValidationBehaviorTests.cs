namespace BookStore.Common.UnitTests.Validation;

using Kovecses.Result.FluentAssertions;

public class ValidationBehaviorTests
{
    public sealed record TestRequest : IRequest<Result>;

    private readonly List<IValidator<TestRequest>> _validators = [];
    private readonly ValidationBehavior<TestRequest, Result> _sut;

    public ValidationBehaviorTests()
    {
        _sut = new ValidationBehavior<TestRequest, Result>(_validators);
    }

    [Fact]
    public async Task Handle_WhenNoValidators_ShouldCallNext()
    {
        // Arrange
        var next = Substitute.For<RequestHandlerDelegate<Result>>();
        var expected = Result.Success();
        next(Arg.Any<CancellationToken>()).Returns(expected);

        // Act
        var actual = await _sut.Handle(new TestRequest(), next, TestContext.Current.CancellationToken);

        // Assert
        actual.IsSuccess.Should().BeTrue();
        await next.Received(1)(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_WhenValidationSucceeds_ShouldCallNext()
    {
        // Arrange
        var validator = Substitute.For<IValidator<TestRequest>>();
        validator.ValidateAsync(Arg.Any<TestRequest>(), TestContext.Current.CancellationToken)
            .Returns(new FluentValidation.Results.ValidationResult());
        _validators.Add(validator);

        var next = Substitute.For<RequestHandlerDelegate<Result>>();
        var expected = Result.Success();
        next(Arg.Any<CancellationToken>()).Returns(expected);

        // Act
        var actual = await _sut.Handle(new TestRequest(), next, TestContext.Current.CancellationToken);

        // Assert
        actual.IsSuccess.Should().BeTrue();
        await next.Received(1)(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_WhenValidationFails_ShouldReturnFailureResult()
    {
        // Arrange
        var validator = Substitute.For<IValidator<TestRequest>>();
        var failures = new List<FluentValidation.Results.ValidationFailure>
        {
            new("Property1", "Error 1"),
            new("Property1", "Error 2"),
            new("Property2", "Error 3")
        };

        validator.ValidateAsync(Arg.Any<TestRequest>(), TestContext.Current.CancellationToken)
            .Returns(new FluentValidation.Results.ValidationResult(failures));
        _validators.Add(validator);

        var next = Substitute.For<RequestHandlerDelegate<Result>>();

        // Act
        var actual = await _sut.Handle(new TestRequest(), next, TestContext.Current.CancellationToken);

        // Assert
        actual.Should().BeFailure()
            .HaveError(ErrorCodes.Validation)
                .HaveValidationProperty("Property1")
                    .HaveCount(2).And
                .HaveValidationProperty("Property2")
                    .HaveCount(1);

        await next.DidNotReceive()(Arg.Any<CancellationToken>());
    }
}
