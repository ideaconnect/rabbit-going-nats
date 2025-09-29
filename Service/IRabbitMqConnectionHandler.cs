namespace RabbitGoingNats.Service;

/// <summary>
/// Interface for RabbitMQ connection handling operations.
/// </summary>
public interface IRabbitMqConnectionHandler : IAsyncDisposable
{
    /// <summary>
    /// Starts consuming messages from the configured RabbitMQ queue.
    /// </summary>
    void Consume();
}