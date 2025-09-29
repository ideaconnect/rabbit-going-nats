namespace RabbitGoingNats.Service;

/// <summary>
/// Interface for NATS connection handling operations.
///
/// This interface defines the contract for NATS messaging operations within the application.
/// It abstracts the NATS client functionality to enable:
/// - Dependency injection and loose coupling
/// - Easy unit testing with mock implementations
/// - Future extensibility for additional NATS operations
/// - Clean separation between NATS-specific logic and business logic
/// - Proper resource management through IAsyncDisposable pattern
///
/// Implementations should handle:
/// - Connection management and reconnection logic
/// - Message serialization and publishing
/// - Error handling and logging
/// - Graceful shutdown and resource cleanup
/// </summary>
public interface INatsConnectionHandler : IAsyncDisposable
{
    /// <summary>
    /// Publishes a message to the configured NATS subject asynchronously.
    ///
    /// This method sends a string message to the NATS subject specified in the application
    /// configuration. The implementation should handle:
    /// - Connection validation before publishing
    /// - Automatic reconnection if the connection is lost
    /// - Proper error handling and logging
    /// - Message acknowledgment if configured
    ///
    /// The method is designed to be called from message consumers (like RabbitMQ handlers)
    /// to forward messages to NATS subscribers.
    /// </summary>
    /// <param name="message">
    /// The message content to publish to NATS. This should be a UTF-8 encoded string
    /// that represents the message payload. The implementation may apply additional
    /// encoding or serialization as needed.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous publish operation. The task completes when:
    /// - The message has been successfully sent to the NATS server
    /// - An error occurs during publishing (task will be faulted)
    /// - The operation is cancelled
    ///
    /// Callers should await this task to ensure proper error handling and flow control.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the NATS connection is not properly initialized or configured.
    /// </exception>
    /// <exception cref="TimeoutException">
    /// Thrown when the publish operation times out due to network issues or server unavailability.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is cancelled via a cancellation token.
    /// </exception>
    Task Publish(string message);
}