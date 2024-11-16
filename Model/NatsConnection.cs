namespace RabbitGoingNats.Model;

/// <summary>
/// NATS connection details: DSN, Subject and Secret. Leave secret empty to
/// sign in without password.
/// </summary>
public class NatsConnection
{
    public required string Url { get; set; }

    public required string Subject { get; set; }

    public string? Secret { get; set; } = null;

    public string? User { get; set; } = null;
    public string? Password { get; set; } = null;
}