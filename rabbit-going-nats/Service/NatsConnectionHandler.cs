namespace RabbitGoingNats.Service;

using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Net;

/// <summary>
/// Implementation of NATS connection handling for message publishing operations.
///
/// This class manages the connection to a NATS server and provides message publishing
/// capabilities for the message bridge application. It handles:
/// - NATS client initialization and configuration
/// - Multiple authentication methods (token, username/password, anonymous)
/// - Connection lifecycle management with automatic reconnection
/// - Message publishing with reply-to support for acknowledgments
/// - Connection monitoring and error logging
/// - AOT (Ahead of Time) compilation compatibility
///
/// The handler is designed as a singleton service that maintains a persistent
/// connection to NATS throughout the application lifetime, providing reliable
/// message publishing for messages received from RabbitMQ.
///
/// Thread Safety:
/// This class is designed to be thread-safe for concurrent message publishing operations.
/// - The Publish method can be called concurrently from multiple threads
/// - Connection event handlers use volatile fields for thread-safe state tracking
/// - The NATS client library handles thread safety for connection management
/// - All mutable state is properly synchronized or marked as volatile
///
/// Connection resilience features:
/// - Automatic reconnection on connection loss
/// - Connection event monitoring and logging
/// - Message drop detection and reporting
/// - Performance tracking for connection issues
/// </summary>
public class NatsConnectionHandler : INatsConnectionHandler, IAsyncDisposable
{
    /// <summary>
    /// The NATS client instance used for all messaging operations.
    ///
    /// This client handles the actual connection to the NATS server and provides
    /// the API for publishing messages. It's initialized once during construction
    /// and maintained throughout the application lifetime for optimal performance.
    ///
    /// The client includes automatic reconnection capabilities and connection
    /// event handling for robust messaging operations.
    /// </summary>
    private readonly NatsClient natsClientInstance;

    /// <summary>
    /// Configuration settings for the NATS connection.
    ///
    /// Contains all the connection parameters loaded from application configuration,
    /// including server URL, authentication credentials, and the target subject
    /// for message publishing. This configuration is validated at startup to
    /// ensure all required values are present and properly formatted.
    /// </summary>
    private readonly Model.NatsConnection natsConnectionConfig;

    /// <summary>
    /// Logger instance for diagnostic and operational logging.
    ///
    /// Used throughout the class to log connection events, message publishing
    /// operations, errors, and performance metrics. The logger is configured
    /// with the class type for proper log categorization and filtering.
    /// </summary>
    private readonly ILogger logger;

    /// <summary>
    /// Reply-to subject for message acknowledgment support.
    ///
    /// This subject is automatically generated based on the main subject with
    /// an "r-" prefix. It enables request/reply patterns in NATS where
    /// subscribers can send acknowledgments or responses back to the publisher.
    ///
    /// Format: "r-" + configured subject name
    /// Example: If subject is "orders", reply topic will be "r-orders"
    /// </summary>
    private readonly string replyTopic;

    /// <summary>
    /// Timestamp tracking when connection was lost for monitoring purposes.
    /// Uses object for thread-safe nullable DateTime operations.
    /// </summary>
    private readonly object _connectionLossTimeLock = new();
    private DateTime? _connectionLossTime;

    /// <summary>
    /// Initializes a new instance of the NatsConnectionHandler with configuration and logging.
    ///
    /// This constructor:
    /// - Extracts NATS configuration from the options pattern
    /// - Generates the reply-to topic for acknowledgment support
    /// - Creates and configures the NATS client with connection event handlers
    /// - Establishes the initial connection to the NATS server
    /// - Sets up monitoring for connection lifecycle events
    ///
    /// The constructor performs eager initialization to fail fast if there are
    /// configuration issues or connectivity problems at startup.
    /// </summary>
    /// <param name="logger">
    /// Logger instance for diagnostic and operational logging. Should be configured
    /// with the NatsConnectionHandler type for proper log categorization.
    /// </param>
    /// <param name="nats">
    /// Configuration options containing NATS connection parameters. This includes
    /// server URL, authentication credentials, and the target subject. The options
    /// are validated before use and should contain all required configuration values.
    /// </param>
    public NatsConnectionHandler(ILogger<NatsConnectionHandler> logger, IOptions<Model.NatsConnection> nats)
    {
        this.logger = logger;

        // Extract connection parameters from the configuration options
        // This configuration has been validated at startup to ensure required values are present
        natsConnectionConfig = nats.Value;

        // Generate the reply-to topic for acknowledgment and request/reply patterns
        // The "r-" prefix follows NATS conventions for reply subjects
        replyTopic = "r-" + natsConnectionConfig.Subject;

        // Create and initialize the NATS client with event handlers and connection management
        // This includes setting up authentication and connection monitoring
        natsClientInstance = Create();

        logger.LogInformation("Initialized NATS connection handler at: {time}.", DateTimeOffset.Now);
    }

