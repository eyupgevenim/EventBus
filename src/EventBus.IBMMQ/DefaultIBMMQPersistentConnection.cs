using IBM.XMS;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

//V1: Install-Package IBMXMSDotnetClient
namespace EventBus.IBMMQ
{
    public class DefaultIBMMQPersistentConnection : IIBMMQPersistentConnection
    {
        private readonly ILogger<DefaultIBMMQPersistentConnection> _logger;
        private readonly IConnectionFactory _connectionFactory;
        private readonly string _topic;
        bool _disposed;

        public DefaultIBMMQPersistentConnection(ILogger<DefaultIBMMQPersistentConnection> logger,
            IDictionary<string, string> config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _topic = config["topic"];

            // Get an instance of factory.
            var _factoryFactory = XMSFactoryFactory.GetInstance(XMSC.CT_WMQ);
            // Create WMQ Connection Factory.
            _connectionFactory = _factoryFactory.CreateConnectionFactory();

            _connectionFactory.SetStringProperty(XMSC.USERID, config["userid"]);
            _connectionFactory.SetStringProperty(XMSC.PASSWORD, config["password"]);

            _connectionFactory.SetStringProperty(XMSC.WMQ_HOST_NAME, config["host"]);
            _connectionFactory.SetIntProperty(XMSC.WMQ_PORT, int.Parse(config["port"]));
            _connectionFactory.SetStringProperty(XMSC.WMQ_CHANNEL, config["channel"]);
            _connectionFactory.SetStringProperty(XMSC.WMQ_QUEUE_MANAGER, config["qmgr"]);
            
            _connectionFactory.SetIntProperty(XMSC.WMQ_CONNECTION_MODE, XMSC.WMQ_CM_CLIENT);
        }

        public void MessageProducer(Action<IMessageProducer, ISession> producer)
        {
            Context((session, destination) =>
            {
                // Create producer
                using (var messageProducer = session.CreateProducer(destination))
                {
                    producer(messageProducer, session);
                }
            });
        }

        public Task MessageConsumer(Action<IMessageConsumer> consumer)
        {
            return Task.Run(() =>
            {
                Context((session, destination) =>
                {
                    // Create subscriber
                    using (var messageConsumer = session.CreateConsumer(destination))
                    {
                        consumer(messageConsumer);
                    }
                });
            });
            ///return Task.CompletedTask;
        }

