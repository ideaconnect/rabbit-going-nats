namespace RabbitGoingNats.Service;

/// <summary>
/// Interface for NATS connection handling operations.
/// </summary>
public interface INatsConnectionHandler
{
    /// <summary>
    /// Publishes a message to the configured NATS subject.
    /// </summary>
    /// <param name="message">The message to publish</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task Publish(string message);
}