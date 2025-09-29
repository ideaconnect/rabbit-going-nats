namespace RabbitGoingNats.Model;

/// <summary>
/// Configuration model for NATS (Neural Autonomic Transport System) connection parameters.
///
/// This class represents the configuration required to establish a connection to a NATS server
/// and defines the messaging parameters for publishing messages received from RabbitMQ.
///
/// NATS is a lightweight, high-performance messaging system that supports:
/// - Publish/Subscribe messaging patterns
/// - Request/Reply communication
/// - Load-balanced message distribution
/// - Clustering and high availability
///
/// This configuration supports multiple authentication methods:
/// - No authentication (for development/testing)
/// - Token-based authentication (using Secret)
/// - Username/Password authentication
///
/// The configuration is typically loaded from appsettings.json under the "Nats" section
/// and validated at application startup to ensure all required values are provided.
/// </summary>
public class NatsConnection
{
    /// <summary>
    /// URL to the NATS server including protocol scheme, hostname/IP, and port.
    ///
    /// This should be a fully qualified URL that the NATS client can use to establish
    /// a connection. The URL must include the appropriate scheme for the connection type:
    /// - nats:// for standard unencrypted connections
    /// - nats+tls:// for TLS-encrypted connections
    ///
    /// The port is typically 4222 for standard NATS, but can be customized.
    /// Multiple URLs can be provided for cluster setups (comma-separated).
    /// </summary>
    /// <example>
    /// Standard connection: nats://localhost:4222
    /// TLS connection: nats+tls://nats.example.com:4222
    /// Cluster: nats://server1:4222,nats://server2:4222
    /// </example>
    public required string Url { get; set; }

    /// <summary>
    /// NATS subject (topic) to which messages retrieved from RabbitMQ will be published.
    ///
    /// In NATS, subjects are used for message routing and are hierarchical, dot-separated
    /// strings that allow for powerful subscription patterns. Subjects can use wildcards
    /// for subscription (* for single token, > for multiple tokens).
    ///
    /// Choose subjects that reflect your message routing needs and follow your
    /// organization's naming conventions. Avoid subjects that are too generic to
    /// prevent unintended message routing.
    /// </summary>
    /// <example>
    /// Simple subject: orders
    /// Hierarchical: orders.created
    /// Environment-specific: prod.orders.payments
    /// Service-specific: rabbitmq.bridge.messages
    /// </example>
    public required string Subject { get; set; }

    /// <summary>
    /// NATS authentication token (secret) for token-based authentication.
    ///
    /// This is used when the NATS server is configured for token-based authentication.
    /// Token authentication is simpler than username/password and is recommended for
    /// service-to-service communication.
    ///
    /// NOTE: If both Secret and User/Password are provided, the User/Password
    /// authentication will take precedence over token authentication.
    ///
    /// Leave this null or empty for servers that don't require authentication
    /// (typically development environments).
    /// </summary>
    /// <example>
    /// Simple token: s3cr3t
    /// Complex token: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
    /// </example>
    public string? Secret { get; set; } = null;

    /// <summary>
    /// Username for username/password authentication when the NATS server requires it.
    ///
    /// This is used in conjunction with the Password property for traditional
    /// username/password authentication. This authentication method is common
    /// in environments where user-based access control is required.
    ///
    /// When both User/Password and Secret are provided, the User/Password
    /// authentication takes precedence over token-based authentication.
    ///
    /// Leave this null for token-based authentication or anonymous connections.
    /// </summary>
    /// <example>
    /// Service account: rabbitmq-bridge-service
    /// User account: admin
    /// </example>
    public string? User { get; set; } = null;

    /// <summary>
    /// Password for username/password authentication, required when User is specified.
    ///
    /// This password is used together with the User property for authentication.
    /// Ensure this password meets your NATS server's security requirements and
    /// is stored securely (preferably using user secrets or environment variables
    /// in production environments).
    ///
    /// The password should be kept confidential and rotated regularly according
    /// to your organization's security policies.
    /// </summary>
    /// <example>
    /// Simple password: mypassword123
    /// Complex password: MySecure!Pass@2024
    /// </example>
    public string? Password { get; set; } = null;
}