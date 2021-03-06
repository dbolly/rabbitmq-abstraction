﻿namespace Vtex.RabbitMQ.Messaging.Interfaces
{
    public interface IMessageFeedbackSender
    {
        ulong DeliveryTag { get; }

        bool MessageAcknoledged { get; }

        void Ack();

        void Nack(bool requeue);
    }
}
