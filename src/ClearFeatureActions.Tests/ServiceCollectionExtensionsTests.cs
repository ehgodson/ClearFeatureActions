using FluentResults;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Clear.Tests
{
    public class TestRequest : IRequest<bool> { }
    public class TestRequestHandler : IRequestHandler<TestRequest, bool>
    {
        public Task<Result<bool>> Handle(TestRequest command, CancellationToken cancellationToken)
        {
            return Task.FromResult(Result.Ok(true));
        }
    }
    public class TestRequestValidator : AbstractValidator<TestRequest> { }

    public class AnotherTestRequest : IRequest<string> { }
    public class AnotherTestRequestHandler : IRequestHandler<AnotherTestRequest, string>
    {
        public Task<Result<string>> Handle(AnotherTestRequest command, CancellationToken cancellationToken)
        {
            return Task.FromResult(Result.Ok("Success"));
        }
    }

    public class ServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddFeatureActions_ShouldRegisterServices()
        {
            // Arrange
            var services = new ServiceCollection();
            var assembly = Assembly.GetExecutingAssembly();

            // Act
            services.AddFeatureActions(assembly);

            // Assert
            var serviceProvider = services.BuildServiceProvider();

            Assert.NotNull(serviceProvider.GetService<IRequestHandler<TestRequest, bool>>());
            Assert.NotNull(serviceProvider.GetService<IValidator<TestRequest>>());
            Assert.NotNull(serviceProvider.GetService<IFeatureAction<TestRequest, bool>>());

            Assert.NotNull(serviceProvider.GetService<IRequestHandler<AnotherTestRequest, string>>());
            Assert.NotNull(serviceProvider.GetService<IFeatureAction<AnotherTestRequest, string>>());
        }
    }
}