namespace RabbitGoingNats.Model;

/// <summary>
/// RabbitMQ connection details
/// </summary>
public class RabbitMqConnection
{
    /// <summary>
    /// Hostname/IP of the RabbitMQ.
    /// </summary>
    public required string HostName { get; set; }

    /// <summary>
    /// Port, defaults to 5672 in the default client.
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Username when RabbitMQ has user/password authorization enabled.
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Password when RabbitMQ has user/password authorization enabled.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// VirtualHost, defaults to "/";
    /// </summary>
    public string? VirtualHost { get; set; }

    /// <summary>
    /// Queue name. Required.
    /// </summary>
    public required string QueueName { get; set; }
}