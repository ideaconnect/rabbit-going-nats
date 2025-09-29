using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RabbitGoingNats;
using RabbitGoingNats.Model;
using RabbitGoingNats.Service;

namespace Tests.Integration;

public class ProgramConfigurationTests
{
    [Fact]
    public void ConfigureServices_ShouldRegisterAllRequiredServices()
    {
        // Arrange
        var configuration = CreateTestConfiguration();
        var services = new ServiceCollection();

        // Act - Simulate the service configuration from Program.cs
        ConfigureTestServices(services, configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(serviceProvider.GetService<INatsConnectionHandler>());
        Assert.NotNull(serviceProvider.GetService<IRabbitMqConnectionHandler>());
        Assert.NotNull(serviceProvider.GetService<Worker>());

        // Verify configuration options are registered
        Assert.NotNull(serviceProvider.GetService<IOptions<RabbitMqConnection>>());
        Assert.NotNull(serviceProvider.GetService<IOptions<NatsConnection>>());
    }

    [Fact]
    public void RabbitMqConnectionValidation_ShouldThrow_WhenHostNameIsEmpty()
    {
        // Arrange
        var configuration = CreateTestConfiguration(rabbitMqHostName: "");
        var services = new ServiceCollection();
        ConfigureTestServices(services, configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<RabbitMqConnection>>();
            _ = options.Value; // Force evaluation
        });

        Assert.Contains("RabbitMQ HostName is required", exception.Message);
    }

    [Fact]
    public void RabbitMqConnectionValidation_ShouldThrow_WhenQueueNameIsEmpty()
    {
        // Arrange
        var configuration = CreateTestConfiguration(rabbitMqQueueName: "");
        var services = new ServiceCollection();
        ConfigureTestServices(services, configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<RabbitMqConnection>>();
            _ = options.Value; // Force evaluation
        });

        Assert.Contains("RabbitMQ QueueName is required", exception.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(65536)]
    [InlineData(100000)]
    public void RabbitMqConnectionValidation_ShouldThrow_WhenPortIsInvalid(int invalidPort)
    {
        // Arrange
        var configuration = CreateTestConfiguration(rabbitMqPort: invalidPort);
        var services = new ServiceCollection();
        ConfigureTestServices(services, configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<RabbitMqConnection>>();
            _ = options.Value; // Force evaluation
        });

