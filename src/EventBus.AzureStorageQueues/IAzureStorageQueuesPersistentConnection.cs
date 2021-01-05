using Azure.Storage.Queues;
using System;
using System.Threading.Tasks;

namespace EventBus.AzureStorageQueues
{
    public interface IAzureStorageQueuesPersistentConnection : IDisposable
    {
        QueueClient GetQueueClient(string queueName);
        Task CreateQueue(string queueName);
    }
}
