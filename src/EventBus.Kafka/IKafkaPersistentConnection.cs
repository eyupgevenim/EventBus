using Confluent.Kafka;
using System;
using System.Threading;

namespace EventBus.Kafka
{
    public interface IKafkaPersistentConnection : IDisposable
    {
        string Topic { get; }
        IProducer<string, byte[]> CreateProducer();
        IConsumer<string, byte[]> CreateConsumer();
        CancellationToken ConsumerCancellationToken => default(CancellationToken);
    }
}
