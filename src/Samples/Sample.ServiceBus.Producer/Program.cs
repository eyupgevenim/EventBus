using Autofac;
using EventBus;
using EventBus.Abstractions;
using EventBus.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sample.Shared;
using System;

// https://docs.microsoft.com/en-US/azure/service-bus-messaging/service-bus-quickstart-topics-subscriptions-portal#get-the-connection-string
// create service-bus https://portal.azure.com
// https://github.com/Azure/azure-service-bus/issues/223
namespace Sample.ServiceBus.Producer
{
    class Program
    {
        const string APP_NAME = "ServiceBus APP Producer";
        static void Main(string[] args)
        {
            var services = new ServiceCollection() as IServiceCollection;
            ConfigureServices(services);

            while (true) System.Threading.Thread.Sleep(2000);
        }

        static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IServiceBusPersisterConnection>(sp =>
            {
                var eventBusConnection = Environment.GetEnvironmentVariable("SERVICEBUS_CONNECTIONSTRING");//"Endpoint=sb://{your_bus}.servicebus.windows.net/;SharedAccessKeyName=Root;SharedAccessKey={your_topic_policy_key};EntityPath=e_event_bus;";
                var logger = sp.GetRequiredService<ILogger<DefaultServiceBusPersisterConnection>>();
                return new DefaultServiceBusPersisterConnection(logger, eventBusConnection);
            });

            services.AddSingleton<IEventBus, EventBusServiceBus>(sp =>
            {
                var subscriptionClientName = Environment.GetEnvironmentVariable("SERVICEBUS_SUBSCRIPTIONNAME");//"your-subscription";
                var persistentConnection = sp.GetRequiredService<IServiceBusPersisterConnection>();
                var iLifetimeScope = sp.GetRequiredService<ILifetimeScope>();
                var logger = sp.GetRequiredService<ILogger<EventBusServiceBus>>();
                var eventBusSubcriptionsManager = sp.GetRequiredService<IEventBusSubscriptionsManager>();

                return new EventBusServiceBus(persistentConnection, logger, eventBusSubcriptionsManager, iLifetimeScope, subscriptionClientName);
            });

            services.AddEventBusConfigure();
            services.ServiceProviderBuild().RunSampleProducer(APP_NAME);
        }
    }
}
