using RabbitGoingNats.Model;

namespace Tests;

public class NatsConnectionTests
{
    [Fact]
    public void NatsConnection_CanBeCreated_WithRequiredProperties()
    {
        // Arrange & Act
        var natsConnection = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject"
        };

        // Assert
        Assert.Equal("nats://localhost:4222", natsConnection.Url);
        Assert.Equal("test.subject", natsConnection.Subject);
        Assert.Null(natsConnection.Secret);
        Assert.Null(natsConnection.User);
        Assert.Null(natsConnection.Password);
    }

    [Fact]
    public void NatsConnection_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var natsConnection = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject"
        };

        // Assert
        Assert.Null(natsConnection.Secret);
        Assert.Null(natsConnection.User);
        Assert.Null(natsConnection.Password);
    }

    [Fact]
    public void NatsConnection_CanBeConfigured_WithTokenAuthentication()
    {
        // Arrange & Act
        var natsConnection = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject",
            Secret = "test-token-123"
        };

        // Assert
        Assert.Equal("nats://localhost:4222", natsConnection.Url);
        Assert.Equal("test.subject", natsConnection.Subject);
        Assert.Equal("test-token-123", natsConnection.Secret);
        Assert.Null(natsConnection.User);
        Assert.Null(natsConnection.Password);
    }

    [Fact]
    public void NatsConnection_CanBeConfigured_WithUsernamePasswordAuthentication()
    {
        // Arrange & Act
        var natsConnection = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject",
            User = "testuser",
            Password = "testpassword"
        };

        // Assert
        Assert.Equal("nats://localhost:4222", natsConnection.Url);
        Assert.Equal("test.subject", natsConnection.Subject);
        Assert.Null(natsConnection.Secret);
        Assert.Equal("testuser", natsConnection.User);
        Assert.Equal("testpassword", natsConnection.Password);
    }

    [Fact]
    public void NatsConnection_CanBeConfigured_WithBothAuthenticationMethods()
    {
        // Arrange & Act
        var natsConnection = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject",
            Secret = "test-token-123",
            User = "testuser",
            Password = "testpassword"
        };

        // Assert
        Assert.Equal("nats://localhost:4222", natsConnection.Url);
        Assert.Equal("test.subject", natsConnection.Subject);
        Assert.Equal("test-token-123", natsConnection.Secret);
        Assert.Equal("testuser", natsConnection.User);
        Assert.Equal("testpassword", natsConnection.Password);
    }

    [Theory]
    [InlineData("nats://localhost:4222")]
    [InlineData("nats+tls://nats.example.com:4222")]
    [InlineData("nats://server1:4222,nats://server2:4222")]
    public void NatsConnection_Url_CanBeSetToVariousFormats(string url)
    {
        // Arrange & Act
        var natsConnection = new NatsConnection
        {
            Url = url,
            Subject = "test.subject"
        };

        // Assert
        Assert.Equal(url, natsConnection.Url);
    }

    [Theory]
    [InlineData("orders")]
    [InlineData("orders.created")]
    [InlineData("prod.orders.payments")]
    [InlineData("rabbitmq.bridge.messages")]
    public void NatsConnection_Subject_CanBeSetToVariousFormats(string subject)
    {
        // Arrange & Act
        var natsConnection = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = subject
        };

        // Assert
        Assert.Equal(subject, natsConnection.Subject);
    }

    [Theory]
    [InlineData("s3cr3t")]
    [InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9")]
    [InlineData("")]
    public void NatsConnection_Secret_CanBeSetToVariousValues(string secret)
    {
        // Arrange & Act
        var natsConnection = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject",
            Secret = secret
        };

        // Assert
        Assert.Equal(secret, natsConnection.Secret);
    }

    [Theory]
    [InlineData("rabbitmq-bridge-service")]
    [InlineData("admin")]
    [InlineData("")]
    public void NatsConnection_User_CanBeSetToVariousValues(string user)
    {
        // Arrange & Act
        var natsConnection = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject",
            User = user
        };

        // Assert
        Assert.Equal(user, natsConnection.User);
    }

    [Theory]
    [InlineData("mypassword123")]
    [InlineData("MySecure!Pass@2024")]
    [InlineData("")]
    public void NatsConnection_Password_CanBeSetToVariousValues(string password)
    {
        // Arrange & Act
        var natsConnection = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject",
            Password = password
        };

        // Assert
        Assert.Equal(password, natsConnection.Password);
    }

    [Fact]
    public void NatsConnection_Properties_CanBeModifiedAfterInitialization()
    {
        // Arrange
        var natsConnection = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject"
        };

        // Act
        natsConnection.Url = "nats://newserver:4222";
        natsConnection.Subject = "new.subject";
        natsConnection.Secret = "new-token";
        natsConnection.User = "newuser";
        natsConnection.Password = "newpassword";

        // Assert
        Assert.Equal("nats://newserver:4222", natsConnection.Url);
        Assert.Equal("new.subject", natsConnection.Subject);
        Assert.Equal("new-token", natsConnection.Secret);
        Assert.Equal("newuser", natsConnection.User);
        Assert.Equal("newpassword", natsConnection.Password);
    }

    [Fact]
    public void NatsConnection_OptionalProperties_CanBeSetToNull()
    {
        // Arrange & Act
        var natsConnection = new NatsConnection
        {
            Url = "nats://localhost:4222",
            Subject = "test.subject",
            Secret = "token",
            User = "user",
            Password = "password"
        };

        // Modify to null
        natsConnection.Secret = null;
        natsConnection.User = null;
        natsConnection.Password = null;

        // Assert
        Assert.Null(natsConnection.Secret);
        Assert.Null(natsConnection.User);
        Assert.Null(natsConnection.Password);
    }
}
