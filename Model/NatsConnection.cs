namespace RabbitGoingNats.Model;

/// <summary>
/// NATS connection details: DSN, Subject and Secret. Leave secret empty to
/// sign in without password.
/// </summary>
public class NatsConnection
{
    /// <summary>
    /// URL to the server including IP/Hostname and port. Including scheme.
    /// </summary>
    /// <example>
    /// nats://localhost:4222
    /// </example>
    public required string Url { get; set; }

    /// <summary>
    /// Subject to which send the retrieved messages.
    /// </summary>
    /// <example>
    /// test-subject
    /// </example>
    public required string Subject { get; set; }

    /// <summary>
    /// NATS' Secret, used for authentication. NOTE: if used together with
    /// username then username will be used.
    /// </summary>
    public string? Secret { get; set; } = null;

    /// <summary>
    /// Username in case user+password authentication is used by the server.
    /// </summary>
    public string? User { get; set; } = null;

    /// <summary>
    /// Password, needed when NATS uses user+password authentication.
    /// </summary>
    public string? Password { get; set; } = null;
}