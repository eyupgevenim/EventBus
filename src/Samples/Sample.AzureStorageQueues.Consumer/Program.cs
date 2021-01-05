using Autofac;
using EventBus;
using EventBus.Abstractions;
using EventBus.AzureStorageQueues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sample.Shared;
using System;

namespace Sample.AzureStorageQueues.Consumer
{
    class Program
    {
        const string APP_NAME = "AzureStorageQueues APP Consumer";
        static void Main(string[] args)
        {
            var services = new ServiceCollection() as IServiceCollection;
            ConfigureServices(services);

            while (true) System.Threading.Thread.Sleep(2000);
        }

        static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IAzureStorageQueuesPersistentConnection>(sp =>
            {
                var connectionString = Environment.GetEnvironmentVariable("AZURE_SQ_CONNECTIONSTRING");
                var logger = sp.GetRequiredService<ILogger<DefaultAzureStorageQueuesPersistentConnection>>();
                return new DefaultAzureStorageQueuesPersistentConnection(logger, connectionString);
            });

            services.AddSingleton<IEventBus, EventBusAzureStorageQueues>(sp =>
            {
                var queueName = Environment.GetEnvironmentVariable("AZURE_SQ_QUEUENAME");
                var persistentConnection = sp.GetRequiredService<IAzureStorageQueuesPersistentConnection>();
                var iLifetimeScope = sp.GetRequiredService<ILifetimeScope>();
                var logger = sp.GetRequiredService<ILogger<EventBusAzureStorageQueues>>();
                var eventBusSubcriptionsManager = sp.GetRequiredService<IEventBusSubscriptionsManager>();

                return new EventBusAzureStorageQueues(persistentConnection, logger, eventBusSubcriptionsManager, iLifetimeScope, queueName);
            });

            services
                .AddEventBusConfigure()
                .AddIntegrationEventHandler();

            services
                .ServiceProviderBuild()
                .RunSampleConsumer(APP_NAME);
        }
    }

    /**
    //https://docs.microsoft.com/en-us/azure/storage/common/storage-use-azurite?toc=/azure/storage/blobs/toc.json
    //https://docs.microsoft.com/en-us/azure/storage/common/storage-use-emulator
    //https://hub.docker.com/r/microsoft/azure-storage-emulator/
    //docker run -p 10000:10000 -p 10001:10001 -p 10002:10002 microsoft/azure-storage-emulator
    static string GenerateConnStr(string ip = "127.0.0.1", int blobport = 10000, int queueport = 10001, int tableport = 10002)
    {
        return $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://{ip}:{blobport}/devstoreaccount1;TableEndpoint=http://{ip}:{tableport}/devstoreaccount1;QueueEndpoint=http://{ip}:{queueport}/devstoreaccount1;";
    }

    //...
    var cloudStorageAccount = CloudStorageAccount.Parse(GenerateConnStr());
    // ...
   */
}
