﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Bolt.PubSub.RabbitMq.Subscribers
{
    internal sealed class QueueSingleChannelListener : IDisposable
    {
        private IModel channel;
        private QueueSettings queueSettings;
        private IServiceProvider serviceProvider;
        private ILogger logger;
        private const int DefaultDelayInMs = 1 * 60 * 1000; // 1 Minute

        
        public void Listen(IServiceProvider serviceProvider, QueueSettings queueSettings)
        {
            this.channel = serviceProvider.GetRequiredService<RabbitMqConnection>().GetOrCreate().CreateModel();
            this.queueSettings = queueSettings;
            this.serviceProvider = serviceProvider;
            this.logger = serviceProvider.GetRequiredService<ILogger<RabbitMqLogger>>();

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.Received += Consumer_Received;

            if (queueSettings.PrefetchCount.HasValue)
            {
                logger.LogTrace("Setting prefetch count to {prefetchCount} per consumer for {queue}.", queueSettings.PrefetchCount, queueSettings.QueueName);

                channel.BasicQos(0, (ushort)queueSettings.PrefetchCount.Value, false);
            }

            var tag = channel.BasicConsume(queueSettings.QueueName, false, consumer);
        }


        private async Task Consumer_Received(object sender, BasicDeliverEventArgs evnt)
        {
            using var _ = logger.BeginScope("{traceId}", evnt.BasicProperties.CorrelationId ?? Guid.NewGuid().ToString());

            logger.LogTrace("Message recieved with {msgId}", evnt.BasicProperties.MessageId);

            using var scope = serviceProvider.CreateScope();

            var contentType = evnt.BasicProperties.ContentType;

            var serializers = scope.ServiceProvider.GetServices<IMessageSerializer>();

            var serializer = serializers.FirstOrDefault(x => x.IsApplicable(contentType.EmptyAlternative(ContentTypeNames.Json)));

            if (serializer == null)
            {
                logger.LogError("No serializer found that support {contentType}", contentType);

                await PublishToErrorExchange(evnt, "SerializerNotFound", 5 * 60);

                return;
            }

            var msg = messageReader.Read(evnt, queueSettings);

            var handlers = scope.ServiceProvider.GetServices<IMessageHandler>();

            var handler = handlers.FirstOrDefault(x => x.IsApplicable(msg));

            if (handler == null)
            {
                logger.LogError("No handler found for {msgId} and {msgType} {tenant} {appId}", msg.Id, msg.Type, msg.Tenant, msg.AppId);

                await PublishToErrorExchange(evnt, $"NoHandlerApplicable", DefaultDelayInMs);

                return;
            }

            try
            {
                if (logger.IsEnabled(LogLevel.Trace))
                {
                    logger.LogTrace("{handler} start handling message {msgId}", handler.GetType(), msg.Id);
                }

                var rsp = await handler.Handle(msg, evnt.Body.ToArray(), serializer);

                if (rsp.Status == HandlerStatusCode.Success)
                {
                    logger.LogTrace("Handler process the message {msgId} successfully. Acknowledging so that message can be removed from queue.", msg.Id);

                    channel.BasicAck(evnt.DeliveryTag, false);

                    return;
                }

                if (rsp.Status == HandlerStatusCode.TransientError)
                {
                    logger.LogWarning("Handler indicates there was a transient failure to process the messsage {msgId}", msg.Id);

                    await Requeue(evnt, DefaultDelayInMs);

                    return;
                }

                logger.LogError("Handler indicates failure {status} to process the messsage {msgId}", rsp.Status, msg.Id);

                await PublishToErrorExchange(evnt, $"HandlerFailed {rsp.Status}", DefaultDelayInMs);

                return;
            }
            catch (Exception e)
            {
                logger.LogError(e, e.Message);

                await PublishToErrorExchange(evnt, e.Message, DefaultDelayInMs);
            }
        }

        private async Task Requeue(BasicDeliverEventArgs evnt, int defaultDelayInMs)
        {
            var delayInMs = queueSettings.RequeueDelayInMs > 0 ? queueSettings.RequeueDelayInMs : defaultDelayInMs;

            if (delayInMs > 0)
            {
                if (evnt.Redelivered)
                {
                    logger.LogTrace("Increase delay in MS by 2 as the message is redlivered");

                    delayInMs = delayInMs * 2;
                }

                logger.LogTrace("Adding some delay {delayInMs} before requeue the message", delayInMs);

                await Task.Delay(delayInMs);
            }

            logger.LogTrace("Nack the message and ask to requeue");

            channel.BasicNack(evnt.DeliveryTag, false, true);

            return;
        }

        private Task PublishToErrorExchange(BasicDeliverEventArgs evnt, string reason, int defaultDelayInMs)
        {
            if (queueSettings.ErrorExchangeName.IsEmpty())
            {
                logger.LogTrace("Requeue the message {errReason} as no error exchange defined.", reason);

                return Requeue(evnt, defaultDelayInMs);
            };

            logger.LogTrace("Publising message to error exchange {errorExchangeName} {errorReason}", queueSettings.ErrorExchangeName, reason);

            reason = reason == null ? null : reason.Length > 64 ? reason.Substring(64) : reason;

            evnt.BasicProperties.SetHeader(HeaderNames.ErrorReason, reason);

            channel.BasicPublish(queueSettings.ErrorExchangeName, string.Empty, evnt.BasicProperties, evnt.Body);

            logger.LogTrace("Acknowledge the message so that can be removed from queue.");

            channel.BasicAck(evnt.DeliveryTag, false);

            return Task.CompletedTask;
        }


        public void Dispose()
        {
            logger.LogInformation("Disposing channel");

            channel?.Dispose();
        }
    }
}
