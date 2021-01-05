using Apache.NMS;
using Autofac;
using EventBus.Abstractions;
using EventBus.Events;
using EventBus.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using System;
using System.Net.Sockets;
using System.Text;

namespace EventBus.ActiveMQ
{
    public class EventBusActiveMQ : SubscribeProcessEvent, IEventBus, IDisposable
    {
        const string APP_NAME = "ActiveMQ";
        const string AUTOFAC_SCOPE_NAME = "e_event_bus";

        private readonly IActiveMQPersistentConnection _persistentConnection;
        private readonly int _retryCount = 5;

        public EventBusActiveMQ(IActiveMQPersistentConnection persistentConnection, 
            ILogger<EventBusActiveMQ> logger,
            ILifetimeScope autofac, 
            IEventBusSubscriptionsManager subsManager)
            : base(logger, subsManager, autofac, appName: APP_NAME, autofacScopeName: AUTOFAC_SCOPE_NAME)
        {
            _persistentConnection = persistentConnection ?? throw new ArgumentNullException(nameof(persistentConnection));
            _subsManager.OnEventRemoved += SubsManager_OnEventRemoved;
        }

        private void SubsManager_OnEventRemoved(object sender, string eventName)
        {
            //TODO:....
        }

        public void Publish(IntegrationEvent @event)
        {
            var eventName = @event.GetType().Name;

            _logger.LogTrace("Creating ActiveMQ channel to publish event: {EventId} ({EventName})", @event.Id, eventName);

            var (messageProducer, session) = _persistentConnection.Producer;
            var message = JsonConvert.SerializeObject(@event);
            var body = Encoding.UTF8.GetBytes(message);

            _logger.LogTrace("Publishing event to ActiveMQ: {EventId}", @event.Id);

            var bytesMessage = session.CreateBytesMessage();
            bytesMessage.NMSCorrelationID = eventName;
            bytesMessage.WriteBytes(body);
            messageProducer.Send(bytesMessage);
        }

        public void SubscribeDynamic<TH>(string eventName)
            where TH : IDynamicIntegrationEventHandler
        {
            _logger.LogInformation("Subscribing to dynamic event {EventName} with {EventHandler}", eventName, typeof(TH).GetGenericTypeName());

            _subsManager.AddDynamicSubscription<TH>(eventName);
            Consumer();
        }

        public void Subscribe<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventName = _subsManager.GetEventKey<T>();
            
            _logger.LogInformation("Subscribing to event {EventName} with {EventHandler}", eventName, typeof(TH).GetGenericTypeName());

            _subsManager.AddSubscription<T, TH>();

            Consumer();
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
            _persistentConnection?.Dispose();
            _subsManager.Clear();
        }

        private void Consumer()
        {
            var policy = RetryPolicy
                .Handle<Apache.NMS.NMSConnectionException>()
                .Or<SocketException>()
                .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                {
                    _logger.LogError(ex, "Could not consumer after {Timeout}s ({ExceptionMessage})", $"{time.TotalSeconds:n1}", ex.Message);
                });

            policy.Execute(() =>
            {
                var consumer = _persistentConnection.Consumer;
                consumer.Listener += new MessageListener(OnMessage);
            });

            /**
            try
            {
                var consumer = _persistentConnection.Consumer;
                consumer.Listener += new MessageListener(OnMessage);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"----- ERROR Processing message {ex.Message}");
            }
            */
        }

        private void OnMessage(IMessage message)
        {
            var qMessage = message as Apache.NMS.ActiveMQ.Commands.Message;

            var eventName = qMessage.CorrelationId;
            var body = Encoding.UTF8.GetString(qMessage.Content);

            base.ProcessEvent(eventName, body).Wait();
        }

    }
}
