using Autofac;
using EventBus;
using EventBus.Abstractions;
using EventBus.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Sample.Shared;
using System;

namespace Sample.RabbitMQ.Consumer
{
    class Program
    {
        const string APP_NAME = "RabbitMQ APP Consumer";
        static void Main(string[] args)
        {
            var services = new ServiceCollection() as IServiceCollection;
            ConfigureServices(services);

            while (true) System.Threading.Thread.Sleep(2000);
        }

        static void ConfigureServices(IServiceCollection services)
        {
            int RetryCount = 5;

            services.AddSingleton<IRabbitMQPersistentConnection>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<DefaultRabbitMQPersistentConnection>>();
                var factory = new ConnectionFactory()
                {
                    DispatchConsumersAsync = true,
                    HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOSTNAME"),//localhost, rabbitmq
                    UserName = Environment.GetEnvironmentVariable("RABBITMQ_DEFAULT_USER"),
                    Password = Environment.GetEnvironmentVariable("RABBITMQ_DEFAULT_PASS")
                };

                return new DefaultRabbitMQPersistentConnection(factory, logger, RetryCount);
            });

            services.AddSingleton<IEventBus, EventBusRabbitMQ>(sp =>
            {
                string subscriptionClientName = Environment.GetEnvironmentVariable("RABBITMQ_SUBSCRIPTION_CLIENTNAME");
                var rabbitMQPersistentConnection = sp.GetRequiredService<IRabbitMQPersistentConnection>();
                var iLifetimeScope = sp.GetRequiredService<ILifetimeScope>();
                var logger = sp.GetRequiredService<ILogger<EventBusRabbitMQ>>();
                var eventBusSubcriptionsManager = sp.GetRequiredService<IEventBusSubscriptionsManager>();

                return new EventBusRabbitMQ(rabbitMQPersistentConnection, logger, iLifetimeScope, eventBusSubcriptionsManager, subscriptionClientName, RetryCount);
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
