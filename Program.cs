using RabbitGoingNats;
using RabbitGoingNats.Model;
using RabbitGoingNats.Service;
using NLog.Extensions.Logging;

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

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        var configuration = hostContext.Configuration;
        // Triggers AOT warnings, yet for NET 8+ workaround is actually not needed.
        services.Configure<RabbitMqConnection>(configuration.GetSection("RabbitMq"));
        services.Configure<NatsConnection>(configuration.GetSection("Nats"));
        
        // Add configuration validation
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
        services.AddLogging(static loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.AddNLog();
            });
    })
    .Build();

await host.RunAsync();
