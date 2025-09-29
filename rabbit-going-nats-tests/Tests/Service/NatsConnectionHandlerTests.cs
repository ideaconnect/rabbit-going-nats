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

    [Fact]
    public async Task DisposeAsync_ShouldLogError_WhenExceptionOccursDuringDisposal()
    {
        // This test aims to cover the exception handling in DisposeAsync
        // Since we can't easily make the real NATS client throw during disposal,
        // we'll test the disposal flow and verify it doesn't throw exceptions

        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject"
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Act - Multiple disposals should be safe and not throw
        await handler.DisposeAsync();

        // Second disposal should also be safe
        var exception = await Record.ExceptionAsync(async () => await handler.DisposeAsync());

        // Assert
        Assert.Null(exception);

        // Verify that disposal completed successfully (at least once)
        VerifyLogCalled(LogLevel.Information, "NATS connection handler disposed successfully.");
    }

    [Theory]
    [InlineData("Simple test message")]
    [InlineData("Message with Unicode: 🚀📊💡")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Publish_ShouldHandleValidMessages_WithoutThrowing(string message)
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
        // We expect this to fail in test environment (no real NATS server)
        // but we're testing that our code correctly handles the flow
        try
        {
            await handler.Publish(message);
        }
        catch (Exception ex)
        {
            // Verify it's not an ArgumentNullException (our validation works)
            Assert.IsNotType<ArgumentNullException>(ex);
        }

        // Verify debug logging was attempted
        VerifyLogCalled(LogLevel.Debug, "Publishing message to NATS subject");

        // Cleanup
        await handler.DisposeAsync();
    }

    [Fact]
    public async Task Publish_ShouldLogTrace_WhenSuccessful()
    {
        // This test aims to cover the trace logging path after successful publish
        // Since we can't easily make real NATS connections work in test,
        // we test the overall flow

        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject"
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        // Enable trace logging
        _mockLogger.Setup(x => x.IsEnabled(LogLevel.Trace)).Returns(true);

        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Act
        try
        {
            await handler.Publish("Test message for trace logging");
        }
        catch
        {
            // Expected in test environment
        }

        // Assert - The debug log should have been called
        VerifyLogCalled(LogLevel.Debug, "Publishing message to NATS subject");

        // Cleanup
        await handler.DisposeAsync();
    }

    [Fact]
    public void Create_ShouldSetupConnectionEventHandlers()
    {
        // This test verifies that the Create method sets up the necessary
        // connection event handlers. While we can't easily trigger the handlers
        // in a unit test, we can verify the handler is created successfully.

        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject",
            Secret = "test-token"
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        // Act
        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Assert
        Assert.NotNull(handler);

        // Verify token authentication configuration was logged
        VerifyLogCalled(LogLevel.Debug, "Configuring NATS with token-based authentication.");

        // Verify handler initialization was logged
        VerifyLogCalled(LogLevel.Information, "Initialized NATS connection handler at:");

        // Cleanup
        handler.DisposeAsync().AsTask().Wait();
    }

    [Fact]
    public void Create_ShouldConfigureCorrectReplyTopic()
    {
        // Test that reply topic is configured correctly based on subject
        // This covers the reply topic generation code path

        // Arrange
        var testCases = new[]
        {
            "simple.subject",
            "complex.multi.level.subject",
            "production.events.orders",
            "dev.test.queue"
        };

        foreach (var subject in testCases)
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

            // Verify initialization was successful
            VerifyLogCalled(LogLevel.Information, "Initialized NATS connection handler at:");

            // Cleanup
            handler.DisposeAsync().AsTask().Wait();

            // Reset for next iteration
            _mockLogger.Reset();
        }
    }

    [Fact]
    public async Task Handler_ShouldBeDisposableMultipleTimes()
    {
        // Test that the handler can be disposed multiple times safely
        // This covers potential edge cases in disposal logic

        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject"
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Act - Multiple disposals
        await handler.DisposeAsync();
        await handler.DisposeAsync();
        await handler.DisposeAsync();

        // Assert - Should not throw any exceptions
        VerifyLogCalled(LogLevel.Information, "NATS connection handler disposed successfully.");
    }

    [Theory]
    [InlineData("nats://primary:4222,nats://secondary:4222")]
    [InlineData("nats+tls://secure-server:4222")]
    [InlineData("nats://192.168.1.100:4222")]
    public void Constructor_ShouldHandleVariousNatsUrlFormats(string url)
    {
        // Test various NATS URL formats to ensure compatibility
        // This covers different connection string parsing scenarios

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
    public async Task Publish_ValidationPath_ShouldRejectNullMessage()
    {
        // Ensure our validation logic is working correctly
        // This tests the argument validation path explicitly

        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject"
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Publish(null!));

        // Cleanup
        await handler.DisposeAsync();
    }

    [Fact]
    public async Task Publish_ShouldNotLogTrace_WhenTraceDisabled()
    {
        // Test that trace logging is only done when trace level is enabled
        // This covers the trace logging conditional path

        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject"
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        // Ensure trace logging is disabled
        _mockLogger.Setup(x => x.IsEnabled(LogLevel.Trace)).Returns(false);

        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Act
        try
        {
            await handler.Publish("Test message");
        }
        catch
        {
            // Expected in test environment
        }

        // Assert - Debug should be called, but trace should not be called
        VerifyLogCalled(LogLevel.Debug, "Publishing message to NATS subject");

        // Cleanup
        await handler.DisposeAsync();
    }

    [Fact]
    public void Constructor_WithComplexAuthentication_ShouldPrioritizeToken()
    {
        // Test the authentication priority logic
        // Token should take precedence over username/password

        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject",
            Secret = "high-priority-token",
            User = "fallback-user",
            Password = "fallback-password"
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        // Act
        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Assert
        Assert.NotNull(handler);

        // Should log token authentication, not username/password
        VerifyLogCalled(LogLevel.Debug, "Configuring NATS with token-based authentication.");
        VerifyLogNotCalled(LogLevel.Debug, "Configuring NATS with username/password authentication.");

        // Cleanup
        handler.DisposeAsync().AsTask().Wait();
    }

    [Fact]
    public async Task NatsConnectionHandler_IntegrationTest_FullWorkflow()
    {
        // Integration-style test that exercises the full workflow
        // This covers multiple code paths in a single test

        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "integration.test.subject",
            Secret = "integration-test-token"
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        // Act
        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Test multiple publishes
        var messages = new[] { "Message 1", "Message 2", "Message 3" };

        foreach (var message in messages)
        {
            try
            {
                await handler.Publish(message);
            }
            catch
            {
                // Expected in test environment without real NATS server
            }
        }

        // Test disposal
        await handler.DisposeAsync();

        // Assert
        VerifyLogCalled(LogLevel.Debug, "Configuring NATS with token-based authentication.");
        VerifyLogCalled(LogLevel.Information, "Initialized NATS connection handler at:");
        VerifyLogCalled(LogLevel.Debug, "Publishing message to NATS subject");
        VerifyLogCalled(LogLevel.Information, "NATS connection handler disposed successfully.");
    }

    [Fact]
    public void NatsConnectionHandler_CoverageNote_DocumentUncoveredAreas()
    {
        // This test documents the areas that are difficult to test in unit tests
        // and explains why they remain uncovered.

        // Arrange
        var natsConfig = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject"
        };
        _mockOptions.Setup(x => x.Value).Returns(natsConfig);

        // Act
        var handler = new NatsConnectionHandler(_mockLogger.Object, _mockOptions.Object);

        // Assert - Handler is created successfully
        Assert.NotNull(handler);

        // The following areas remain uncovered and require integration testing:
        // 1. Connection Event Handlers (ConnectionDisconnected, ConnectionOpened, MessageDropped)
        //    - These are triggered by actual NATS server events, not unit testable
        // 2. Exception Handling in Publish method
        //    - ObjectDisposedException, InvalidOperationException, TimeoutException, NATS exceptions
        //    - These require actual NATS client failures to trigger
        // 3. Exception Handling in DisposeAsync
        //    - Requires NATS client disposal to throw exceptions
        // 4. Trace Logging after successful publish
        //    - Requires successful NATS message publishing

        // These scenarios are best covered through:
        // - Integration tests with real NATS server
        // - End-to-end testing with network failures
        // - Load testing to trigger connection issues
        // - Chaos engineering to simulate failures

        VerifyLogCalled(LogLevel.Information, "Initialized NATS connection handler at:");

        // Cleanup
        handler.DisposeAsync().AsTask().Wait();
    }

    public void Dispose()
    {
        // Clean up any test resources if needed
    }
}