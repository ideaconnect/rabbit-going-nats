namespace RabbitGoingNats.Model;

/// <summary>
/// RabbitMQ connection details
/// </summary>
public class RabbitMqConnection
{
    public required string HostName { get; set; }
    public required int Port { get; set; }
    public required string UserName { get; set; }
    public required string Password { get; set; }
    public required string VirtualHost { get; set; }
    public required string QueueName { get; set; }
}