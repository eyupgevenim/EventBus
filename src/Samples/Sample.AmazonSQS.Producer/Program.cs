using Autofac;
using EventBus;
using EventBus.Abstractions;
using EventBus.AmazonSQS;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sample.Shared;
using System;
using System.Collections.Generic;

namespace Sample.AmazonSQS.Producer
{
    class Program
    {
        const string APP_NAME = "AmazonSQS APP Producer";
        static void Main(string[] args)
        {
            var services = new ServiceCollection() as IServiceCollection;
            ConfigureServices(services);

            while (true) System.Threading.Thread.Sleep(200);
        }

        //https://medium.com/swlh/testing-aws-sqs-locally-in-node-js-a79545cf4506
        //docker run -p 9324:9324 softwaremill/elasticmq
        static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IAmazonSQSPersistentConnection>(sp =>
            {
                var config = new Dictionary<string, string>
                {
                    ["region"] = Environment.GetEnvironmentVariable("AMAZON_SQS_REGION"),//"REGION"
                    ["access.key.id"] = Environment.GetEnvironmentVariable("AMAZON_SQS_ACCESSKEYID"),//"na"
                    ["secret.access.key"] = Environment.GetEnvironmentVariable("AMAZON_SQS_SECRET_ACCESSKEY"),//"na"
                    ["endpoint"] = Environment.GetEnvironmentVariable("AMAZON_SQS_ENDPOINT"),//"http://localhost:9324"
                };

                var logger = sp.GetRequiredService<ILogger<DefaultAmazonSQSPersistentConnection>>();
                return new DefaultAmazonSQSPersistentConnection(logger, config);
            });

            services.AddSingleton<IEventBus, EventBusAmazonSQS>(sp =>
            {
                var queueName = Environment.GetEnvironmentVariable("AMAZON_SQS_QUEUENAME");//"test-queue-2"

                int awsQueueLongPollTimeSeconds;
                if (!int.TryParse(Environment.GetEnvironmentVariable("AMAZON_SQS_LONG_POLL_TIME_SEC") ?? string.Empty, out awsQueueLongPollTimeSeconds))
                {
                    awsQueueLongPollTimeSeconds = 15;
                }

                var persistentConnection = sp.GetRequiredService<IAmazonSQSPersistentConnection>();
                var iLifetimeScope = sp.GetRequiredService<ILifetimeScope>();
                var logger = sp.GetRequiredService<ILogger<EventBusAmazonSQS>>();
                var eventBusSubcriptionsManager = sp.GetRequiredService<IEventBusSubscriptionsManager>();

                return new EventBusAmazonSQS(persistentConnection, logger, iLifetimeScope, eventBusSubcriptionsManager, awsQueueLongPollTimeSeconds: awsQueueLongPollTimeSeconds, queueName: queueName);
            });

            services.AddEventBusConfigure();
            services.ServiceProviderBuild().RunSampleProducer(APP_NAME);
        }
    }
}
