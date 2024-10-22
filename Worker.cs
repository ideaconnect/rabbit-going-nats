namespace RabbitGoingNats;

using Microsoft.Extensions.Options;
using RabbitGoingNats.Model;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using NATS.Net;
using NATS.Client.Core;
using System.Diagnostics;

/// <summary>
/// Main worker class, connects to RabbitMQ's queue and passes thru to NATS PubSub.
/// </summary>
/// <param name="logger"></param>
/// <param name="rabbitMq"></param>
/// <param name="nats"></param>
public class Worker(ILogger<Worker> logger, IOptions<RabbitMqConnection> rabbitMq, IOptions<RabbitGoingNats.Model.NatsConnection> nats) : BackgroundService
{
    /// <summary>
    /// Logger's handle: allows tracking of issues.
    /// </summary>
    private readonly ILogger<Worker> _logger = logger;

    /// <summary>
    /// RabbitMQ's connection details.
    /// </summary>
    private readonly RabbitMqConnection _rabbitMqConnectionConfig = rabbitMq.Value;

    /// <summary>
    /// NATS' connection details.
    /// </summary>
    private readonly RabbitGoingNats.Model.NatsConnection _natsConnectionConfig = nats.Value;

    // RabbitMq instance
    private IConnection? _connection;
    private IModel? _channel;

    // NATS instance
    private NatsClient? _natsClient;

    /// <summary>
    /// Blocks main thread.
    /// </summary>
    /// <todo>
    /// Do proper thread handling one day...
    /// </todo>
    private ManualResetEvent? _resetEvent;

    /// <summary>
    /// Tracks current progress in sending of messages in order to ping every 1000 of them that we are still alive.
    /// </summary>
    private int _progressTracker = 0;

    /// <summary>
    /// Connection loss of RabbitMQ (if occurred) time, for logging.
    /// </summary>
    private DateTime? _connectionLossTime;

    /// <summary>
    /// Connection loss of NATS (if occurred) time, for logging.
    /// </summary>
    private DateTime? _connectionLossTimeNats;

    private Stopwatch _stopWatch = new();

    /// <summary>
    /// Main worker thread.
    /// </summary>
    /// <param name="stoppingToken"></param>
    /// <returns></returns>
    /// <todo>Move the initialization part to service setup method.</todo>
    /// <exception cref="Exception"></exception>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Run(async () =>
        {
            // Initialize RabbitMQ
            _connection = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                RequestedHeartbeat = TimeSpan.FromSeconds(30),
                HostName = _rabbitMqConnectionConfig.HostName,
                Port = _rabbitMqConnectionConfig.Port,
                UserName = _rabbitMqConnectionConfig.UserName,
                Password = _rabbitMqConnectionConfig.Password,
                VirtualHost = _rabbitMqConnectionConfig.VirtualHost.Length > 0 ? _rabbitMqConnectionConfig.VirtualHost : "/",
                NetworkRecoveryInterval = TimeSpan.FromMilliseconds(25)
            }.CreateConnection();

            _channel = _connection.CreateModel();

            string secret = _natsConnectionConfig.Secret;

            NatsOpts n;
            // Initialize NATS, with secret if configured.
            // TODO support more auth options
            if (secret.Length > 0)
            {
                n = new()
                {
                    Url = _natsConnectionConfig.Url,
                    AuthOpts = new NatsAuthOpts()
                    {
                        Token = secret
                    }
                };
            }
            else
            {
                n = new()
                {
                    Url = _natsConnectionConfig.Url,
                };
            }

            // Although NatsClient is not suggested with AOT it seems to work more
            // than fine with NET 8+, no need for a workaround with NatsConnection.
            _natsClient = new NatsClient(n);


            _resetEvent = new ManualResetEvent(false);

            if (_channel == null)
            {
                throw new Exception("Cannot operate without a channel.");
            }

            if (_natsClient == null)
            {
                throw new Exception("Cannot operate without a NATS configured.");
            }

            if (_resetEvent == null)
            {
                throw new Exception("Reset Event badly configured.");
            }

            // TODO move those methods to a proper initialization section

