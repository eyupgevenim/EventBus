using Apache.NMS;
using System;

namespace EventBus.ActiveMQ
{
    public interface IActiveMQPersistentConnection : IDisposable
    {
        (IMessageProducer, ISession) Producer { get; }
        IMessageConsumer Consumer { get; }
    }
}
