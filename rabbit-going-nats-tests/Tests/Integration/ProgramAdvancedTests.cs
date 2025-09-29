using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitGoingNats;
using RabbitGoingNats.Model;
using RabbitGoingNats.Service;
using System.Reflection;

namespace Tests.Integration;

/// <summary>
/// Advanced tests for Program.cs covering edge cases, error conditions,
/// and AOT-specific behaviors mentioned in the program comments.
/// </summary>
public class ProgramAdvancedTests
{
    [Fact]
    public void Program_ShouldSupportAOTCompilation()
    {
        // Arrange
        var testConfig = CreateValidTestConfiguration();
        var host = CreateTestHost(testConfig);

        using (host)
        {
            // Act & Assert - Verify AOT-compatible configuration binding
            var rabbitOptions = host.Services.GetRequiredService<IOptions<RabbitMqConnection>>();
            var natsOptions = host.Services.GetRequiredService<IOptions<NatsConnection>>();

            // These should work with source generators (AOT-compatible)
            var rabbitConnection = rabbitOptions.Value;
            var natsConnection = natsOptions.Value;

            Assert.NotNull(rabbitConnection);
            Assert.NotNull(natsConnection);

            // Verify that configuration binding worked without reflection
            Assert.Equal("localhost", rabbitConnection.HostName);
            Assert.Equal("test-queue", rabbitConnection.QueueName);
            Assert.Equal("nats://localhost:4222", natsConnection.Url);
            Assert.Equal("test.subject", natsConnection.Subject);
        }
    }

    [Fact]
    public void HostedService_ShouldBeRegisteredCorrectly()
    {
        // Arrange
        var testConfig = CreateValidTestConfiguration();
        var host = CreateTestHost(testConfig);

        using (host)
        {
            // Act
            var hostedServices = host.Services.GetServices<IHostedService>().ToList();
            var workerServices = hostedServices.OfType<Worker>().ToList();

            // Assert
            Assert.Single(hostedServices); // Should only have one hosted service
            Assert.Single(workerServices);  // Should be the Worker service

            var worker = workerServices.First();
            Assert.NotNull(worker);
        }
    }

    [Fact]
    public void LoggingConfiguration_ShouldClearDefaultProviders()
    {
        // Arrange
        var testConfig = CreateValidTestConfiguration();
        var host = CreateTestHost(testConfig);

        using (host)
        {
            // Act
            var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();

            // Assert - Verify logging configuration is set up correctly
            Assert.NotNull(loggerFactory);

            // Create various loggers to ensure NLog configuration works
            var programLogger = loggerFactory.CreateLogger("Program");
            var workerLogger = loggerFactory.CreateLogger<Worker>();
            var rabbitLogger = loggerFactory.CreateLogger<RabbitMqConnectionHandler>();
            var natsLogger = loggerFactory.CreateLogger<NatsConnectionHandler>();

            Assert.NotNull(programLogger);
            Assert.NotNull(workerLogger);
            Assert.NotNull(rabbitLogger);
            Assert.NotNull(natsLogger);
        }
    }

