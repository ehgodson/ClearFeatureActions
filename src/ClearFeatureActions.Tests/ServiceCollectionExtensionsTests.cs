using FluentResults;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Clear.Tests;

#region feature action objects

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

#endregion

#region notification objects

public class TestNotification : INotification
{
    public string Message { get; set; } = string.Empty;
}

public class TestNotificationHandler1 : INotificationHandler<TestNotification>
{
    public bool SupportsConcurrentExecution => true;

    public Task Handle(TestNotification notification, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public class TestNotificationHandler2 : INotificationHandler<TestNotification>
{
    public bool SupportsConcurrentExecution => true;

    public Task Handle(TestNotification notification, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public class TestNotificationHandler3 : INotificationHandler<TestNotification>
{
    public bool SupportsConcurrentExecution => false;

    public Task Handle(TestNotification notification, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

#endregion

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddFeatureActions_ShouldRegisterServices_WithAssembly()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddFeatureActions(Assembly.GetExecutingAssembly());

        // Assert
        var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetService<IRequestHandler<TestRequest, bool>>());
        Assert.NotNull(serviceProvider.GetService<IValidator<TestRequest>>());
        Assert.NotNull(serviceProvider.GetService<IFeatureAction<TestRequest, bool>>());

        Assert.NotNull(serviceProvider.GetService<IRequestHandler<AnotherTestRequest, string>>());
        Assert.NotNull(serviceProvider.GetService<IFeatureAction<AnotherTestRequest, string>>());
    }

    [Fact]
    public void AddFeatureActions_ShouldRegisterServices_WithoutAssembly()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddFeatureActions();

        // Assert
        var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetService<IRequestHandler<TestRequest, bool>>());
        Assert.NotNull(serviceProvider.GetService<IValidator<TestRequest>>());
        Assert.NotNull(serviceProvider.GetService<IFeatureAction<TestRequest, bool>>());

        Assert.NotNull(serviceProvider.GetService<IRequestHandler<AnotherTestRequest, string>>());
        Assert.NotNull(serviceProvider.GetService<IFeatureAction<AnotherTestRequest, string>>());
    }

    [Fact]
    public void AddNotificationPublishers_ShouldRegisterServices_WithAssembly()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddNotificationPublishers(Assembly.GetExecutingAssembly());

        // Assert
        var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetService<INotificationHandler<TestNotification>>());
        Assert.NotNull(serviceProvider.GetService<INotificationPublisher<TestNotification>>());

        // Assert that there are exactly 2 implementations of INotificationHandler<TestNotification>
        var notificationHandlers = serviceProvider.GetServices<INotificationHandler<TestNotification>>();
        Assert.Equal(3, notificationHandlers.Count());
    }

    [Fact]
    public void AddNotificationPublishers_ShouldRegisterServices_WithoutAssembly()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddNotificationPublishers();

        // Assert
        var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetService<INotificationHandler<TestNotification>>());
        Assert.NotNull(serviceProvider.GetService<INotificationPublisher<TestNotification>>());

        // Assert that there are exactly 2 implementations of INotificationHandler<TestNotification>
        var notificationHandlers = serviceProvider.GetServices<INotificationHandler<TestNotification>>();
        Assert.Equal(3, notificationHandlers.Count());
    }
}