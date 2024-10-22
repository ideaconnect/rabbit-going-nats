namespace RabbitGoingNats.Model;

/// <summary>
/// NATS connection details: DSN, Subject and Secret. Leave secret empty to
/// sign in without password.
/// </summary>
public class NatsConnection
{
    public required string Url { get; set; }

    public required string Subject { get; set; }

    public required string Secret { get; set; }
}