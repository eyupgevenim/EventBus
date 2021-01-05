using Autofac;
using Azure.Storage.Queues.Models;
using EventBus.Abstractions;
using EventBus.Events;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace EventBus.AzureStorageQueues
{
    public class EventBusAzureStorageQueues : SubscribeProcessEvent, IEventBus, IDisposable
    {
        const string APP_NAME = "AzureStorageQueues";
        const string AUTOFAC_SCOPE_NAME = "e_event_bus";

        private readonly IAzureStorageQueuesPersistentConnection _azureStorageQueuesPersisterConnection;
        private readonly string _queueName;

        public EventBusAzureStorageQueues(IAzureStorageQueuesPersistentConnection azureStorageQueuesPersisterConnection,
            ILogger<EventBusAzureStorageQueues> logger, 
            IEventBusSubscriptionsManager subsManager, 
            ILifetimeScope autofac, 
            string queueName)
            : base(logger, subsManager, autofac, appName: APP_NAME, autofacScopeName: AUTOFAC_SCOPE_NAME)
        {
            _azureStorageQueuesPersisterConnection = azureStorageQueuesPersisterConnection;
            _queueName = queueName;

            _azureStorageQueuesPersisterConnection.CreateQueue(_queueName);
        }

        public void Publish(IntegrationEvent @event)
        {
            var eventName = @event.GetType().Name;
            var jsonMessage = JsonConvert.SerializeObject(@event);
            var messageData = new MessageData { Type = eventName, Body = jsonMessage };

            var queueClient = _azureStorageQueuesPersisterConnection.GetQueueClient(_queueName);
            queueClient.SendMessageAsync(JsonConvert.SerializeObject(messageData));
        }

        public void SubscribeDynamic<TH>(string eventName)
            where TH : IDynamicIntegrationEventHandler
        {
            _logger.LogInformation("Subscribing to dynamic event {EventName} with {EventHandler}", eventName, typeof(TH).Name);

            _subsManager.AddDynamicSubscription<TH>(eventName);
        }

        public void Subscribe<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventName = typeof(T).Name;

            var containsKey = _subsManager.HasSubscriptionsForEvent<T>();
            if (!containsKey)
            {
                //TODO:...
            }

            _logger.LogInformation("Subscribing to event {EventName} with {EventHandler}", eventName, typeof(TH).Name);

            _subsManager.AddSubscription<T, TH>();

            Consume().Wait();
        }

        public void Unsubscribe<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventName = typeof(T).Name;

            //TODO:...

            _logger.LogInformation("Unsubscribing from event {EventName}", eventName);

            _subsManager.RemoveSubscription<T, TH>();
        }

        public void UnsubscribeDynamic<TH>(string eventName)
            where TH : IDynamicIntegrationEventHandler
        {
            _logger.LogInformation("Unsubscribing from dynamic event {EventName}", eventName);

            _subsManager.RemoveDynamicSubscription<TH>(eventName);
        }

        public void Dispose()
        {
            _subsManager.Clear();
        }

        private async Task Consume()
        {
            var queueClient = _azureStorageQueuesPersisterConnection.GetQueueClient(_queueName);

            while (true)
            {
                // Get the next messages from the queue
                foreach (QueueMessage message in (await queueClient.ReceiveMessagesAsync(maxMessages: 10)).Value)
                {
                    // "Process" the message
                    var messageData = message.Body.ToObjectFromJson<MessageData>();
                    await base.ProcessEvent(messageData.Type, messageData.Body);

                    // Let the service know we're finished with the message and
                    // it can be safely deleted.
                    await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt);
                }

                System.Threading.Thread.Sleep(200);
            }
        }

        class MessageData
        {
            public string Type { get; set; }
            public string Body { get; set; }
        }

    }
}
