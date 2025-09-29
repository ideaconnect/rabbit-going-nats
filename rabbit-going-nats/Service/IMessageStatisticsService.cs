using System.Collections.Concurrent;

namespace RabbitGoingNats.Service;

/// <summary>
/// Interface for tracking message statistics throughout the application lifecycle.
/// Provides thread-safe statistics tracking for both received and sent messages.
/// </summary>
public interface IMessageStatisticsService
{
    /// <summary>
    /// Records when a message was received from RabbitMQ.
    /// This method is thread-safe and can be called from multiple consumer threads.
    /// </summary>
    void RecordMessageReceived();

    /// <summary>
    /// Records when a message was sent to NATS.
    /// This method is thread-safe and can be called from multiple publisher threads.
    /// </summary>
    void RecordMessageSent();

    /// <summary>
    /// Gets comprehensive message statistics.
    /// </summary>
    /// <returns>Current message statistics snapshot</returns>
    MessageStatistics GetStatistics();
}

/// <summary>
/// Data transfer object containing current message statistics.
/// Used for JSON serialization in the web API responses.
/// </summary>
public class MessageStatistics
{
    /// <summary>
    /// UTC timestamp of the last message received from RabbitMQ.
    /// Null if no messages have been received yet.
    /// </summary>
    public DateTime? LastMessageReceived { get; set; }

    /// <summary>
    /// UTC timestamp of the last message sent to NATS.
    /// Null if no messages have been sent yet.
    /// </summary>
    public DateTime? LastMessageSent { get; set; }

    /// <summary>
    /// Number of messages processed per minute (rolling average).
    /// Calculated based on messages received in the last minute.
    /// </summary>
    public double MessagesPerMinute { get; set; }

    /// <summary>
    /// Total number of messages processed within the last hour.
    /// Includes both received and successfully forwarded messages.
    /// </summary>
    public int MessagesInLastHour { get; set; }
}

/// <summary>
/// Thread-safe implementation of message statistics tracking.
/// Uses concurrent collections and atomic operations for performance in high-throughput scenarios.
/// </summary>
public class MessageStatisticsService : IMessageStatisticsService
{
    private readonly object _lockObject = new();
    private DateTime? _lastMessageReceived;
    private DateTime? _lastMessageSent;

    // Use a concurrent queue to store message timestamps for efficient time-based calculations
    private readonly ConcurrentQueue<DateTime> _messageTimestamps = new();

    /// <inheritdoc />
    public void RecordMessageReceived()
    {
        var now = DateTime.UtcNow;
        
        lock (_lockObject)
        {
            _lastMessageReceived = now;
        }
        
        _messageTimestamps.Enqueue(now);
        CleanupOldTimestamps();
    }

    /// <inheritdoc />
    public void RecordMessageSent()
    {
        var now = DateTime.UtcNow;
        
        lock (_lockObject)
        {
            _lastMessageSent = now;
        }
    }

    /// <inheritdoc />
    public MessageStatistics GetStatistics()
    {
        CleanupOldTimestamps();
        
        var now = DateTime.UtcNow;
        var oneHourAgo = now.AddHours(-1);
        var oneMinuteAgo = now.AddMinutes(-1);

        // Convert to array for safe iteration (snapshot)
        var timestamps = _messageTimestamps.ToArray();
        
        var messagesInLastHour = timestamps.Count(t => t >= oneHourAgo);
        var messagesInLastMinute = timestamps.Count(t => t >= oneMinuteAgo);

        lock (_lockObject)
        {
            return new MessageStatistics
            {
                LastMessageReceived = _lastMessageReceived,
                LastMessageSent = _lastMessageSent,
                MessagesPerMinute = messagesInLastMinute, // Simple count for now, could be rolling average
                MessagesInLastHour = messagesInLastHour
            };
        }
    }

    /// <summary>
    /// Removes timestamps older than one hour to prevent memory leaks.
    /// Called automatically during statistics operations.
    /// </summary>
    private void CleanupOldTimestamps()
    {
        var oneHourAgo = DateTime.UtcNow.AddHours(-1);
        
        // Remove old timestamps to prevent memory leak
        while (_messageTimestamps.TryPeek(out var oldestTimestamp) && oldestTimestamp < oneHourAgo)
        {
            _messageTimestamps.TryDequeue(out _);
        }
    }
}