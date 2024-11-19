namespace RabbitGoingNats.Service;

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Net;
using RabbitGoingNats.Model;

public class NatsConnectionService
{
    private readonly NatsClient natsClientInstance;

    private readonly Model.NatsConnection natsConnectionConfig;

    private readonly ILogger logger;

    private readonly string replyTopic;

    /// <summary>
    /// Connection loss of NATS (if occurred) time, for logging.
    /// </summary>
    private DateTime? connectionLossTime;
    public NatsConnectionService(ILogger<Worker> logger, IOptions<Model.NatsConnection> nats)
    {
        this.logger = logger;
        natsConnectionConfig = nats.Value;
        replyTopic = "r-" + natsConnectionConfig.Subject;
        natsClientInstance = Create();
    }

    public async ValueTask Publish(string message)
    {
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
            connectionLossTime = DateTime.UtcNow;
            logger.LogError("Lost connection with NATS.");
            return ValueTask.CompletedTask;
        };

        // When conenction gets reopened
        client.Connection.ConnectionOpened += (m, e) =>
        {
            if (connectionLossTime != null)
            {
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