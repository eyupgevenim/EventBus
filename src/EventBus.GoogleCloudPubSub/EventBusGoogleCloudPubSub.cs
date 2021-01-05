using Autofac;
using EventBus.Abstractions;
using EventBus.Events;
using EventBus.Extensions;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

//https://googleapis.github.io/google-cloud-dotnet/docs/Google.Cloud.PubSub.V1/
namespace EventBus.GoogleCloudPubSub
{
    public class EventBusGoogleCloudPubSub : SubscribeProcessEvent, IEventBus, IDisposable
    {
        const string APP_NAME = "GoogleCloudPubSub";
        const string AUTOFAC_SCOPE_NAME = "e_event_bus";
        const string EVENT_NAME_ATTRIBUTE = "EVENT_NAME";

        private readonly IGoogleCloudPubSubPersistentConnection _persistentConnection;

        public EventBusGoogleCloudPubSub(IGoogleCloudPubSubPersistentConnection persistentConnection, 
            ILogger<EventBusGoogleCloudPubSub> logger,
            ILifetimeScope autofac, 
            IEventBusSubscriptionsManager subsManager)
            : base(logger, subsManager, autofac, appName: APP_NAME, autofacScopeName: AUTOFAC_SCOPE_NAME)
        {
            _persistentConnection = persistentConnection ?? throw new ArgumentNullException(nameof(persistentConnection));
            _subsManager.OnEventRemoved += SubsManager_OnEventRemoved;
        }

        private void SubsManager_OnEventRemoved(object sender, string eventName)
        {
            //TODO:..
        }

        public void Publish(IntegrationEvent @event)
        {
            var eventName = @event.GetType().Name;

            _logger.LogTrace("Creating GoogleCloudPubSub channel to publish event: {EventId} ({EventName})", @event.Id, eventName);

            Publisher(eventName, @event).Wait();
        }

        public void SubscribeDynamic<TH>(string eventName)
            where TH : IDynamicIntegrationEventHandler
        {
            _logger.LogInformation("Subscribing to dynamic event {EventName} with {EventHandler}", eventName, typeof(TH).GetGenericTypeName());

            _subsManager.AddDynamicSubscription<TH>(eventName);
            Consume().Wait();
        }

        public void Subscribe<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventName = _subsManager.GetEventKey<T>();

            _logger.LogInformation("Subscribing to event {EventName} with {EventHandler}", eventName, typeof(TH).GetGenericTypeName());

            _subsManager.AddSubscription<T, TH>();
            Consume().Wait();
        }

        public void Unsubscribe<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventName = _subsManager.GetEventKey<T>();

            _logger.LogInformation("Unsubscribing from event {EventName}", eventName);

            _subsManager.RemoveSubscription<T, TH>();
        }

        public void UnsubscribeDynamic<TH>(string eventName)
            where TH : IDynamicIntegrationEventHandler
        {
            _subsManager.RemoveDynamicSubscription<TH>(eventName);
        }

        public void Dispose()
        {
            if (_persistentConnection != null)
            {
                _persistentConnection.Dispose();
            }

            _subsManager.Clear();
        }

        private async Task Publisher(string eventName, IntegrationEvent @event)
        {
            await _persistentConnection.Pub(async publisher =>
            {
                var message = new PubsubMessage { Data = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(@event)) };
                message.Attributes.Add(EVENT_NAME_ATTRIBUTE, eventName);

                // PublishAsync() has various overloads. Here we're using the string overload.
                await publisher.PublishAsync(message);

                // PublisherClient instance should be shutdown after use.
                // The TimeSpan specifies for how long to attempt to publish locally queued messages.
                await publisher.ShutdownAsync(TimeSpan.FromSeconds(1));
            });
        }

        private async Task Consume()
        {
            _logger.LogTrace("Starting GoogleCloudPubSub basic consume");

            while (true)
            {
                await _persistentConnection.Sub(subscriber =>
                {
                    return subscriber.StartAsync((msg, cancellationToken) =>
                    {
                        _logger.LogInformation($"GoogleCloudPubSub Received message id {msg.MessageId} published at {msg.PublishTime.ToDateTime()}");

                        var eventName = msg.Attributes[EVENT_NAME_ATTRIBUTE];
                        var message = msg.Data.ToStringUtf8();

                        try
                        {
                            if (message.ToLowerInvariant().Contains("throw-fake-exception"))
                            {
                                throw new InvalidOperationException($"Fake exception requested: \"{message}\"");
                            }

                            base.ProcessEvent(eventName, message).Wait();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "----- ERROR Processing message \"{Message}\"", message);
                        }

                        // Stop this subscriber after one message is received.
                        // This is non-blocking, and the returned Task may be awaited.
                        subscriber.StopAsync(TimeSpan.FromSeconds(1));
                        // Return Reply.Ack to indicate this message has been handled.
                        return Task.FromResult(SubscriberClient.Reply.Ack);
                    });
                });

                System.Threading.Thread.Sleep(200);
            }
        }

    }
}