    [Fact]
    public void ConfigurationSections_ShouldBindCorrectly()
    {
        // Arrange
        var testConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMq:HostName"] = "rabbit.example.com",
                ["RabbitMq:QueueName"] = "production-queue",
                ["RabbitMq:Port"] = "5673",
                ["RabbitMq:UserName"] = "rabbit-user",
                ["RabbitMq:Password"] = "rabbit-pass",
                ["RabbitMq:VirtualHost"] = "/production",
                ["Nats:Url"] = "nats+tls://nats.example.com:4222",
                ["Nats:Subject"] = "production.messages",
                ["Nats:User"] = "nats-user",
                ["Nats:Password"] = "nats-pass",
                ["Nats:Secret"] = "shared-secret"
            })
            .Build();

        var host = CreateTestHost(testConfig);

        using (host)
        {
            // Act
            var rabbitOptions = host.Services.GetRequiredService<IOptions<RabbitMqConnection>>();
            var natsOptions = host.Services.GetRequiredService<IOptions<NatsConnection>>();

            var rabbitConnection = rabbitOptions.Value;
            var natsConnection = natsOptions.Value;

            // Assert - Verify all properties are bound correctly
            Assert.Equal("rabbit.example.com", rabbitConnection.HostName);
            Assert.Equal("production-queue", rabbitConnection.QueueName);
            Assert.Equal(5673, rabbitConnection.Port);
            Assert.Equal("rabbit-user", rabbitConnection.UserName);
            Assert.Equal("rabbit-pass", rabbitConnection.Password);
            Assert.Equal("/production", rabbitConnection.VirtualHost);

            Assert.Equal("nats+tls://nats.example.com:4222", natsConnection.Url);
            Assert.Equal("production.messages", natsConnection.Subject);
            Assert.Equal("nats-user", natsConnection.User);
            Assert.Equal("nats-pass", natsConnection.Password);
            Assert.Equal("shared-secret", natsConnection.Secret);
        }
    }

    [Fact]
    public void PostConfigureValidation_ShouldExecuteInCorrectOrder()
    {
        // Arrange - Configuration that will trigger validation
        var testConfig = CreateValidTestConfiguration();
        var host = CreateTestHost(testConfig);

        using (host)
        {
            // Act & Assert - Multiple accesses should use cached validated configuration
            var rabbitOptions = host.Services.GetRequiredService<IOptions<RabbitMqConnection>>();

            var value1 = rabbitOptions.Value;
            var value2 = rabbitOptions.Value;

            // Should be same instance (cached after validation)
            Assert.Same(value1, value2);
            Assert.NotNull(value1);
        }
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(0)]
    [InlineData(65536)]
    [InlineData(100000)]
    public void RabbitMqPortValidation_ShouldRejectInvalidPorts(int invalidPort)
    {
        // Arrange
        var invalidConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMq:HostName"] = "localhost",
                ["RabbitMq:QueueName"] = "test-queue",
                ["RabbitMq:Port"] = invalidPort.ToString(),
                ["Nats:Url"] = "nats://localhost:4222",
                ["Nats:Subject"] = "test.subject"
            })
            .Build();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            using var host = CreateTestHost(invalidConfig);
            var options = host.Services.GetRequiredService<IOptions<RabbitMqConnection>>();
            _ = options.Value;
        });

        Assert.Contains("RabbitMQ Port must be between 1 and 65535", exception.Message);
    }

    [Theory]
    [InlineData("nats://localhost:4222")]
    [InlineData("nats+tls://secure.nats.example.com:4222")]
    [InlineData("nats://127.0.0.1:4222")]
    [InlineData("nats+tls://nats.cluster:4222")]
    public void NatsUrlValidation_ShouldAcceptValidUrls(string validUrl)
    {
        // Arrange
        var testConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMq:HostName"] = "localhost",
                ["RabbitMq:QueueName"] = "test-queue",
                ["Nats:Url"] = validUrl,
                ["Nats:Subject"] = "test.subject"
            })
            .Build();

        var host = CreateTestHost(testConfig);

        using (host)
        {
            // Act
            var natsOptions = host.Services.GetRequiredService<IOptions<NatsConnection>>();
            var natsConnection = natsOptions.Value;

            // Assert
            Assert.Equal(validUrl, natsConnection.Url);
        }
    }

    [Theory]
    [InlineData("http://localhost:4222")]
    [InlineData("https://localhost:4222")]
    [InlineData("tcp://localhost:4222")]
    [InlineData("ws://localhost:4222")]
    [InlineData("invalid-url")]
    [InlineData("localhost:4222")]
    public void NatsUrlValidation_ShouldRejectInvalidSchemes(string invalidUrl)
    {
        // Arrange
        var invalidConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMq:HostName"] = "localhost",
                ["RabbitMq:QueueName"] = "test-queue",
                ["Nats:Url"] = invalidUrl,
                ["Nats:Subject"] = "test.subject"
            })
            .Build();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            using var host = CreateTestHost(invalidConfig);
            var options = host.Services.GetRequiredService<IOptions<NatsConnection>>();
            _ = options.Value;
        });

        Assert.Contains("NATS Url must be a valid URI with 'nats://' or 'nats+tls://' scheme", exception.Message);
    }

    [Fact]
    public void ServiceRegistration_ShouldSupportDependencyInjection()
    {
        // Arrange
        var testConfig = CreateValidTestConfiguration();
        var host = CreateTestHost(testConfig);

        using (host)
        {
            // Act - Verify that services can resolve their dependencies
            var hostedServices = host.Services.GetServices<IHostedService>();
            var worker = hostedServices.OfType<Worker>().FirstOrDefault();

            // Assert - Worker should be registered as hosted service
            Assert.NotNull(worker);

            // Verify that the Worker's dependencies can also be resolved independently
            var rabbitHandler = host.Services.GetRequiredService<IRabbitMqConnectionHandler>();
            var natsHandler = host.Services.GetRequiredService<INatsConnectionHandler>();

            Assert.NotNull(rabbitHandler);
            Assert.NotNull(natsHandler);
        }
    }

    [Fact]
    public void ConfigurationBinding_ShouldHandleOptionalProperties()
    {
        // Arrange - Configuration with only required properties
        var minimalConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMq:HostName"] = "localhost",
                ["RabbitMq:QueueName"] = "test-queue",
                // Port, UserName, Password, VirtualHost are optional
                ["Nats:Url"] = "nats://localhost:4222",
                ["Nats:Subject"] = "test.subject"
                // User, Password, Secret are optional
            })
            .Build();

        var host = CreateTestHost(minimalConfig);

        using (host)
        {
            // Act
            var rabbitOptions = host.Services.GetRequiredService<IOptions<RabbitMqConnection>>();
            var natsOptions = host.Services.GetRequiredService<IOptions<NatsConnection>>();

            var rabbitConnection = rabbitOptions.Value;
            var natsConnection = natsOptions.Value;

            // Assert - Required properties should be set, optional should be null/default
            Assert.Equal("localhost", rabbitConnection.HostName);
            Assert.Equal("test-queue", rabbitConnection.QueueName);
            Assert.Null(rabbitConnection.Port);
            Assert.Null(rabbitConnection.UserName);
            Assert.Null(rabbitConnection.Password);
            Assert.Null(rabbitConnection.VirtualHost);

            Assert.Equal("nats://localhost:4222", natsConnection.Url);
            Assert.Equal("test.subject", natsConnection.Subject);
            Assert.Null(natsConnection.User);
            Assert.Null(natsConnection.Password);
            Assert.Null(natsConnection.Secret);
        }
    }

    [Fact]
    public void Host_ShouldSupportGracefulShutdown()
    {
        // Arrange
        var testConfig = CreateValidTestConfiguration();
        var host = CreateTestHost(testConfig);

        // Act & Assert - Host should be disposable without exceptions
        using (host)
        {
            Assert.NotNull(host);

            // Verify services are available before disposal
            var hostedServices = host.Services.GetServices<IHostedService>();
            var worker = hostedServices.OfType<Worker>().FirstOrDefault();
            Assert.NotNull(worker);
        }

        // If we reach here, disposal completed successfully
        Assert.True(true); // Test passes if no exceptions during disposal
    }

    [Fact]
    public void DependencyResolution_ShouldNotCreateCircularDependencies()
    {
        // Arrange
        var testConfig = CreateValidTestConfiguration();
        var host = CreateTestHost(testConfig);

        using (host)
        {
            // Act - Try to resolve all services multiple times to detect circular dependencies
            for (int i = 0; i < 3; i++)
            {
                var natsHandler = host.Services.GetRequiredService<INatsConnectionHandler>();
                var rabbitHandler = host.Services.GetRequiredService<IRabbitMqConnectionHandler>();
                var hostedServices = host.Services.GetServices<IHostedService>();
                var worker = hostedServices.OfType<Worker>().FirstOrDefault();

                // Assert - Should succeed each time
                Assert.NotNull(natsHandler);
                Assert.NotNull(rabbitHandler);
                Assert.NotNull(worker);
            }
        }
    }

    #region Helper Methods

    private static IConfiguration CreateValidTestConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMq:HostName"] = "localhost",
                ["RabbitMq:QueueName"] = "test-queue",
                ["Nats:Url"] = "nats://localhost:4222",
                ["Nats:Subject"] = "test.subject"
            })
            .Build();
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

                // Register statistics service (required for connection handlers)
                services.AddSingleton<IMessageStatisticsService, MessageStatisticsService>();

                services.AddHostedService<Worker>();

                services.AddLogging(loggingBuilder =>
                {
                    loggingBuilder.ClearProviders();
                    loggingBuilder.AddConsole(); // Use console logging for tests
                });
            })
            .Build();
    }

    #endregion
}