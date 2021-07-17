using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;

namespace Bolt.PubSub.RabbitMq.Subscribers
{
    internal sealed class MessageReader
    {
        public Message Read(BasicDeliverEventArgs evnt, QueueSettings settings)
        {
            return new Message
            {
                AppId = evnt.BasicProperties.TryReadHeader($"{settings.ImplicitHeaderPrefix}{HeaderNames.AppId}"),
                CorrelationId = evnt.BasicProperties.CorrelationId,
                CreatedAt = evnt.BasicProperties.TryReadHeader($"{settings.ImplicitHeaderPrefix}{HeaderNames.CreatedAt}").TryParseUtcFormat(),
                Id = Guid.TryParse(evnt.BasicProperties.MessageId, out var result) ? result : null,
                Tenant = evnt.BasicProperties.TryReadHeader($"{settings.ImplicitHeaderPrefix}{HeaderNames.Tenant}"),
                Type = evnt.BasicProperties.TryReadHeader($"{settings.ImplicitHeaderPrefix}{HeaderNames.MessageType}"),
                Version = evnt.BasicProperties.TryReadHeader($"{settings.ImplicitHeaderPrefix}{HeaderNames.Version}").ToInt() ?? 1,
                Headers = ToHeaders(evnt.BasicProperties),
            };
        }

        private Dictionary<string, string> ToHeaders(IBasicProperties prop)
        {
            var result = new Dictionary<string, string>();

            if (prop.Headers == null) return result;

            foreach (var item in prop.Headers)
            {
                result[item.Key] = System.Text.Encoding.UTF8.GetString((byte[])item.Value);
            }

            return result;
        }
    }
}
