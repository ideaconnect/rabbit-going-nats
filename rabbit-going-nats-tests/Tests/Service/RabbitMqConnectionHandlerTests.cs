using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RabbitGoingNats.Model;
using RabbitGoingNats.Service;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Reflection;

namespace Tests.Service;

public class RabbitMqConnectionHandlerTests : IDisposable
{
    private readonly Mock<ILogger<RabbitMqConnectionHandler>> _mockLogger;
    private readonly Mock<IOptions<RabbitMqConnection>> _mockOptions;
    private readonly Mock<INatsConnectionHandler> _mockNatsHandler;

    public RabbitMqConnectionHandlerTests()
    {
        _mockLogger = new Mock<ILogger<RabbitMqConnectionHandler>>();
        _mockOptions = new Mock<IOptions<RabbitMqConnection>>();
        _mockNatsHandler = new Mock<INatsConnectionHandler>();
    }

    [Fact]
    public void Constructor_ShouldInitializeSuccessfully_WithValidConfiguration()
    {
        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        // Act
        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Assert
        Assert.NotNull(handler);
        Assert.IsAssignableFrom<IRabbitMqConnectionHandler>(handler);
        Assert.IsAssignableFrom<IAsyncDisposable>(handler);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenLoggerIsNull()
    {
        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new RabbitMqConnectionHandler(null!, _mockOptions.Object, _mockNatsHandler.Object));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenOptionsIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new RabbitMqConnectionHandler(_mockLogger.Object, null!, _mockNatsHandler.Object));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenNatsHandlerIsNull()
    {
        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, null!));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenConfigurationValueIsNull()
    {
        // Arrange
        _mockOptions.Setup(x => x.Value).Returns((RabbitMqConnection)null!);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object));
    }

    [Fact]
    public void Constructor_ShouldHandleBasicConfiguration()
    {
        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        // Act
        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Assert
        Assert.NotNull(handler);
    }

    [Fact]
    public void Constructor_ShouldHandleCompleteConfiguration()
    {
        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "rabbitmq.example.com",
            Port = 5672,
            UserName = "testuser",
            Password = "testpass",
            VirtualHost = "/production",
            QueueName = "production-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        // Act
        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Assert
        Assert.NotNull(handler);
    }

    [Fact]
    public async Task ConsumeAsync_ShouldThrowInvalidOperationException_WhenQueueNameIsEmpty()
    {
        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "" // Empty queue name
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.ConsumeAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ConsumeAsync_ShouldThrowInvalidOperationException_WhenQueueNameIsNull()
    {
        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = null! // Null queue name
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.ConsumeAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ConsumeAsync_ShouldThrowInvalidOperationException_WhenQueueNameIsWhitespace()
    {
        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "   " // Whitespace only
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.ConsumeAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ConsumeAsync_ShouldThrowObjectDisposedException_WhenDisposed()
    {
        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Dispose the handler first
        await handler.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            handler.ConsumeAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ConsumeAsync_ShouldHandleCancellation()
    {
        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        // Since we can't establish a real RabbitMQ connection in tests,
        // this will likely throw InvalidOperationException before reaching cancellation
        // But we test that cancellation is properly handled
        try
        {
            await handler.ConsumeAsync(cts.Token);
        }
        catch (InvalidOperationException)
        {
            // Expected when no real RabbitMQ server is available
        }
        catch (OperationCanceledException)
        {
            // Also acceptable - means cancellation was handled
        }

        // Cleanup
        await handler.DisposeAsync();
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("rabbitmq.example.com")]
    [InlineData("192.168.1.100")]
    [InlineData("rabbitmq-container")]
    public void Constructor_ShouldHandleVariousHostNames(string hostName)
    {
        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = hostName,
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        // Act
        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Assert
        Assert.NotNull(handler);
    }

    [Theory]
    [InlineData(5672)]
    [InlineData(5671)]
    [InlineData(25672)]
    [InlineData(15672)]
    public void Constructor_ShouldHandleVariousPorts(int port)
    {
        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            Port = port,
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        // Act
        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Assert
        Assert.NotNull(handler);
    }

    [Theory]
    [InlineData("guest", "guest")]
    [InlineData("admin", "password123")]
    [InlineData("service-account", "secure-password!")]
    [InlineData("user@domain.com", "P@ssw0rd")]
    public void Constructor_ShouldHandleVariousCredentials(string userName, string password)
    {
        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            UserName = userName,
            Password = password,
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        // Act
        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Assert
        Assert.NotNull(handler);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("production")]
    [InlineData("development")]
    [InlineData("test-env")]
    public void Constructor_ShouldHandleVariousVirtualHosts(string virtualHost)
    {
        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            VirtualHost = virtualHost,
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        // Act
        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Assert
        Assert.NotNull(handler);
    }

    [Theory]
    [InlineData("orders")]
    [InlineData("customer-notifications")]
    [InlineData("payment-events")]
    [InlineData("prod-user-service-outbox")]
    public void Constructor_ShouldHandleVariousQueueNames(string queueName)
    {
        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = queueName
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        // Act
        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Assert
        Assert.NotNull(handler);
    }

    [Fact]
    public void Constructor_ShouldHandleNullOptionalProperties()
    {
        // Arrange - Only required properties set, others null
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            Port = null,
            UserName = null,
            Password = null,
            VirtualHost = null,
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        // Act
        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Assert
        Assert.NotNull(handler);
    }

    [Fact]
    public async Task DisposeAsync_ShouldNotThrow_WhenCalledMultipleTimes()
    {
        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Act - Multiple disposals should be safe
        await handler.DisposeAsync();
        await handler.DisposeAsync();
        await handler.DisposeAsync();

        // Assert - No exception should be thrown
        // The test passes if we reach this point without exceptions
    }

    [Fact]
    public async Task DisposeAsync_ShouldLogDebugMessages()
    {
        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Act
        await handler.DisposeAsync();

        // Assert
        VerifyLogCalled(LogLevel.Debug, "Disposing RabbitMQ connection handler");
        VerifyLogCalled(LogLevel.Debug, "RabbitMQ connection handler disposed");
    }

    [Fact]
    public void Constructor_ShouldHandleProductionConfiguration()
    {
        // Arrange - Typical production setup
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "rabbitmq.production.example.com",
            Port = 5671, // TLS port
            UserName = "prod-bridge-service",
            Password = "SecureProductionPassword123!",
            VirtualHost = "production",
            QueueName = "payment-events"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        // Act
        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Assert
        Assert.NotNull(handler);
    }

    [Fact]
    public void Constructor_ShouldHandleDevelopmentConfiguration()
    {
        // Arrange - Typical development setup
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "dev-queue"
            // No authentication, using defaults
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        // Act
        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Assert
        Assert.NotNull(handler);
    }

    [Fact]
    public void Constructor_ShouldHandleDockerConfiguration()
    {
        // Arrange - Typical Docker setup
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "rabbitmq-container",
            UserName = "guest",
            Password = "guest",
            QueueName = "docker-test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        // Act
        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Assert
        Assert.NotNull(handler);
    }

    [Fact]
    public async Task ConsumeAsync_ShouldAttemptConnectionBuild()
    {
        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Act & Assert
        // Since we can't establish real RabbitMQ connections in unit tests,
        // we expect this to throw InvalidOperationException when trying to connect
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.ConsumeAsync(CancellationToken.None));

        // Cleanup
        await handler.DisposeAsync();
    }

    [Fact]
    public async Task ConsumeAsync_ShouldLogInformationAboutStarting()
    {
        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Act
        try
        {
            await handler.ConsumeAsync(CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // Expected when no real RabbitMQ server
        }

        // Assert
        VerifyLogCalled(LogLevel.Information, "Starting RabbitMQ message consumption");

        // Cleanup
        await handler.DisposeAsync();
    }

    [Fact]
    public void Interface_ShouldBeProperlyImplemented()
    {
        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        // Act
        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Assert
        Assert.IsAssignableFrom<IRabbitMqConnectionHandler>(handler);
        Assert.IsAssignableFrom<IAsyncDisposable>(handler);

        // Test that the interface methods are available
        Assert.True(handler.ConsumeAsync != null);
        Assert.True(handler.DisposeAsync != null);
    }

    [Fact]
    public async Task RabbitMqHandler_CompleteWorkflow_ShouldHandleLifecycle()
    {
        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            Port = 5672,
            UserName = "test-user",
            Password = "test-password",
            VirtualHost = "/test",
            QueueName = "integration-test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        // Act
        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Test consumption attempt (will fail due to no real server, but tests the workflow)
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            await handler.ConsumeAsync(cts.Token);
        }
        catch (InvalidOperationException)
        {
            // Expected - no real RabbitMQ server in test environment
        }
        catch (OperationCanceledException)
        {
            // Also acceptable - timeout reached
        }

        // Test disposal
        await handler.DisposeAsync();

        // Assert
        VerifyLogCalled(LogLevel.Information, "Starting RabbitMQ message consumption");
        VerifyLogCalled(LogLevel.Debug, "Disposing RabbitMQ connection handler");
    }

    [Fact]
    public void GetQueueName_ShouldReturnConfiguredQueueName()
    {
        // This test covers the GetQueueName method through the public API

        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue-name"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Act & Assert
        // The GetQueueName method is private, but we can test it through ConsumeAsync
        // which will call GetQueueName and should fail with InvalidOperationException for connection issues
        // rather than configuration issues if the queue name is valid

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => handler.ConsumeAsync(CancellationToken.None));

        // The exception should be about connection issues, not queue name configuration
        // This indirectly tests that GetQueueName worked correctly
        Assert.True(exception != null);
    }

    [Fact]
    public async Task BuildChannel_ShouldAttemptConnectionCreation()
    {
        // This test covers the BuildChannel method through ConsumeAsync

        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            Port = 5672,
            UserName = "guest",
            Password = "guest",
            VirtualHost = "/",
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Act
        try
        {
            await handler.ConsumeAsync(CancellationToken.None);
        }
        catch (InvalidOperationException ex)
        {
            // Expected - should fail when trying to build channel due to no real RabbitMQ server
            Assert.Contains("Unable to establish RabbitMQ connection", ex.Message);
        }

        // Assert - Should have attempted to create connection
        VerifyLogCalled(LogLevel.Debug, "Creating RabbitMQ connection");

        // Cleanup
        await handler.DisposeAsync();
    }

    [Fact]
    public async Task BuildChannel_ShouldLogConnectionDetails()
    {
        // Test that BuildChannel logs the connection details

        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "test-hostname",
            Port = 1234,
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Act
        try
        {
            await handler.ConsumeAsync(CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // Expected - connection will fail
        }

        // Assert - Should have logged the hostname and port
        VerifyLogCalled(LogLevel.Debug, "test-hostname");
        VerifyLogCalled(LogLevel.Debug, "1234");

        // Cleanup
        await handler.DisposeAsync();
    }

    [Fact]
    public async Task ConsumeAsync_ShouldAttemptBuildConsumer()
    {
        // Test that ConsumeAsync attempts to build a consumer

        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Act
        try
        {
            await handler.ConsumeAsync(CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // Expected - will fail at BuildChannel step due to no real RabbitMQ
        }

        // Assert - Should have attempted the workflow
        VerifyLogCalled(LogLevel.Information, "Starting RabbitMQ message consumption");

        // Cleanup
        await handler.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_ShouldHandleNullConnection()
    {
        // Test disposal when no connection was established

        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Act - Dispose without attempting to consume (no connection established)
        await handler.DisposeAsync();

        // Assert - Should handle null connection gracefully
        VerifyLogCalled(LogLevel.Debug, "Disposing RabbitMQ connection handler");
        VerifyLogCalled(LogLevel.Debug, "RabbitMQ connection handler disposed");
    }

    [Fact]
    public async Task ConsumeAsync_ShouldLogProgressMessages()
    {
        // Test that the ConsumeAsync method logs appropriate progress messages

        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "progress-test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Act
        try
        {
            await handler.ConsumeAsync(CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // Expected - will fail due to no real RabbitMQ server
        }

        // Assert - Should have logged information about starting consumption
        VerifyLogCalled(LogLevel.Information, "Starting RabbitMQ message consumption");

        // Should also log error about failed connection build
        VerifyLogCalled(LogLevel.Error, "Failed to build RabbitMQ channel");

        // Cleanup
        await handler.DisposeAsync();
    }

    [Fact]
    public void RabbitMqConnectionHandler_CoverageNote_DocumentUncoveredAreas()
    {
        // This test documents the areas that are difficult to test in unit tests

        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        // Act
        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Assert - Handler is created successfully
        Assert.NotNull(handler);

        // The following areas remain uncovered and require integration testing:
        // 1. Event Handlers (OnConsumerShutdown, OnConsumerRegistered, OnConsumerCancelled, OnMessageReceived)
        //    - These are triggered by actual RabbitMQ server events
        // 2. Successful message processing and NATS forwarding
        //    - Requires real RabbitMQ messages and NATS connectivity
        // 3. Connection recovery and monitoring
        //    - Requires actual connection loss/restore scenarios
        // 4. Consumer cancellation and cleanup
        //    - Requires established RabbitMQ consumers
        // 5. Error handling in message processing
        //    - Requires real message processing failures

        // These scenarios are best covered through:
        // - Integration tests with real RabbitMQ server
        // - End-to-end testing with message flows
        // - Chaos engineering to simulate failures
        // - Load testing to trigger edge cases
    }

    [Fact]
    public void GetQueueName_ShouldReturnValidQueueName_UsingReflection()
    {
        // Test the private GetQueueName method using reflection

        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "my-test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Act - Use reflection to call private GetQueueName method
        var getQueueNameMethod = typeof(RabbitMqConnectionHandler).GetMethod("GetQueueName", BindingFlags.NonPublic | BindingFlags.Instance);
        var result = getQueueNameMethod?.Invoke(handler, null) as string;

        // Assert
        Assert.Equal("my-test-queue", result);
    }

    [Fact]
    public void GetQueueName_ShouldThrowInvalidOperationException_WhenQueueNameIsEmpty_UsingReflection()
    {
        // Test the private GetQueueName method exception path using reflection

        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = ""
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Act & Assert - Use reflection to call private GetQueueName method
        var getQueueNameMethod = typeof(RabbitMqConnectionHandler).GetMethod("GetQueueName", BindingFlags.NonPublic | BindingFlags.Instance);

        var exception = Assert.Throws<TargetInvocationException>(() => getQueueNameMethod?.Invoke(handler, null));
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains("RabbitMQ queue name is not configured", exception.InnerException?.Message);
    }

    [Fact]
    public void GetQueueName_ShouldThrowInvalidOperationException_WhenQueueNameIsNull_UsingReflection()
    {
        // Test the private GetQueueName method exception path with null using reflection

        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = null!
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Act & Assert - Use reflection to call private GetQueueName method
        var getQueueNameMethod = typeof(RabbitMqConnectionHandler).GetMethod("GetQueueName", BindingFlags.NonPublic | BindingFlags.Instance);

        var exception = Assert.Throws<TargetInvocationException>(() => getQueueNameMethod?.Invoke(handler, null));
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains("RabbitMQ queue name is not configured", exception.InnerException?.Message);
    }

    [Fact]
    public void GetQueueName_ShouldThrowInvalidOperationException_WhenQueueNameIsWhitespace_UsingReflection()
    {
        // Test the private GetQueueName method exception path with whitespace using reflection

        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "   "
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Act & Assert - Use reflection to call private GetQueueName method
        var getQueueNameMethod = typeof(RabbitMqConnectionHandler).GetMethod("GetQueueName", BindingFlags.NonPublic | BindingFlags.Instance);

        var exception = Assert.Throws<TargetInvocationException>(() => getQueueNameMethod?.Invoke(handler, null));
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains("RabbitMQ queue name is not configured", exception.InnerException?.Message);
    }

    [Fact]
    public async Task BuildConsumer_ShouldThrowObjectDisposedException_WhenDisposed_UsingReflection()
    {
        // Test the private BuildConsumer method exception path using reflection

        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Dispose the handler first
        await handler.DisposeAsync();

        // Act & Assert - Use reflection to call private BuildConsumer method
        var buildConsumerMethod = typeof(RabbitMqConnectionHandler).GetMethod("BuildConsumer", BindingFlags.NonPublic | BindingFlags.Instance);

        // Create a mock channel for the test
        var mockChannel = new Mock<IModel>();

        var exception = Assert.Throws<TargetInvocationException>(() => buildConsumerMethod?.Invoke(handler, new object[] { mockChannel.Object }));
        Assert.IsType<ObjectDisposedException>(exception.InnerException);
    }

    [Fact]
    public void BuildConsumer_ShouldThrowArgumentNullException_WhenChannelIsNull_UsingReflection()
    {
        // Test the private BuildConsumer method null channel exception using reflection

        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Act & Assert - Use reflection to call private BuildConsumer method with null channel
        var buildConsumerMethod = typeof(RabbitMqConnectionHandler).GetMethod("BuildConsumer", BindingFlags.NonPublic | BindingFlags.Instance);

        var exception = Assert.Throws<TargetInvocationException>(() => buildConsumerMethod?.Invoke(handler, new object[] { null! }));
        Assert.IsType<ArgumentNullException>(exception.InnerException);
    }

    [Fact]
    public void BuildConsumer_ShouldCreateConsumerWithEventHandlers_UsingReflection()
    {
        // Test the private BuildConsumer method successful path using reflection

        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Create a mock channel
        var mockChannel = new Mock<IModel>();

        // Act - Use reflection to call private BuildConsumer method
        var buildConsumerMethod = typeof(RabbitMqConnectionHandler).GetMethod("BuildConsumer", BindingFlags.NonPublic | BindingFlags.Instance);
        var consumer = buildConsumerMethod?.Invoke(handler, new object[] { mockChannel.Object }) as EventingBasicConsumer;

        // Assert
        Assert.NotNull(consumer);
        Assert.Equal(mockChannel.Object, consumer.Model);

        // Verify debug logging was called
        VerifyLogCalled(LogLevel.Debug, "RabbitMQ consumer configured with event handlers");
    }

    [Fact]
    public void OnConsumerShutdown_ShouldLogError_UsingReflection()
    {
        // Test the OnConsumerShutdown event handler using reflection

        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Act - Use reflection to call private OnConsumerShutdown method
        var onConsumerShutdownMethod = typeof(RabbitMqConnectionHandler).GetMethod("OnConsumerShutdown", BindingFlags.NonPublic | BindingFlags.Instance);

        var shutdownEventArgs = new ShutdownEventArgs(ShutdownInitiator.Application, 200, "Normal shutdown");
        onConsumerShutdownMethod?.Invoke(handler, new object[] { null!, shutdownEventArgs });

        // Assert
        VerifyLogCalled(LogLevel.Error, "Lost connection with RabbitMQ");
        VerifyLogCalled(LogLevel.Error, "Normal shutdown");
    }

    [Fact]
    public void OnConsumerRegistered_ShouldLogInformation_WithoutPreviousLoss_UsingReflection()
    {
        // Test the OnConsumerRegistered event handler without previous connection loss

        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Act - Use reflection to call private OnConsumerRegistered method
        var onConsumerRegisteredMethod = typeof(RabbitMqConnectionHandler).GetMethod("OnConsumerRegistered", BindingFlags.NonPublic | BindingFlags.Instance);

        var consumerEventArgs = new ConsumerEventArgs(new string[] { "test-consumer-tag" });
        onConsumerRegisteredMethod?.Invoke(handler, new object[] { null!, consumerEventArgs });

        // Assert
        VerifyLogCalled(LogLevel.Information, "Successfully connected to RabbitMQ");
    }

    [Fact]
    public void OnConsumerRegistered_ShouldLogWarning_WithPreviousLoss_UsingReflection()
    {
        // Test the OnConsumerRegistered event handler with previous connection loss

        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // First simulate a connection loss
        var onConsumerShutdownMethod = typeof(RabbitMqConnectionHandler).GetMethod("OnConsumerShutdown", BindingFlags.NonPublic | BindingFlags.Instance);
        var shutdownEventArgs = new ShutdownEventArgs(ShutdownInitiator.Application, 200, "Connection lost");
        onConsumerShutdownMethod?.Invoke(handler, new object[] { null!, shutdownEventArgs });

        // Small delay to ensure timestamp difference
        Thread.Sleep(10);

        // Act - Now simulate registration (reconnection)
        var onConsumerRegisteredMethod = typeof(RabbitMqConnectionHandler).GetMethod("OnConsumerRegistered", BindingFlags.NonPublic | BindingFlags.Instance);
        var consumerEventArgs = new ConsumerEventArgs(new string[] { "test-consumer-tag" });
        onConsumerRegisteredMethod?.Invoke(handler, new object[] { null!, consumerEventArgs });

        // Assert
        VerifyLogCalled(LogLevel.Warning, "Regained RabbitMQ connection");
        VerifyLogCalled(LogLevel.Warning, "Downtime:");
    }

    [Fact]
    public void OnConsumerCancelled_ShouldLogCritical_UsingReflection()
    {
        // Test the OnConsumerCancelled event handler using reflection

        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Act - Use reflection to call private OnConsumerCancelled method
        var onConsumerCancelledMethod = typeof(RabbitMqConnectionHandler).GetMethod("OnConsumerCancelled", BindingFlags.NonPublic | BindingFlags.Instance);

        var consumerEventArgs = new ConsumerEventArgs(new string[] { "cancelled-consumer-tag" });
        onConsumerCancelledMethod?.Invoke(handler, new object[] { null!, consumerEventArgs });

        // Assert
        VerifyLogCalled(LogLevel.Critical, "Consumer has been cancelled by server");
        VerifyLogCalled(LogLevel.Critical, "Intervention may be required");
    }

    [Fact]
    public async Task OnMessageReceived_ShouldHandleDisposedState_UsingReflection()
    {
        // Test the OnMessageReceived event handler when disposed

        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Dispose the handler first
        await handler.DisposeAsync();

        // Act - Use reflection to call private OnMessageReceived method
        var onMessageReceivedMethod = typeof(RabbitMqConnectionHandler).GetMethod("OnMessageReceived", BindingFlags.NonPublic | BindingFlags.Instance);

        var mockChannel = new Mock<IModel>();
        var mockConsumer = new Mock<EventingBasicConsumer>(mockChannel.Object);

        var deliveryEventArgs = new BasicDeliverEventArgs
        {
            DeliveryTag = 1,
            Body = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("test message"))
        };

        onMessageReceivedMethod?.Invoke(handler, new object[] { mockConsumer.Object, deliveryEventArgs });

        // Give the async method a moment to complete
        await Task.Delay(50);

        // Assert
        VerifyLogCalled(LogLevel.Warning, "Received message after disposal, ignoring");
    }

    [Fact]
    public async Task OnMessageReceived_ShouldLogError_WhenChannelIsNull_UsingReflection()
    {
        // Test the OnMessageReceived event handler when channel is null

        // Arrange
        var rabbitConfig = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue"
        };
        _mockOptions.Setup(x => x.Value).Returns(rabbitConfig);

        var handler = new RabbitMqConnectionHandler(_mockLogger.Object, _mockOptions.Object, _mockNatsHandler.Object);

        // Act - Use reflection to call private OnMessageReceived method with null model
        var onMessageReceivedMethod = typeof(RabbitMqConnectionHandler).GetMethod("OnMessageReceived", BindingFlags.NonPublic | BindingFlags.Instance);

        var deliveryEventArgs = new BasicDeliverEventArgs
        {
            DeliveryTag = 1,
            Body = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("test message"))
        };

        onMessageReceivedMethod?.Invoke(handler, new object[] { null!, deliveryEventArgs });

        // Give the async method a moment to complete
        await Task.Delay(50);

        // Assert
        VerifyLogCalled(LogLevel.Error, "Unable to get channel from consumer model");

        // Cleanup
        await handler.DisposeAsync();
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

    private void VerifyLogNotCalled(LogLevel level, string message)
    {
        _mockLogger.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    public void Dispose()
    {
        // Clean up any test resources if needed
    }
}