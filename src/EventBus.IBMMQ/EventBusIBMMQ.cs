using Autofac;
using EventBus.Abstractions;
using EventBus.Events;
using EventBus.Extensions;
using IBM.XMS;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace EventBus.IBMMQ
{
    public class EventBusIBMMQ : SubscribeProcessEvent, IEventBus, IDisposable
    {
        const string APP_NAME = "IBMMQ";
        const string AUTOFAC_SCOPE_NAME = "e_event_bus";

        private readonly IIBMMQPersistentConnection _persistentConnection;

        public EventBusIBMMQ(IIBMMQPersistentConnection persistentConnection, 
            ILogger<EventBusIBMMQ> logger,
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

            _logger.LogTrace("Creating IBMMQ channel to publish event: {EventId} ({EventName})", @event.Id, eventName);

            _persistentConnection.MessageProducer((producer, session) =>
            {
                var message = JsonConvert.SerializeObject(new { type = eventName, body = @event });
                _logger.LogTrace("Publishing event to IBMMQ: {EventId}", @event.Id);
                var textMessage = session.CreateTextMessage();
                textMessage.Text = message;
                producer.Send(textMessage);
            });
        }

        public void SubscribeDynamic<TH>(string eventName)
            where TH : IDynamicIntegrationEventHandler
        {
            _logger.LogInformation("Subscribing to dynamic event {EventName} with {EventHandler}", eventName, typeof(TH).GetGenericTypeName());

            _subsManager.AddDynamicSubscription<TH>(eventName);
            Consumer().Wait();
        }

        public void Subscribe<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventName = _subsManager.GetEventKey<T>();
            
            _logger.LogInformation("Subscribing to event {EventName} with {EventHandler}", eventName, typeof(TH).GetGenericTypeName());

            _subsManager.AddSubscription<T, TH>();

            Consumer().Wait();
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

        private async Task Consumer()
        {
            while (true)
            {
                try
                {
                    await _persistentConnection.MessageConsumer(async consumer =>
                    {
                        var message = consumer.Receive(20000);
                        if (message != null)
                        {
                            var textMessage = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>((message as ITextMessage).Text);
                            string messageBody = textMessage.GetValue("body").ToString();
                            string eventName = textMessage.GetValue("type").ToString();

                            await base.ProcessEvent(eventName, messageBody);
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"----- ERROR Processing message {ex.Message}");
                }

                System.Threading.Thread.Sleep(100);
            }

        }


        public void Dispose()
        {
            _persistentConnection?.Dispose();
            _subsManager.Clear();
        }

    }
}
