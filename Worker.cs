namespace RabbitGoingNats;

using Microsoft.Extensions.Options;
using RabbitGoingNats.Model;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using NATS.Net;
using NATS.Client.Core;
using RabbitGoingNats.Service;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Main worker class, connects to RabbitMQ's queue and passes thru to NATS PubSub.
/// </summary>
/// <param name="logger"></param>
/// <param name="rabbitMq"></param>
/// <param name="nats"></param>
public class Worker(ILogger<Worker> logger, RabbitMqConnectionHandler rabbitMqConnectionHandler) : IHostedService
{
    // /// <summary>
    // /// Main worker thread.
    // /// </summary>
    // /// <param name="stoppingToken"></param>
    // /// <returns></returns>
    // /// <todo>Move the initialization part to service setup method.</todo>
    // /// <exception cref="Exception"></exception>
    // protected override Task ExecuteAsync(CancellationToken stoppingToken)
    // {

    // }

    // /// <summary>
    // /// Gracefully closes the connections and app.
    // /// </summary>
    // /// <param name="cancellationToken"></param>
    // /// <returns></returns>
    // public override async Task StopAsync(CancellationToken cancellationToken)
    // {

    //     logger.LogInformation("Gracefully stopping RabbitMQ.");
    //     //connection?.Close();

    //     logger.LogInformation("Gracefully stopping NATS.");
    //     // if (natsClient != null)
    //     // {
    //     //     await _natsClient.DisposeAsync();
    //     // }

    //     logger.LogInformation("Closed connections.");
    // }

    private ManualResetEvent resetEvent = new(false);


    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(() => {
            rabbitMqConnectionHandler.Consume();
            logger.LogInformation("Worker started running at: {time}.", DateTimeOffset.Now);
            resetEvent.WaitOne(); //one day actually add cancellation feature
            logger.LogError("Worker exited the consumption task.");
        }, CancellationToken.None);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        resetEvent.Set();

        return Task.CompletedTask;
    }
}