            // When conenction gets dropped
            _natsClient.Connection.ConnectionDisconnected += (m, e) =>
            {
                _connectionLossTimeNats = DateTime.UtcNow;
                _logger.LogError("Lost connection with NATS.");
                return ValueTask.CompletedTask;
            };

            // When conenction gets reopened
            _natsClient.Connection.ConnectionOpened += (m, e) =>
            {
                if (_connectionLossTimeNats != null)
                {
                    TimeSpan? diff = DateTime.UtcNow - _connectionLossTimeNats;
                    _logger.LogError("Regained NATS connection. Issue took {@}s", ((diff?.TotalSeconds) ?? -1));
                    _connectionLossTimeNats = null; //reset timer
                } else {
                    _logger.LogInformation("Successfully connected to NATS.");
                }

                return ValueTask.CompletedTask;
            };

            // Dropped messages
            _natsClient.Connection.MessageDropped += (m, e) =>
            {
                _logger.LogError("Message dropped! Body: {e.message}", e);
                return ValueTask.CompletedTask;
            };

            // Main consumer
            var consumer = new EventingBasicConsumer(_channel);

            // In case of connection loss...
            consumer.Shutdown += (model, ea) =>
            {
                _connectionLossTime = DateTime.UtcNow;
                _logger.LogError("Lost connection with RabbitMQ.");
            };

            // When connection is regained...
            consumer.Registered += (model, ea) =>
            {
                if (_connectionLossTime != null)
                {
                    TimeSpan? diff = DateTime.UtcNow - _connectionLossTime;
                    _logger.LogError("Regained RabbitMQ connection. Issue took {@}s", ((diff?.TotalSeconds) ?? -1));
                    _connectionLossTime = null; //reset timer
                } else {
                    _logger.LogInformation("Successfully connected to RabbitMQ.");
                }
            };

            // If consumer got cancelled by second side
            consumer.ConsumerCancelled += (model, ea) =>
            {
                _connectionLossTime = DateTime.UtcNow;
                _logger.LogCritical("Consumer has been cancelled. Intervention may be required!");
            };

            // main callback: on new message
            consumer.Received += async (model, ea) =>
            {
                _stopWatch.Start();
                // read the message from the queue
                var body = ea.Body.ToArray();
                var message = System.Text.Encoding.UTF8.GetString(body);

                // we need to acknowledge BEFORE sending to NATS as if NATS gets
                // stuck then this would cause closing of the consumer.
                _channel.BasicAck(ea.DeliveryTag, true);

                // send it further to NATS
                await _natsClient.PublishAsync(subject: _natsConnectionConfig.Subject, data: message, replyTo: _natsConnectionConfig.Subject);
                _stopWatch.Stop();

                // cannot measure exactly as at least on ARM64 ticks counting is rubbish and none form of integrated
                // stopwatches or even miliseconds counting is precise enough.
                if (_stopWatch.ElapsedMilliseconds > 500) {
                    _logger.LogError("Hiccup! Passing of the message took longer than 500ms.");
                }

                // Log the whole message if configured.
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("OK: {@message}", message);
                }

                // Do a heartbeat on every 1000 messages.
                if (++_progressTracker > 1000)
                {
                    _logger.LogInformation("Worker still running at: {time}.", DateTimeOffset.Now);
                    _progressTracker = 0;
                }
            };

            // Actually starts the consumer.
            //await _natsClient.ConnectAsync();
            _channel.BasicConsume(_rabbitMqConnectionConfig.QueueName, false, consumer);
            _logger.LogInformation("Worker started running at: {time}", DateTimeOffset.Now);
            _resetEvent.WaitOne(); //one day actually add cancellation feature
        }, stoppingToken);

        _logger.LogError("Worker exited the main task, in this version this should not occur");
    }

    /// <summary>
    /// Gracefully closes the connections and app.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Gracefully stopping RabbitMQ.");
        _connection?.Close();

        _logger.LogInformation("Gracefully stopping NATS.");
        if (_natsClient != null)
        {
            await _natsClient.DisposeAsync();
        }

        _logger.LogInformation("Closed connections.");
    }
}
