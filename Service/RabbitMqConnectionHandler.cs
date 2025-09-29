using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitGoingNats.Model;
using System.Text;

namespace RabbitGoingNats.Service;

/// <summary>
/// Handles RabbitMQ connections and message consumption for relaying messages to NATS.
/// This service provides robust connection management, automatic reconnection handling,
/// and efficient message processing with comprehensive monitoring and error handling.
/// </summary>
/// <remarks>
/// This implementation includes:
/// - Thread-safe connection state management
/// - Automatic reconnection with monitoring
/// - Comprehensive error handling and logging
/// - Performance monitoring and heartbeat tracking
/// - Proper resource disposal following IAsyncDisposable pattern
/// - Cancellation token support for graceful shutdown
///
/// The service consumes messages from a configured RabbitMQ queue and forwards them
/// to NATS using the injected INatsConnectionHandler. Messages are acknowledged
/// before forwarding to prevent message loss in case of NATS failures.
/// </remarks>
/// <example>
/// Usage in DI container:
/// <code>
/// services.AddSingleton&lt;IRabbitMqConnectionHandler, RabbitMqConnectionHandler&gt;();
/// </code>
///
/// Starting consumption:
/// <code>
/// var cancellationToken = new CancellationToken();
/// await rabbitMqHandler.ConsumeAsync(cancellationToken);
/// </code>
/// </example>
public class RabbitMqConnectionHandler(
    ILogger<RabbitMqConnectionHandler> logger,
    IOptions<RabbitMqConnection> rabbitMqOptions,
    INatsConnectionHandler natsConnectionHandler) : IRabbitMqConnectionHandler, IAsyncDisposable
{
    /// <summary>
    /// Logger instance for this service.
    /// </summary>
    private readonly ILogger<RabbitMqConnectionHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// RabbitMQ connection configuration options.
    /// </summary>
    private readonly RabbitMqConnection _rabbitMqConfig = rabbitMqOptions?.Value ?? throw new ArgumentNullException(nameof(rabbitMqOptions));

    /// <summary>
    /// NATS connection handler for message forwarding.
    /// </summary>
    private readonly INatsConnectionHandler _natsConnectionHandler = natsConnectionHandler ?? throw new ArgumentNullException(nameof(natsConnectionHandler));

    /// <summary>
    /// RabbitMQ connection instance. Thread-safe access required.
    /// </summary>
    private volatile IConnection? _connection;

    /// <summary>
    /// Timestamp tracking when connection was lost for monitoring purposes.
    /// Uses object for thread-safe nullable DateTime operations.
    /// </summary>
    private readonly object _connectionLossTimeLock = new();
    private DateTime? _connectionLossTime;

    /// <summary>
    /// Progress counter for heartbeat logging every 1000 messages.
    /// </summary>
    private volatile int _progressTracker;

    /// <summary>
    /// Indicates whether the instance has been disposed.
    /// </summary>
    private volatile bool _disposed;

    /// <summary>
    /// Gets the configured queue name for message consumption.
    /// </summary>
    /// <returns>The queue name from configuration.</returns>
    /// <exception cref="InvalidOperationException">Thrown when queue name is not configured.</exception>
    private string GetQueueName()
    {
        if (string.IsNullOrWhiteSpace(_rabbitMqConfig.QueueName))
        {
            throw new InvalidOperationException("RabbitMQ queue name is not configured.");
        }
        return _rabbitMqConfig.QueueName;
    }

    /// <summary>
    /// Builds and configures a RabbitMQ channel with proper error handling.
    /// </summary>
    /// <returns>A configured IModel instance representing the channel.</returns>
    /// <exception cref="InvalidOperationException">Thrown when connection cannot be established.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the handler has been disposed.</exception>
    private IModel BuildChannel()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RabbitMqConnectionHandler));
        }

        try
        {
            // Create connection factory with configuration
            var factory = new ConnectionFactory
            {
                HostName = _rabbitMqConfig.HostName ?? "localhost",
                Port = _rabbitMqConfig.Port ?? 5672,
                UserName = _rabbitMqConfig.UserName ?? "guest",
                Password = _rabbitMqConfig.Password ?? "guest",
                VirtualHost = _rabbitMqConfig.VirtualHost ?? "/",
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                RequestedHeartbeat = TimeSpan.FromSeconds(60),
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };

            _logger.LogDebug("Creating RabbitMQ connection to {HostName}:{Port}", factory.HostName, factory.Port);

            // Create connection and channel
            _connection = factory.CreateConnection();
            var channel = _connection.CreateModel();

            _logger.LogInformation("Successfully created RabbitMQ channel");
            return channel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build RabbitMQ channel");
            throw new InvalidOperationException("Unable to establish RabbitMQ connection", ex);
        }
    }

    /// <summary>
    /// Builds and configures a RabbitMQ consumer with comprehensive event handlers.
    /// </summary>
    /// <param name="channel">The RabbitMQ channel to use for the consumer.</param>
    /// <returns>A configured EventingBasicConsumer instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when channel is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the handler has been disposed.</exception>
    private EventingBasicConsumer BuildConsumer(IModel channel)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RabbitMqConnectionHandler));
        }

        if (channel == null)
        {
            throw new ArgumentNullException(nameof(channel));
        }

        var consumer = new EventingBasicConsumer(channel);

        // Configure connection loss monitoring
        consumer.Shutdown += OnConsumerShutdown;
        consumer.Registered += OnConsumerRegistered;
        consumer.ConsumerCancelled += OnConsumerCancelled;
        consumer.Received += OnMessageReceived;

        _logger.LogDebug("RabbitMQ consumer configured with event handlers");
        return consumer;
    }

    /// <summary>
    /// Handles consumer shutdown events for connection monitoring.
    /// </summary>
    /// <param name="model">The consumer model.</param>
    /// <param name="ea">Event arguments containing shutdown details.</param>
    private void OnConsumerShutdown(object? model, ShutdownEventArgs ea)
    {
        lock (_connectionLossTimeLock)
        {
            _connectionLossTime = DateTime.UtcNow;
        }
        _logger.LogError("Lost connection with RabbitMQ. Reason: {ReplyText}", ea.ReplyText);
    }

    /// <summary>
    /// Handles consumer registration events for connection monitoring.
    /// </summary>
    /// <param name="model">The consumer model.</param>
    /// <param name="ea">Event arguments containing registration details.</param>
    private void OnConsumerRegistered(object? model, ConsumerEventArgs ea)
    {
        DateTime? lossTime;
        lock (_connectionLossTimeLock)
        {
            lossTime = _connectionLossTime;
            _connectionLossTime = null; // Reset after reading
        }

        if (lossTime != null)
        {
            // Connection was regained after a loss
            var connectionDownTime = DateTime.UtcNow - lossTime.Value;
            _logger.LogWarning("Regained RabbitMQ connection. Downtime: {DowntimeSeconds:F2}s",
                connectionDownTime.TotalSeconds);
        }
        else
        {
            _logger.LogInformation("Successfully connected to RabbitMQ");
        }
    }

    /// <summary>
    /// Handles consumer cancellation events.
    /// </summary>
    /// <param name="model">The consumer model.</param>
    /// <param name="ea">Event arguments containing cancellation details.</param>
    private void OnConsumerCancelled(object? model, ConsumerEventArgs ea)
    {
        lock (_connectionLossTimeLock)
        {
            _connectionLossTime = DateTime.UtcNow;
        }
        _logger.LogCritical("Consumer has been cancelled by server. Intervention may be required!");
    }

    /// <summary>
    /// Handles incoming message processing and forwarding to NATS.
    /// </summary>
    /// <param name="model">The consumer model.</param>
    /// <param name="ea">Event arguments containing the received message.</param>
    private async void OnMessageReceived(object? model, BasicDeliverEventArgs ea)
    {
        if (_disposed)
        {
            _logger.LogWarning("Received message after disposal, ignoring");
            return;
        }

        var startTime = DateTime.UtcNow;
        
        // Get the channel from the consumer (AOT-safe approach)
        IModel? channel = null;
        if (model is EventingBasicConsumer consumer)
        {
            channel = consumer.Model;
        }
        
        if (channel == null)
        {
            _logger.LogError("Unable to get channel from consumer model: {ModelType}", model?.GetType().Name ?? "null");
            return;
        }

        try
        {
            // Extract and decode message
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            _logger.LogTrace("Received message with delivery tag {DeliveryTag}, size: {MessageSize} bytes",
                ea.DeliveryTag, body.Length);

            // Acknowledge message BEFORE forwarding to NATS to prevent RabbitMQ queue blockage
            // if NATS becomes unavailable
            channel.BasicAck(ea.DeliveryTag, false);

            // Forward message to NATS
            await _natsConnectionHandler.Publish(message);

            // Performance monitoring
            var processingTime = DateTime.UtcNow - startTime;
            if (processingTime.TotalMilliseconds > 500)
            {
                _logger.LogWarning("Message processing took longer than expected: {ProcessingTimeMs:F2}ms",
                    processingTime.TotalMilliseconds);
            }

            // Debug logging for message content
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Successfully processed message: {Message}", message);
            }

            // Heartbeat logging every 1000 messages
            if (Interlocked.Increment(ref _progressTracker) % 1000 == 0)
            {
                _logger.LogInformation("Worker heartbeat: {MessageCount} messages processed at {Timestamp}",
                    _progressTracker, DateTimeOffset.Now);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message with delivery tag {DeliveryTag}", ea.DeliveryTag);

            try
            {
                // Negative acknowledgment to requeue the message
                channel.BasicNack(ea.DeliveryTag, false, true);
                _logger.LogDebug("Message requeued due to processing error");
            }
            catch (Exception nackEx)
            {
                _logger.LogError(nackEx, "Failed to NACK message with delivery tag {DeliveryTag}", ea.DeliveryTag);
            }
        }
    }

    /// <summary>
    /// Starts consuming messages from the configured RabbitMQ queue.
    /// This method will run continuously until cancellation is requested.
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when consumption is cancelled.</returns>
    /// <exception cref="InvalidOperationException">Thrown when RabbitMQ connection cannot be established.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the handler has been disposed.</exception>
    /// <remarks>
    /// This method will:
    /// 1. Build a RabbitMQ channel and consumer
    /// 2. Start consuming messages from the configured queue
    /// 3. Process messages asynchronously using event handlers
    /// 4. Handle cancellation gracefully with proper cleanup
    ///
    /// The method blocks until cancellation is requested via the cancellation token.
    /// All message processing occurs asynchronously in event handlers.
    /// </remarks>
    public async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RabbitMqConnectionHandler));
        }

        IModel? channel = null;
        string? consumerTag = null;

        try
        {
            _logger.LogInformation("Starting RabbitMQ message consumption");

            // Build channel and consumer
            channel = BuildChannel();
            _logger.LogDebug("RabbitMQ channel built successfully");

            var consumer = BuildConsumer(channel);
            _logger.LogDebug("RabbitMQ consumer built successfully");

            // Start consuming messages
            var queueName = GetQueueName();
            consumerTag = channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);

            _logger.LogInformation("Started consuming messages from queue '{QueueName}' with consumer tag '{ConsumerTag}'",
                queueName, consumerTag);

            // Wait for cancellation request
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Message consumption cancellation requested");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during message consumption");
            throw;
        }
        finally
        {
            // Cleanup consumer and channel
            await CleanupConsumer(channel, consumerTag);
        }
    }

    /// <summary>
    /// Cleans up consumer and channel resources during shutdown.
    /// </summary>
    /// <param name="channel">The channel to clean up.</param>
    /// <param name="consumerTag">The consumer tag to cancel.</param>
    private async Task CleanupConsumer(IModel? channel, string? consumerTag)
    {
        if (channel != null && !string.IsNullOrEmpty(consumerTag))
        {
            try
            {
                _logger.LogDebug("Cancelling consumer with tag '{ConsumerTag}'", consumerTag);
                channel.BasicCancel(consumerTag);

                // Give some time for graceful shutdown
                await Task.Delay(100);

                _logger.LogDebug("Consumer cancelled successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cancelling consumer with tag '{ConsumerTag}'", consumerTag);
            }
        }

        if (channel != null)
        {
            try
            {
                if (channel.IsOpen)
                {
                    channel.Close();
                    _logger.LogDebug("RabbitMQ channel closed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing RabbitMQ channel");
            }
        }
    }

    /// <summary>
    /// Disposes of RabbitMQ connection resources asynchronously.
    /// </summary>
    /// <returns>A ValueTask representing the disposal operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogDebug("Disposing RabbitMQ connection handler");

        try
        {
            if (_connection?.IsOpen == true)
            {
                _connection.Close();
                _logger.LogDebug("RabbitMQ connection closed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error closing RabbitMQ connection during disposal");
        }
        finally
        {
            _connection?.Dispose();
            _disposed = true;
        }

        // Small delay to allow for graceful shutdown
        await Task.Delay(50);

        GC.SuppressFinalize(this);
        _logger.LogDebug("RabbitMQ connection handler disposed");
    }
}