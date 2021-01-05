using Google.Cloud.PubSub.V1;
using System;
using System.Threading.Tasks;

namespace EventBus.GoogleCloudPubSub
{
    public interface IGoogleCloudPubSubPersistentConnection : IDisposable
    {
        Task Pub(Action<PublisherClient> publisheHandler);
        Task Sub(Func<SubscriberClient, Task> subscribeHandler);
    }
}
