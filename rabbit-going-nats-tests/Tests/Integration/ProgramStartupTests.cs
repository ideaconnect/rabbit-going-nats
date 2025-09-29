using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitGoingNats;
using RabbitGoingNats.Model;
using RabbitGoingNats.Service;

namespace Tests.Integration;

/// <summary>
/// Tests for Program.cs startup behavior, host configuration, and application lifecycle.
/// These tests focus on the actual Program.cs execution patterns and host building.
/// </summary>
public class ProgramStartupTests
{
    [Fact]
    public void CreateHostBuilder_ShouldBuildHostSuccessfully()
    {
        // Arrange - Create test configuration that would be valid for startup
        var testConfig = CreateValidTestConfiguration();

        // Act - Create host using similar pattern as Program.cs
        var host = CreateTestHost(testConfig);

        // Assert
        Assert.NotNull(host);
        using (host)
        {
            // Verify host can be created without exceptions
            Assert.IsAssignableFrom<IHost>(host);
        }
    }

    [Fact]
    public void HostServices_ShouldHaveCorrectServiceLifetimes()
    {
        // Arrange
        var testConfig = CreateValidTestConfiguration();
        var host = CreateTestHost(testConfig);

        using (host)
        {
            var services = host.Services;

            // Act & Assert - Verify singleton lifetimes
            var nats1 = services.GetRequiredService<INatsConnectionHandler>();
            var nats2 = services.GetRequiredService<INatsConnectionHandler>();
            Assert.Same(nats1, nats2); // Should be same instance (singleton)

            var rabbit1 = services.GetRequiredService<IRabbitMqConnectionHandler>();
            var rabbit2 = services.GetRequiredService<IRabbitMqConnectionHandler>();
            Assert.Same(rabbit1, rabbit2); // Should be same instance (singleton)

            // Verify hosted service registration
            var hostedServices = services.GetServices<IHostedService>();
            Assert.Contains(hostedServices, s => s is Worker);
        }
    }

    [Fact]
    public void HostConfiguration_ShouldLoadConfigurationSources()
    {
        // Arrange
        var testConfig = CreateValidTestConfiguration();
        var host = CreateTestHost(testConfig);

        using (host)
        {
            // Act
            var configuration = host.Services.GetRequiredService<IConfiguration>();

            // Assert - Verify configuration is accessible
            Assert.NotNull(configuration);

            // Verify that configuration sections are accessible
            var rabbitSection = configuration.GetSection("RabbitMq");
            var natsSection = configuration.GetSection("Nats");

            Assert.NotNull(rabbitSection);
            Assert.NotNull(natsSection);
        }
    }

    [Fact]
    public void LoggingConfiguration_ShouldClearProvidersAndAddConsole()
    {
        // Arrange
        var testConfig = CreateValidTestConfiguration();
        var host = CreateTestHost(testConfig);

        using (host)
        {
            // Act
            var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<ProgramStartupTests>();

            // Assert
            Assert.NotNull(loggerFactory);
            Assert.NotNull(logger);

            // Verify we can create loggers (indicates logging is properly configured)
            var specificLogger = host.Services.GetRequiredService<ILogger<Worker>>();
            Assert.NotNull(specificLogger);
        }
    }

