using Autofac;
using EventBus;
using EventBus.Abstractions;
using EventBus.IBMMQ;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sample.Shared;
using System;
using System.Collections.Generic;

namespace Sample.IBMMQ.Producer
{
    class Program
    {
        const string APP_NAME = "IBMMQ APP Producer";
        static void Main(string[] args)
        {
            var services = new ServiceCollection() as IServiceCollection;
            ConfigureServices(services);

            while (true) System.Threading.Thread.Sleep(2000);
        }

        //docker run --env LICENSE=accept --env MQ_QMGR_NAME=QM1 --publish 1414:1414 --publish 9443:9443 ibmcom/mq
        static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IIBMMQPersistentConnection>(sp =>
            {
                var config = new Dictionary<string, string>
                {
                    ["userid"] = Environment.GetEnvironmentVariable("IBMMQ_USERID"),//app
                    ["password"] = Environment.GetEnvironmentVariable("IBMMQ_PASSWORD"),//passw0rd

                    ["topic"] = Environment.GetEnvironmentVariable("IBMMQ_TOPIC"),//"dev/"
                    ["host"] = Environment.GetEnvironmentVariable("IBMMQ_HOST"),//"localhost"
                    ["port"] = Environment.GetEnvironmentVariable("IBMMQ_PORT"),// 1414
                    ["channel"] = Environment.GetEnvironmentVariable("IBMMQ_CHANNEL"),//"DEV.APP.SVRCONN"
                    ["qmgr"] = Environment.GetEnvironmentVariable("IBMMQ_QMGR"),//"*QM1TLS"
                };

                var logger = sp.GetRequiredService<ILogger<DefaultIBMMQPersistentConnection>>();
                return new DefaultIBMMQPersistentConnection(logger, config);
            });

            services.AddSingleton<IEventBus, EventBusIBMMQ>(sp =>
            {
                var persistentConnection = sp.GetRequiredService<IIBMMQPersistentConnection>();
                var iLifetimeScope = sp.GetRequiredService<ILifetimeScope>();
                var logger = sp.GetRequiredService<ILogger<EventBusIBMMQ>>();
                var eventBusSubcriptionsManager = sp.GetRequiredService<IEventBusSubscriptionsManager>();

                return new EventBusIBMMQ(persistentConnection, logger, iLifetimeScope, eventBusSubcriptionsManager);
            });

            services.AddEventBusConfigure();
            services.ServiceProviderBuild().RunSampleProducer(APP_NAME);
        }
    }
}
