using Autofac;
using EventBus;
using EventBus.Abstractions;
using EventBus.GoogleCloudPubSub;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sample.Shared;
using System;
using System.Collections.Generic;

namespace Sample.GoogleCloudPubSub.Producer
{
    class Program
    {
        const string APP_NAME = "GoogleCloudPubSub APP Producer";
        static void Main(string[] args)
        {
            var services = new ServiceCollection() as IServiceCollection;
            ConfigureServices(services);

            while (true) System.Threading.Thread.Sleep(200);
        }

        //docker run -it --rm -p "8085:8085" -e PUBSUB_PROJECT_ID=my-project google/cloud-sdk gcloud beta emulators pubsub start --host-port=0.0.0.0:8085
        static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IGoogleCloudPubSubPersistentConnection>(sp =>
            {
                var config = new Dictionary<string, string>
                {
                    ["projectid"] = Environment.GetEnvironmentVariable("PUBSUB_PROJECT_ID"),//"my-project"
                    ["topicid"] = Environment.GetEnvironmentVariable("PUBSUB_TOPIC_ID"),//"my-topic"
                    ["subscriptionid"] = Environment.GetEnvironmentVariable("PUBSUB_SUBSCRIPTION_ID"),//"my-sub"
                    ["pubsub.emulator.host"] = Environment.GetEnvironmentVariable("PUBSUB_EMULATOR_HOST"),//string.Empty for prod
                };

                var logger = sp.GetRequiredService<ILogger<DefaultGoogleCloudPubSubPersistentConnection>>();
                return new DefaultGoogleCloudPubSubPersistentConnection(logger, config);
            });

            services.AddSingleton<IEventBus, EventBusGoogleCloudPubSub>(sp =>
            {
                var persistentConnection = sp.GetRequiredService<IGoogleCloudPubSubPersistentConnection>();
                var iLifetimeScope = sp.GetRequiredService<ILifetimeScope>();
                var logger = sp.GetRequiredService<ILogger<EventBusGoogleCloudPubSub>>();
                var eventBusSubcriptionsManager = sp.GetRequiredService<IEventBusSubscriptionsManager>();

                return new EventBusGoogleCloudPubSub(persistentConnection, logger, iLifetimeScope, eventBusSubcriptionsManager);
            });

            services.AddEventBusConfigure();
            services.ServiceProviderBuild().RunSampleProducer(APP_NAME);
        }
    }
}
