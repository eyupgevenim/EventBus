using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace EventBus.AmazonSQS
{
    /// <summary>
    /// https://automationrhapsody.com/aws-examples-in-c-basic-sqs-queue-operations/
    /// </summary>
    public class DefaultAmazonSQSPersistentConnection : IAmazonSQSPersistentConnection
    {
        private readonly ILogger<DefaultAmazonSQSPersistentConnection> _logger;
        private readonly string _awsRegion;
        private readonly string _awsAccessKey;
        private readonly string _awsSecretKey;
        private readonly string _awsEndpoint;

        IAmazonSQS _sqsClient;
        bool _disposed;

        public DefaultAmazonSQSPersistentConnection(ILogger<DefaultAmazonSQSPersistentConnection> logger,
            IDictionary<string, string> config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _awsRegion = config["region"];
            _awsAccessKey = config["access.key.id"];
            _awsSecretKey = config["secret.access.key"];

            //for local test
            _awsEndpoint = config["endpoint"];
        }

        public IAmazonSQS SQSClient
        {
            get
            {
                if(_sqsClient == null)
                {
                    var sqsConfig = new AmazonSQSConfig { RegionEndpoint = RegionEndpoint.GetBySystemName(_awsRegion) };
                    if (!string.IsNullOrEmpty(_awsEndpoint))
                    {
                        sqsConfig.ServiceURL = _awsEndpoint;
                    }

                    var awsCredentials = new AwsCredentials(_awsAccessKey, _awsSecretKey);
                    _sqsClient = new AmazonSQSClient(awsCredentials, sqsConfig);
                }
                
                return _sqsClient;
            }
        }

        public async Task CreateQueueAsync(string awsQueueName, string awsDeadLetterQueueName, int awsQueueLongPollTimeSeconds, bool awsQueueIsFifo = false)
        {
            const string arnAttribute = "QueueArn";

            try
            {
                var createQueueRequest = new CreateQueueRequest();
                if (awsQueueIsFifo)
                {
                    createQueueRequest.Attributes.Add("FifoQueue", "true");
                }

                createQueueRequest.QueueName = awsQueueName;
                var createQueueResponse = await SQSClient.CreateQueueAsync(createQueueRequest);
                createQueueRequest.QueueName = awsDeadLetterQueueName;
                var createDeadLetterQueueResponse = await SQSClient.CreateQueueAsync(createQueueRequest);

                // Get the the ARN of dead letter queue and configure main queue to deliver messages to it
                var attributes = await SQSClient.GetQueueAttributesAsync(new GetQueueAttributesRequest
                {
                    QueueUrl = createDeadLetterQueueResponse.QueueUrl,
                    AttributeNames = new List<string> { arnAttribute }
                });
                var deadLetterQueueArn = attributes.Attributes[arnAttribute];

                // RedrivePolicy on main queue to deliver messages to dead letter queue if they fail processing after 3 times
                var redrivePolicy = new
                {
                    maxReceiveCount = "3",
                    deadLetterTargetArn = deadLetterQueueArn
                };
                await SQSClient.SetQueueAttributesAsync(new SetQueueAttributesRequest
                {
                    QueueUrl = createQueueResponse.QueueUrl,
                    Attributes = new Dictionary<string, string>
                    {
                        {"RedrivePolicy", JsonConvert.SerializeObject(redrivePolicy)},
				        // Enable Long polling
				        {"ReceiveMessageWaitTimeSeconds", awsQueueLongPollTimeSeconds.ToString()}
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error when creating SQS queue {awsQueueName} and {awsDeadLetterQueueName}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            try
            {
                if (_sqsClient != null)
                {
                    _sqsClient.Dispose();
                    _sqsClient = null;
                }
            }
            catch (IOException ex)
            {
                _logger.LogCritical(ex.ToString());
            }
        }
    }

    class AwsCredentials : AWSCredentials
    {
        private readonly string _awsAccessKey;
        private readonly string _awsSecretKey;
        public AwsCredentials(string awsAccessKey, string awsSecretKey)
        {
            _awsAccessKey = awsAccessKey;
            _awsSecretKey = awsSecretKey;
        }

        public override ImmutableCredentials GetCredentials()
        {
            return new ImmutableCredentials(_awsAccessKey, _awsSecretKey, null);
        }
    }

}
