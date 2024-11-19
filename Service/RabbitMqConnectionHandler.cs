namespace RabbitGoingNats.Service;

using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NLog.LayoutRenderers;
using RabbitGoingNats.Model;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public class RabbitMqConnectionHandler(ILogger<Worker> logger, IOptions<RabbitMqConnection> rabbitMq, NatsConnectionService natsConnectionService) : IAsyncDisposable
{
    /// <summary>
    /// Connection loss of RabbitMQ (if occurred) time, for logging.
    /// </summary>
    private DateTime? connectionLossTime;

    /// <summary>
    /// Tracks current progress in sending of messages in order to ping every 1000 of them that we are still alive.
    /// </summary>
    private int progressTracker = 0;

    private IConnection? connection = null;

    private IModel BuildChannel()
    {
        var rabbitMqConnectionConfig = rabbitMq.Value;
        // Initialize RabbitMQ
        connection = new ConnectionFactory
        {
            AutomaticRecoveryEnabled = true,
            RequestedHeartbeat = TimeSpan.FromSeconds(30),
            HostName = rabbitMqConnectionConfig.HostName,
            Port = rabbitMqConnectionConfig.Port,
            UserName = rabbitMqConnectionConfig.UserName,
            Password = rabbitMqConnectionConfig.Password,
            VirtualHost = rabbitMqConnectionConfig.VirtualHost.Length > 0 ? rabbitMqConnectionConfig.VirtualHost : "/",
            NetworkRecoveryInterval = TimeSpan.FromMilliseconds(25)
        }.CreateConnection();

        return connection.CreateModel();
    }

    private string GetQueueName()
    {
        return rabbitMq.Value.QueueName;
    }
    private EventingBasicConsumer BuildConsumer(IModel channel)
    {
        var consumer = new EventingBasicConsumer(channel);
        // In case of connection loss...
        consumer.Shutdown += (model, ea) =>
        {
            connectionLossTime = DateTime.UtcNow;
            logger.LogError("Lost connection with RabbitMQ.");
        };

        // When connection is regained...
        consumer.Registered += (model, ea) =>
        {
            if (connectionLossTime != null)
            {
                TimeSpan? diff = DateTime.UtcNow - connectionLossTime;
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
            connectionLossTime = DateTime.UtcNow;
            logger.LogCritical("Consumer has been cancelled. Intervention may be required!");
        };

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

    public void Consume()
    {
        var channel = BuildChannel();
        logger.LogDebug("Channel built.");
        var consumer = BuildConsumer(channel);
        logger.LogDebug("Consumer built.");

        logger.LogDebug("Starting messages consumption.");

        channel.BasicConsume(GetQueueName(), false, consumer);
    }

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