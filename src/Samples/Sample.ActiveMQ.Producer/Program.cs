using Autofac;
using EventBus;
using EventBus.Abstractions;
using EventBus.ActiveMQ;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sample.Shared;
using System;
using System.Collections.Generic;

namespace Sample.ActiveMQ.Producer
{
    class Program
    {
        const string APP_NAME = "ActiveMQ APP Producer";
        static void Main(string[] args)
        {
            var services = new ServiceCollection() as IServiceCollection;
            ConfigureServices(services);

            while (true) System.Threading.Thread.Sleep(2000);
        }

        //docker run -d -p 61616:61616 -p 8161:8161 --name activemq rmohr/activemq      //tcp://activemq:61616
        //docker run -p 61616:61616 -p 8161:8161 rmohr/activemq                         //tcp://localhost:61616
        static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IActiveMQPersistentConnection>(sp =>
            {
                var config = new Dictionary<string, string>
                {
                    ["username"] = Environment.GetEnvironmentVariable("ACTIVEMQ_USERNAME"),//admin
                    ["password"] = Environment.GetEnvironmentVariable("ACTIVEMQ_PASSWORD"),//admin
                    ["uri"] = Environment.GetEnvironmentVariable("ACTIVEMQ_URI"),//"tcp://localhost:61616"//docker run -p 61616:61616 -p 8161:8161 rmohr/activemq
                    ["topic"] = Environment.GetEnvironmentVariable("ACTIVEMQ_TOPIC"),//"test-topic"
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

            services.AddEventBusConfigure();
            services.ServiceProviderBuild().RunSampleProducer(APP_NAME);
        }
    }
}