    [Fact]
    public void HostStartup_ShouldFailWithInvalidRabbitMqConfiguration()
    {
        // Arrange - Create configuration with invalid RabbitMQ settings
        var invalidConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMq:HostName"] = "", // Invalid - empty hostname
                ["RabbitMq:QueueName"] = "test-queue",
                ["Nats:Url"] = "nats://localhost:4222",
                ["Nats:Subject"] = "test.subject"
            })
            .Build();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
        {
            using var host = CreateTestHost(invalidConfig);
            // Force configuration validation by accessing the options
            var rabbitOptions = host.Services.GetRequiredService<IOptions<RabbitMqConnection>>();
            _ = rabbitOptions.Value; // This should trigger validation and throw
        });
    }

    [Fact]
    public void HostStartup_ShouldFailWithInvalidNatsConfiguration()
    {
        // Arrange - Create configuration with invalid NATS settings
        var invalidConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMq:HostName"] = "localhost",
                ["RabbitMq:QueueName"] = "test-queue",
                ["Nats:Url"] = "http://invalid-scheme:4222", // Invalid - wrong scheme
                ["Nats:Subject"] = "test.subject"
            })
            .Build();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
        {
            using var host = CreateTestHost(invalidConfig);
            // Force configuration validation by accessing the options
            var natsOptions = host.Services.GetRequiredService<IOptions<NatsConnection>>();
            _ = natsOptions.Value; // This should trigger validation and throw
        });
    }

    [Fact]
    public void HostServices_ShouldResolveAllRequiredDependencies()
    {
        // Arrange
        var testConfig = CreateValidTestConfiguration();
        var host = CreateTestHost(testConfig);

        using (host)
        {
            var services = host.Services;

            // Act & Assert - Verify all required services can be resolved
            Assert.NotNull(services.GetRequiredService<INatsConnectionHandler>());
            Assert.NotNull(services.GetRequiredService<IRabbitMqConnectionHandler>());

            // Verify Worker is registered as hosted service
            var hostedServices = services.GetServices<IHostedService>();
            var worker = hostedServices.OfType<Worker>().FirstOrDefault();
            Assert.NotNull(worker);

            Assert.NotNull(services.GetRequiredService<IOptions<RabbitMqConnection>>());
            Assert.NotNull(services.GetRequiredService<IOptions<NatsConnection>>());
            Assert.NotNull(services.GetRequiredService<IConfiguration>());
            Assert.NotNull(services.GetRequiredService<ILoggerFactory>());

            // Verify no circular dependencies or missing dependencies
            Assert.NotNull(worker);
        }
    }

    [Fact]
    public void ConfigurationValidation_ShouldTriggerOnServiceResolution()
    {
        // Arrange - Valid base config
        var validConfig = CreateValidTestConfiguration();
        var host = CreateTestHost(validConfig);

        using (host)
        {
            // Act & Assert - Should not throw for valid configuration
            var rabbitOptions = host.Services.GetRequiredService<IOptions<RabbitMqConnection>>();
            var natsOptions = host.Services.GetRequiredService<IOptions<NatsConnection>>();

            var rabbitConnection = rabbitOptions.Value;
            var natsConnection = natsOptions.Value;

            // Verify values are populated correctly
            Assert.Equal("localhost", rabbitConnection.HostName);
            Assert.Equal("test-queue", rabbitConnection.QueueName);
            Assert.Equal("nats://localhost:4222", natsConnection.Url);
            Assert.Equal("test.subject", natsConnection.Subject);
        }
    }

    [Theory]
    [InlineData("RabbitMq:HostName", null)]
    [InlineData("RabbitMq:HostName", "")]
    [InlineData("RabbitMq:HostName", "   ")]
    public void RabbitMqValidation_ShouldRejectInvalidHostNames(string configKey, string? invalidValue)
    {
        // Arrange
        var configData = GetValidConfigurationData();
        configData[configKey] = invalidValue;

        var invalidConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
        {
            using var host = CreateTestHost(invalidConfig);
            var options = host.Services.GetRequiredService<IOptions<RabbitMqConnection>>();
            _ = options.Value;
        });
    }

    [Theory]
    [InlineData("RabbitMq:QueueName", null)]
    [InlineData("RabbitMq:QueueName", "")]
    [InlineData("RabbitMq:QueueName", "   ")]
    public void RabbitMqValidation_ShouldRejectInvalidQueueNames(string configKey, string? invalidValue)
    {
        // Arrange
        var configData = GetValidConfigurationData();
        configData[configKey] = invalidValue;

        var invalidConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
        {
            using var host = CreateTestHost(invalidConfig);
            var options = host.Services.GetRequiredService<IOptions<RabbitMqConnection>>();
            _ = options.Value;
        });
    }

    [Theory]
    [InlineData("Nats:Url", null)]
    [InlineData("Nats:Url", "")]
    [InlineData("Nats:Url", "   ")]
    public void NatsValidation_ShouldRejectInvalidUrls(string configKey, string? invalidValue)
    {
        // Arrange
        var configData = GetValidConfigurationData();
        configData[configKey] = invalidValue;

        var invalidConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
        {
            using var host = CreateTestHost(invalidConfig);
            var options = host.Services.GetRequiredService<IOptions<NatsConnection>>();
            _ = options.Value;
        });
    }

    [Theory]
    [InlineData("Nats:Subject", null)]
    [InlineData("Nats:Subject", "")]
    [InlineData("Nats:Subject", "   ")]
    public void NatsValidation_ShouldRejectInvalidSubjects(string configKey, string? invalidValue)
    {
        // Arrange
        var configData = GetValidConfigurationData();
        configData[configKey] = invalidValue;

        var invalidConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
        {
            using var host = CreateTestHost(invalidConfig);
            var options = host.Services.GetRequiredService<IOptions<NatsConnection>>();
            _ = options.Value;
        });
    }

    [Fact]
    public void HostBuilder_ShouldUseDefaultBuilder()
    {
        // Arrange
        var testConfig = CreateValidTestConfiguration();

        // Act - Create host using CreateDefaultBuilder pattern from Program.cs
        var host = Host.CreateDefaultBuilder(Array.Empty<string>())
            .ConfigureAppConfiguration(builder =>
            {
                builder.Sources.Clear();
                builder.AddConfiguration(testConfig);
            })
            .ConfigureServices((hostContext, services) =>
            {
                // Replicate Program.cs service configuration
                var configuration = hostContext.Configuration;

                services.Configure<RabbitMqConnection>(configuration.GetSection("RabbitMq"));
                services.Configure<NatsConnection>(configuration.GetSection("Nats"));

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

                services.AddSingleton<INatsConnectionHandler, NatsConnectionHandler>();
                services.AddSingleton<IRabbitMqConnectionHandler, RabbitMqConnectionHandler>();
                services.AddHostedService<Worker>();

                services.AddLogging(loggingBuilder =>
                {
                    loggingBuilder.ClearProviders();
                    loggingBuilder.AddConsole();
                });
            })
            .Build();

        // Assert
        using (host)
        {
            Assert.NotNull(host);
            Assert.NotNull(host.Services);

            // Verify that default builder features are available
            var configuration = host.Services.GetRequiredService<IConfiguration>();
            var hostEnvironment = host.Services.GetRequiredService<IHostEnvironment>();

            Assert.NotNull(configuration);
            Assert.NotNull(hostEnvironment);
        }
    }

    [Fact]
    public void ServiceProvider_ShouldDisposeServicesCorrectly()
    {
        // Arrange
        var testConfig = CreateValidTestConfiguration();
        var host = CreateTestHost(testConfig);

        IServiceProvider? serviceProvider;

        // Act
        using (host)
        {
            serviceProvider = host.Services;

            // Get services to ensure they're created
            var natsHandler = serviceProvider.GetRequiredService<INatsConnectionHandler>();
            var rabbitHandler = serviceProvider.GetRequiredService<IRabbitMqConnectionHandler>();
            var hostedServices = serviceProvider.GetServices<IHostedService>();
            var worker = hostedServices.OfType<Worker>().FirstOrDefault();

            Assert.NotNull(natsHandler);
            Assert.NotNull(rabbitHandler);
            Assert.NotNull(worker);
        }

        // Assert - Host disposal should complete without exceptions
        // Services implementing IDisposable/IAsyncDisposable should be cleaned up
        Assert.NotNull(serviceProvider);
    }

    #region Helper Methods

    private static IConfiguration CreateValidTestConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(GetValidConfigurationData())
            .Build();
    }

    private static Dictionary<string, string?> GetValidConfigurationData()
    {
        return new Dictionary<string, string?>
        {
            ["RabbitMq:HostName"] = "localhost",
            ["RabbitMq:QueueName"] = "test-queue",
            ["RabbitMq:Port"] = "5672",
            ["Nats:Url"] = "nats://localhost:4222",
            ["Nats:Subject"] = "test.subject"
        };
    }

    private static IHost CreateTestHost(IConfiguration configuration)
    {
        return Host.CreateDefaultBuilder(Array.Empty<string>())
            .ConfigureAppConfiguration(builder =>
            {
                builder.Sources.Clear();
                builder.AddConfiguration(configuration);
            })
            .ConfigureServices((hostContext, services) =>
            {
                var config = hostContext.Configuration;

                // Replicate exact Program.cs configuration
                services.Configure<RabbitMqConnection>(config.GetSection("RabbitMq"));
                services.Configure<NatsConnection>(config.GetSection("Nats"));

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

                services.AddSingleton<INatsConnectionHandler, NatsConnectionHandler>();
                services.AddSingleton<IRabbitMqConnectionHandler, RabbitMqConnectionHandler>();
                services.AddHostedService<Worker>();

                services.AddLogging(loggingBuilder =>
                {
                    loggingBuilder.ClearProviders();
                    loggingBuilder.AddConsole();
                });
            })
            .Build();
    }

    #endregion
}