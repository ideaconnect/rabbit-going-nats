using Microsoft.Extensions.Logging;
using Moq;
using RabbitGoingNats;
using RabbitGoingNats.Service;

namespace Tests.Service;

public class WorkerTests : IDisposable
{
    private readonly Mock<ILogger<Worker>> _mockLogger;
    private readonly Mock<IRabbitMqConnectionHandler> _mockRabbitMqHandler;
    private readonly Worker _worker;

    public WorkerTests()
    {
        _mockLogger = new Mock<ILogger<Worker>>();
        _mockRabbitMqHandler = new Mock<IRabbitMqConnectionHandler>();
        _worker = new Worker(_mockLogger.Object, _mockRabbitMqHandler.Object);
    }

    [Fact]
    public async Task StartAsync_ShouldStartSuccessfully_WhenCalled()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Setup the mock to complete successfully
        _mockRabbitMqHandler
            .Setup(x => x.ConsumeAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = _worker.StartAsync(cancellationToken);

        // Assert
        await result; // Should not throw
        Assert.True(result.IsCompletedSuccessfully);

        // Verify logging
        VerifyLogCalled(LogLevel.Information, "Worker starting at:");
    }

    [Fact]
    public async Task StartAsync_ShouldHandleImmediateFailure_WhenConsumeAsyncFailsImmediately()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        var expectedException = new InvalidOperationException("Immediate failure");

