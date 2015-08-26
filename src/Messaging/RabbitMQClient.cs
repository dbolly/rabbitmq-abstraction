﻿using System;
using System.Collections.Generic;
using System.Text;
using RabbitMQ.Client;
using Vtex.RabbitMQ.Interfaces;
using Vtex.RabbitMQ.Logging.Interfaces;
using Vtex.RabbitMQ.Messaging.Interfaces;
using Vtex.RabbitMQ.Serialization;
using Vtex.RabbitMQ.Serialization.Interfaces;

namespace Vtex.RabbitMQ.Messaging
{
    public class RabbitMQClient : IQueueClient
    {
        private readonly ISerializer _serializer;

        private readonly ILogger _logger;

        private readonly RabbitMQConnectionPool _connectionPool;

        public RabbitMQClient(string hostName, int port, string userName, string password, string virtualHost,
            ISerializer serializer = null, ILogger logger = null)
        {
            var connectionFactory = new ConnectionFactory
            {
                HostName = hostName,
                Port = port,
                UserName = userName,
                Password = password,
                VirtualHost = virtualHost
            };

            _connectionPool = new RabbitMQConnectionPool(connectionFactory);
            _serializer = serializer ?? new JsonSerializer();
            _logger = logger;
        }

        public RabbitMQClient(ConnectionFactory connectionFactory, ISerializer serializer = null, ILogger logger = null)
        {
            _connectionPool = new RabbitMQConnectionPool(connectionFactory);
            _serializer = serializer ?? new JsonSerializer();
            _logger = logger;
        }

        public RabbitMQClient(RabbitMQConnectionPool connectionPool, ISerializer serializer = null, ILogger logger = null)
        {
            _connectionPool = connectionPool;
            _serializer = serializer ?? new JsonSerializer();
            _logger = logger;
        }

        public void Publish<T>(string exchangeName, string routingKey, T content)
        {
            var serializedContent = _serializer.Serialize(content);
            using (var model = _connectionPool.GetConnection().CreateModel())
            {
                var props = model.CreateBasicProperties();
                props.DeliveryMode = 2;
                var payload = Encoding.UTF8.GetBytes(serializedContent);
                model.BasicPublish(exchangeName, routingKey, props, payload);
            }
        }

        public void BatchPublish<T>(string exchangeName, string routingKey, IEnumerable<T> contentList)
        {
            using (var model = _connectionPool.GetConnection().CreateModel())
            {
                var props = model.CreateBasicProperties();
                props.DeliveryMode = 2;

                foreach (var content in contentList)
                {
                    var serializedContent = _serializer.Serialize(content);

                    var payload = Encoding.UTF8.GetBytes(serializedContent);
                    model.BasicPublish(exchangeName, routingKey, props, payload);
                }
            }
        }

        public void BatchPublishTransactional<T>(string exchangeName, string routingKey, IEnumerable<T> contentList)
        {
            using (var model = _connectionPool.GetConnection().CreateModel())
            {
                try
                {
                    model.TxSelect();

                    var props = model.CreateBasicProperties();
                    props.DeliveryMode = 2;

                    foreach (var content in contentList)
                    {
                        var serializedContent = _serializer.Serialize(content);

                        var payload = Encoding.UTF8.GetBytes(serializedContent);
                        model.BasicPublish(exchangeName, routingKey, props, payload);
                    }

                    model.TxCommit();
                }
                catch (Exception)
                {
                    if (model.IsOpen)
                    {
                        model.TxRollback();
                    }
                    
                    throw;
                }
            }
        }

        public void QueueDeclare(string queueName, bool durable = true, bool exclusive = false, bool autoDelete = false, 
            IDictionary<string, object> arguments = null)
        {
            using (var model = _connectionPool.GetConnection().CreateModel())
            {
                model.QueueDeclare(queueName, durable, exclusive, autoDelete, arguments);
            }
        }

        public void QueueDeclarePassive(string queueName)
        {
            using (var model = _connectionPool.GetConnection().CreateModel())
            {
                model.QueueDeclarePassive(queueName);
            }
        }

        public uint QueueDelete(string queueName)
        {
            using (var model = _connectionPool.GetConnection().CreateModel())
            {
                return model.QueueDelete(queueName);
            }
        }

        public void QueueBind(string queueName, string exchangeName, string routingKey)
        {
            using (var model = _connectionPool.GetConnection().CreateModel())
            {
                model.QueueBind(queueName, exchangeName, routingKey);
            }
        }

        public void ExchangeDeclare(string exchangeName, bool passive = false)
        {
            using (var model = _connectionPool.GetConnection().CreateModel())
            {
                if (passive)
                {
                    model.ExchangeDeclarePassive(exchangeName);
                }
                else
                {
                    model.ExchangeDeclare(exchangeName, ExchangeType.Topic, true);
                }
            }
        }

        public bool QueueExists(string queueName)
        {
            using (var model = _connectionPool.GetConnection().CreateModel())
            {
                try
                {
                    model.QueueDeclarePassive(queueName);
                }
                catch (Exception)
                {
                    return false;
                }

                return true;
            }
        }

        public void EnsureQueueExists(string queueName, bool durable = true, bool exclusive = false, bool autoDelete = false, 
            IDictionary<string, object> arguments = null)
        {
            if (!QueueExists(queueName))
            {
                QueueDeclare(queueName, durable, exclusive, autoDelete, arguments);
            }
        }

        public uint QueuePurge(string queueName)
        {
            using (var model = _connectionPool.GetConnection().CreateModel())
            {
                var returnValue = model.QueuePurge(queueName);
                return returnValue;
            }
        }

        public uint GetMessageCount(string queueName)
        {
            using (var model = _connectionPool.GetConnection().CreateModel())
            {
                var queueDeclareOk = model.QueueDeclarePassive(queueName);

                return queueDeclareOk.MessageCount;
            }
        }

        public uint GetConsumerCount(string queueName)
        {
            using (var model = _connectionPool.GetConnection().CreateModel())
            {
                var queueDeclareOk = model.QueueDeclarePassive(queueName);

                return queueDeclareOk.ConsumerCount;
            }
        }

        public IQueueConsumer GetConsumer<T>(string queueName, IConsumerCountManager consumerCountManager, 
            IMessageProcessingWorker<T> messageProcessingWorker, IMessageRejectionHandler messageRejectionHandler) 
            where T : class
        {
            return new RabbitMQConsumer<T>(
                connectionPool: _connectionPool,
                queueName: queueName,
                serializer: _serializer,
                logger: _logger,
                messageProcessingWorker: messageProcessingWorker,
                consumerCountManager: consumerCountManager,
                messageRejectionHandler: messageRejectionHandler);
        }

        public void Dispose()
        {
            if (_connectionPool != null)
            {
                _connectionPool.Dispose();
            }
        }
    }
}