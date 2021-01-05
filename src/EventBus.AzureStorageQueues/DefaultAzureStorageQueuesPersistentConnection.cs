using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace EventBus.AzureStorageQueues
{
    public class DefaultAzureStorageQueuesPersistentConnection : IAzureStorageQueuesPersistentConnection
    {
        private readonly ILogger<DefaultAzureStorageQueuesPersistentConnection> _logger;
        private readonly string _connectionString;

        bool _disposed;

        public DefaultAzureStorageQueuesPersistentConnection(ILogger<DefaultAzureStorageQueuesPersistentConnection> logger, string connectionString)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connectionString = connectionString;
        }

        public QueueClient GetQueueClient(string queueName)
        {
            return new QueueClient(_connectionString, queueName);
        }

        public async Task CreateQueue(string queueName)
        {
            try
            {
                // Try to create a queue that already exists
                var queueClient = new QueueClient(_connectionString, queueName);
                await queueClient.CreateAsync();
            }
            catch (RequestFailedException ex) when (ex.ErrorCode == QueueErrorCode.QueueAlreadyExists)
            {
                _logger.LogError(ex, ex.Message);

                // Ignore any errors if the queue already exists
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
        }
    }
}
