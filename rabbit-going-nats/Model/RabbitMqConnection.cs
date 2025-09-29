namespace RabbitGoingNats.Model;

/// <summary>
/// Configuration model for RabbitMQ connection parameters and queue settings.
///
/// This class represents the configuration required to establish a connection to a RabbitMQ server
/// and defines the queue from which messages will be consumed for forwarding to NATS.
///
/// RabbitMQ is a robust message broker that supports:
/// - Advanced Message Queuing Protocol (AMQP 0-9-1)
/// - Reliable message delivery with acknowledgments
/// - Message persistence and durability
/// - Complex routing patterns with exchanges
/// - Clustering and high availability
///
/// Authentication in RabbitMQ:
/// - Anonymous access (no username/password) for development environments
/// - Username/Password authentication for secure environments
/// - Client certificate authentication (not supported by this configuration)
///
/// Virtual Hosts provide logical separation within a single RabbitMQ instance,
/// similar to database schemas in SQL databases.
///
/// The configuration is typically loaded from appsettings.json under the "RabbitMq" section
/// and validated at application startup to ensure connectivity requirements are met.
/// </summary>
public class RabbitMqConnection
{
    /// <summary>
    /// Hostname or IP address of the RabbitMQ server.
    ///
    /// This can be:
    /// - A hostname that resolves via DNS (e.g., "rabbitmq.example.com")
    /// - An IPv4 address (e.g., "192.168.1.100")
    /// - An IPv6 address (e.g., "::1" for localhost)
    /// - "localhost" for local development
    ///
    /// The hostname should be reachable from the application's network context.
    /// For production deployments, consider using load balancer hostnames or
    /// cluster endpoints for high availability.
    /// </summary>
    /// <example>
    /// Development: localhost
    /// Production: rabbitmq.production.example.com
    /// Docker: rabbitmq-container
    /// IP Address: 10.0.1.50
    /// </example>
    public required string HostName { get; set; }

    /// <summary>
    /// TCP port number for the RabbitMQ server connection.
    ///
    /// RabbitMQ standard ports:
    /// - 5672: Default AMQP port for non-TLS connections
    /// - 5671: Default AMQP port for TLS-secured connections
    /// - 15672: Management web interface (HTTP)
    /// - 15671: Management web interface (HTTPS)
    ///
    /// If not specified (null), the RabbitMQ client will use its default port (5672).
    /// Custom ports may be used in environments with port restrictions or
    /// when running multiple RabbitMQ instances on the same server.
    /// </summary>
    /// <example>
    /// Standard: 5672 (or null for default)
    /// TLS: 5671
    /// Custom: 25672
    /// </example>
    public int? Port { get; set; }

    /// <summary>
    /// Username for RabbitMQ authentication when the server requires credentials.
    ///
    /// This is used in conjunction with the Password property for basic authentication.
    /// The username must have appropriate permissions for:
    /// - Connecting to the specified virtual host
    /// - Reading from the specified queue
    /// - Acknowledging messages (for reliable delivery)
    ///
    /// In production environments, use dedicated service accounts with minimal
    /// required permissions rather than administrative accounts.
    ///
    /// Leave this null for RabbitMQ servers configured for anonymous access
    /// (typically only in development environments).
    /// </summary>
    /// <example>
    /// Service account: rabbitmq-bridge-consumer
    /// Development: guest
    /// Application-specific: myapp-consumer
    /// </example>
    public string? UserName { get; set; }

    /// <summary>
    /// Password for RabbitMQ authentication, required when UserName is specified.
    ///
    /// This password is used together with the UserName for basic authentication.
    /// Security considerations:
    /// - Use strong passwords that meet your organization's security policies
    /// - Store passwords securely using user secrets, environment variables, or key vaults
    /// - Rotate passwords regularly according to security best practices
    /// - Avoid hardcoding passwords in configuration files
    ///
    /// The password should grant the minimum permissions necessary for the application
    /// to function (principle of least privilege).
    /// </summary>
    /// <example>
    /// Development: guest
    /// Production: Use secure password from key vault or environment variables
    /// </example>
    public string? Password { get; set; }

    /// <summary>
    /// RabbitMQ virtual host for logical separation of resources.
    ///
    /// Virtual hosts provide namespace isolation within a single RabbitMQ instance,
    /// similar to database schemas. Each virtual host has its own:
    /// - Exchanges, queues, and bindings
    /// - User permissions and access controls
    /// - Message isolation and routing rules
    ///
    /// Common virtual host patterns:
    /// - "/" (default): The default virtual host, used when not specified
    /// - Environment-based: "dev", "staging", "prod"
    /// - Application-based: "app1", "microservice-x"
    /// - Team-based: "team-alpha", "team-beta"
    ///
    /// If not specified (null), the RabbitMQ client will use the default virtual host "/".
    /// </summary>
    /// <example>
    /// Default: "/" or null
    /// Environment: "production"
    /// Application: "order-processing"
    /// Team: "platform-team"
    /// </example>
    public string? VirtualHost { get; set; }

    /// <summary>
    /// Name of the RabbitMQ queue from which messages will be consumed.
    ///
    /// This queue must exist on the RabbitMQ server or be created by the application
    /// with appropriate permissions. The queue serves as the source of messages
    /// that will be forwarded to NATS.
    ///
    /// Queue naming considerations:
    /// - Use descriptive names that indicate the message type or purpose
    /// - Follow your organization's naming conventions
    /// - Consider environment prefixes for multi-environment deployments
    /// - Avoid generic names that might cause confusion
    ///
    /// The application will consume messages from this queue using a durable consumer
    /// with manual acknowledgment to ensure reliable message processing.
    /// </summary>
    /// <example>
    /// Simple: orders
    /// Descriptive: customer-notifications
    /// Environment-specific: prod-payment-events
    /// Service-specific: user-service-outbox
    /// </example>
    public required string QueueName { get; set; }
}