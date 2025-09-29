namespace RabbitGoingNats.Model;

/// <summary>
/// Configuration model for the HTTP monitoring web service.
/// Contains settings for the embedded HTTP server that provides message statistics.
/// </summary>
public class WebServiceConfiguration
{
    /// <summary>
    /// The host address to bind the HTTP server to.
    /// Defaults to "localhost" for security reasons.
    /// Use "0.0.0.0" to bind to all network interfaces if needed.
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// The port number for the HTTP server.
    /// Defaults to 8018. Must be between 1 and 65535.
    /// </summary>
    public int Port { get; set; } = 8018;

    /// <summary>
    /// Whether the web service is enabled.
    /// Defaults to true. Set to false to disable the HTTP monitoring endpoint.
    /// </summary>
    public bool Enabled { get; set; } = true;
}