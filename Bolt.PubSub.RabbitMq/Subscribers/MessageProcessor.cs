using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Bolt.PubSub.RabbitMq.Subscribers
{
    internal sealed class MessageProcessor
    {
        private readonly ILogger<RabbitMqLogger> logger;
        private readonly IEnumerable<IMessageSerializer> serializers;
        private readonly IEnumerable<IMessageHandler> handlers;
        private readonly MessageReader messageReader;
        private readonly IEnumerable<ICollectUsageData> usageDataCollectors;
        private const int DefaultDelayInMs = 1 * 60 * 1000; // 1 Minute

        public MessageProcessor(ILogger<RabbitMqLogger> logger, 
            IEnumerable<IMessageSerializer> serializers,
            IEnumerable<IMessageHandler> handlers,
            MessageReader messageReader,
            IEnumerable<ICollectUsageData> usageDataCollectors)
        {
            this.logger = logger;
            this.serializers = serializers;
            this.handlers = handlers;
            this.messageReader = messageReader;
            this.usageDataCollectors = usageDataCollectors;
        }

        public async Task Process(IModel channel, BasicDeliverEventArgs evnt, QueueSettings queueSettings)
        {
            var msg = messageReader.Read(evnt, queueSettings);

            await NotifyUsageData(evnt, msg, queueSettings, UsageDataType.MessageReceived, null);

            var rsp = await ProcessInternal(evnt, msg, queueSettings);

            if(rsp.Status == HandlerStatusCode.Success)
            {
                channel.BasicAck(evnt.DeliveryTag, false);
            }
            else if(rsp.Status == HandlerStatusCode.TransientError)
            {
                await Requeue(channel, evnt, queueSettings, DefaultDelayInMs);

                await NotifyUsageData(evnt, msg, queueSettings, UsageDataType.MessageTransientError, null);
            }
            else
            {
                await PublishToErrorExchange(channel, evnt, queueSettings, rsp.StatusReason, DefaultDelayInMs);

                await NotifyUsageData(evnt, msg, queueSettings, UsageDataType.MessageFatalError, null);
            }
        }

        private async Task NotifyUsageData(BasicDeliverEventArgs evnt, 
            Message msg,
            QueueSettings queueSettings,
            UsageDataType usageDataType,
            Dictionary<string, object> data)
        {
            if (usageDataCollectors == null || usageDataCollectors.Any() is false) return;

            var usageData = new UsageData 
            {
                MessageId = msg.Id?.ToString(),
                MessageType = msg.Type,
                AppId = msg.AppId,
                QueueName = queueSettings.QueueName,
                Type = usageDataType,
                Data = data ?? new Dictionary<string, object>()
            };

            try
            {
                await Task.WhenAll(usageDataCollectors.Select(x => x.Notify(usageData)));
            }
            catch(Exception e)
            {
                logger.LogError(e, "Failed to publish usage data with {errMsg}", e.Message);
            }
        }

        private async Task<HandlerResponse> ProcessInternal(BasicDeliverEventArgs evnt, Message msg, QueueSettings queueSettings)
        {
            var contentType = evnt.BasicProperties.ContentType;

            var serializer = serializers.FirstOrDefault(x => x.IsApplicable(contentType.EmptyAlternative(ContentTypeNames.Json)));

            if (serializer == null)
            {
                logger.LogError("No serializer found that support {contentType}", contentType);

                return new HandlerResponse { Status = HandlerStatusCode.FatalError, StatusReason = "SerializerNotFound" };
            }

            var handler = handlers.FirstOrDefault(x => x.IsApplicable(queueSettings.QueueName, msg));

            if (handler == null)
            {
                logger.LogError("No handler found for {msgId} and {msgType} {tenant} {appId}", msg.Id, msg.Type, msg.Tenant, msg.AppId);

                return new HandlerResponse { Status = HandlerStatusCode.FatalError, StatusReason = "NoHandlerApplicable" };
            }

            try
            {
                if (logger.IsEnabled(LogLevel.Trace))
                {
                    logger.LogTrace("{handler} start handling message {msgId}", handler.GetType(), msg.Id);
                }

                var sw = Stopwatch.StartNew();

                var rsp = await handler.Handle(msg, evnt.Body.ToArray(), serializer);

                sw.Stop();

                if (rsp.Status == HandlerStatusCode.Success)
                {
                    await NotifyUsageData(evnt, msg, queueSettings, UsageDataType.MessageProcessed, new Dictionary<string, object> 
                    {
                        [UsageDataPropertyNames.TimeTakenInMs] = sw.ElapsedMilliseconds
                    });

                    logger.LogTrace("Handler process the message {msgId} successfully.", msg.Id);
                }
                else
                {
                    logger.LogError("Handler indicates failure {status} to process the messsage {msgId}", rsp.Status, msg.Id);
                }

                return rsp;
            }
            catch (Exception e)
            {
                logger.LogError(e, e.Message);

                return new HandlerResponse { Status = HandlerStatusCode.FatalError, StatusReason = e.Message };
            }
        }

        private async Task Requeue(IModel channel, BasicDeliverEventArgs evnt, QueueSettings queueSettings, int defaultDelayInMs)
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

        private Task PublishToErrorExchange(IModel channel, BasicDeliverEventArgs evnt, QueueSettings queueSettings, string reason, int defaultDelayInMs)
        {
            if (queueSettings.ErrorExchangeName.IsEmpty())
            {
                logger.LogTrace("Requeue the message {errReason} as no error exchange defined.", reason);

                return Requeue(channel, evnt, queueSettings, defaultDelayInMs);
            };

            logger.LogTrace("Publising message to error exchange {errorExchangeName} {errorReason}", queueSettings.ErrorExchangeName, reason);

            reason = reason == null ? null : reason.Length > 64 ? reason.Substring(64) : reason;

            evnt.BasicProperties.SetHeader($"{queueSettings.ImplicitHeaderPrefix}{HeaderNames.ErrorReason}", reason);

            channel.BasicPublish(queueSettings.ErrorExchangeName, queueSettings.ErrorQueueName, evnt.BasicProperties, evnt.Body);

            logger.LogTrace("Acknowledge the message so that can be removed from queue.");

            channel.BasicAck(evnt.DeliveryTag, false);

            return Task.CompletedTask;
        }
    }
}
