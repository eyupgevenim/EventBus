using Autofac;
using EventBus;
using EventBus.Abstractions;
using EventBus.ActiveMQ;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sample.Shared;
using System;
using System.Collections.Generic;

namespace Sample.ActiveMQ.Consumer
{
    class Program
    {
        const string APP_NAME = "ActiveMQ APP Consumer";
        static void Main(string[] args)
        {
            var services = new ServiceCollection() as IServiceCollection;
            ConfigureServices(services);

            while (true) System.Threading.Thread.Sleep(2000);
        }

        //docker run -d -p 61616:61616 -p 8161:8161 --name activemq rmohr/activemq 
        //docker run -p 61616:61616 -p 8161:8161 rmohr/activemq
        static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IActiveMQPersistentConnection>(sp =>
            {
                var config = new Dictionary<string, string>
                {
                    ["uri"] = Environment.GetEnvironmentVariable("ACTIVEMQ_URI"),//"tcp://localhost:61616"//docker run -p 61616:61616 -p 8161:8161 rmohr/activemq
                    ["topic"] = Environment.GetEnvironmentVariable("ACTIVEMQ_TOPIC"),//test-topic
                    ["username"] = Environment.GetEnvironmentVariable("ACTIVEMQ_USERNAME"),//admin
                    ["password"] = Environment.GetEnvironmentVariable("ACTIVEMQ_PASSWORD"),//admin
                };

                var logger = sp.GetRequiredService<ILogger<DefaultActiveMQPersistentConnection>>();
                return new DefaultActiveMQPersistentConnection(logger, config);
            });

            services.AddSingleton<IEventBus, EventBusActiveMQ>(sp =>
            {
                var activeMQPersistentConnection = sp.GetRequiredService<IActiveMQPersistentConnection>();
                var iLifetimeScope = sp.GetRequiredService<ILifetimeScope>();
                var logger = sp.GetRequiredService<ILogger<EventBusActiveMQ>>();
                var eventBusSubcriptionsManager = sp.GetRequiredService<IEventBusSubscriptionsManager>();

                return new EventBusActiveMQ(activeMQPersistentConnection, logger, iLifetimeScope, eventBusSubcriptionsManager);
            });

            services
                .AddEventBusConfigure()
                .AddIntegrationEventHandler();

            services
                .ServiceProviderBuild()
                .RunSampleConsumer(APP_NAME);
        }
    }
}
