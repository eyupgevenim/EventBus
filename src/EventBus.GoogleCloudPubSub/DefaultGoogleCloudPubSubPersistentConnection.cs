using Google.Cloud.PubSub.V1;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EventBus.GoogleCloudPubSub
{
    public class DefaultGoogleCloudPubSubPersistentConnection : IGoogleCloudPubSubPersistentConnection
    {
        private readonly ILogger<DefaultGoogleCloudPubSubPersistentConnection> _logger;
        private readonly string _projectId;
        private readonly string _topicId;
        private readonly string _subscriptionId;
        private readonly string _pubsubEmulatorHost;

        bool _disposed;

        public DefaultGoogleCloudPubSubPersistentConnection(ILogger<DefaultGoogleCloudPubSubPersistentConnection> logger,
            IDictionary<string, string> config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _projectId = config["projectid"];
            _topicId = config["topicid"];
            _subscriptionId = config["subscriptionid"];
            _pubsubEmulatorHost = config["pubsub.emulator.host"];

            Initial().Wait();
        }

        public async Task Pub(Action<PublisherClient> publisheHandler)
        {
            TopicName topicName = new TopicName(_projectId, _topicId);
            // Publish a message to the topic using PublisherClient.
            PublisherClient publisher = await CreatePublisher(topicName);
            publisheHandler(publisher);
        }

        public async Task Sub(Func<SubscriberClient, Task> subscribeHandler)
        {
            SubscriptionName subscriptionName = new SubscriptionName(_projectId, _subscriptionId);
            // Pull messages from the subscription using SubscriberClient.
            SubscriberClient subscriber = await CreateSubscriber(subscriptionName);
            await subscribeHandler(subscriber);
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

        }

        private async Task Initial()
        {
            var topicName = new TopicName(_projectId, _topicId);
            await CreateTopic(topicName);

            var subscriptionName = new SubscriptionName(_projectId, _subscriptionId);
            await CreateSubscription(subscriptionName, topicName);
        }

        private async Task CreateTopic(TopicName topicName)
        {
            var publisherServiceApiClient = string.IsNullOrEmpty(_pubsubEmulatorHost)
                    ? await PublisherServiceApiClient.CreateAsync()
                    : await new PublisherServiceApiClientBuilder { Endpoint = _pubsubEmulatorHost, ChannelCredentials = ChannelCredentials.Insecure }.BuildAsync();

            try
            {
                await publisherServiceApiClient.CreateTopicAsync(topicName);
            }
            catch (RpcException e) when (e.Status.StatusCode == StatusCode.AlreadyExists)
            {
                _logger.LogError(e, e.Message);
                ///await publisherServiceApiClient.GetTopicAsync(topicName);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message, ex);
            }
        }

        private async Task CreateSubscription(SubscriptionName subscriptionName, TopicName topicName)
        {
            var subscriberServiceApiClient = string.IsNullOrEmpty(_pubsubEmulatorHost)
                    ? await SubscriberServiceApiClient.CreateAsync()
                    : await new SubscriberServiceApiClientBuilder { Endpoint = _pubsubEmulatorHost, ChannelCredentials = ChannelCredentials.Insecure }.BuildAsync();

            try
            {
                await subscriberServiceApiClient.CreateSubscriptionAsync(subscriptionName, topicName, pushConfig: null, ackDeadlineSeconds: 60);
            }
            catch (RpcException e) when (e.Status.StatusCode == StatusCode.AlreadyExists)
            {
                _logger.LogError(e, e.Message);
                ///await subscriberServiceApiClient.GetSubscriptionAsync(subscriptionName);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message, ex);
            }
        }

        private async Task<PublisherClient> CreatePublisher(TopicName topicName)
        {
            // Publish a message to the topic using PublisherClient.
            return string.IsNullOrEmpty(_pubsubEmulatorHost) 
                ? await PublisherClient.CreateAsync(topicName) 
                : await PublisherClient.CreateAsync(topicName, clientCreationSettings: new PublisherClient.ClientCreationSettings(credentials: ChannelCredentials.Insecure, serviceEndpoint: _pubsubEmulatorHost));
        }

        private async Task<SubscriberClient> CreateSubscriber(SubscriptionName subscriptionName)
        {
            // Pull messages from the subscription using SubscriberClient.
            return string.IsNullOrEmpty(_pubsubEmulatorHost) 
                ? await SubscriberClient.CreateAsync(subscriptionName) 
                : await SubscriberClient.CreateAsync(subscriptionName, clientCreationSettings: new SubscriberClient.ClientCreationSettings(credentials: ChannelCredentials.Insecure, serviceEndpoint: _pubsubEmulatorHost));
        }

    }
}
