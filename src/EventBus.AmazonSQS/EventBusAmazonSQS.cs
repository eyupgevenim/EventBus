using Amazon.SQS;
using Amazon.SQS.Model;
using Autofac;
using EventBus.Abstractions;
using EventBus.Events;
using EventBus.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace EventBus.AmazonSQS
{
    public class EventBusAmazonSQS : SubscribeProcessEvent, IEventBus, IDisposable
    {
        const string APP_NAME = "AmazonSQS";
        const string AUTOFAC_SCOPE_NAME = "e_event_bus";

        private readonly IAmazonSQSPersistentConnection _persistentConnection;

        private readonly ConcurrentDictionary<string, string> _queueUrlCache = new ConcurrentDictionary<string, string>();
        private readonly int _awsQueueLongPollTimeSeconds;
        private const string FifoSuffix = ".fifo";
        private readonly bool _awsQueueIsFifo;
        private readonly string _queueName;

        private string QueueName => _awsQueueIsFifo ? _queueName + FifoSuffix : _queueName;
        private string DeadLetterQueueName => $"{_queueName}-exceptions{(_awsQueueIsFifo ? FifoSuffix : string.Empty)}";

        public EventBusAmazonSQS(IAmazonSQSPersistentConnection persistentConnection, 
            ILogger<EventBusAmazonSQS> logger,
            ILifetimeScope autofac, 
            IEventBusSubscriptionsManager subsManager, 
            int awsQueueLongPollTimeSeconds,
            string queueName, 
            bool awsQueueIsFifo = false)
            : base(logger, subsManager, autofac, appName: APP_NAME, autofacScopeName: AUTOFAC_SCOPE_NAME)
        {
            _persistentConnection = persistentConnection ?? throw new ArgumentNullException(nameof(persistentConnection));
            _queueName = queueName;
            _awsQueueLongPollTimeSeconds = awsQueueLongPollTimeSeconds;
            _awsQueueIsFifo = awsQueueIsFifo;
            _subsManager.OnEventRemoved += SubsManager_OnEventRemoved;
        }

        private void SubsManager_OnEventRemoved(object sender, string eventName)
        {
            //TODO:....
            RestoreFromDeadLetterQueueAsync().Wait();
        }

        public void Publish(IntegrationEvent @event)
        {
            var eventName = @event.GetType().Name;

            _logger.LogTrace("Creating AmazonSQS channel to publish event: {EventId} ({EventName})", @event.Id, eventName);

            var message = JsonConvert.SerializeObject(@event);
            PostMessageAsync(QueueName, message, eventName).Wait();
        }

        public void SubscribeDynamic<TH>(string eventName)
            where TH : IDynamicIntegrationEventHandler
        {
            _logger.LogInformation("Subscribing to dynamic event {EventName} with {EventHandler}", eventName, typeof(TH).GetGenericTypeName());
            //TODO:...
            _subsManager.AddDynamicSubscription<TH>(eventName);
            Consumer().Wait();
        }

        public void Subscribe<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventName = _subsManager.GetEventKey<T>();
            //TODO:...
            _logger.LogInformation("Subscribing to event {EventName} with {EventHandler}", eventName, typeof(TH).GetGenericTypeName());

            _subsManager.AddSubscription<T, TH>();
            Consumer().Wait();
        }

        public void Unsubscribe<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            //TODO:...
            var eventName = _subsManager.GetEventKey<T>();

            _logger.LogInformation("Unsubscribing from event {EventName}", eventName);

            _subsManager.RemoveSubscription<T, TH>();
        }

        public void UnsubscribeDynamic<TH>(string eventName)
            where TH : IDynamicIntegrationEventHandler
        {
            //TODO:...
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

        private async Task Consumer()
        {
            while (true)
            {
                var messages = await GetMessagesAsync(QueueName);
                if (!messages.Any())
                {
                    continue;
                }

                messages.ForEach(async message =>
                {
                    var eventName = message.MessageAttributes.GetMessageTypeAttributeValue();
                    await base.ProcessEvent(eventName, message.Body);
                }); 
            }
        }

        private async Task<List<Message>> GetMessagesAsync(string queueName, CancellationToken cancellationToken = default)
        {
            var queueUrl = await GetQueueUrl(queueName);

            try
            {
                var response = await _persistentConnection.SQSClient.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    WaitTimeSeconds = _awsQueueLongPollTimeSeconds,
                    AttributeNames = new List<string> { "ApproximateReceiveCount" },
                    MessageAttributeNames = new List<string> { "All" }
                }, cancellationToken);

                if (response.HttpStatusCode != HttpStatusCode.OK)
                {
                    throw new AmazonSQSException($"Failed to GetMessagesAsync for queue {queueName}. Response: {response.HttpStatusCode}");
                }

                return response.Messages;
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning($"Failed to GetMessagesAsync for queue {queueName} because the task was canceled");
                return new List<Message>();
            }
            catch (Exception)
            {
                _logger.LogError($"Failed to GetMessagesAsync for queue {queueName}");
                throw;
            }
        }

        private async Task PostMessageAsync<T>(string queueName, T message)
        {
            var queueUrl = await GetQueueUrl(queueName);
            try
            {
                var sendMessageRequest = new SendMessageRequest
                {
                    QueueUrl = queueUrl,
                    MessageBody = JsonConvert.SerializeObject(message),
                    MessageAttributes = SqsMessageTypeAttribute.CreateAttributes<T>()
                };
                if (_awsQueueIsFifo)
                {
                    sendMessageRequest.MessageGroupId = typeof(T).Name;
                    sendMessageRequest.MessageDeduplicationId = Guid.NewGuid().ToString();
                }

                await _persistentConnection.SQSClient.SendMessageAsync(sendMessageRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to PostMessagesAsync to queue '{queueName}'. Exception: {ex.Message}");
            }
        }

        private async Task PostMessageAsync(string queueName, string messageBody, string messageType)
        {
            var queueUrl = await GetQueueUrl(queueName);
            try
            {
                var sendMessageRequest = new SendMessageRequest
                {
                    QueueUrl = queueUrl,
                    MessageBody = messageBody,
                    MessageAttributes = SqsMessageTypeAttribute.CreateAttributes(messageType)
                };
                if (_awsQueueIsFifo)
                {
                    sendMessageRequest.MessageGroupId = messageType;
                    sendMessageRequest.MessageDeduplicationId = Guid.NewGuid().ToString();
                }

                await _persistentConnection.SQSClient.SendMessageAsync(sendMessageRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to PostMessagesAsync to queue '{queueName}'. Exception: {ex.Message}");
            }
        }

        private async Task DeleteMessageAsync(string queueName, string receiptHandle)
        {
            var queueUrl = await GetQueueUrl(queueName);
            try
            {
                var response = await _persistentConnection.SQSClient.DeleteMessageAsync(queueUrl, receiptHandle);

                if (response.HttpStatusCode != HttpStatusCode.OK)
                {
                    throw new AmazonSQSException($"Failed to DeleteMessageAsync with for [{receiptHandle}] from queue '{queueName}'. Response: {response.HttpStatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to DeleteMessageAsync from queue {queueName}");
            }
        }

        private async Task RestoreFromDeadLetterQueueAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var token = new CancellationTokenSource();
                while (!token.Token.IsCancellationRequested)
                {
                    var messages = await GetMessagesAsync(DeadLetterQueueName, cancellationToken);
                    if (!messages.Any())
                    {
                        token.Cancel();
                        continue;
                    }

                    messages.ForEach(async message =>
                    {
                        var messageType = message.MessageAttributes.GetMessageTypeAttributeValue();
                        if (messageType != null)
                        {
                            await PostMessageAsync(message.Body, messageType);
                            await DeleteMessageAsync(DeadLetterQueueName, message.ReceiptHandle);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to ReprocessMessages from queue {DeadLetterQueueName}");
            }
        }

        private async Task<string> GetQueueUrl(string queueName)
        {
            if (string.IsNullOrEmpty(queueName))
            {
                throw new ArgumentException("Queue name should not be blank.");
            }

            if (_queueUrlCache.TryGetValue(queueName, out var result))
            {
                return result;
            }

            try
            {
                var response = await _persistentConnection.SQSClient.GetQueueUrlAsync(queueName);
                return _queueUrlCache.AddOrUpdate(queueName, response.QueueUrl, (q, url) => url);
            }
            catch (QueueDoesNotExistException ex)
            {
                //TODO:....

                await _persistentConnection.CreateQueueAsync(QueueName, DeadLetterQueueName, _awsQueueLongPollTimeSeconds, _awsQueueIsFifo);
                _logger.LogError(ex, $"Could not retrieve the URL for the queue '{queueName}' as it does not exist or you do not have access to it.");

                return await GetQueueUrl(queueName);

                ///throw new InvalidOperationException($"Could not retrieve the URL for the queue '{queueName}' as it does not exist or you do not have access to it.", ex);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message, ex);
            }
        }
    }

    static class SqsMessageTypeAttribute
    {
        private const string AttributeName = "MessageType";

        public static string GetMessageTypeAttributeValue(this Dictionary<string, MessageAttributeValue> attributes)
        {
            return attributes.SingleOrDefault(x => x.Key == AttributeName).Value?.StringValue;
        }

        public static Dictionary<string, MessageAttributeValue> CreateAttributes<T>()
        {
            return CreateAttributes(typeof(T).Name);
        }

        public static Dictionary<string, MessageAttributeValue> CreateAttributes(string messageType)
        {
            return new Dictionary<string, MessageAttributeValue>
            {
                { AttributeName, new MessageAttributeValue { DataType = nameof(String), StringValue = messageType} }
            };
        }
    }

}
