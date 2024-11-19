namespace RabbitGoingNats;

using RabbitGoingNats.Service;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Main worker class, connects to RabbitMQ's queue and passes thru to NATS PubSub.
/// </summary>
/// <param name="logger">Logger instance</param>
/// <param name="rabbitMqConnectionHandler">RabbitMQ's connection handler.</param>
/// <todo>
/// Move sending to nats from rabbit's connection handler.
/// </todo>
public class Worker(ILogger<Worker> logger, RabbitMqConnectionHandler rabbitMqConnectionHandler) : IHostedService
{
    private readonly ManualResetEvent resetEvent = new(false);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(() => {
            rabbitMqConnectionHandler.Consume();
            logger.LogInformation("Worker started running at: {time}.", DateTimeOffset.Now);
            resetEvent.WaitOne(); //one day actually add cancellation feature
            logger.LogError("Worker exited the consumption task.");
        }, CancellationToken.None); //TODO cancellation token inside would require a loop

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        //Triggers graceful end of the consumption thread.
        resetEvent.Set();
        //TODO we should wait here for it to actually end.

        return Task.CompletedTask;
    }
}