        private void Context(Action<ISession, IDestination> pubsub)
        {
            // Create connection.
            using (var connection = _connectionFactory.CreateConnection())
            // Create session
            using (var session = connection.CreateSession(false, AcknowledgeMode.AutoAcknowledge))
            // Create destination
            using (var destination = session.CreateTopic(_topic))
            {
                destination.SetIntProperty(XMSC.WMQ_TARGET_CLIENT, XMSC.WMQ_TARGET_DEST_MQ);
                // Start the connection to receive messages.
                connection.Start();
                pubsub(session, destination);

                ////// Create subscriber
                ////var consumer = session.CreateConsumer(destination);
                ////// Create producer
                ////var producer = session.CreateProducer(destination);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
        }
    }
}


/** //V2: Install-Package IBMMQDotnetClient
//ibmxmsdotnetclient
//.nuget\packages\ibmxmsdotnetclient\9.2.1\lib\netstandard2.0\amqmdnetstd.dll
namespace EventBus.IBMMQ
{
    using IBM.WMQ;
    using Newtonsoft.Json;
    using System;
    using System.Collections;
    using System.Threading;
    using System.Threading.Tasks;

    public class IbmMqWMQManager
    {
        private readonly Hashtable _queueManagerProperties;
        private readonly string _queueManagerName;
        private readonly string _queue;

        public IbmMqWMQManager(IbmMqWMQManagerConnectionConfiguration configuration)
        {
            _queueManagerName = configuration.QueueManagerName;
            _queue = configuration.Queue;

            _queueManagerProperties = new Hashtable
            {
                { MQC.TRANSPORT_PROPERTY, MQC.TRANSPORT_MQSERIES_MANAGED },
                { MQC.HOST_NAME_PROPERTY, configuration.HostName },
                { MQC.PORT_PROPERTY, configuration.Port },
                { MQC.CHANNEL_PROPERTY, configuration.Channel },
                { MQC.USER_ID_PROPERTY, configuration.UserId },
                { MQC.PASSWORD_PROPERTY, configuration.Password }
            };
        }

        public void Producer(Action<MQQueue, MQMessage> messageHandler)
        {
            try
            {
                using (var queueManager = new MQQueueManager(_queueManagerName, _queueManagerProperties))
                using (var mqQueue = queueManager.AccessQueue(_queue, MQC.MQOO_INPUT_AS_Q_DEF + MQC.MQOO_OUTPUT + MQC.MQOO_FAIL_IF_QUIESCING))
                {
                    var mqMessage = new MQMessage { Persistence = MQC.MQPER_PERSISTENCE_AS_TOPIC_DEF };
                    messageHandler(mqQueue, mqMessage);
                    mqQueue.Put(mqMessage);
                }
            }
            catch (Exception exception)
            {
                //TODO:....
            }
        }

        public Task Consumer(Action<MQMessage> messageHandler)
        {
            var mqGetMessageOptions = new MQGetMessageOptions();
            mqGetMessageOptions.Options |= MQC.MQGMO_NO_WAIT | MQC.MQGMO_FAIL_IF_QUIESCING;

            try
            {
                using (var queueManager = new MQQueueManager(_queueManagerName, _queueManagerProperties))
                using (var mqQueue = queueManager.AccessQueue(_queue, MQC.MQTOPIC_OPEN_AS_SUBSCRIPTION))
                {
                    while (true)
                    {
                        try
                        {
                            var mqMessage = new MQMessage();
                            mqQueue.Get(mqMessage, mqGetMessageOptions);
                            messageHandler(mqMessage);
                        }
                        catch (MQException mqException) when (mqException.Reason == MQC.MQRC_NO_MSG_AVAILABLE)
                        {
                            Thread.Sleep(200);
                        }
                        catch (Exception exception)
                        {
                            throw new Exception(exception.Message, exception);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                //TODO:...
            }

            return Task.CompletedTask;
        }
    }

    //docker run --env LICENSE=accept --env MQ_QMGR_NAME=QM1 --publish 1414:1414 --publish 9443:9443 ibmcom/mq
    public class UsageSample
    {
        private readonly IbmMqWMQManagerConnectionConfiguration _connection;
        private readonly IbmMqWMQManager _ibmMqProxy;
        public UsageSample()
        {
            _connection = new IbmMqWMQManagerConnectionConfiguration
            {
                UserId = "app",
                Password = "passw0rd",
                Port = 1414,
                Channel = "DEV.APP.SVRCONN",
                HostName = "localhost(1414)",

                QueueManagerName = "QM1",
                Queue = "DEV.QUEUE.1",
            };
        }

        public void Pub(object @event)
        {
            _ibmMqProxy.Producer((mqQueue, mqMessage) => 
            {
                var message = JsonConvert.SerializeObject(@event);
                var eventName = @event.GetType().Name;

                mqMessage.WriteString(message);
                mqMessage.SetStringProperty("EventName", eventName);
            });
        }

        public async Task Sub()
        {
            await Task.Run(() => _ibmMqProxy.Consumer((message) =>
            {
                var messageBody = message.ReadString(message.DataLength);
                var eventName = message.GetStringProperty("EventName");

                //TODO:....
                
            }));
        }
    }

    public class IbmMqWMQManagerConnectionConfiguration
    {
        public string UserId { get; set; }
        public string Password { get; set; }
        public int Port { get; set; }
        public string Channel { get; set; }
        public string HostName { get; set; }

        public string QueueManagerName { get; set; }
        public string Queue { get; set; }
    }
}
*/