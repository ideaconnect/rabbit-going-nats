namespace RabbitGoingNats.Service;

using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Net;

public class NatsConnectionService
{
    /// <summary>
    /// Instance of the connector.
    /// </summary>
    private readonly NatsClient natsClientInstance;

    /// <summary>
    /// Connection details.
    /// </summary>
    private readonly Model.NatsConnection natsConnectionConfig;

    /// <summary>
    /// Logger's instance handler.
    /// </summary>
    private readonly ILogger logger;

    /// <summary>
    /// Reply-To topic which allows ACK.
    /// </summary>
    private readonly string replyTopic;

    /// <summary>
    /// Connection loss of NATS (if occurred) time, for logging.
    /// </summary>
    private DateTime? connectionLossTime;

    /// <summary>
    /// Creates the connection service for NATS.
    /// </summary>
    /// <param name="logger">Logger's instance</param>
    /// <param name="nats">Options which define connection params.</param>
    public NatsConnectionService(ILogger<NatsConnectionService> logger, IOptions<Model.NatsConnection> nats)
    {
        this.logger = logger;

        //Connection parameters.
        natsConnectionConfig = nats.Value;

        //Default reply-to topic: allows ACK.
        replyTopic = "r-" + natsConnectionConfig.Subject;

        //creates a new instance of the client connector.
        natsClientInstance = Create();
        logger.LogInformation("Initialized NATS connection service at: {time}.", DateTimeOffset.Now);
    }

    /// <summary>
    /// Sends the actual message.
    /// </summary>
    /// <param name="message">String, text, payload, message.</param>
    /// <returns></returns>
    public async Task Publish(string message)
    {
        logger.LogDebug("Message: {message}", message);
        await natsClientInstance.PublishAsync(subject: natsConnectionConfig.Subject, data: message, replyTo: replyTopic);
    }
    private NatsClient Create()
    {
        string? secret = natsConnectionConfig.Secret;
        string? user = natsConnectionConfig.User;
        string? password = natsConnectionConfig.Password;

        NatsOpts n;

        // Initialize NATS, with secret if configured.
        // TODO support more auth options
        NatsAuthOpts? authOpts = null;

        if (secret?.Length > 0)
        {
            logger.LogDebug("Preparing NATS with Secret auth.");
            authOpts = new NatsAuthOpts()
            {
                Token = secret
            };
        }
        else if (user?.Length > 0 && password?.Length > 0)
        {
            logger.LogDebug("Preparing NATS with User/Password auth.");
            authOpts = new NatsAuthOpts()
            {
                Username = user,
                Password = password
            };
        }

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

        // Although NatsClient is not suggested with AOT it seems to work more
        // than fine with NET 8+, no need for a workaround with NatsConnection.
        var client = new NatsClient(n);

        // When conenction gets dropped
        client.Connection.ConnectionDisconnected += (m, e) =>
        {
            // Track the connection loss time. Allows us to log how long it took.
            connectionLossTime = DateTime.UtcNow;
            logger.LogError("Lost connection with NATS.");
            return ValueTask.CompletedTask;
        };

        // When conenction gets reopened
        client.Connection.ConnectionOpened += (m, e) =>
        {
            if (connectionLossTime != null)
            {
                // If there was already a shortage of the connection: report how long.
                TimeSpan? diff = DateTime.UtcNow - connectionLossTime;
                logger.LogError("Regained NATS connection. Issue took {@}s", (diff?.TotalSeconds) ?? -1);
                connectionLossTime = null; //reset timer
            }
            else
            {
                logger.LogInformation("Successfully connected to NATS.");
            }

            return ValueTask.CompletedTask;
        };

        // Dropped messages
        client.Connection.MessageDropped += (m, e) =>
        {
            logger.LogError("Message dropped! Body: {e.message}", e);
            return ValueTask.CompletedTask;
        };

        return client;
    }
}