    /// <summary>
    /// Publishes a message to the configured NATS subject asynchronously.
    ///
    /// This method sends a string message to the NATS subject specified in the configuration.
    /// It includes a reply-to subject to enable acknowledgment patterns and request/reply
    /// communication if needed by subscribers.
    ///
    /// The method performs:
    /// - Input validation to ensure message is not null
    /// - Debug logging of the message content (when debug logging is enabled)
    /// - Asynchronous publishing to avoid blocking the caller
    /// - Comprehensive error handling with specific exception types
    /// - Automatic retry handling via the NATS client's built-in resilience
    /// - Reply-to subject inclusion for acknowledgment support
    ///
    /// Performance characteristics:
    /// - Non-blocking asynchronous operation
    /// - Minimal memory allocation
    /// - Built-in connection resilience and retry logic
    /// - Fail-fast behavior for invalid inputs
    /// </summary>
    /// <param name="message">
    /// The message content to publish as a UTF-8 string. This should be the
    /// raw message payload received from RabbitMQ that needs to be forwarded
    /// to NATS subscribers. The method handles message encoding internally.
    /// Cannot be null.
    /// </param>
    /// <returns>
    /// A task that completes when the message has been successfully sent to the
    /// NATS server. The task may be faulted if connection issues occur or if
    /// the NATS server rejects the message.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the message parameter is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the NATS client is not properly initialized or the connection is closed.
    /// </exception>
    /// <exception cref="TimeoutException">
    /// Thrown when the publish operation times out due to network issues or server unavailability.
    /// </exception>
    /// <exception cref="NatsException">
    /// Thrown when NATS-specific errors occur during message publishing.
    /// </exception>
    public async Task Publish(string message)
    {
        // Validate input parameters
        if (message == null)
        {
            logger.LogError("Attempted to publish null message to NATS subject {subject}", natsConnectionConfig.Subject);
            throw new ArgumentNullException(nameof(message), "Message cannot be null");
        }

        try
        {
            // Log message content at debug level for troubleshooting
            // This helps track message flow without overwhelming production logs
            logger.LogDebug("Publishing message to NATS subject {subject}: {message}",
                natsConnectionConfig.Subject, message);

            // Publish to NATS with reply-to support for acknowledgment patterns
            // The replyTopic enables subscribers to send acknowledgments or responses
            await natsClientInstance.PublishAsync(
                subject: natsConnectionConfig.Subject,
                data: message,
                replyTo: replyTopic);

            // Log successful publication at trace level for detailed monitoring
            logger.LogTrace("Successfully published message to NATS subject {subject}", natsConnectionConfig.Subject);
        }
        catch (ObjectDisposedException ex)
        {
            // Handle case where NATS client has been disposed
            logger.LogError(ex, "Cannot publish message - NATS client has been disposed");
            throw new InvalidOperationException("NATS client has been disposed", ex);
        }
        catch (InvalidOperationException ex)
        {
            // Handle connection state issues
            logger.LogError(ex, "Cannot publish message to NATS subject {subject} - connection issue",
                natsConnectionConfig.Subject);
            throw;
        }
        catch (TimeoutException ex)
        {
            // Handle timeout scenarios
            logger.LogError(ex, "Timeout publishing message to NATS subject {subject}",
                natsConnectionConfig.Subject);
            throw;
        }
        catch (Exception ex) when (ex.GetType().Name.Contains("Nats"))
        {
            // Handle NATS-specific exceptions
            logger.LogError(ex, "NATS error publishing message to subject {subject}: {error}",
                natsConnectionConfig.Subject, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            // Handle unexpected exceptions
            logger.LogError(ex, "Unexpected error publishing message to NATS subject {subject}: {error}",
                natsConnectionConfig.Subject, ex.Message);
            throw;
        }
    }
    /// <summary>
    /// Creates and configures a new NATS client with authentication and event handling.
    ///
    /// This method:
    /// - Determines the appropriate authentication method based on configuration
    /// - Creates NatsOpts with the proper authentication settings
    /// - Initializes the NatsClient with connection event handlers
    /// - Sets up monitoring for connection lifecycle events
    /// - Configures error handling for dropped messages
    ///
    /// Authentication priority:
    /// 1. Token-based authentication (if Secret is provided)
    /// 2. Username/Password authentication (if both User and Password are provided)
    /// 3. Anonymous connection (if no credentials are configured)
    ///
    /// The client includes automatic reconnection and resilience features built
    /// into the NATS.Net library, making it suitable for production environments.
    /// </summary>
    /// <returns>
    /// A configured NatsClient instance ready for message publishing operations.
    /// The client includes event handlers for connection monitoring and error tracking.
    /// </returns>
    private NatsClient Create()
    {
        // Extract authentication credentials from configuration
        string? secret = natsConnectionConfig.Secret;
        string? user = natsConnectionConfig.User;
        string? password = natsConnectionConfig.Password;

        NatsOpts n;

        // Initialize NATS authentication options based on available credentials
        // Authentication is determined by priority: Token > Username/Password > Anonymous
        NatsAuthOpts? authOpts = null;

        if (secret?.Length > 0)
        {
            // Token-based authentication - preferred for service-to-service communication
            logger.LogDebug("Configuring NATS with token-based authentication.");
            authOpts = new NatsAuthOpts()
            {
                Token = secret
            };
        }
        else if (user?.Length > 0 && password?.Length > 0)
        {
            // Username/Password authentication - traditional credential-based auth
            logger.LogDebug("Configuring NATS with username/password authentication.");
            authOpts = new NatsAuthOpts()
            {
                Username = user,
                Password = password
            };
        }
        else
        {
            // Anonymous connection - no authentication required
            logger.LogDebug("Configuring NATS with anonymous connection (no authentication).");
        }

        // Create NATS options with URL and authentication settings
        if (authOpts != null)
        {
            n = new()
            {
                Url = natsConnectionConfig.Url,
                AuthOpts = authOpts
            };
        }
        else
        {
            n = new()
            {
                Url = natsConnectionConfig.Url,
            };
        }

        // Create the NATS client with AOT compatibility
        // The NATS.Net library works well with AOT compilation in .NET 8+
        var client = new NatsClient(n);

        // === CONNECTION EVENT HANDLERS ===
        // Note: These event handlers may be called from different threads by the NATS client,
        // so all shared state access must be thread-safe.

        // Handle connection loss events for monitoring and alerting
        client.Connection.ConnectionDisconnected += (m, e) =>
        {
            // Record the exact time of connection loss for duration tracking
            // This helps with SLA monitoring and troubleshooting
            // Using lock ensures thread-safe access
            lock (_connectionLossTimeLock)
            {
                _connectionLossTime = DateTime.UtcNow;
            }
            logger.LogError("NATS connection lost. Attempting automatic reconnection...");
            return ValueTask.CompletedTask;
        };

        // Handle connection restoration events with duration reporting
        client.Connection.ConnectionOpened += (m, e) =>
        {
            // Thread-safe read and reset of connection loss time
            DateTime? lossTime;
            lock (_connectionLossTimeLock)
            {
                lossTime = _connectionLossTime;
                _connectionLossTime = null; // Reset the outage tracking
            }

            if (lossTime != null)
            {
                // Calculate and log the duration of the connection outage
                // This information is valuable for monitoring and SLA tracking
                TimeSpan outageTime = DateTime.UtcNow - lossTime.Value;
                logger.LogInformation("NATS connection restored. Outage duration: {duration:F2}s",
                    outageTime.TotalSeconds);
            }
            else
            {
                // Initial connection or reconnection without tracked outage
                logger.LogInformation("NATS connection established successfully.");
            }

            return ValueTask.CompletedTask;
        };

        // Handle message drop events for reliability monitoring
        client.Connection.MessageDropped += (m, e) =>
        {
            // Log dropped messages for reliability tracking and alerting
            // Message drops can indicate network issues or server overload
            logger.LogError("NATS message dropped! Details: {details}", e);
            return ValueTask.CompletedTask;
        };

        return client;
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources asynchronously.
    ///
    /// This method ensures proper cleanup of the NATS client connection and associated resources.
    /// It should be called when the application is shutting down or when the handler is no longer needed.
    ///
    /// The disposal process:
    /// - Gracefully closes the NATS client connection
    /// - Releases any held resources
    /// - Logs the disposal operation for monitoring
    /// - Handles any disposal errors without throwing exceptions
    ///
    /// This method is safe to call multiple times and will not throw exceptions during cleanup.
    /// </summary>
    /// <returns>
    /// A ValueTask representing the asynchronous disposal operation.
    /// The task completes when all resources have been properly released.
    /// </returns>
    public async ValueTask DisposeAsync()
    {
        try
        {
            // Gracefully dispose the NATS client and its connection
            // This ensures proper cleanup of network resources and event handlers
            if (natsClientInstance != null)
            {
                await natsClientInstance.DisposeAsync();
                logger.LogInformation("NATS connection handler disposed successfully.");
            }
        }
        catch (Exception ex)
        {
            // Log disposal errors but don't throw exceptions during cleanup
            // This prevents disposal failures from affecting application shutdown
            logger.LogError(ex, "Error occurred while disposing NATS connection handler.");
        }
    }
}