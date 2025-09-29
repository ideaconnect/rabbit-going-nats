using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RabbitGoingNats.Model;
using RabbitGoingNats.Service;
using System.Net.Http;
using System.Text.Json;
using Xunit;

namespace Tests.Service;

/// <summary>
/// Unit tests for the WebMonitoringService class.
/// Tests HTTP endpoint functionality, JSON serialization, and service lifecycle.
/// </summary>
public class WebMonitoringServiceTests : IDisposable
{
    private readonly Mock<ILogger<WebMonitoringService>> _mockLogger;
    private readonly Mock<IMessageStatisticsService> _mockStatisticsService;
    private readonly WebMonitoringService _webService;
    private readonly HttpClient _httpClient;
    private readonly WebServiceConfiguration _config;
    private readonly IOptions<WebServiceConfiguration> _options;

    public WebMonitoringServiceTests()
    {
        _mockLogger = new Mock<ILogger<WebMonitoringService>>();
        _mockStatisticsService = new Mock<IMessageStatisticsService>();
        
        // Use a different port for tests to avoid conflicts
        _config = new WebServiceConfiguration
        {
            Host = "localhost",
            Port = 8019, // Different port for tests
            Enabled = true
        };
        
        _options = Options.Create(_config);
        _webService = new WebMonitoringService(_mockLogger.Object, _options, _mockStatisticsService.Object);
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// Test that the service can start and stop without errors when enabled.
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenEnabled_ShouldStartSuccessfully()
    {
        // Act & Assert - Should not throw
        await _webService.StartAsync(CancellationToken.None);
        await _webService.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// Test that the service does nothing when disabled in configuration.
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenDisabled_ShouldNotStartListener()
    {
        // Arrange
        var disabledConfig = new WebServiceConfiguration { Enabled = false };
        var disabledOptions = Options.Create(disabledConfig);
        var disabledService = new WebMonitoringService(_mockLogger.Object, disabledOptions, _mockStatisticsService.Object);

        // Act & Assert - Should not throw and should not start HTTP listener
        await disabledService.StartAsync(CancellationToken.None);
        await disabledService.StopAsync(CancellationToken.None);

        // Verify that the logger was called indicating the service is disabled
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("disabled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Test the statistics endpoint returns proper JSON structure.
    /// </summary>
    [Fact]
    public async Task StatisticsEndpoint_ShouldReturnValidJson()
    {
        // Arrange
        var mockStats = new MessageStatistics
        {
            LastMessageReceived = DateTime.UtcNow.AddMinutes(-1),
            LastMessageSent = DateTime.UtcNow.AddSeconds(-30),
            MessagesPerMinute = 5.0,
            MessagesInLastHour = 150
        };

        _mockStatisticsService.Setup(x => x.GetStatistics()).Returns(mockStats);

        await _webService.StartAsync(CancellationToken.None);

        try
        {
            // Act
            var response = await _httpClient.GetAsync($"http://{_config.Host}:{_config.Port}/stats");
            var jsonContent = await response.Content.ReadAsStringAsync();

            // Assert
            response.IsSuccessStatusCode.Should().BeTrue();
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

            // Verify JSON structure (case-insensitive for camelCase)
            var stats = JsonSerializer.Deserialize<MessageStatistics>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            stats.Should().NotBeNull();
            stats!.MessagesPerMinute.Should().Be(5.0);
            stats.MessagesInLastHour.Should().Be(150);
        }
        finally
        {
            await _webService.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Test the health endpoint returns proper JSON structure.
    /// </summary>
    [Fact]
    public async Task HealthEndpoint_ShouldReturnHealthyStatus()
    {
        // Arrange
        await _webService.StartAsync(CancellationToken.None);

        try
        {
            // Act
            var response = await _httpClient.GetAsync($"http://{_config.Host}:{_config.Port}/health");
            var jsonContent = await response.Content.ReadAsStringAsync();

            // Assert
            response.IsSuccessStatusCode.Should().BeTrue();
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

            // Verify JSON structure
            var health = JsonSerializer.Deserialize<HealthResponse>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            health.Should().NotBeNull();
            health!.Status.Should().Be("healthy");
            health.Service.Should().Be("RabbitGoingNats");
            health.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        }
        finally
        {
            await _webService.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Test that invalid endpoints return 404 Not Found.
    /// </summary>
    [Fact]
    public async Task InvalidEndpoint_ShouldReturn404()
    {
        // Arrange
        await _webService.StartAsync(CancellationToken.None);

        try
        {
            // Act
            var response = await _httpClient.GetAsync($"http://{_config.Host}:{_config.Port}/invalid");

            // Assert
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
        }
        finally
        {
            await _webService.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Test that POST requests return 405 Method Not Allowed.
    /// </summary>
    [Fact]
    public async Task PostRequest_ShouldReturn405()
    {
        // Arrange
        await _webService.StartAsync(CancellationToken.None);

        try
        {
            // Act
            var response = await _httpClient.PostAsync($"http://{_config.Host}:{_config.Port}/stats", 
                new StringContent("test"));

            // Assert
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.MethodNotAllowed);
        }
        finally
        {
            await _webService.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Test that the root endpoint also returns statistics.
    /// </summary>
    [Fact]
    public async Task RootEndpoint_ShouldReturnStatistics()
    {
        // Arrange
        var mockStats = new MessageStatistics
        {
            MessagesPerMinute = 10.0,
            MessagesInLastHour = 600
        };

        _mockStatisticsService.Setup(x => x.GetStatistics()).Returns(mockStats);
        await _webService.StartAsync(CancellationToken.None);

        try
        {
            // Act
            var response = await _httpClient.GetAsync($"http://{_config.Host}:{_config.Port}/");

            // Assert
            response.IsSuccessStatusCode.Should().BeTrue();
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        }
        finally
        {
            await _webService.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Test CORS headers are present in responses.
    /// </summary>
    [Fact]
    public async Task AllEndpoints_ShouldIncludeCorsHeaders()
    {
        // Arrange
        _mockStatisticsService.Setup(x => x.GetStatistics()).Returns(new MessageStatistics());
        await _webService.StartAsync(CancellationToken.None);

        try
        {
            // Act
            var response = await _httpClient.GetAsync($"http://{_config.Host}:{_config.Port}/stats");

            // Assert
            response.Headers.Should().ContainKey("Access-Control-Allow-Origin");
            response.Headers.GetValues("Access-Control-Allow-Origin").Should().Contain("*");
        }
        finally
        {
            await _webService.StopAsync(CancellationToken.None);
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _webService?.Dispose();
    }
}