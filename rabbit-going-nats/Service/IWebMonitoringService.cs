using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitGoingNats.Model;
using System.Net;
using System.Text;
using System.Text.Json;

namespace RabbitGoingNats.Service;

/// <summary>
/// Interface for the HTTP monitoring web service.
/// Provides a RESTful endpoint for retrieving message statistics.
/// </summary>
public interface IWebMonitoringService
{
    /// <summary>
    /// Starts the HTTP server asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown</param>
    /// <returns>Task representing the async operation</returns>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops the HTTP server asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for shutdown timeout</param>
    /// <returns>Task representing the async operation</returns>
    Task StopAsync(CancellationToken cancellationToken);
}

/// <summary>
/// HTTP monitoring web service that provides message statistics via REST API.
/// Uses HttpListener for lightweight HTTP server functionality without heavy dependencies.
/// Implements IHostedService for integration with .NET hosting infrastructure.
/// </summary>
public class WebMonitoringService : IWebMonitoringService, IHostedService, IDisposable
{
    private readonly ILogger<WebMonitoringService> _logger;
    private readonly WebServiceConfiguration _config;
    private readonly IMessageStatisticsService _statisticsService;
    private HttpListener? _httpListener;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _listenerTask;

    /// <summary>
    /// Initializes a new instance of the WebMonitoringService.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    /// <param name="config">Web service configuration options</param>
    /// <param name="statisticsService">Service providing message statistics</param>
    public WebMonitoringService(
        ILogger<WebMonitoringService> logger,
        IOptions<WebServiceConfiguration> config,
        IMessageStatisticsService statisticsService)
    {
        _logger = logger;
        _config = config.Value;
        _statisticsService = statisticsService;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_config.Enabled)
        {
            _logger.LogInformation("Web monitoring service is disabled in configuration");
            return Task.CompletedTask;
        }

        try
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _httpListener = new HttpListener();
            
            var url = $"http://{_config.Host}:{_config.Port}/";
            _httpListener.Prefixes.Add(url);
            _httpListener.Start();

            _logger.LogInformation("Web monitoring service started on {Url}", url);

            // Start the background task to handle requests
            _listenerTask = HandleRequestsAsync(_cancellationTokenSource.Token);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start web monitoring service on {Host}:{Port}", 
                _config.Host, _config.Port);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_config.Enabled || _httpListener == null)
        {
            return;
        }

        try
        {
            _logger.LogInformation("Stopping web monitoring service...");

            // Signal cancellation
            _cancellationTokenSource?.Cancel();

            // Stop the HTTP listener
            _httpListener.Stop();

            // Wait for the listener task to complete
            if (_listenerTask != null)
            {
                await _listenerTask.ConfigureAwait(false);
            }

            _logger.LogInformation("Web monitoring service stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping web monitoring service");
        }
    }

    /// <summary>
    /// Background task that handles incoming HTTP requests.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown</param>
    private async Task HandleRequestsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _httpListener != null && _httpListener.IsListening)
        {
            try
            {
                // Get the next incoming request
                var context = await _httpListener.GetContextAsync().ConfigureAwait(false);
                
                // Handle the request in a fire-and-forget manner to not block other requests
                _ = Task.Run(async () => await ProcessRequestAsync(context).ConfigureAwait(false), 
                    cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                // Expected when shutting down
                break;
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 995) // ERROR_OPERATION_ABORTED
            {
                // Expected when shutting down
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in HTTP listener loop");
                // Continue processing other requests
            }
        }
    }

    /// <summary>
    /// Processes an individual HTTP request.
    /// </summary>
    /// <param name="context">HTTP context containing request and response objects</param>
    private async Task ProcessRequestAsync(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            var response = context.Response;

            _logger.LogDebug("Processing HTTP request: {Method} {Url}", request.HttpMethod, request.Url);

            // Set CORS headers for web browser compatibility
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            // Handle preflight OPTIONS request
            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 200;
                response.Close();
                return;
            }

            // Only support GET requests for statistics
            if (request.HttpMethod != "GET")
            {
                await SendErrorResponse(response, 405, "Method not allowed. Only GET is supported.").ConfigureAwait(false);
                return;
            }

            // Route requests based on path
            switch (request.Url?.AbsolutePath)
            {
                case "/":
                case "/stats":
                case "/statistics":
                    await SendStatisticsResponse(response).ConfigureAwait(false);
                    break;
                
                case "/health":
                    await SendHealthResponse(response).ConfigureAwait(false);
                    break;
                
                default:
                    await SendErrorResponse(response, 404, "Endpoint not found. Available endpoints: /stats, /health").ConfigureAwait(false);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing HTTP request");
            try
            {
                await SendErrorResponse(context.Response, 500, "Internal server error").ConfigureAwait(false);
            }
            catch
            {
                // Ignore errors when sending error response
            }
        }
    }

    /// <summary>
    /// Sends the message statistics as JSON response.
    /// </summary>
    /// <param name="response">HTTP response object</param>
    private async Task SendStatisticsResponse(HttpListenerResponse response)
    {
        var statistics = _statisticsService.GetStatistics();
        var json = JsonSerializer.Serialize(statistics, ApiJsonSerializerContext.Default.MessageStatistics);

        await SendJsonResponse(response, 200, json).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a health check response.
    /// </summary>
    /// <param name="response">HTTP response object</param>
    private async Task SendHealthResponse(HttpListenerResponse response)
    {
        var health = new HealthResponse
        {
            Status = "healthy",
            Timestamp = DateTime.UtcNow,
            Service = "RabbitGoingNats"
        };

        var json = JsonSerializer.Serialize(health, ApiJsonSerializerContext.Default.HealthResponse);

        await SendJsonResponse(response, 200, json).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a JSON response with the specified status code.
    /// </summary>
    /// <param name="response">HTTP response object</param>
    /// <param name="statusCode">HTTP status code</param>
    /// <param name="json">JSON content to send</param>
    private async Task SendJsonResponse(HttpListenerResponse response, int statusCode, string json)
    {
        var buffer = Encoding.UTF8.GetBytes(json);
        
        response.StatusCode = statusCode;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = buffer.Length;

        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
        response.Close();
    }

    /// <summary>
    /// Sends an error response with the specified status code and message.
    /// </summary>
    /// <param name="response">HTTP response object</param>
    /// <param name="statusCode">HTTP status code</param>
    /// <param name="message">Error message</param>
    private async Task SendErrorResponse(HttpListenerResponse response, int statusCode, string message)
    {
        var error = new ErrorResponse { Error = message, StatusCode = statusCode };
        var json = JsonSerializer.Serialize(error, ApiJsonSerializerContext.Default.ErrorResponse);

        await SendJsonResponse(response, statusCode, json).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _httpListener?.Stop();
        _httpListener?.Close();
    }
}