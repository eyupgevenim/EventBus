using Microsoft.Extensions.Logging;
using System;
using System.IO;
using Apache.NMS;
using Apache.NMS.Util;
using System.Collections.Generic;

namespace EventBus.ActiveMQ
{
    /// <summary>
    /// https://activemq.apache.org/components/nms/examples/nms-simple-synchronous-consumer-example
    /// https://activemq.apache.org/components/nms/examples/nms-simple-asynchronous-consumer-example
    /// </summary>
    public class DefaultActiveMQPersistentConnection : IActiveMQPersistentConnection
    {
        private readonly ILogger<DefaultActiveMQPersistentConnection> _logger;
        private readonly IConnectionFactory _connectionFactory;
        private readonly string _topic;
        private readonly string _userName;
        private readonly string _password;

        IConnection _connection;
        ISession _session;
        IDestination _destination;
        IMessageProducer _messageProducer;
        IMessageConsumer _messageConsumer;
        bool _disposed;

        public DefaultActiveMQPersistentConnection(ILogger<DefaultActiveMQPersistentConnection> logger,
            IDictionary<string, string> config = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if(config == null)
            {
                config = new Dictionary<string, string>
                {
                    ["uri"] = "tcp://localhost:61616",
                    ["topic"] = "test-topic",
                    ["username"] = "admin",
                    ["password"] = "admin",
                };
            }

            _topic = config["topic"];
            _userName = config["username"];
            _password = config["password"];

            var connecturi = new Uri(config["uri"]);
            // Create WMQ Connection Factory.
            _connectionFactory = new NMSConnectionFactory(connecturi);
        }

        private IConnection Connection
        {
            get
            {
                if(_connection == null)
                {
                    // Create connection.
                    _connection = _connectionFactory.CreateConnection(userName: _userName, password: _password);
                }

                return _connection;
            }
        }

        private ISession Session
        {
            get
            {
                if (_session == null)
                {
                    // Create session
                    _session = Connection.CreateSession(AcknowledgementMode.AutoAcknowledge);
                }

                return _session;
            }
        }

        private IDestination Destination
        {
            get
            {
                if (_destination == null)
                {
                    // Create destination
                    ///_destination = SessionUtil.GetDestination($"queue://{_queue}");
                    _destination = SessionUtil.GetDestination(Session, $"topic://{_topic}");
                }

                return _destination;
            }
        }

        public (IMessageProducer, ISession) Producer
        {
            get
            {
                if(_messageProducer == null)
                {
                    // Create producer
                    _messageProducer = Session.CreateProducer(Destination);

                    // Start the connection to receive messages.
                    Connection.Start();
                }

                return (_messageProducer, Session);
            }
        }

        public IMessageConsumer Consumer 
        { 
            get 
            {
                if(_messageConsumer == null)
                {
                    // Create subscriber
                    _messageConsumer = Session.CreateConsumer(Destination);

                    // Start the connection to receive messages.
                    Connection.Start();
                }

                return _messageConsumer;
            } 
        }

        private void DisposeMessageConsumer()
        {
            try
            {
                _messageConsumer?.Close();
                _messageConsumer = null;
            }
            catch (IOException ex)
            {
                _logger.LogCritical(ex.ToString());
            }
        }
        
        private void DisposeMessageProducer()
        {
            try
            {
                _messageProducer?.Close();
                _messageProducer = null;
            }
            catch (IOException ex)
            {
                _logger.LogCritical(ex.ToString());
            }
        }

        private void DisposeDestination()
        {
            try
            {
                _destination?.Dispose();
                _destination = null;
            }
            catch (IOException ex)
            {
                _logger.LogCritical(ex.ToString());
            }
        }

        private void DisposeSession()
        {
            try
            {
                _session?.Dispose();
                _session = null;
            }
            catch (IOException ex)
            {
                _logger.LogCritical(ex.ToString());
            }
        }

        private void DisposeConnection()
        {
            try
            {
                _connection?.Close();
                _connection = null;
            }
            catch (IOException ex)
            {
                _logger.LogCritical(ex.ToString());
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            DisposeMessageConsumer();
            DisposeMessageProducer();
            DisposeDestination();
            DisposeSession();
            DisposeConnection();
        }

    }
}