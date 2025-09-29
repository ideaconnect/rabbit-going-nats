namespace RabbitGoingNats.Service;

/// <summary>
/// Interface for RabbitMQ connection handling operations.
///
/// This interface defines the contract for RabbitMQ messaging operations within the application.
/// It abstracts the RabbitMQ client functionality to enable:
/// - Dependency injection and loose coupling from specific messaging implementations
/// - Easy unit testing with mock implementations for isolated testing
/// - Future extensibility for additional RabbitMQ operations (publishing, management, etc.)
/// - Clean separation between RabbitMQ-specific logic and business logic
/// - Graceful resource management through IAsyncDisposable pattern
///
/// Implementations should handle:
/// - Connection establishment and management with automatic recovery
/// - Queue declaration and consumer setup
/// - Message acknowledgment and error handling
/// - Connection loss detection and reconnection logic
/// - Graceful shutdown and resource cleanup
/// - Integration with NATS for message forwarding
/// </summary>
public interface IRabbitMqConnectionHandler : IAsyncDisposable
{
    /// <summary>
    /// Starts consuming messages from the configured RabbitMQ queue asynchronously.
    ///
    /// This method establishes a connection to RabbitMQ, sets up a consumer for the configured
    /// queue, and begins processing incoming messages. The method runs continuously until
    /// cancellation is requested through the provided cancellation token.
    ///
    /// The implementation should:
    /// - Establish connection to RabbitMQ using configured connection parameters
    /// - Create and configure the message consumer with appropriate settings
    /// - Set up event handlers for message processing, connection events, and errors
    /// - Forward received messages to NATS via the INatsConnectionHandler
    /// - Handle message acknowledgment to prevent message loss
    /// - Provide progress logging and performance monitoring
    /// - Respond to cancellation requests for graceful shutdown
    /// - Clean up consumer resources when stopping
    ///
    /// The method uses an event-driven consumption model where messages are processed
    /// asynchronously via event callbacks, allowing for high-throughput message processing.
    /// </summary>
    /// <param name="cancellationToken">
    /// Token to monitor for cancellation requests from the hosting environment.
    /// When cancellation is requested, the method should:
    /// - Stop accepting new messages
    /// - Complete processing of any messages currently being handled
    /// - Cancel the RabbitMQ consumer cleanly
    /// - Release connection resources
    /// - Exit gracefully without throwing exceptions
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous consumption operation. The task completes when:
    /// - Cancellation is requested via the cancellation token (normal shutdown)
    /// - An unrecoverable error occurs (task will be faulted)
    /// - The connection is lost and cannot be recovered (task will be faulted)
    ///
    /// The task should not complete under normal operating conditions until shutdown is requested.
    /// Callers should await this task to ensure proper lifecycle management.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the RabbitMQ connection cannot be established due to configuration issues
    /// or when the specified queue does not exist and cannot be created.
    /// </exception>
    /// <exception cref="TimeoutException">
    /// Thrown when connection attempts timeout due to network issues or server unavailability.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is cancelled via the cancellation token during shutdown.
    /// This is the expected termination condition for normal application shutdown.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when authentication fails due to invalid credentials or insufficient permissions.
    /// </exception>
    Task ConsumeAsync(CancellationToken cancellationToken);
}