using System.Text.Json.Serialization;
using RabbitGoingNats.Service;

namespace RabbitGoingNats.Model;

/// <summary>
/// JSON serialization context for AOT-compatible JSON serialization.
/// This context pre-generates serialization code at compile time, eliminating
/// the need for runtime reflection and making the application AOT-compatible.
/// </summary>
[JsonSerializable(typeof(MessageStatistics))]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    GenerationMode = JsonSourceGenerationMode.Default)]
public partial class ApiJsonSerializerContext : JsonSerializerContext
{
}

/// <summary>
/// Health check response model for the web API.
/// </summary>
public class HealthResponse
{
    /// <summary>
    /// Status of the service (e.g., "healthy").
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the health check was performed.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Name of the service being monitored.
    /// </summary>
    public string Service { get; set; } = string.Empty;
}

/// <summary>
/// Error response model for the web API.
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Error message describing what went wrong.
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// HTTP status code associated with the error.
    /// </summary>
    public int StatusCode { get; set; }
}