using RabbitGoingNats.Model;

namespace Tests;

public class RabbitMqConnectionTests
{
    [Fact]
    public void RabbitMqConnection_CanBeCreated_WithRequiredProperties()
    {
        // Arrange & Act
        var rabbitMqConnection = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue"
        };

        // Assert
        Assert.Equal("localhost", rabbitMqConnection.HostName);
        Assert.Equal("test-queue", rabbitMqConnection.QueueName);
        Assert.Null(rabbitMqConnection.Port);
        Assert.Null(rabbitMqConnection.UserName);
        Assert.Null(rabbitMqConnection.Password);
        Assert.Null(rabbitMqConnection.VirtualHost);
    }

    [Fact]
    public void RabbitMqConnection_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var rabbitMqConnection = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue"
        };

        // Assert
        Assert.Null(rabbitMqConnection.Port);
        Assert.Null(rabbitMqConnection.UserName);
        Assert.Null(rabbitMqConnection.Password);
        Assert.Null(rabbitMqConnection.VirtualHost);
    }

    [Fact]
    public void RabbitMqConnection_CanBeConfigured_WithAuthentication()
    {
        // Arrange & Act
        var rabbitMqConnection = new RabbitMqConnection
        {
            HostName = "rabbitmq.example.com",
            QueueName = "orders",
            UserName = "testuser",
            Password = "testpassword"
        };

        // Assert
        Assert.Equal("rabbitmq.example.com", rabbitMqConnection.HostName);
        Assert.Equal("orders", rabbitMqConnection.QueueName);
        Assert.Equal("testuser", rabbitMqConnection.UserName);
        Assert.Equal("testpassword", rabbitMqConnection.Password);
        Assert.Null(rabbitMqConnection.Port);
        Assert.Null(rabbitMqConnection.VirtualHost);
    }

    [Fact]
    public void RabbitMqConnection_CanBeConfigured_WithCustomPort()
    {
        // Arrange & Act
        var rabbitMqConnection = new RabbitMqConnection
        {
            HostName = "rabbitmq.example.com",
            QueueName = "orders",
            Port = 5671
        };

        // Assert
        Assert.Equal("rabbitmq.example.com", rabbitMqConnection.HostName);
        Assert.Equal("orders", rabbitMqConnection.QueueName);
        Assert.Equal(5671, rabbitMqConnection.Port);
        Assert.Null(rabbitMqConnection.UserName);
        Assert.Null(rabbitMqConnection.Password);
        Assert.Null(rabbitMqConnection.VirtualHost);
    }

    [Fact]
    public void RabbitMqConnection_CanBeConfigured_WithVirtualHost()
    {
        // Arrange & Act
        var rabbitMqConnection = new RabbitMqConnection
        {
            HostName = "rabbitmq.example.com",
            QueueName = "orders",
            VirtualHost = "production"
        };

        // Assert
        Assert.Equal("rabbitmq.example.com", rabbitMqConnection.HostName);
        Assert.Equal("orders", rabbitMqConnection.QueueName);
        Assert.Equal("production", rabbitMqConnection.VirtualHost);
        Assert.Null(rabbitMqConnection.Port);
        Assert.Null(rabbitMqConnection.UserName);
        Assert.Null(rabbitMqConnection.Password);
    }

    [Fact]
    public void RabbitMqConnection_CanBeConfigured_WithAllProperties()
    {
        // Arrange & Act
        var rabbitMqConnection = new RabbitMqConnection
        {
            HostName = "rabbitmq.production.com",
            Port = 5672,
            UserName = "app-user",
            Password = "secure-password",
            VirtualHost = "prod",
            QueueName = "customer-notifications"
        };

        // Assert
        Assert.Equal("rabbitmq.production.com", rabbitMqConnection.HostName);
        Assert.Equal(5672, rabbitMqConnection.Port);
        Assert.Equal("app-user", rabbitMqConnection.UserName);
        Assert.Equal("secure-password", rabbitMqConnection.Password);
        Assert.Equal("prod", rabbitMqConnection.VirtualHost);
        Assert.Equal("customer-notifications", rabbitMqConnection.QueueName);
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("rabbitmq.example.com")]
    [InlineData("192.168.1.100")]
    [InlineData("::1")]
    [InlineData("rabbitmq-container")]
    public void RabbitMqConnection_HostName_CanBeSetToVariousFormats(string hostName)
    {
        // Arrange & Act
        var rabbitMqConnection = new RabbitMqConnection
        {
            HostName = hostName,
            QueueName = "test-queue"
        };

        // Assert
        Assert.Equal(hostName, rabbitMqConnection.HostName);
    }

    [Theory]
    [InlineData(5672)]
    [InlineData(5671)]
    [InlineData(25672)]
    [InlineData(15672)]
    public void RabbitMqConnection_Port_CanBeSetToVariousValues(int port)
    {
        // Arrange & Act
        var rabbitMqConnection = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue",
            Port = port
        };

        // Assert
        Assert.Equal(port, rabbitMqConnection.Port);
    }

    [Theory]
    [InlineData("guest")]
    [InlineData("rabbitmq-bridge-consumer")]
    [InlineData("myapp-consumer")]
    [InlineData("")]
    public void RabbitMqConnection_UserName_CanBeSetToVariousValues(string userName)
    {
        // Arrange & Act
        var rabbitMqConnection = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue",
            UserName = userName
        };

        // Assert
        Assert.Equal(userName, rabbitMqConnection.UserName);
    }

    [Theory]
    [InlineData("guest")]
    [InlineData("SecurePassword123!")]
    [InlineData("")]
    public void RabbitMqConnection_Password_CanBeSetToVariousValues(string password)
    {
        // Arrange & Act
        var rabbitMqConnection = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue",
            Password = password
        };

        // Assert
        Assert.Equal(password, rabbitMqConnection.Password);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("production")]
    [InlineData("dev")]
    [InlineData("order-processing")]
    [InlineData("team-alpha")]
    [InlineData("")]
    public void RabbitMqConnection_VirtualHost_CanBeSetToVariousValues(string virtualHost)
    {
        // Arrange & Act
        var rabbitMqConnection = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue",
            VirtualHost = virtualHost
        };

        // Assert
        Assert.Equal(virtualHost, rabbitMqConnection.VirtualHost);
    }

    [Theory]
    [InlineData("orders")]
    [InlineData("customer-notifications")]
    [InlineData("prod-payment-events")]
    [InlineData("user-service-outbox")]
    public void RabbitMqConnection_QueueName_CanBeSetToVariousFormats(string queueName)
    {
        // Arrange & Act
        var rabbitMqConnection = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = queueName
        };

        // Assert
        Assert.Equal(queueName, rabbitMqConnection.QueueName);
    }

    [Fact]
    public void RabbitMqConnection_Properties_CanBeModifiedAfterInitialization()
    {
        // Arrange
        var rabbitMqConnection = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue"
        };

        // Act
        rabbitMqConnection.HostName = "newhost";
        rabbitMqConnection.Port = 5671;
        rabbitMqConnection.UserName = "newuser";
        rabbitMqConnection.Password = "newpassword";
        rabbitMqConnection.VirtualHost = "newvhost";
        rabbitMqConnection.QueueName = "new-queue";

        // Assert
        Assert.Equal("newhost", rabbitMqConnection.HostName);
        Assert.Equal(5671, rabbitMqConnection.Port);
        Assert.Equal("newuser", rabbitMqConnection.UserName);
        Assert.Equal("newpassword", rabbitMqConnection.Password);
        Assert.Equal("newvhost", rabbitMqConnection.VirtualHost);
        Assert.Equal("new-queue", rabbitMqConnection.QueueName);
    }

    [Fact]
    public void RabbitMqConnection_OptionalProperties_CanBeSetToNull()
    {
        // Arrange & Act
        var rabbitMqConnection = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "test-queue",
            Port = 5672,
            UserName = "user",
            Password = "password",
            VirtualHost = "vhost"
        };

        // Modify to null
        rabbitMqConnection.Port = null;
        rabbitMqConnection.UserName = null;
        rabbitMqConnection.Password = null;
        rabbitMqConnection.VirtualHost = null;

        // Assert
        Assert.Null(rabbitMqConnection.Port);
        Assert.Null(rabbitMqConnection.UserName);
        Assert.Null(rabbitMqConnection.Password);
        Assert.Null(rabbitMqConnection.VirtualHost);
    }

    [Fact]
    public void RabbitMqConnection_CanBeConfigured_ForDevelopmentEnvironment()
    {
        // Arrange & Act (typical development setup)
        var rabbitMqConnection = new RabbitMqConnection
        {
            HostName = "localhost",
            QueueName = "dev-queue"
            // No authentication, using defaults
        };

        // Assert
        Assert.Equal("localhost", rabbitMqConnection.HostName);
        Assert.Equal("dev-queue", rabbitMqConnection.QueueName);
        Assert.Null(rabbitMqConnection.Port); // Will use default 5672
        Assert.Null(rabbitMqConnection.UserName); // Anonymous access
        Assert.Null(rabbitMqConnection.Password);
        Assert.Null(rabbitMqConnection.VirtualHost); // Will use default "/"
    }

    [Fact]
    public void RabbitMqConnection_CanBeConfigured_ForProductionEnvironment()
    {
        // Arrange & Act (typical production setup)
        var rabbitMqConnection = new RabbitMqConnection
        {
            HostName = "rabbitmq.production.example.com",
            Port = 5671, // TLS port
            UserName = "prod-bridge-service",
            Password = "SecureProductionPassword123!",
            VirtualHost = "production",
            QueueName = "payment-events"
        };

        // Assert
        Assert.Equal("rabbitmq.production.example.com", rabbitMqConnection.HostName);
        Assert.Equal(5671, rabbitMqConnection.Port);
        Assert.Equal("prod-bridge-service", rabbitMqConnection.UserName);
        Assert.Equal("SecureProductionPassword123!", rabbitMqConnection.Password);
        Assert.Equal("production", rabbitMqConnection.VirtualHost);
        Assert.Equal("payment-events", rabbitMqConnection.QueueName);
    }

    [Fact]
    public void RabbitMqConnection_CanBeConfigured_ForDockerEnvironment()
    {
        // Arrange & Act (typical Docker setup)
        var rabbitMqConnection = new RabbitMqConnection
        {
            HostName = "rabbitmq-container",
            UserName = "guest",
            Password = "guest",
            QueueName = "docker-test-queue"
        };

        // Assert
        Assert.Equal("rabbitmq-container", rabbitMqConnection.HostName);
        Assert.Equal("guest", rabbitMqConnection.UserName);
        Assert.Equal("guest", rabbitMqConnection.Password);
        Assert.Equal("docker-test-queue", rabbitMqConnection.QueueName);
        Assert.Null(rabbitMqConnection.Port); // Using default
        Assert.Null(rabbitMqConnection.VirtualHost); // Using default
    }
}