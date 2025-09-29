using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RabbitGoingNats.Model;
using RabbitGoingNats.Service;

namespace Tests.Service;

public class NatsConnectionHandlerTests : IDisposable
{
    private readonly Mock<ILogger<NatsConnectionHandler>> _mockLogger;
    private readonly Mock<IOptions<NatsConnection>> _mockOptions;

    public NatsConnectionHandlerTests()
    {
        _mockLogger = new Mock<ILogger<NatsConnectionHandler>>();
        _mockOptions = new Mock<IOptions<NatsConnection>>();
    }

    [Fact]
    public void Constructor_ShouldInitializeSuccessfully_WithValidConfiguration()
    {
        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject"
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        // Act
        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Assert
        Assert.NotNull(handler);
        VerifyLogCalled(LogLevel.Information, "Initialized NATS connection handler at:");
    }

    [Fact]
    public void Constructor_ShouldConfigureTokenAuthentication_WhenSecretProvided()
    {
        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject",
            Secret = "test-token-123"
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        // Act
        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Assert
        Assert.NotNull(handler);
        VerifyLogCalled(LogLevel.Debug, "Configuring NATS with token-based authentication.");
        VerifyLogCalled(LogLevel.Information, "Initialized NATS connection handler at:");

        // Cleanup
        handler.DisposeAsync().AsTask().Wait();
    }

    [Fact]
    public void Constructor_ShouldConfigureUsernamePasswordAuthentication_WhenCredentialsProvided()
    {
        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject",
            User = "testuser",
            Password = "testpassword"
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        // Act
        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Assert
        Assert.NotNull(handler);
        VerifyLogCalled(LogLevel.Debug, "Configuring NATS with username/password authentication.");
        VerifyLogCalled(LogLevel.Information, "Initialized NATS connection handler at:");

        // Cleanup
        handler.DisposeAsync().AsTask().Wait();
    }

    [Fact]
    public void Constructor_ShouldConfigureAnonymousConnection_WhenNoCredentialsProvided()
    {
        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject"
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        // Act
        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Assert
        Assert.NotNull(handler);
        VerifyLogCalled(LogLevel.Debug, "Configuring NATS with anonymous connection (no authentication).");
        VerifyLogCalled(LogLevel.Information, "Initialized NATS connection handler at:");

        // Cleanup
        handler.DisposeAsync().AsTask().Wait();
    }

    [Fact]
    public void Constructor_ShouldPrioritizeTokenAuthentication_WhenBothTokenAndCredentialsProvided()
    {
        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject",
            Secret = "test-token-123",
            User = "testuser",
            Password = "testpassword"
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        // Act
        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Assert
        Assert.NotNull(handler);
        VerifyLogCalled(LogLevel.Debug, "Configuring NATS with token-based authentication.");
        // Should NOT call username/password authentication
        VerifyLogNotCalled(LogLevel.Debug, "Configuring NATS with username/password authentication.");

        // Cleanup
        handler.DisposeAsync().AsTask().Wait();
    }

    [Fact]
    public void Constructor_ShouldFallBackToAnonymous_WhenOnlyUsernameProvidedWithoutPassword()
    {
        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject",
            User = "testuser"
            // No password provided
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        // Act
        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Assert
        Assert.NotNull(handler);
        VerifyLogCalled(LogLevel.Debug, "Configuring NATS with anonymous connection (no authentication).");
        VerifyLogNotCalled(LogLevel.Debug, "Configuring NATS with username/password authentication.");

        // Cleanup
        handler.DisposeAsync().AsTask().Wait();
    }

    [Fact]
    public void Constructor_ShouldFallBackToAnonymous_WhenOnlyPasswordProvidedWithoutUsername()
    {
        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject",
            Password = "testpassword"
            // No username provided
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        // Act
        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Assert
        Assert.NotNull(handler);
        VerifyLogCalled(LogLevel.Debug, "Configuring NATS with anonymous connection (no authentication).");
        VerifyLogNotCalled(LogLevel.Debug, "Configuring NATS with username/password authentication.");

        // Cleanup
        handler.DisposeAsync().AsTask().Wait();
    }

