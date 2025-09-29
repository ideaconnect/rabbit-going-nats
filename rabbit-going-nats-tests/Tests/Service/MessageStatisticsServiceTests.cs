using FluentAssertions;
using RabbitGoingNats.Service;
using Xunit;

namespace Tests.Service;

/// <summary>
/// Unit tests for the MessageStatisticsService class.
/// Tests the message tracking, statistics calculation, and thread safety.
/// </summary>
public class MessageStatisticsServiceTests
{
    /// <summary>
    /// Test that statistics are initially empty for a new service instance.
    /// </summary>
    [Fact]
    public void GetStatistics_WhenNew_ShouldReturnEmptyStatistics()
    {
        // Arrange
        var service = new MessageStatisticsService();

        // Act
        var stats = service.GetStatistics();

        // Assert
        stats.LastMessageReceived.Should().BeNull();
        stats.LastMessageSent.Should().BeNull();
        stats.MessagesPerMinute.Should().Be(0);
        stats.MessagesInLastHour.Should().Be(0);
    }

    /// <summary>
    /// Test that recording a received message updates the last received timestamp.
    /// </summary>
    [Fact]
    public void RecordMessageReceived_ShouldUpdateLastMessageReceived()
    {
        // Arrange
        var service = new MessageStatisticsService();
        var beforeCall = DateTime.UtcNow;

        // Act
        service.RecordMessageReceived();
        var afterCall = DateTime.UtcNow;

        // Assert
        var stats = service.GetStatistics();
        stats.LastMessageReceived.Should().NotBeNull();
        stats.LastMessageReceived.Should().BeOnOrAfter(beforeCall);
        stats.LastMessageReceived.Should().BeOnOrBefore(afterCall);
        stats.MessagesInLastHour.Should().Be(1);
        stats.MessagesPerMinute.Should().Be(1);
    }

    /// <summary>
    /// Test that recording a sent message updates the last sent timestamp.
    /// </summary>
    [Fact]
    public void RecordMessageSent_ShouldUpdateLastMessageSent()
    {
        // Arrange
        var service = new MessageStatisticsService();
        var beforeCall = DateTime.UtcNow;

        // Act
        service.RecordMessageSent();
        var afterCall = DateTime.UtcNow;

        // Assert
        var stats = service.GetStatistics();
        stats.LastMessageSent.Should().NotBeNull();
        stats.LastMessageSent.Should().BeOnOrAfter(beforeCall);
        stats.LastMessageSent.Should().BeOnOrBefore(afterCall);
        // Sent messages don't count toward received statistics
        stats.MessagesInLastHour.Should().Be(0);
        stats.MessagesPerMinute.Should().Be(0);
    }

    /// <summary>
    /// Test that multiple received messages are counted correctly.
    /// </summary>
    [Fact]
    public void RecordMessageReceived_MultipleMessages_ShouldCountCorrectly()
    {
        // Arrange
        var service = new MessageStatisticsService();

        // Act
        service.RecordMessageReceived();
        service.RecordMessageReceived();
        service.RecordMessageReceived();

        // Assert
        var stats = service.GetStatistics();
        stats.MessagesInLastHour.Should().Be(3);
        stats.MessagesPerMinute.Should().Be(3);
    }

    /// <summary>
    /// Test that the service correctly handles multiple message types.
    /// </summary>
    [Fact]
    public void RecordMessages_MixedTypes_ShouldTrackSeparately()
    {
        // Arrange
        var service = new MessageStatisticsService();

        // Act
        service.RecordMessageReceived();
        service.RecordMessageSent();
        service.RecordMessageReceived();

        // Assert
        var stats = service.GetStatistics();
        stats.LastMessageReceived.Should().NotBeNull();
        stats.LastMessageSent.Should().NotBeNull();
        stats.MessagesInLastHour.Should().Be(2); // Only received messages count
        stats.MessagesPerMinute.Should().Be(2);
    }

    /// <summary>
    /// Test that the statistics correctly update with subsequent calls.
    /// </summary>
    [Fact]
    public void GetStatistics_CalledMultipleTimes_ShouldProvideConsistentResults()
    {
        // Arrange
        var service = new MessageStatisticsService();
        service.RecordMessageReceived();
        service.RecordMessageSent();

        // Act
        var stats1 = service.GetStatistics();
        var stats2 = service.GetStatistics();

        // Assert
        stats1.LastMessageReceived.Should().Be(stats2.LastMessageReceived);
        stats1.LastMessageSent.Should().Be(stats2.LastMessageSent);
        stats1.MessagesInLastHour.Should().Be(stats2.MessagesInLastHour);
        stats1.MessagesPerMinute.Should().Be(stats2.MessagesPerMinute);
    }

    /// <summary>
    /// Test thread safety by calling methods from multiple threads concurrently.
    /// </summary>
    [Fact]
    public async Task MessageStatisticsService_ConcurrentAccess_ShouldBeThreadSafe()
    {
        // Arrange
        var service = new MessageStatisticsService();
        const int numberOfTasks = 10;
        const int messagesPerTask = 100;

        // Act - Start multiple tasks that record messages concurrently
        var tasks = new List<Task>();
        for (int i = 0; i < numberOfTasks; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < messagesPerTask; j++)
                {
                    service.RecordMessageReceived();
                    service.RecordMessageSent();
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        var stats = service.GetStatistics();
        stats.LastMessageReceived.Should().NotBeNull();
        stats.LastMessageSent.Should().NotBeNull();
        stats.MessagesInLastHour.Should().Be(numberOfTasks * messagesPerTask);
        stats.MessagesPerMinute.Should().Be(numberOfTasks * messagesPerTask);
    }
}