/*
    RabbitGoingNats - Message Bridge Application

    This application acts as a bridge between RabbitMQ and NATS messaging systems.
    It consumes messages from a configured RabbitMQ queue and republishes them
    to a NATS subject, enabling message flow between these two messaging platforms.

    Key Features:
    - Real-time message consumption from RabbitMQ
    - Seamless republishing to NATS
    - Configurable connection parameters for both systems
    - Robust error handling and logging
    - Graceful shutdown support
    - AOT (Ahead of Time) compilation ready
*/

// Import necessary namespaces for the application
using RabbitGoingNats;                    // Main worker class
using RabbitGoingNats.Model;              // Configuration models (RabbitMqConnection, NatsConnection)
using RabbitGoingNats.Service;            // Service interfaces and implementations
using NLog.Extensions.Logging;            // NLog integration for structured logging

/*
   Copyright 2024 IDCT Bartosz Pachołek

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/

/*
    Application uses:
    NLog from https://www.nuget.org/packages/nLog/ under BSD-3 License.
    Copyright (c) 2004-2024 NLog Project - https://nlog-project.org/
    Source code has not been altered.

    RabbitMQ.Client from https://www.nuget.org/packages/RabbitMQ.Client/7.0.0-rc.12
    under Apache-2.0 OR MPL-2.0 license.
    Copyright (c) 2007-2024 Broadcom. All Rights Reserved. The term "Broadcom" refers to Broadcom Inc. and/or its subsidiaries.
    Source code has not been altered.

    NATS.Net from https://www.nuget.org/packages/NATS.Net
    under Apache-2.0 license
    Copyright © The NATS Authors 2016-2024
    Source code has not been altered.

    All licenses provided in LICENSES folder.
    Copied from: https://licenses.nuget.org/.
*/

// Create and configure the application host using .NET's generic host builder
// This provides dependency injection, logging, configuration, and hosting services
IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        // Get the configuration instance to access appsettings.json and other config sources
        var configuration = hostContext.Configuration;

        // === CONFIGURATION BINDING ===
        // Using source generators for AOT-compatible configuration binding
        // This eliminates reflection and improves startup performance in AOT scenarios
        services.Configure<RabbitMqConnection>(configuration.GetSection("RabbitMq"));
        services.Configure<NatsConnection>(configuration.GetSection("Nats"));
        services.Configure<WebServiceConfiguration>(configuration.GetSection("WebService"));

        // === CONFIGURATION VALIDATION ===
        // Validate RabbitMQ configuration at startup to fail fast if misconfigured
        // This prevents runtime errors and provides clear error messages
        services.PostConfigure<RabbitMqConnection>(options =>
        {
            // Ensure required RabbitMQ connection parameters are provided
            if (string.IsNullOrWhiteSpace(options.HostName))
                throw new InvalidOperationException("RabbitMQ HostName is required and cannot be empty");
            if (string.IsNullOrWhiteSpace(options.QueueName))
                throw new InvalidOperationException("RabbitMQ QueueName is required and cannot be empty");

            // Validate port range if specified (standard TCP port range)
            if (options.Port.HasValue && (options.Port <= 0 || options.Port > 65535))
                throw new InvalidOperationException("RabbitMQ Port must be between 1 and 65535");
        });

        // Validate NATS configuration at startup for the same reasons as above
        services.PostConfigure<NatsConnection>(options =>
        {
            // Ensure required NATS connection parameters are provided
            if (string.IsNullOrWhiteSpace(options.Url))
                throw new InvalidOperationException("NATS Url is required and cannot be empty");
            if (string.IsNullOrWhiteSpace(options.Subject))
                throw new InvalidOperationException("NATS Subject is required and cannot be empty");

            // Validate URL format and scheme to ensure it's a proper NATS URL
            if (!Uri.TryCreate(options.Url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "nats" && uri.Scheme != "nats+tls"))
                throw new InvalidOperationException("NATS Url must be a valid URI with 'nats://' or 'nats+tls://' scheme");
        });

        // Validate WebService configuration at startup
        services.PostConfigure<WebServiceConfiguration>(options =>
        {
            // Validate port range if web service is enabled
            if (options.Enabled && (options.Port <= 0 || options.Port > 65535))
                throw new InvalidOperationException("WebService Port must be between 1 and 65535");

            // Validate host if web service is enabled
            if (options.Enabled && string.IsNullOrWhiteSpace(options.Host))
                throw new InvalidOperationException("WebService Host is required when web service is enabled");
        });

        // === SERVICE REGISTRATION ===
        // Register messaging service implementations with their interfaces
        // Using Singleton lifetime because these manage persistent connections
        services.AddSingleton<INatsConnectionHandler, NatsConnectionHandler>();
        services.AddSingleton<IRabbitMqConnectionHandler, RabbitMqConnectionHandler>();

        // Register statistics and web monitoring services
        services.AddSingleton<IMessageStatisticsService, MessageStatisticsService>();

        // Register the main worker as a hosted service
        // This integrates with .NET's hosting infrastructure for lifecycle management
        services.AddHostedService<Worker>();

        // Register the web monitoring service as a hosted service
        // Note: WebMonitoringService implements both IWebMonitoringService and IHostedService
        services.AddHostedService<WebMonitoringService>();

        // === LOGGING CONFIGURATION ===
        // Configure structured logging using NLog
        // Clear default providers and use NLog for consistent logging across the application
        services.AddLogging(static loggingBuilder =>
            {
                loggingBuilder.ClearProviders();    // Remove console/debug providers
                loggingBuilder.AddNLog();            // Add NLog as the logging provider
            });
    })
    .Build();  // Build the configured host

// Start the application and run until shutdown is requested
// This will start all hosted services (including our Worker) and keep the application running
await host.RunAsync();
