using IBM.XMS;
using System;
using System.Threading.Tasks;

namespace EventBus.IBMMQ
{
    public interface IIBMMQPersistentConnection : IDisposable
    {
        void MessageProducer(Action<IMessageProducer, ISession> producer);
        Task MessageConsumer(Action<IMessageConsumer> consumer);
    }
}
