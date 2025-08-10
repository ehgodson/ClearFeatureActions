using Clear.FeatureActions;
using Moq;

namespace Clear.Tests;

public class NotificationPublisherTests
{
    public class TestNotificationWithData : INotification<string>
    {
        public string Data { get; }
        public TestNotificationWithData(string data)
        {
            Data = data;
        }
    }

    [Fact]
    public async Task Publish_WithConcurrentHandlers_ExecutesInParallel()
    {
        // Arrange
        var notification = new TestNotification { Message = "Test" };
        var executionOrder = new List<string>();

        var handler1 = new Mock<INotificationHandler<TestNotification>>();
        var handler2 = new Mock<INotificationHandler<TestNotification>>();

        handler1.Setup(h => h.SupportsConcurrentExecution).Returns(true);
        handler2.Setup(h => h.SupportsConcurrentExecution).Returns(true);

        handler1.Setup(h => h.Handle(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(50); // Simulate work
                lock (executionOrder) { executionOrder.Add("Handler1"); }
            });

        handler2.Setup(h => h.Handle(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(50); // Simulate work
                lock (executionOrder) { executionOrder.Add("Handler2"); }
            });

        var handlers = new[] { handler1.Object, handler2.Object };
        var publisher = new NotificationPublisher<TestNotification>(handlers);

        // Act
        var startTime = DateTime.UtcNow;
        await publisher.Publish(notification, CancellationToken.None);
        var endTime = DateTime.UtcNow;

        // Assert
        Assert.Equal(2, executionOrder.Count);
        // Both handlers should complete in roughly the same time if running concurrently
        // Since each handler takes 50ms and they run concurrently, total should be around 50ms, not 100ms
        Assert.True((endTime - startTime).TotalMilliseconds < 80, "Handlers should execute concurrently");
        
        handler1.Verify(h => h.Handle(notification, It.IsAny<CancellationToken>()), Times.Once);
        handler2.Verify(h => h.Handle(notification, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Publish_WithSequentialHandlers_ExecutesInOrder()
    {
        // Arrange
        var notification = new TestNotification { Message = "Test" };
        var executionOrder = new List<string>();

        var handler1 = new Mock<INotificationHandler<TestNotification>>();
        var handler2 = new Mock<INotificationHandler<TestNotification>>();

        handler1.Setup(h => h.SupportsConcurrentExecution).Returns(false);
        handler2.Setup(h => h.SupportsConcurrentExecution).Returns(false);

        handler1.Setup(h => h.Handle(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(10);
                executionOrder.Add("Handler1");
            });

        handler2.Setup(h => h.Handle(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(10);
                executionOrder.Add("Handler2");
            });

        var handlers = new[] { handler1.Object, handler2.Object };
        var publisher = new NotificationPublisher<TestNotification>(handlers);

        // Act
        await publisher.Publish(notification, CancellationToken.None);

        // Assert
        Assert.Equal(new[] { "Handler1", "Handler2" }, executionOrder);
        
        handler1.Verify(h => h.Handle(notification, It.IsAny<CancellationToken>()), Times.Once);
        handler2.Verify(h => h.Handle(notification, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Publish_WithMixedHandlers_ConcurrentFirst_ThenSequential()
    {
        // Arrange
        var notification = new TestNotification { Message = "Test" };
        var executionOrder = new List<string>();
        var concurrentCompleted = new TaskCompletionSource<bool>();

        var concurrentHandler = new Mock<INotificationHandler<TestNotification>>();
        var sequentialHandler = new Mock<INotificationHandler<TestNotification>>();

        concurrentHandler.Setup(h => h.SupportsConcurrentExecution).Returns(true);
        sequentialHandler.Setup(h => h.SupportsConcurrentExecution).Returns(false);

        concurrentHandler.Setup(h => h.Handle(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(50);
                lock (executionOrder) { executionOrder.Add("Concurrent"); }
                concurrentCompleted.SetResult(true);
            });

        sequentialHandler.Setup(h => h.Handle(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                // Sequential should only start after concurrent completes
                Assert.True(concurrentCompleted.Task.IsCompleted, "Sequential handler started before concurrent completed");
                await Task.Delay(10);
                lock (executionOrder) { executionOrder.Add("Sequential"); }
            });

        var handlers = new[] { concurrentHandler.Object, sequentialHandler.Object };
        var publisher = new NotificationPublisher<TestNotification>(handlers);

        // Act
        await publisher.Publish(notification, CancellationToken.None);

        // Assert
        Assert.Equal(new[] { "Concurrent", "Sequential" }, executionOrder);
    }

    [Fact]
    public async Task Publish_WithNotificationData_PassesDataCorrectly()
    {
        // Arrange
        var testData = "test-data";
        var notification = new TestNotificationWithData(testData);
        
        var handler = new Mock<INotificationHandler<TestNotificationWithData>>();
        handler.Setup(h => h.SupportsConcurrentExecution).Returns(true);

        var handlers = new[] { handler.Object };
        var publisher = new NotificationPublisher<TestNotificationWithData>(handlers);

        // Act
        await publisher.Publish(notification, CancellationToken.None);

        // Assert
        handler.Verify(h => h.Handle(
            It.Is<TestNotificationWithData>(n => n.Data == testData), 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Publish_WithEmptyHandlers_CompletesSuccessfully()
    {
        // Arrange
        var notification = new TestNotification { Message = "Test" };
        var handlers = Array.Empty<INotificationHandler<TestNotification>>();
        var publisher = new NotificationPublisher<TestNotification>(handlers);

        // Act & Assert - Should not throw
        await publisher.Publish(notification, CancellationToken.None);
    }

    [Fact]
    public async Task Publish_WithCancellationToken_PassesToHandlers()
    {
        // Arrange
        var notification = new TestNotification { Message = "Test" };
        var cancellationToken = new CancellationToken();
        
        var handler = new Mock<INotificationHandler<TestNotification>>();
        handler.Setup(h => h.SupportsConcurrentExecution).Returns(true);

        var handlers = new[] { handler.Object };
        var publisher = new NotificationPublisher<TestNotification>(handlers);

        // Act
        await publisher.Publish(notification, cancellationToken);

        // Assert
        handler.Verify(h => h.Handle(notification, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Publish_WhenHandlerThrows_PropagatesException()
    {
        // Arrange
        var notification = new TestNotification { Message = "Test" };
        var expectedException = new InvalidOperationException("Handler failed");
        
        var handler = new Mock<INotificationHandler<TestNotification>>();
        handler.Setup(h => h.SupportsConcurrentExecution).Returns(true);
        handler.Setup(h => h.Handle(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        var handlers = new[] { handler.Object };
        var publisher = new NotificationPublisher<TestNotification>(handlers);

        // Act & Assert
        var actualException = await Assert.ThrowsAsync<InvalidOperationException>(
            () => publisher.Publish(notification, CancellationToken.None));
        
        Assert.Equal(expectedException.Message, actualException.Message);
    }

    [Fact]
    public async Task Publish_WithMultipleConcurrentHandlers_ExecutesAllConcurrently()
    {
        // Arrange
        var notification = new TestNotification { Message = "Test" };
        var handlerCount = 5;
        var handlers = new List<Mock<INotificationHandler<TestNotification>>>();
        var executionTimes = new List<DateTime>();

        for (int i = 0; i < handlerCount; i++)
        {
            var handler = new Mock<INotificationHandler<TestNotification>>();
            handler.Setup(h => h.SupportsConcurrentExecution).Returns(true);
            
            var handlerIndex = i; // Capture for closure
            handler.Setup(h => h.Handle(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    await Task.Delay(20);
                    lock (executionTimes) { executionTimes.Add(DateTime.UtcNow); }
                });
            
            handlers.Add(handler);
        }

        var publisher = new NotificationPublisher<TestNotification>(handlers.Select(h => h.Object));

        // Act
        var startTime = DateTime.UtcNow;
        await publisher.Publish(notification, CancellationToken.None);
        var endTime = DateTime.UtcNow;

        // Assert
        Assert.Equal(handlerCount, executionTimes.Count);
        // All handlers should complete in roughly the same time if running concurrently
        var totalTime = (endTime - startTime).TotalMilliseconds;
        Assert.True(totalTime < 100, $"Expected concurrent execution to complete quickly, but took {totalTime}ms");
        
        foreach (var handler in handlers)
        {
            handler.Verify(h => h.Handle(notification, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}