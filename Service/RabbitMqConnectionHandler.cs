namespace RabbitGoingNats.Service;

using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using RabbitGoingNats.Model;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

/// <summary>
/// Handles the connection with RabbitMQ and initializes the resending process.
/// </summary>
/// <todo>
/// Make a connection adapter's interface which will actually allow us to use
/// different targets than NATS potentially (although that is not the domain of
/// this project.)
/// </todo>
/// <param name="logger">Supported Logger instance, NLog by default.</param>
/// <param name="rabbitMq">RabbitMQ options (connection detauls).</param>
/// <param name="natsConnectionService">Previously initialized NATS connection
/// servie </param>
public class RabbitMqConnectionHandler(ILogger<RabbitMqConnectionHandler> logger, IOptions<RabbitMqConnection> rabbitMq, NatsConnectionService natsConnectionService) : IAsyncDisposable
{
    /// <summary>
    /// Connection loss of RabbitMQ (if occurred) time, for logging.
    /// </summary>
    private DateTime? connectionLossTime;

    /// <summary>
    /// Tracks current progress in sending of messages in order to ping every
    /// 1000 of them that we are still alive.
    /// </summary>
    private int progressTracker = 0;

    /// <summary>
    /// Handle to the RabbitMQ's connection which allows us to gracefully close
    /// it.
    /// </summary>
    private IConnection? connection = null;

    /// <summary>
    /// Builds the instance of the connection channel.
    /// </summary>
    /// <returns>Channel's model</returns>
    private IModel BuildChannel()
    {
        //gets the connection detauls
        var rabbitMqConnectionConfig = rabbitMq.Value;

        // Initialize RabbitMQ's connection factory with the required parts
        var factory = new ConnectionFactory
        {
            AutomaticRecoveryEnabled = true,
            RequestedHeartbeat = TimeSpan.FromSeconds(30),
            HostName = rabbitMqConnectionConfig.HostName,
            NetworkRecoveryInterval = TimeSpan.FromMilliseconds(25)
        };

        // Optional parts
        if (rabbitMqConnectionConfig.Port != null) {
            factory.Port = (int) rabbitMqConnectionConfig.Port;
        }

        if (rabbitMqConnectionConfig.UserName != null) {
            factory.UserName = rabbitMqConnectionConfig.UserName;
        }

        if (rabbitMqConnectionConfig.Password != null) {
            factory.Password = rabbitMqConnectionConfig.Password;
        }

        if (rabbitMqConnectionConfig.VirtualHost != null) {
            factory.VirtualHost = rabbitMqConnectionConfig.VirtualHost;
        }

        logger.LogInformation("Attempting connection to RabbitMQ");
        connection = factory.CreateConnection();
        logger.LogInformation("Connected to RabbitMQ");
        return connection.CreateModel();
    }

    /// <summary>
    /// Gets the queue name from config. Just a helper wrapper.
    /// </summary>
    /// <returns>Queue name, string</returns>
    private string GetQueueName()
    {
        return rabbitMq.Value.QueueName;
    }

    /// <summary>
    /// Builds the actual consumer of messages from RabbitMQ. Initializes all
    /// the events.
    /// </summary>
    /// <param name="channel">Connection model</param>
    /// <returns>Eventing consumer connected to RabbitMQ.</returns>
    private EventingBasicConsumer BuildConsumer(IModel channel)
    {
        var consumer = new EventingBasicConsumer(channel);

        // In case of connection loss...
        consumer.Shutdown += (model, ea) =>
        {
            //we keep track of the time to later log how long it was disconnected.
            connectionLossTime = DateTime.UtcNow;
            logger.LogError("Lost connection with RabbitMQ.");
        };

        // When connection is regained...
        consumer.Registered += (model, ea) =>
        {
            if (connectionLossTime != null)
            {
                //if there was a connection before and this was a shortage...
                TimeSpan? diff = DateTime.UtcNow - connectionLossTime;
                //...then we log how log it took.
                logger.LogError("Regained RabbitMQ connection. Issue took {@}s", (diff?.TotalSeconds) ?? -1);
                connectionLossTime = null; //reset timer
            }
            else
            {
                logger.LogInformation("Successfully connected to RabbitMQ.");
            }
        };

        // If consumer got cancelled by second side
        consumer.ConsumerCancelled += (model, ea) =>
        {
            /* theoretically we should check first if we are not in a disconnected
            state already, but getting disconnected from the other side while we
            already believe that we are disconnected would be a very rare case
            theoretically impossible. */
            connectionLossTime = DateTime.UtcNow;
            logger.LogCritical("Consumer has been cancelled. Intervention may be required!");
        };

        // main receieiver.
        consumer.Received += async (model, ea) =>
        {
            var start = DateTime.UtcNow;
            // read the message from the queue
            var body = ea.Body.ToArray();
            var message = System.Text.Encoding.UTF8.GetString(body);

            // we need to acknowledge BEFORE sending to NATS as if NATS gets
            // stuck then this would cause closing of the consumer.
            channel.BasicAck(ea.DeliveryTag, true);

            // send it further to NATS
            await natsConnectionService.Publish(message);

            var diff = DateTime.UtcNow - start;

            // cannot measure exactly as at least on ARM64 ticks counting is rubbish and none form of integrated
            // stopwatches or even miliseconds counting is precise enough.
            if (diff.TotalMilliseconds > 500)
            {
                logger.LogError("Hiccup! Passing of the message took longer than 500ms.");
            }

            // Log the whole message if configured.
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("OK: {@message}", message);
            }

            // Do a heartbeat on every 1000 messages.
            if (++progressTracker > 1000)
            {
                logger.LogInformation("Worker still running at: {time}.", DateTimeOffset.Now);
                progressTracker = 0;
            }
        };

        return consumer;
    }

    /// <summary>
    /// Starts the consumption. Builds required instances, connects and listens.
    /// </summary>
    /// <todo>
    /// Separate listening from sending at some point.
    /// </todo>
    public void Consume()
    {
        var channel = BuildChannel();
        logger.LogDebug("Channel built.");
        var consumer = BuildConsumer(channel);
        logger.LogDebug("Consumer built.");

        logger.LogDebug("Starting messages consumption.");
        channel.BasicConsume(GetQueueName(), false, consumer);
    }

    /// <summary>
    /// Before we actually destroy we can explicitly shut down the connection.
    /// </summary>
    /// <returns></returns>
    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        //Close connection to RabbitMQ.
        if (connection != null) {
            connection?.Close();
        }

        return ValueTask.CompletedTask;
    }
}