using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;

namespace EventBus.Kafka
{
    public class DefaultKafkaPersistentConnection : IKafkaPersistentConnection
    {
        private readonly ILogger<DefaultKafkaPersistentConnection> _logger;
        private readonly CancellationToken _consumerCancellationToken;

        private readonly ProducerConfig _producerConfig;
        private readonly ConsumerConfig _consumerConfig;
        private readonly string _topic;


        bool _disposed;

        /// <summary>
        /// Create Kafka claint
        /// </summary>
        /// <param name="clientConfig">
        /// new Dictionary<string, string>
        /// {
        ///     ["bootstrap.servers"] = "localhost:9092",//Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS"),
        ///     ["topic"] = Environment.GetEnvironmentVariable("KAFKA_TOPIC"),
        ///     ["group.id"] = Environment.GetEnvironmentVariable("KAFKA_GROUP_ID"),
        ///     ...
        /// };
        /// </param>
        /// <param name="logger">ILogger<DefaultKafkaPersistentConnection></param>
        /// <param name="consumerCancellationToken">CancellationToken</param>
        public DefaultKafkaPersistentConnection(IDictionary<string, string> clientConfig, 
            ILogger<DefaultKafkaPersistentConnection> logger,
            CancellationToken consumerCancellationToken = default(CancellationToken))
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _consumerCancellationToken = consumerCancellationToken;

            _topic = clientConfig["topic"];
            _producerConfig = new ProducerConfig() { BootstrapServers = clientConfig["bootstrap.servers"] };
            _consumerConfig = new ConsumerConfig { BootstrapServers = clientConfig["bootstrap.servers"] };
            SetConfigDetails(clientConfig);
        }

        private void SetConfigDetails(IDictionary<string, string> clientConfig)
        {
            if (clientConfig.ContainsKey("group.id"))
            {
                _consumerConfig.GroupId = clientConfig["group.id"];
            }

            //TODO:...
        }


        public string Topic => _topic;

        public IProducer<string, byte[]> CreateProducer() => new ProducerBuilder<string, byte[]>(_producerConfig).Build();

        public IConsumer<string, byte[]> CreateConsumer() 
            => new ConsumerBuilder<string, byte[]>(_consumerConfig)
                // Note: All handlers are called on the main .Consume thread.
                .SetErrorHandler((_, e) => _logger.LogError($"Error: {e.Reason}"))
                .SetStatisticsHandler((_, json) => _logger.LogInformation($"Statistics: {json}"))
                .SetPartitionsAssignedHandler((c, partitions) =>
                {
                    _logger.LogInformation($"Assigned partitions: [{string.Join(", ", partitions)}]");
                    // possibly manually specify start offsets or override the partition assignment provided by
                    // the consumer group by returning a list of topic/partition/offsets to assign to, e.g.:
                    // 
                    // return partitions.Select(tp => new TopicPartitionOffset(tp, externalOffsets[tp]));
                })
                .SetPartitionsRevokedHandler((c, partitions) =>
                {
                    _logger.LogInformation($"Revoking assignment: [{string.Join(", ", partitions)}]");
                })
                .Build();

        public CancellationToken ConsumerCancellationToken => _consumerCancellationToken;

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
        }

    }
}

