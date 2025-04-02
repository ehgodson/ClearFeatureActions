using FluentResults;
using FluentValidation;
using Moq;

namespace Clear.Tests
{
    public class FeatureActionTests
    {
        private class TestFeatureAction<TRequest, TResponse> : BaseFeatureAction<TRequest, TResponse>
            where TRequest : IRequest<TResponse>
        {
            public TestFeatureAction(IRequestHandler<TRequest, TResponse> handler, IValidator<TRequest> validator = null)
                : base(handler, validator)
            {
            }
        }

        [Fact]
        public async Task Execute_ShouldReturnFailure_WhenValidationFails()
        {
            // Arrange
            var command = new Mock<IRequest<bool>>().Object;
            var cancellationToken = new CancellationToken();

            var validationResult = new FluentValidation.Results.ValidationResult(new List<FluentValidation.Results.ValidationFailure>
            {
                new FluentValidation.Results.ValidationFailure("Property", "Error message")
            });

            var validator = new Mock<IValidator<IRequest<bool>>>();
            validator.Setup(v => v.ValidateAsync(command, cancellationToken)).ReturnsAsync(validationResult);

            var handler = new Mock<IRequestHandler<IRequest<bool>, bool>>();

            var featureAction = new TestFeatureAction<IRequest<bool>, bool>(handler.Object, validator.Object);

            // Act
            var result = await featureAction.Execute(command, cancellationToken);

            // Assert
            Assert.True(result.IsFailed);
            Assert.Contains("Error message", result.Errors.Select(e => e.Message));
        }

        [Fact]
        public async Task Execute_ShouldReturnSuccess_WhenValidationPasses()
        {
            // Arrange
            var command = new Mock<IRequest<bool>>().Object;
            var cancellationToken = new CancellationToken();

            var validationResult = new FluentValidation.Results.ValidationResult();

            var validator = new Mock<IValidator<IRequest<bool>>>();
            validator.Setup(v => v.ValidateAsync(command, cancellationToken)).ReturnsAsync(validationResult);

            var handler = new Mock<IRequestHandler<IRequest<bool>, bool>>();
            handler.Setup(h => h.Handle(command, cancellationToken)).ReturnsAsync(Result.Ok(true));

            var featureAction = new TestFeatureAction<IRequest<bool>, bool>(handler.Object, validator.Object);

            // Act
            var result = await featureAction.Execute(command, cancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.True(result.Value);
        }

        [Fact]
        public async Task Execute_ShouldReturnSuccess_WhenNoValidatorProvided()
        {
            // Arrange
            var command = new Mock<IRequest<bool>>().Object;
            var cancellationToken = new CancellationToken();

            var handler = new Mock<IRequestHandler<IRequest<bool>, bool>>();
            handler.Setup(h => h.Handle(command, cancellationToken)).ReturnsAsync(Result.Ok(true));

            var featureAction = new TestFeatureAction<IRequest<bool>, bool>(handler.Object);

            // Act
            var result = await featureAction.Execute(command, cancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.True(result.Value);
        }
    }
}
