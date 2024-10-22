using RabbitGoingNats;
using RabbitGoingNats.Model;
using NLog.Extensions.Logging;

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

    Main application Rabbit-Going-Nats is licenses under MIT license.
    Creator of the application gives all the credits to the authors of the libraries
    used in this software and claims no ownership to those parts.
*/

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        var configuration = hostContext.Configuration;
        // Triggers AOT warnings, yet for NET 8+ workaround is actually not needed.
        services.Configure<RabbitMqConnection>(configuration.GetSection("RabbitMq"));
        services.Configure<NatsConnection>(configuration.GetSection("Nats"));
        services.AddHostedService<Worker>();
        services.AddLogging(static loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.AddNLog();
            });
    })
    .Build();

host.Run();
