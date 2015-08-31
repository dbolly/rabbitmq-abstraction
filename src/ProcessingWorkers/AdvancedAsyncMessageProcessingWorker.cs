﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vtex.RabbitMQ.Messaging.Interfaces;

namespace Vtex.RabbitMQ.ProcessingWorkers
{
    public class AdvancedAsyncMessageProcessingWorker<T> : AbstractAdvancedMessageProcessingWorker<T> where T : class
    {
        private readonly Func<T, CancellationToken, Task> _callbackFunc;

        private readonly CancellationTokenSource _cancellationTokenSource;

        public AdvancedAsyncMessageProcessingWorker(IQueueConsumer consumer, 
            Func<T, CancellationToken, Task> callbackFunc, CancellationTokenSource cancellationTokenSource,
            ExceptionHandlingStrategy exceptionHandlingStrategy = ExceptionHandlingStrategy.Requeue, 
            int invokeRetryCount = 1, int invokeRetryWaitMilliseconds = 0, bool autoStartup = true)
            : base(consumer, exceptionHandlingStrategy, invokeRetryCount, invokeRetryWaitMilliseconds, autoStartup)
        {
            _callbackFunc = callbackFunc;
            _cancellationTokenSource = cancellationTokenSource;
        }

        public AdvancedAsyncMessageProcessingWorker(IQueueClient queueClient, string queueName, 
            Func<T, CancellationToken, Task> callbackFunc, CancellationTokenSource cancellationTokenSource,
            ExceptionHandlingStrategy exceptionHandlingStrategy = ExceptionHandlingStrategy.Requeue, 
            int invokeRetryCount = 1, int invokeRetryWaitMilliseconds = 0, ConsumerCountManager consumerCountManager = null, 
            IMessageRejectionHandler messageRejectionHandler = null, bool autoStartup = true)
            : base(queueClient, queueName, exceptionHandlingStrategy, invokeRetryCount, invokeRetryWaitMilliseconds, 
            consumerCountManager, messageRejectionHandler, autoStartup)
        {
            _callbackFunc = callbackFunc;
            _cancellationTokenSource = cancellationTokenSource;
        }

        protected override bool TryInvoke(T message, List<Exception> exceptions)
        {
            try
            {
                _callbackFunc(message, _cancellationTokenSource.Token).Wait(_cancellationTokenSource.Token);

                return true;
            }
            catch (Exception exception)
            {
                exceptions.Add(exception);

                return false;
            }
        }
    }
}