        Assert.Contains("RabbitMQ Port must be between 1 and 65535", exception.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5672)]
    [InlineData(5671)]
    [InlineData(65535)]
    public void RabbitMqConnectionValidation_ShouldSucceed_WhenPortIsValid(int validPort)
    {
        // Arrange
        var configuration = CreateTestConfiguration(rabbitMqPort: validPort);
        var services = new ServiceCollection();
        ConfigureTestServices(services, configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var options = serviceProvider.GetRequiredService<IOptions<RabbitMqConnection>>();
        var rabbitMqConnection = options.Value;

        Assert.Equal(validPort, rabbitMqConnection.Port);
    }

    [Fact]
    public void NatsConnectionValidation_ShouldThrow_WhenUrlIsEmpty()
    {
        // Arrange
        var configuration = CreateTestConfiguration(natsUrl: "");
        var services = new ServiceCollection();
        ConfigureTestServices(services, configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<NatsConnection>>();
            _ = options.Value; // Force evaluation
        });

        Assert.Contains("NATS Url is required", exception.Message);
    }

    [Fact]
    public void NatsConnectionValidation_ShouldThrow_WhenSubjectIsEmpty()
    {
        // Arrange
        var configuration = CreateTestConfiguration(natsSubject: "");
        var services = new ServiceCollection();
        ConfigureTestServices(services, configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<NatsConnection>>();
            _ = options.Value; // Force evaluation
        });

        Assert.Contains("NATS Subject is required", exception.Message);
    }

    [Theory]
    [InlineData("http://invalid.url")]
    [InlineData("https://invalid.url")]
    [InlineData("ftp://invalid.url")]
    [InlineData("invalid-url")]
    [InlineData("tcp://invalid.url")]
    public void NatsConnectionValidation_ShouldThrow_WhenUrlSchemeIsInvalid(string invalidUrl)
    {
        // Arrange
        var configuration = CreateTestConfiguration(natsUrl: invalidUrl);
        var services = new ServiceCollection();
        ConfigureTestServices(services, configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<NatsConnection>>();
            _ = options.Value; // Force evaluation
        });

        Assert.Contains("NATS Url must be a valid URI with 'nats://' or 'nats+tls://' scheme", exception.Message);
    }

    [Theory]
    [InlineData("nats://localhost:4222")]
    [InlineData("nats+tls://nats.example.com:4222")]
    public void NatsConnectionValidation_ShouldSucceed_WhenUrlSchemeIsValid(string validUrl)
    {
        // Arrange
        var configuration = CreateTestConfiguration(natsUrl: validUrl);
        var services = new ServiceCollection();
        ConfigureTestServices(services, configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var options = serviceProvider.GetRequiredService<IOptions<NatsConnection>>();
        var natsConnection = options.Value;

        Assert.Equal(validUrl, natsConnection.Url);
    }

    [Fact]
    public void ConfigureServices_ShouldRegisterSingletonServices()
    {
        // Arrange
        var configuration = CreateTestConfiguration();
        var services = new ServiceCollection();
        ConfigureTestServices(services, configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Act - Get services multiple times
        var natsHandler1 = serviceProvider.GetService<INatsConnectionHandler>();
        var natsHandler2 = serviceProvider.GetService<INatsConnectionHandler>();
        var rabbitHandler1 = serviceProvider.GetService<IRabbitMqConnectionHandler>();
        var rabbitHandler2 = serviceProvider.GetService<IRabbitMqConnectionHandler>();

        // Assert - Should be same instances (singleton)
        Assert.Same(natsHandler1, natsHandler2);
        Assert.Same(rabbitHandler1, rabbitHandler2);
    }

    [Fact]
    public void ConfigureServices_ShouldBindConfigurationCorrectly()
    {
        // Arrange
        var configuration = CreateTestConfiguration(
            rabbitMqHostName: "test-rabbit-host",
            rabbitMqQueueName: "test-queue",
            rabbitMqPort: 5673,
            rabbitMqUserName: "test-user",
            rabbitMqPassword: "test-password",
            rabbitMqVirtualHost: "test-vhost",
            natsUrl: "nats://test-nats:4222",
            natsSubject: "test.subject",
            natsSecret: "test-secret",
            natsUser: "nats-user",
            natsPassword: "nats-password"
        );
        var services = new ServiceCollection();
        ConfigureTestServices(services, configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var rabbitMqOptions = serviceProvider.GetRequiredService<IOptions<RabbitMqConnection>>();
        var natsOptions = serviceProvider.GetRequiredService<IOptions<NatsConnection>>();

        // Assert
        var rabbitMqConnection = rabbitMqOptions.Value;
        Assert.Equal("test-rabbit-host", rabbitMqConnection.HostName);
        Assert.Equal("test-queue", rabbitMqConnection.QueueName);
        Assert.Equal(5673, rabbitMqConnection.Port);
        Assert.Equal("test-user", rabbitMqConnection.UserName);
        Assert.Equal("test-password", rabbitMqConnection.Password);
        Assert.Equal("test-vhost", rabbitMqConnection.VirtualHost);

        var natsConnection = natsOptions.Value;
        Assert.Equal("nats://test-nats:4222", natsConnection.Url);
        Assert.Equal("test.subject", natsConnection.Subject);
        Assert.Equal("test-secret", natsConnection.Secret);
        Assert.Equal("nats-user", natsConnection.User);
        Assert.Equal("nats-password", natsConnection.Password);
    }

    [Fact]
    public void ConfigureServices_ShouldRegisterWorkerAsHostedService()
    {
        // Arrange
        var configuration = CreateTestConfiguration();
        var services = new ServiceCollection();
        ConfigureTestServices(services, configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var hostedServices = serviceProvider.GetServices<IHostedService>();

        // Assert
        Assert.Contains(hostedServices, service => service is Worker);
    }

    private static IConfiguration CreateTestConfiguration(
        string rabbitMqHostName = "localhost",
        string rabbitMqQueueName = "test-queue",
        int? rabbitMqPort = null,
        string? rabbitMqUserName = null,
        string? rabbitMqPassword = null,
        string? rabbitMqVirtualHost = null,
        string natsUrl = "nats://localhost:4222",
        string natsSubject = "test.subject",
        string? natsSecret = null,
        string? natsUser = null,
        string? natsPassword = null)
    {
        var configData = new Dictionary<string, string?>
        {
            ["RabbitMq:HostName"] = rabbitMqHostName,
            ["RabbitMq:QueueName"] = rabbitMqQueueName,
            ["RabbitMq:UserName"] = rabbitMqUserName,
            ["RabbitMq:Password"] = rabbitMqPassword,
            ["RabbitMq:VirtualHost"] = rabbitMqVirtualHost,
            ["Nats:Url"] = natsUrl,
            ["Nats:Subject"] = natsSubject,
            ["Nats:Secret"] = natsSecret,
            ["Nats:User"] = natsUser,
            ["Nats:Password"] = natsPassword
        };

        if (rabbitMqPort.HasValue)
        {
            configData["RabbitMq:Port"] = rabbitMqPort.Value.ToString();
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    private static void ConfigureTestServices(IServiceCollection services, IConfiguration configuration)
    {
        // Replicate the service configuration from Program.cs
        services.Configure<RabbitMqConnection>(configuration.GetSection("RabbitMq"));
        services.Configure<NatsConnection>(configuration.GetSection("Nats"));

        // Add validation (PostConfigure)
        services.PostConfigure<RabbitMqConnection>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.HostName))
                throw new InvalidOperationException("RabbitMQ HostName is required and cannot be empty");
            if (string.IsNullOrWhiteSpace(options.QueueName))
                throw new InvalidOperationException("RabbitMQ QueueName is required and cannot be empty");

            if (options.Port.HasValue && (options.Port <= 0 || options.Port > 65535))
                throw new InvalidOperationException("RabbitMQ Port must be between 1 and 65535");
        });

        services.PostConfigure<NatsConnection>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.Url))
                throw new InvalidOperationException("NATS Url is required and cannot be empty");
            if (string.IsNullOrWhiteSpace(options.Subject))
                throw new InvalidOperationException("NATS Subject is required and cannot be empty");

            if (!Uri.TryCreate(options.Url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "nats" && uri.Scheme != "nats+tls"))
                throw new InvalidOperationException("NATS Url must be a valid URI with 'nats://' or 'nats+tls://' scheme");
        });

        // Add logging
        services.AddLogging();

        // Register services
        services.AddSingleton<INatsConnectionHandler, NatsConnectionHandler>();
        services.AddSingleton<IRabbitMqConnectionHandler, RabbitMqConnectionHandler>();
        services.AddSingleton<Worker>();
        services.AddHostedService<Worker>();
    }
}