        // Setup the mock to fail immediately
        _mockRabbitMqHandler
            .Setup(x => x.ConsumeAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        // When the task fails immediately, StartAsync returns the faulted task
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _worker.StartAsync(cancellationToken);
        });

        Assert.Equal("Immediate failure", exception.Message);

        // Verify logging occurred
        VerifyLogCalled(LogLevel.Information, "Worker starting at:");
        VerifyLogCalled(LogLevel.Error, "Worker encountered an error during consumption.");
    }

    [Fact]
    public async Task StopAsync_ShouldStopGracefully_WhenCalled()
    {
        // Arrange
        var startCts = new CancellationTokenSource();
        var stopCts = new CancellationTokenSource();

        // Setup a long-running consume operation
        var consumeTaskCompletion = new TaskCompletionSource<bool>();
        _mockRabbitMqHandler
            .Setup(x => x.ConsumeAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                try
                {
                    await consumeTaskCompletion.Task;
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancelled
                }
            });

        // Start the worker
        await _worker.StartAsync(startCts.Token);

        // Give it a moment to start
        await Task.Delay(50);

        // Act
        var stopTask = _worker.StopAsync(stopCts.Token);

        // Complete the consume task to simulate graceful shutdown
        consumeTaskCompletion.SetResult(true);

        await stopTask;

        // Assert
        VerifyLogCalled(LogLevel.Information, "Worker stop requested.");
        VerifyLogCalled(LogLevel.Information, "Worker stopped at:");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleOperationCancelledException_Gracefully()
    {
        // Arrange
        var cancellationToken = new CancellationToken(true); // Pre-cancelled token

        _mockRabbitMqHandler
            .Setup(x => x.ConsumeAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        await _worker.StartAsync(CancellationToken.None);

        // Give time for execution
        await Task.Delay(100);

        // Assert
        VerifyLogCalled(LogLevel.Information, "Worker started consuming messages.");
        VerifyLogCalled(LogLevel.Information, "Worker consumption cancelled.");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldLogUnexpectedExceptions()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Unexpected error");

        _mockRabbitMqHandler
            .Setup(x => x.ConsumeAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        // The exception should propagate from StartAsync when the task fails immediately
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _worker.StartAsync(CancellationToken.None);
        });

        Assert.Equal("Unexpected error", exception.Message);

        // Verify error logging occurred
        VerifyLogCalled(LogLevel.Error, "Worker encountered an error during consumption.");
    }

    [Fact]
    public async Task StopAsync_ShouldDisposeAsyncDisposableHandler_WhenAvailable()
    {
        // Arrange
        var mockAsyncDisposableHandler = new Mock<IRabbitMqConnectionHandler>();
        mockAsyncDisposableHandler.As<IAsyncDisposable>();
        var worker = new Worker(_mockLogger.Object, mockAsyncDisposableHandler.Object);

        // Setup successful consume
        mockAsyncDisposableHandler
            .Setup(x => x.ConsumeAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(50);

        // Act
        await worker.StopAsync(CancellationToken.None);

        // Assert
        mockAsyncDisposableHandler.As<IAsyncDisposable>()
            .Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task StopAsync_ShouldHandleDisposalExceptions_Gracefully()
    {
        // Arrange
        var mockAsyncDisposableHandler = new Mock<IRabbitMqConnectionHandler>();
        mockAsyncDisposableHandler.As<IAsyncDisposable>();
        var worker = new Worker(_mockLogger.Object, mockAsyncDisposableHandler.Object);

        // Setup disposal to throw exception
        mockAsyncDisposableHandler.As<IAsyncDisposable>()
            .Setup(x => x.DisposeAsync())
            .ThrowsAsync(new InvalidOperationException("Disposal error"));

        // Setup successful consume
        mockAsyncDisposableHandler
            .Setup(x => x.ConsumeAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(50);

        // Act
        await worker.StopAsync(CancellationToken.None);

        // Assert - should not throw and should log error
        VerifyLogCalled(LogLevel.Error, "Error disposing RabbitMQ connection handler.");
    }

    [Fact]
    public void Dispose_ShouldNotThrow_WhenCalled()
    {
        // Act & Assert
        var exception = Record.Exception(() => _worker.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_ShouldBeIdempotent()
    {
        // Act
        _worker.Dispose();
        _worker.Dispose(); // Second call should not throw

        // Assert - no exception should be thrown
        Assert.True(true); // Test passes if no exception
    }

    [Fact]
    public async Task Worker_ShouldImplementIHostedServiceCorrectly()
    {
        // Arrange
        _mockRabbitMqHandler
            .Setup(x => x.ConsumeAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act & Assert
        Assert.IsAssignableFrom<Microsoft.Extensions.Hosting.IHostedService>(_worker);
        Assert.IsAssignableFrom<IDisposable>(_worker);

        // Test the interface methods work
        await _worker.StartAsync(CancellationToken.None);
        await _worker.StopAsync(CancellationToken.None);
        _worker.Dispose();
    }

    [Fact]
    public async Task Worker_ShouldCallConsumeAsyncOnStart()
    {
        // Arrange
        _mockRabbitMqHandler
            .Setup(x => x.ConsumeAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _worker.StartAsync(CancellationToken.None);

        // Give time for background task to start
        await Task.Delay(100);

        // Assert
        _mockRabbitMqHandler.Verify(
            x => x.ConsumeAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private void VerifyLogCalled(LogLevel level, string message)
    {
        _mockLogger.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task StopAsync_ShouldHandleTimeout_WhenExecutingTaskDoesNotComplete()
    {
        // Arrange
        var longRunningTaskCompletion = new TaskCompletionSource<bool>();
        _mockRabbitMqHandler
            .Setup(x => x.ConsumeAsync(It.IsAny<CancellationToken>()))
            .Returns(longRunningTaskCompletion.Task);

        await _worker.StartAsync(CancellationToken.None);
        await Task.Delay(50); // Let it start

        // Create a cancellation token that will timeout quickly for testing
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act
        await _worker.StopAsync(cts.Token);

        // Assert - Should handle timeout gracefully
        VerifyLogCalled(LogLevel.Information, "Worker stop requested.");
        VerifyLogCalled(LogLevel.Warning, "Worker task did not complete within timeout period.");
        VerifyLogCalled(LogLevel.Information, "Worker stopped at:");

        // Cleanup
        longRunningTaskCompletion.SetResult(true);
    }

    [Fact]
    public async Task StopAsync_ShouldHandleNullExecutingTask()
    {
        // Arrange - Create a fresh worker without starting it
        var worker = new Worker(_mockLogger.Object, _mockRabbitMqHandler.Object);

        // Act
        await worker.StopAsync(CancellationToken.None);

        // Assert - Should complete without errors
        VerifyLogCalled(LogLevel.Information, "Worker stop requested.");
        VerifyLogCalled(LogLevel.Information, "Worker stopped at:");

        // Cleanup
        worker.Dispose();
    }

    [Fact]
    public async Task StopAsync_ShouldHandleExceptionInExecutingTask()
    {
        // Arrange
        var faultyTaskCompletion = new TaskCompletionSource<bool>();
        _mockRabbitMqHandler
            .Setup(x => x.ConsumeAsync(It.IsAny<CancellationToken>()))
            .Returns(faultyTaskCompletion.Task);

        await _worker.StartAsync(CancellationToken.None);
        await Task.Delay(50);

        // Act
        var stopTask = _worker.StopAsync(CancellationToken.None);

        // Make the executing task throw an exception
        faultyTaskCompletion.SetException(new InvalidOperationException("Task execution error"));

        await stopTask;

        // Assert
        VerifyLogCalled(LogLevel.Information, "Worker stop requested.");
        VerifyLogCalled(LogLevel.Error, "Error occurred while stopping worker.");
        VerifyLogCalled(LogLevel.Information, "Worker stopped at:");
    }

    [Fact]
    public async Task StopAsync_ShouldLogDebugWhenAsyncDisposableHandlerDisposedSuccessfully()
    {
        // Arrange
        var mockAsyncDisposableHandler = new Mock<IRabbitMqConnectionHandler>();
        mockAsyncDisposableHandler.As<IAsyncDisposable>();

        // Enable debug logging to capture the debug message
        var mockLogger = new Mock<ILogger<Worker>>();
        mockLogger.Setup(x => x.IsEnabled(LogLevel.Debug)).Returns(true);

        var worker = new Worker(mockLogger.Object, mockAsyncDisposableHandler.Object);

        // Setup successful consume and dispose
        mockAsyncDisposableHandler
            .Setup(x => x.ConsumeAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        mockAsyncDisposableHandler.As<IAsyncDisposable>()
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(50);

        // Act
        await worker.StopAsync(CancellationToken.None);

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("RabbitMQ connection handler disposed.")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Cleanup
        worker.Dispose();
    }

    [Fact]
    public async Task StopAsync_ShouldLogTaskCompletedSuccessfully_WhenExecutingTaskFinishes()
    {
        // Arrange
        var taskCompletion = new TaskCompletionSource<bool>();
        _mockRabbitMqHandler
            .Setup(x => x.ConsumeAsync(It.IsAny<CancellationToken>()))
            .Returns(taskCompletion.Task);

        await _worker.StartAsync(CancellationToken.None);
        await Task.Delay(50);

        // Act
        var stopTask = _worker.StopAsync(CancellationToken.None);

        // Complete the task normally
        taskCompletion.SetResult(true);

        await stopTask;

        // Assert
        VerifyLogCalled(LogLevel.Information, "Worker task completed successfully.");
        VerifyLogCalled(LogLevel.Information, "Worker stopped at:");
    }

    [Fact]
    public async Task StopAsync_ShouldHandleHandlerThatIsNotAsyncDisposable()
    {
        // Arrange - Create a new worker with a handler that explicitly does NOT implement IAsyncDisposable
        var nonAsyncDisposableHandler = new Mock<IRabbitMqConnectionHandler>(MockBehavior.Strict);
        nonAsyncDisposableHandler
            .Setup(x => x.ConsumeAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = new Worker(_mockLogger.Object, nonAsyncDisposableHandler.Object);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(50);

        // Act
        await worker.StopAsync(CancellationToken.None);

        // Assert - Should complete without trying to dispose the handler
        VerifyLogCalled(LogLevel.Information, "Worker stop requested.");
        VerifyLogCalled(LogLevel.Information, "Worker stopped at:");

        // Verify that debug log for disposal is NOT called since handler is not IAsyncDisposable
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("RabbitMQ connection handler disposed.")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);

        // Cleanup
        worker.Dispose();
    }

    public void Dispose()
    {
        _worker?.Dispose();
    }
}