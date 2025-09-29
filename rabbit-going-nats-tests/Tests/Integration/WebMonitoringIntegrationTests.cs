using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitGoingNats.Model;
using RabbitGoingNats.Service;
using System.Net.Http;
using System.Text.Json;
using Xunit;

namespace Tests.Integration;

/// <summary>
/// Integration tests for the web monitoring functionality.
/// Tests the complete integration between statistics service, web service, and message handlers.
/// </summary>
public class WebMonitoringIntegrationTests : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ServiceProvider _serviceProvider;
    private readonly IMessageStatisticsService _statisticsService;
    private readonly IWebMonitoringService _webService;
    private const int TestPort = 8020; // Use unique port for integration tests

    public WebMonitoringIntegrationTests()
    {
        // Setup a minimal service container for integration tests
        var services = new ServiceCollection();

        // Configure logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        // Configure web service
        var webConfig = new WebServiceConfiguration
        {
            Host = "localhost",
            Port = TestPort,
            Enabled = true
        };
        services.Configure<WebServiceConfiguration>(config =>
        {
            config.Host = webConfig.Host;
            config.Port = webConfig.Port;
            config.Enabled = webConfig.Enabled;
        });

        // Register services
        services.AddSingleton<IMessageStatisticsService, MessageStatisticsService>();
        services.AddSingleton<IWebMonitoringService, WebMonitoringService>();

        _serviceProvider = services.BuildServiceProvider();
        _statisticsService = _serviceProvider.GetRequiredService<IMessageStatisticsService>();
        _webService = _serviceProvider.GetRequiredService<IWebMonitoringService>();
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// Test the complete workflow: record messages and verify they're reflected in the web API.
    /// </summary>
    [Fact]
    public async Task CompleteWorkflow_ShouldReflectStatisticsInWebAPI()
    {
        // Arrange - Start the web service
        await _webService.StartAsync(CancellationToken.None);

        try
        {
            // Act - Record some message activity
            _statisticsService.RecordMessageReceived();
            await Task.Delay(10); // Small delay to ensure different timestamps
            _statisticsService.RecordMessageSent();
            await Task.Delay(10);
            _statisticsService.RecordMessageReceived();

            // Act - Get statistics from web API
            var response = await _httpClient.GetAsync($"http://localhost:{TestPort}/stats");
            var jsonContent = await response.Content.ReadAsStringAsync();

            // Assert
            response.IsSuccessStatusCode.Should().BeTrue();

            var stats = JsonSerializer.Deserialize<MessageStatistics>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            stats.Should().NotBeNull();
            stats!.LastMessageReceived.Should().NotBeNull();
            stats.LastMessageSent.Should().NotBeNull();
            stats.MessagesInLastHour.Should().Be(2); // Two received messages
            stats.MessagesPerMinute.Should().Be(2);

            // Verify timestamps are recent
            stats.LastMessageReceived.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(30));
            stats.LastMessageSent.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(30));
        }
        finally
        {
            await _webService.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Test that multiple endpoints work correctly.
    /// </summary>
    [Fact]
    public async Task MultipleEndpoints_ShouldAllWork()
    {
        // Arrange
        await _webService.StartAsync(CancellationToken.None);

        try
        {
            // Act & Assert - Test stats endpoint
            var statsResponse = await _httpClient.GetAsync($"http://localhost:{TestPort}/stats");
            statsResponse.IsSuccessStatusCode.Should().BeTrue();

            // Act & Assert - Test health endpoint
            var healthResponse = await _httpClient.GetAsync($"http://localhost:{TestPort}/health");
            healthResponse.IsSuccessStatusCode.Should().BeTrue();

            // Act & Assert - Test root endpoint
            var rootResponse = await _httpClient.GetAsync($"http://localhost:{TestPort}/");
            rootResponse.IsSuccessStatusCode.Should().BeTrue();

            // Verify all return JSON
            foreach (var response in new[] { statsResponse, healthResponse, rootResponse })
            {
                response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
            }
        }
        finally
        {
            await _webService.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Test that the service handles high message volume correctly.
    /// </summary>
    [Fact]
    public async Task HighVolumeMessages_ShouldBeTrackedCorrectly()
    {
        // Arrange
        await _webService.StartAsync(CancellationToken.None);
        const int messageCount = 1000;

        try
        {
            // Act - Simulate high volume message processing
            for (int i = 0; i < messageCount; i++)
            {
                _statisticsService.RecordMessageReceived();
                _statisticsService.RecordMessageSent();
            }

            // Get statistics from API
            var response = await _httpClient.GetAsync($"http://localhost:{TestPort}/stats");
            var jsonContent = await response.Content.ReadAsStringAsync();

            // Assert
            response.IsSuccessStatusCode.Should().BeTrue();

            var stats = JsonSerializer.Deserialize<MessageStatistics>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            stats.Should().NotBeNull();
            stats!.MessagesInLastHour.Should().Be(messageCount);
            stats.MessagesPerMinute.Should().Be(messageCount);
        }
        finally
        {
            await _webService.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Test that the web service handles concurrent requests properly.
    /// </summary>
    [Fact]
    public async Task ConcurrentRequests_ShouldBeHandledCorrectly()
    {
        // Arrange
        await _webService.StartAsync(CancellationToken.None);
        _statisticsService.RecordMessageReceived();

        try
        {
            // Act - Make multiple concurrent requests
            const int requestCount = 20;
            var tasks = new List<Task<HttpResponseMessage>>();

            for (int i = 0; i < requestCount; i++)
            {
                tasks.Add(_httpClient.GetAsync($"http://localhost:{TestPort}/stats"));
            }

            var responses = await Task.WhenAll(tasks);

            // Assert - All requests should succeed
            foreach (var response in responses)
            {
                response.IsSuccessStatusCode.Should().BeTrue();
                response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
            }

            // Dispose responses
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
        finally
        {
            await _webService.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Test that the statistics persist across multiple API calls.
    /// </summary>
    [Fact]
    public async Task Statistics_ShouldPersistAcrossAPICalls()
    {
        // Arrange
        await _webService.StartAsync(CancellationToken.None);

        // Record some initial activity
        _statisticsService.RecordMessageReceived();
        _statisticsService.RecordMessageSent();

        try
        {
            // Act - Make multiple API calls
            var response1 = await _httpClient.GetAsync($"http://localhost:{TestPort}/stats");
            var json1 = await response1.Content.ReadAsStringAsync();

            var response2 = await _httpClient.GetAsync($"http://localhost:{TestPort}/stats");
            var json2 = await response2.Content.ReadAsStringAsync();

            // Assert - Statistics should be consistent
            response1.IsSuccessStatusCode.Should().BeTrue();
            response2.IsSuccessStatusCode.Should().BeTrue();

            var stats1 = JsonSerializer.Deserialize<MessageStatistics>(json1, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            var stats2 = JsonSerializer.Deserialize<MessageStatistics>(json2, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            stats1.Should().NotBeNull();
            stats2.Should().NotBeNull();

            // Statistics should be identical
            stats1!.LastMessageReceived.Should().Be(stats2!.LastMessageReceived);
            stats1.LastMessageSent.Should().Be(stats2.LastMessageSent);
            stats1.MessagesInLastHour.Should().Be(stats2.MessagesInLastHour);
            stats1.MessagesPerMinute.Should().Be(stats2.MessagesPerMinute);
        }
        finally
        {
            await _webService.StopAsync(CancellationToken.None);
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _serviceProvider?.Dispose();
    }
}