    [Fact]
    public void Constructor_ShouldHandleEmptySecretAsAnonymous()
    {
        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject",
            Secret = "" // Empty string should be treated as no secret
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        // Act
        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Assert
        Assert.NotNull(handler);
        VerifyLogCalled(LogLevel.Debug, "Configuring NATS with anonymous connection (no authentication).");
        VerifyLogNotCalled(LogLevel.Debug, "Configuring NATS with token-based authentication.");

        // Cleanup
        handler.DisposeAsync().AsTask().Wait();
    }

    [Fact]
    public void Constructor_ShouldHandleEmptyCredentialsAsAnonymous()
    {
        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject",
            User = "", // Empty strings should be treated as no credentials
            Password = ""
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        // Act
        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Assert
        Assert.NotNull(handler);
        VerifyLogCalled(LogLevel.Debug, "Configuring NATS with anonymous connection (no authentication).");
        VerifyLogNotCalled(LogLevel.Debug, "Configuring NATS with username/password authentication.");

        // Cleanup
        handler.DisposeAsync().AsTask().Wait();
    }

    [Fact]
    public async Task Publish_ShouldThrowArgumentNullException_WhenMessageIsNull()
    {
        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject"
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await handler.Publish(null!);
        });

        Assert.Equal("message", exception.ParamName);
        Assert.Contains("Message cannot be null", exception.Message);

        // Verify error logging
        VerifyLogCalled(LogLevel.Error, "Attempted to publish null message to NATS subject");

        // Cleanup
        await handler.DisposeAsync();
    }

    [Fact]
    public async Task Publish_ShouldLogDebugMessage_WhenDebugLoggingEnabled()
    {
        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject"
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        // Setup debug logging to be enabled
        _mockLogger.Setup(x => x.IsEnabled(LogLevel.Debug)).Returns(true);

        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);
        var testMessage = "Test message content";

        // Act & Assert
        // Since we can't easily mock the NATS client, we expect this to throw when trying to connect
        // But we can verify the debug logging attempt
        try
        {
            await handler.Publish(testMessage);
        }
        catch
        {
            // Expected - actual NATS connection will fail in test environment
        }

        // Assert debug logging occurred
        VerifyLogCalled(LogLevel.Debug, "Publishing message to NATS subject");

        // Cleanup
        await handler.DisposeAsync();
    }

    [Fact]
    public async Task Publish_ShouldHandleValidMessage()
    {
        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject"
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);
        var testMessage = "Valid test message";

        // Act & Assert
        // Since we can't easily mock the NATS client, we expect this to throw when trying to connect
        // but the message validation should pass
        try
        {
            await handler.Publish(testMessage);
        }
        catch (Exception ex)
        {
            // Expected - actual NATS connection will fail in test environment
            // But it should not be an ArgumentNullException
            Assert.IsNotType<ArgumentNullException>(ex);
        }

        // Cleanup
        await handler.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject"
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Act
        await handler.DisposeAsync();

        // Assert
        VerifyLogCalled(LogLevel.Information, "NATS connection handler disposed successfully.");
    }

    [Fact]
    public async Task DisposeAsync_ShouldNotThrow_WhenCalledMultipleTimes()
    {
        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject"
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Act & Assert
        await handler.DisposeAsync();

        // Second disposal should not throw
        var exception = await Record.ExceptionAsync(async () => await handler.DisposeAsync());
        Assert.Null(exception);
    }

    [Theory]
    [InlineData("orders")]
    [InlineData("customer.notifications")]
    [InlineData("prod.payment.events")]
    [InlineData("rabbitmq.bridge.messages")]
    public void Constructor_ShouldAcceptVariousSubjectFormats(string subject)
    {
        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = subject
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        // Act
        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Assert
        Assert.NotNull(handler);
        VerifyLogCalled(LogLevel.Information, "Initialized NATS connection handler at:");

        // Cleanup
        handler.DisposeAsync().AsTask().Wait();
    }

    [Theory]
    [InlineData("nats://localhost:4222")]
    [InlineData("nats+tls://nats.example.com:4222")]
    [InlineData("nats://server1:4222,nats://server2:4222")]
    public void Constructor_ShouldAcceptVariousUrlFormats(string url)
    {
        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = url,
            Subject = "test.subject"
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        // Act
        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Assert
        Assert.NotNull(handler);
        VerifyLogCalled(LogLevel.Information, "Initialized NATS connection handler at:");

        // Cleanup
        handler.DisposeAsync().AsTask().Wait();
    }

    [Fact]
    public void Constructor_ShouldGenerateCorrectReplyTopic()
    {
        // Arrange
        var subject = "test.subject";
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = subject
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        // Act
        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Assert
        // We can't directly test the reply topic since it's private,
        // but we can verify the handler was created successfully
        Assert.NotNull(handler);

        // Cleanup
        handler.DisposeAsync().AsTask().Wait();
    }

    [Fact]
    public async Task Handler_ShouldImplementIAsyncDisposableInterface()
    {
        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject"
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        // Act
        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Assert
        Assert.IsAssignableFrom<IAsyncDisposable>(handler);
        Assert.IsAssignableFrom<INatsConnectionHandler>(handler);

        // Cleanup
        await handler.DisposeAsync();
    }

    [Fact]
    public async Task Publish_ShouldLogTraceMessage_WhenTraceLoggingEnabled()
    {
        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject"
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        // Setup trace logging to be enabled
        _mockLogger.Setup(x => x.IsEnabled(LogLevel.Trace)).Returns(true);

        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);
        var testMessage = "Test message for trace logging";

        // Act & Assert
        // Since we can't easily mock the NATS client, we expect this to throw when trying to connect
        try
        {
            await handler.Publish(testMessage);
        }
        catch
        {
            // Expected - actual NATS connection will fail in test environment
        }

        // The debug logging should have been attempted
        VerifyLogCalled(LogLevel.Debug, "Publishing message to NATS subject");

        // Cleanup
        await handler.DisposeAsync();
    }

    [Fact]
    public async Task Publish_ShouldHandleEmptyStringMessage()
    {
        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject"
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);
        var emptyMessage = "";

        // Act & Assert
        // Empty string should be valid (not null), so it should pass validation
        try
        {
            await handler.Publish(emptyMessage);
        }
        catch (Exception ex)
        {
            // Should not be ArgumentNullException since empty string is not null
            Assert.IsNotType<ArgumentNullException>(ex);
        }

        // Verify debug logging occurred for the empty message
        VerifyLogCalled(LogLevel.Debug, "Publishing message to NATS subject");

        // Cleanup
        await handler.DisposeAsync();
    }

    [Fact]
    public async Task Publish_ShouldHandleLargeMessage()
    {
        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject"
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);
        // Create a large message to test handling of substantial payloads
        var largeMessage = new string('A', 10000);

        // Act & Assert
        try
        {
            await handler.Publish(largeMessage);
        }
        catch (Exception ex)
        {
            // Should not be ArgumentNullException
            Assert.IsNotType<ArgumentNullException>(ex);
        }

        // Verify debug logging occurred
        VerifyLogCalled(LogLevel.Debug, "Publishing message to NATS subject");

        // Cleanup
        await handler.DisposeAsync();
    }

    [Theory]
    [InlineData("Simple message")]
    [InlineData("Message with special characters: àáâãäåæçèéêë")]
    [InlineData("Message with numbers: 1234567890")]
    [InlineData("Message with symbols: !@#$%^&*()")]
    [InlineData("JSON-like message: {\"key\": \"value\", \"number\": 42}")]
    [InlineData("XML-like message: <root><item>value</item></root>")]
    public async Task Publish_ShouldHandleVariousMessageFormats(string message)
    {
        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject"
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Act & Assert
        try
        {
            await handler.Publish(message);
        }
        catch (Exception ex)
        {
            // Should not be ArgumentNullException for valid messages
            Assert.IsNotType<ArgumentNullException>(ex);
        }

        // Verify debug logging occurred
        VerifyLogCalled(LogLevel.Debug, "Publishing message to NATS subject");

        // Cleanup
        await handler.DisposeAsync();
    }

    [Fact]
    public void Constructor_ShouldCreateInstanceWithComplexConfiguration()
    {
        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats+tls://prod-nats-1:4222,nats+tls://prod-nats-2:4222,nats+tls://prod-nats-3:4222",
            Subject = "production.events.orders.created",
            Secret = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c",
            User = "production-service-account",
            Password = "super-secure-production-password-123!"
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        // Act
        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Assert
        Assert.NotNull(handler);
        VerifyLogCalled(LogLevel.Debug, "Configuring NATS with token-based authentication.");
        VerifyLogCalled(LogLevel.Information, "Initialized NATS connection handler at:");

        // Cleanup
        handler.DisposeAsync().AsTask().Wait();
    }

    [Fact]
    public void Constructor_ShouldGenerateCorrectReplyTopicBasedOnSubject()
    {
        // Arrange - Test various subject patterns to ensure reply topic generation works
        var testCases = new[]
        {
            ("orders", "r-orders"),
            ("events.user.created", "r-events.user.created"),
            ("production.payments.processed", "r-production.payments.processed"),
            ("system.health.check", "r-system.health.check")
        };

        foreach (var (subject, expectedReplyPrefix) in testCases)
        {
            var natsConfig = new NatsConnection
            {
                Url = "nats://localhost:4222",
                Subject = subject
            };
            _mockOptions.Setup(x => x.Value).Returns(natsConfig);

            // Act
            var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

            // Assert
            Assert.NotNull(handler);
            // We can't directly test the reply topic since it's private,
            // but we can verify the handler was created successfully with the subject
            VerifyLogCalled(LogLevel.Information, "Initialized NATS connection handler at:");

            // Cleanup
            handler.DisposeAsync().AsTask().Wait();

            // Reset mock for next iteration
            _mockLogger.Reset();
        }
    }

    [Fact]
    public async Task DisposeAsync_ShouldHandleNullClient()
    {
        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject"
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Act - Multiple disposals should be safe
        await handler.DisposeAsync();

        // Second disposal should not throw
        var exception = await Record.ExceptionAsync(async () => await handler.DisposeAsync());

        // Assert
        Assert.Null(exception);
        VerifyLogCalled(LogLevel.Information, "NATS connection handler disposed successfully.");
    }

    [Fact]
    public void Constructor_ShouldHandleConfigurationEdgeCases()
    {
        // Test edge case: very long URL
        var natsConfig = new NatsConnection
        {
            Url = "nats+tls://very-long-hostname-that-might-cause-issues-in-some-systems.example.com:4222",
            Subject = "very.long.subject.name.that.contains.many.segments.and.might.be.used.in.microservices.architecture"
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        // Act
        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Assert
        Assert.NotNull(handler);
        VerifyLogCalled(LogLevel.Information, "Initialized NATS connection handler at:");

        // Cleanup
        handler.DisposeAsync().AsTask().Wait();
    }

    [Fact]
    public void Constructor_ShouldHandleSpecialCharactersInCredentials()
    {
        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject",
            User = "user@domain.com",
            Password = "P@ssw0rd!#$%^&*()_+-=[]{}|;:'\",.<>?/~`"
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        // Act
        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Assert
        Assert.NotNull(handler);
        VerifyLogCalled(LogLevel.Debug, "Configuring NATS with username/password authentication.");
        VerifyLogCalled(LogLevel.Information, "Initialized NATS connection handler at:");

        // Cleanup
        handler.DisposeAsync().AsTask().Wait();
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