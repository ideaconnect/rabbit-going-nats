namespace RabbitGoingNats;

using RabbitGoingNats.Service;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Main worker class, connects to RabbitMQ's queue and passes thru to NATS PubSub.
/// Implements IHostedService for integration with .NET hosting infrastructure and
/// IDisposable for proper resource cleanup.
/// </summary>
/// <param name="logger">Logger instance for diagnostic and operational logging</param>
/// <param name="rabbitMqConnectionHandler">RabbitMQ's connection handler for message consumption</param>
/// <todo>
/// Move sending to nats from rabbit's connection handler.
/// </todo>
public class Worker(ILogger<Worker> logger, IRabbitMqConnectionHandler rabbitMqConnectionHandler) : IHostedService, IDisposable
{
    /// <summary>
    /// Internal cancellation token source to control the worker's execution lifecycle.
    /// This allows us to signal cancellation to the background task independently
    /// of the hosting environment's cancellation token.
    /// </summary>
    private readonly CancellationTokenSource _stoppingCts = new();

    /// <summary>
    /// Reference to the background task that handles message consumption.
    /// This allows us to track task state and wait for completion during shutdown.
    /// </summary>
    private Task? _executingTask;

    /// <summary>
    /// Starts the worker service. Called by the hosting environment when the application starts.
    /// This method should return quickly and not block the hosting environment.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token from the hosting environment</param>
    /// <returns>A task representing the start operation</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Worker starting at: {time}.", DateTimeOffset.Now);

        // Start the background execution task using our internal cancellation token
        // This ensures we have full control over the worker's lifecycle
        _executingTask = ExecuteAsync(_stoppingCts.Token);

        // Check if the task completed synchronously with an error
        // This handles cases where the task fails immediately (e.g., configuration issues)
        if (_executingTask.IsCompleted)
        {
            // Return the task to propagate any immediate exceptions
            // This ensures the hosting environment is aware of startup failures
            return _executingTask;
        }

        // Return completed task to indicate successful startup
        // The actual work continues in the background via _executingTask
        return Task.CompletedTask;
    }

    /// <summary>
    /// The main execution loop for the worker. This method contains the core business logic
    /// and runs until cancellation is requested. It handles the RabbitMQ message consumption
    /// and ensures proper error handling and logging.
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
    /// <returns>A task representing the execution operation</returns>
    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Worker started consuming messages.");

            // Start consuming messages from RabbitMQ using the cancellation-aware async method
            // This will run until cancellation is requested or an error occurs
            await rabbitMqConnectionHandler.ConsumeAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // This is expected when cancellation is requested - not an error condition
            logger.LogInformation("Worker consumption cancelled.");
        }
        catch (Exception ex)
        {
            // Log any unexpected errors that occur during message consumption
            logger.LogError(ex, "Worker encountered an error during consumption.");

            // Re-throw the exception to ensure it's properly handled by the hosting environment
            // This allows the host to decide whether to restart the service or shut down
            throw;
        }
    }

    /// <summary>
    /// Stops the worker service gracefully. Called by the hosting environment during application shutdown.
    /// This method ensures proper cleanup of resources and waits for ongoing operations to complete.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token from the hosting environment (with timeout)</param>
    /// <returns>A task representing the stop operation</returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Worker stop requested.");

        // Signal cancellation to the executing task
        // This will cause the ConsumeAsync method to exit gracefully
        _stoppingCts.Cancel();

        // Wait for the executing task to complete with timeout handling
        // This ensures we don't leave the background task running after shutdown
        if (_executingTask != null)
        {
            try
            {
                // Wait for either the task to complete or the cancellation token to be triggered
                // The 30-second timeout prevents indefinite waiting during shutdown
                var completedTask = await Task.WhenAny(_executingTask, Task.Delay(TimeSpan.FromSeconds(30), cancellationToken));

                if (completedTask == _executingTask)
                {
                    // Task completed within timeout - await it to get any exceptions
                    // This ensures any exceptions from the background task are logged
                    await _executingTask;
                    logger.LogInformation("Worker task completed successfully.");
                }
                else
                {
                    // Task didn't complete within timeout - log warning but continue shutdown
                    logger.LogWarning("Worker task did not complete within timeout period.");
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested during shutdown
                logger.LogInformation("Worker stop operation was cancelled.");
            }
            catch (Exception ex)
            {
                // Log any unexpected errors during shutdown but don't re-throw
                // We want to continue with cleanup even if the background task failed
                logger.LogError(ex, "Error occurred while stopping worker.");
            }
        }

        // Ensure connection handler cleanup
        // This is critical for releasing RabbitMQ connections and other resources
        try
        {
            if (rabbitMqConnectionHandler is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
                logger.LogDebug("RabbitMQ connection handler disposed.");
            }
        }
        catch (Exception ex)
        {
            // Log disposal errors but don't re-throw - we're already shutting down
            logger.LogError(ex, "Error disposing RabbitMQ connection handler.");
        }

        logger.LogInformation("Worker stopped at: {time}.", DateTimeOffset.Now);
    }

    /// <summary>
    /// Disposes of managed resources used by the worker.
    /// This method is called automatically by the .NET runtime when the object is garbage collected
    /// or explicitly when the service is disposed by the hosting environment.
    /// </summary>
    public void Dispose()
    {
        // Dispose the cancellation token source to free its resources
        // This is important for preventing memory leaks in long-running applications
        _stoppingCts?.Dispose();
    }
}
