using Autofac;
using Confluent.Kafka;
using EventBus.Abstractions;
using EventBus.Events;
using EventBus.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading;

namespace EventBus.Kafka
{
    public class EventBusKafka : SubscribeProcessEvent, IEventBus, IDisposable
    {
        const string APP_NAME = "Kafka";
        const string AUTOFAC_SCOPE_NAME = "e_event_bus";

        private readonly IKafkaPersistentConnection _persistentConnection;

        public EventBusKafka(IKafkaPersistentConnection persistentConnection,
            ILogger<EventBusKafka> logger,
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

            _logger.LogTrace("Creating Kafka channel to publish event: {EventId} ({EventName})", @event.Id, eventName);

            var message = JsonConvert.SerializeObject(@event);
            var body = Encoding.UTF8.GetBytes(message);

            using (var producer = _persistentConnection.CreateProducer())
            {
                var task = producer.ProduceAsync(_persistentConnection.Topic, new Message<string, byte[]> { Key = eventName, Value = body });
                task.ContinueWith(response =>
                {
                    if (response.IsCompletedSuccessfully)
                    {
                        _logger.LogInformation($"event: { @event.Id} wrote to offset: {response.Result.Offset}");
                    }
                    else//if (response.IsFaulted)
                    {
                        _logger.LogError($"event: { @event.Id} error Message: {response.Result.Message} || {{ResponseException}}", response.Exception);
                    }
                });
                producer.Flush(TimeSpan.FromMilliseconds(100));
            }
        }

        public void Subscribe<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventName = _subsManager.GetEventKey<T>();
           
            _logger.LogInformation("Subscribing to event {EventName} with {EventHandler}", eventName, typeof(TH).GetGenericTypeName());

            _subsManager.AddSubscription<T, TH>();

            Consume(_persistentConnection.ConsumerCancellationToken);
        }

        public void SubscribeDynamic<TH>(string eventName) 
            where TH : IDynamicIntegrationEventHandler
        {
            _logger.LogInformation("Subscribing to dynamic event {EventName} with {EventHandler}", eventName, typeof(TH).GetGenericTypeName());

            _subsManager.AddDynamicSubscription<TH>(eventName);

            Consume(_persistentConnection.ConsumerCancellationToken);
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
            _subsManager.Clear();
        }

        /// <summary>
        ///     In this example
        ///         - offsets are manually committed.
        ///         - no extra thread is created for the Poll (Consume) loop.
        /// </summary>
        private void Consume(CancellationToken cancellationToken)
        {
            const int commitPeriod = 5;

            // Note: If a key or value deserializer is not set (as is the case below), the 
            // deserializer corresponding to the appropriate type from Confluent.Kafka.Deserializers
            // will be used automatically (where available). The default deserializer for string
            // is UTF8. The default deserializer for Ignore returns null for all input data
            // (including non-null data).
            using (var consumer = _persistentConnection.CreateConsumer())
            {
                consumer.Subscribe(_persistentConnection.Topic);

                try
                {
                    while (true)
                    {
                        try
                        {
                            var consumeResult = consumer.Consume(cancellationToken);
                            if (consumeResult.IsPartitionEOF)
                            {
                                _logger.LogInformation($"Kafka reached end of topic {consumeResult.Topic}, partition {consumeResult.Partition}, offset {consumeResult.Offset}.");
                                continue;
                            }

                            var eventName = consumeResult.Message.Key;
                            var message = Encoding.UTF8.GetString(consumeResult.Message.Value);
                            base.ProcessEvent(eventName, message).Wait();

                            _logger.LogInformation($"Kafka received message at topicPartitionOffset:{consumeResult.TopicPartitionOffset}, key:{eventName}, message: {message}");

                            if (consumeResult.Offset % commitPeriod == 0)
                            {
                                // The Commit method sends a "commit offsets" request to the Kafka
                                // cluster and synchronously waits for the response. This is very
                                // slow compared to the rate at which the consumer is capable of
                                // consuming messages. A high performance application will typically
                                // commit offsets relatively infrequently and be designed handle
                                // duplicate messages in the event of failure.
                                try
                                {
                                    consumer.Commit(consumeResult);
                                }
                                catch (KafkaException e)
                                {
                                    _logger.LogError($"Kafka commit error: {e.Error.Reason}");
                                }
                            }
                        }
                        catch (ConsumeException e)
                        {
                            //consumer.Unsubscribe();
                            _logger.LogError($"Kafka consume error: {e.Error.Reason}");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogError("Kafka closing consumer.");
                    consumer.Close();
                }
            }
        }

    }
}
