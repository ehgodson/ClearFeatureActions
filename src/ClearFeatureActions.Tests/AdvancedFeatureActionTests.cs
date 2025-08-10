using Clear.FeatureActions;
using FluentResults;
using FluentValidation;
using FluentValidation.Results;
using Moq;

namespace Clear.Tests;

public class AdvancedFeatureActionTests
{
    #region Test Classes

    public class ComplexRequest : IRequest<ComplexResponse>
    {
        public string Email { get; }
        public int Age { get; }
        public List<string> Tags { get; }

        public ComplexRequest(string email, int age, List<string> tags)
        {
            Email = email;
            Age = age;
            Tags = tags ?? new List<string>();
        }
    }

    public class ComplexResponse
    {
        public string ProcessedEmail { get; set; }
        public bool IsValid { get; set; }
        public List<string> ProcessedTags { get; set; }
    }

    public class ComplexRequestValidator : AbstractValidator<ComplexRequest>
    {
        public ComplexRequestValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress();

            RuleFor(x => x.Age)
                .InclusiveBetween(18, 100);

            RuleFor(x => x.Tags)
                .Must(tags => tags.Count <= 5)
                .WithMessage("Cannot have more than 5 tags");
        }
    }

    public class ComplexRequestHandler : IRequestHandler<ComplexRequest, ComplexResponse>
    {
        public Task<Result<ComplexResponse>> Handle(ComplexRequest request, CancellationToken cancellationToken)
        {
            var response = new ComplexResponse
            {
                ProcessedEmail = request.Email.ToLowerInvariant(),
                IsValid = true,
                ProcessedTags = request.Tags.Select(t => t.Trim().ToLowerInvariant()).ToList()
            };

            return Task.FromResult(Result.Ok(response));
        }
    }

    private class TestFeatureAction<TRequest, TResponse> : BaseFeatureAction<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public TestFeatureAction(IRequestHandler<TRequest, TResponse> handler, IValidator<TRequest> validator = null)
            : base(handler, validator)
        {
        }
    }

    #endregion

    [Fact]
    public async Task Execute_WithComplexValidation_MultipleErrors_ReturnsAllErrors()
    {
        // Arrange
        var request = new ComplexRequest("invalid-email", 150, new List<string> { "tag1", "tag2", "tag3", "tag4", "tag5", "tag6" });
        
        var validationErrors = new List<ValidationFailure>
        {
            new ValidationFailure("Email", "Email is invalid"),
            new ValidationFailure("Age", "Age must be between 18 and 100"),
            new ValidationFailure("Tags", "Cannot have more than 5 tags")
        };

        var validator = new Mock<IValidator<ComplexRequest>>();
        validator.Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(validationErrors));

        var handler = new Mock<IRequestHandler<ComplexRequest, ComplexResponse>>();
        var featureAction = new TestFeatureAction<ComplexRequest, ComplexResponse>(handler.Object, validator.Object);

        // Act
        var result = await featureAction.Execute(request, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(3, result.Errors.Count);
        Assert.Contains(result.Errors, e => e.Message == "Email is invalid");
        Assert.Contains(result.Errors, e => e.Message == "Age must be between 18 and 100");
        Assert.Contains(result.Errors, e => e.Message == "Cannot have more than 5 tags");
        
        // Verify handler was not called due to validation failure
        handler.Verify(h => h.Handle(It.IsAny<ComplexRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Execute_WithComplexRequest_ValidInput_ProcessesSuccessfully()
    {
        // Arrange
        var request = new ComplexRequest("Test@Example.Com", 25, new List<string> { " Tag1 ", " TAG2 " });
        var expectedResponse = new ComplexResponse
        {
            ProcessedEmail = "test@example.com",
            IsValid = true,
            ProcessedTags = new List<string> { "tag1", "tag2" }
        };

        var validator = new Mock<IValidator<ComplexRequest>>();
        validator.Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var handler = new Mock<IRequestHandler<ComplexRequest, ComplexResponse>>();
        handler.Setup(h => h.Handle(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(expectedResponse));

        var featureAction = new TestFeatureAction<ComplexRequest, ComplexResponse>(handler.Object, validator.Object);

        // Act
        var result = await featureAction.Execute(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedResponse.ProcessedEmail, result.Value.ProcessedEmail);
        Assert.Equal(expectedResponse.IsValid, result.Value.IsValid);
        Assert.Equal(expectedResponse.ProcessedTags, result.Value.ProcessedTags);
        
        handler.Verify(h => h.Handle(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Execute_HandlerReturnsFailure_PropagatesFailure()
    {
        // Arrange
        var request = new ComplexRequest("test@example.com", 25, new List<string>());
        var handlerError = "Processing failed";

        var validator = new Mock<IValidator<ComplexRequest>>();
        validator.Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var handler = new Mock<IRequestHandler<ComplexRequest, ComplexResponse>>();
        handler.Setup(h => h.Handle(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<ComplexResponse>(handlerError));

        var featureAction = new TestFeatureAction<ComplexRequest, ComplexResponse>(handler.Object, validator.Object);

        // Act
        var result = await featureAction.Execute(request, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(handlerError, result.Errors.Select(e => e.Message));
    }

    [Fact]
    public async Task Execute_WithCancellationToken_PassesToValidatorAndHandler()
    {
        // Arrange
        var request = new ComplexRequest("test@example.com", 25, new List<string>());
        var cancellationToken = new CancellationToken();

        var validator = new Mock<IValidator<ComplexRequest>>();
        validator.Setup(v => v.ValidateAsync(request, cancellationToken))
            .ReturnsAsync(new ValidationResult());

        var handler = new Mock<IRequestHandler<ComplexRequest, ComplexResponse>>();
        handler.Setup(h => h.Handle(request, cancellationToken))
            .ReturnsAsync(Result.Ok(new ComplexResponse()));

        var featureAction = new TestFeatureAction<ComplexRequest, ComplexResponse>(handler.Object, validator.Object);

        // Act
        await featureAction.Execute(request, cancellationToken);

        // Assert
        validator.Verify(v => v.ValidateAsync(request, cancellationToken), Times.Once);
        handler.Verify(h => h.Handle(request, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Execute_ValidatorThrowsException_PropagatesException()
    {
        // Arrange
        var request = new ComplexRequest("test@example.com", 25, new List<string>());
        var expectedException = new InvalidOperationException("Validator failed");

        var validator = new Mock<IValidator<ComplexRequest>>();
        validator.Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        var handler = new Mock<IRequestHandler<ComplexRequest, ComplexResponse>>();
        var featureAction = new TestFeatureAction<ComplexRequest, ComplexResponse>(handler.Object, validator.Object);

        // Act & Assert
        var actualException = await Assert.ThrowsAsync<InvalidOperationException>(
            () => featureAction.Execute(request, CancellationToken.None));
        
        Assert.Equal(expectedException.Message, actualException.Message);
        
        // Verify handler was not called due to validator exception
        handler.Verify(h => h.Handle(It.IsAny<ComplexRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Execute_HandlerThrowsException_PropagatesException()
    {
        // Arrange
        var request = new ComplexRequest("test@example.com", 25, new List<string>());
        var expectedException = new InvalidOperationException("Handler failed");

        var validator = new Mock<IValidator<ComplexRequest>>();
        validator.Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var handler = new Mock<IRequestHandler<ComplexRequest, ComplexResponse>>();
        handler.Setup(h => h.Handle(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        var featureAction = new TestFeatureAction<ComplexRequest, ComplexResponse>(handler.Object, validator.Object);

        // Act & Assert
        var actualException = await Assert.ThrowsAsync<InvalidOperationException>(
            () => featureAction.Execute(request, CancellationToken.None));
        
        Assert.Equal(expectedException.Message, actualException.Message);
    }

    [Fact]
    public async Task Execute_WithRealValidator_ValidatesCorrectly()
    {
        // Arrange
        var invalidRequest = new ComplexRequest("invalid-email", 15, new List<string> { "1", "2", "3", "4", "5", "6" });
        var validRequest = new ComplexRequest("test@example.com", 25, new List<string> { "tag1", "tag2" });

        var validator = new ComplexRequestValidator();
        var handler = new ComplexRequestHandler();
        var featureAction = new TestFeatureAction<ComplexRequest, ComplexResponse>(handler, validator);

        // Act - Invalid request
        var invalidResult = await featureAction.Execute(invalidRequest, CancellationToken.None);

        // Assert - Invalid request
        Assert.False(invalidResult.IsSuccess);
        Assert.True(invalidResult.Errors.Count >= 3); // Email, Age, and Tags errors

        // Act - Valid request
        var validResult = await featureAction.Execute(validRequest, CancellationToken.None);

        // Assert - Valid request
        Assert.True(validResult.IsSuccess);
        Assert.Equal("test@example.com", validResult.Value.ProcessedEmail);
        Assert.True(validResult.Value.IsValid);
        Assert.Equal(2, validResult.Value.ProcessedTags.Count);
    }
}