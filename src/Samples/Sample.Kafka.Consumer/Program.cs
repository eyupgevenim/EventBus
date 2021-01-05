using Autofac;
using EventBus;
using EventBus.Abstractions;
using EventBus.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sample.Shared;
using System;
using System.Collections.Generic;

namespace Sample.Kafka.Consumer
{
    class Program
    {
        const string APP_NAME = "Kafka APP Consumer";
        static void Main(string[] args)
        {
            var services = new ServiceCollection() as IServiceCollection;
            ConfigureServices(services);

            while (true) System.Threading.Thread.Sleep(2000);
        }

        static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IKafkaPersistentConnection>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<DefaultKafkaPersistentConnection>>();
                var clientConfig = new Dictionary<string, string>
                {
                    ["bootstrap.servers"] = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS"),
                    ["topic"] = Environment.GetEnvironmentVariable("KAFKA_TOPIC"),
                    ["group.id"] = Environment.GetEnvironmentVariable("KAFKA_GROUP_ID"),

                    /**
                    ["auto.commit.interval.ms"] = Environment.GetEnvironmentVariable("KAFKA_AUTO_COMMIT_INTERVAL_MS"),
                    ["auto.offset.reset"] = Environment.GetEnvironmentVariable("KAFKA_AUTO_OFFSET_RESET"),

                    ["enable.auto.commit"] = Environment.GetEnvironmentVariable("KAFKA_ENABLE_AUTO_COMMIT"),
                    ["statistics.interval.ms"] = Environment.GetEnvironmentVariable("KAFKA_STATISTICS_INTERVAL_MS"),
                    ["session.timeout.ms"] = Environment.GetEnvironmentVariable("KAFKA_SESSION_TIMEOUT_MS"),
                    ["enable.partition.eof"] = Environment.GetEnvironmentVariable("KAFKA_ENABLE_PARTITION_EOF"),

                    ["sasl.username"] = Environment.GetEnvironmentVariable("KAFKA_SASL_USERNAME"),
                    ["sasl.password"] = Environment.GetEnvironmentVariable("KAFKA_SASL_PASSWORD"),
                    ["sasl.mechanism"] = Environment.GetEnvironmentVariable("KAFKA_SASL_MECHANISM"),
                    ["security.protocol"] = Environment.GetEnvironmentVariable("KAFKA_SECURITY_PROTOCOL"),
                    ["debug"] = Environment.GetEnvironmentVariable("KAFKA_DUBUG"),
                    */
                };

                return new DefaultKafkaPersistentConnection(clientConfig, logger);
            });

            services.AddSingleton<IEventBus, EventBusKafka>(sp =>
            {
                var kafkaPersistentConnection = sp.GetRequiredService<IKafkaPersistentConnection>();
                var logger = sp.GetRequiredService<ILogger<EventBusKafka>>();
                var iLifetimeScope = sp.GetRequiredService<ILifetimeScope>();
                var eventBusSubcriptionsManager = sp.GetRequiredService<IEventBusSubscriptionsManager>();

                return new EventBusKafka(kafkaPersistentConnection, logger, iLifetimeScope, eventBusSubcriptionsManager);
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
