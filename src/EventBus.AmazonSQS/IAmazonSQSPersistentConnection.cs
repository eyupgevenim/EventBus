using Amazon.SQS;
using System;
using System.Threading.Tasks;

namespace EventBus.AmazonSQS
{
    public interface IAmazonSQSPersistentConnection : IDisposable
    {
        IAmazonSQS SQSClient { get; }
        Task CreateQueueAsync(string awsQueueName, string awsDeadLetterQueueName, int awsQueueLongPollTimeSeconds, bool awsQueueIsFifo = false);
    }
}
