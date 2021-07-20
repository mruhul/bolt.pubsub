using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Bolt.PubSub.RabbitMq.Publishers
{
    internal sealed class Publisher : IMessagePublisher, IDisposable
    {
        private readonly IRabbitMqPublisher publisherWrapper;
        private readonly IRabbitMqSettings settings;
        private readonly IEnumerable<IMessageFilter> filters;
        private readonly IEnumerable<IMessageSerializer> serializers;
        private readonly IUniqueId uniqueId;
        private readonly ISystemClock clock;
        private readonly ILogger<RabbitMqLogger> logger;

        public Publisher(IRabbitMqPublisher publisherWrapper,
            IRabbitMqSettings settings,
            IEnumerable<IMessageFilter> filters,         
            IEnumerable<IMessageSerializer> serializers,
            IUniqueId uniqueId,
            ISystemClock clock,            
            ILogger<RabbitMqLogger> logger)
        {
            this.publisherWrapper = publisherWrapper;
            this.settings = settings;
            this.filters = filters;
            this.serializers = serializers;
            this.uniqueId = uniqueId;
            this.clock = clock;
            this.logger = logger;
        }

        public Task<Guid> Publish<T>(Message<T> msg)
        {
            if (settings.ExchangeName.IsEmpty()) 
                throw new Exception("Exchange name cannot be empty. Make sure you provide an exchange name in settings.");

            if(msg.Id == null || msg.Headers == null)
            {
                msg = msg with
                {
                    Id = msg.Id.HasValue ? msg.Id : uniqueId.New(),
                    Headers = msg.Headers == null ? new Dictionary<string, string>() : msg.Headers, 
                };
            }

            using var _ = logger.BeginScope("{msgId}", msg.Id);

            var msgType = msg.Type.EmptyAlternative(MessageTypeNameProvider.Get<T>());

            if (filters != null)
            {
                foreach (var filter in filters)
                {
                    if (logger.IsEnabled(LogLevel.Trace))
                    {
                        logger.LogTrace("Start applying filter {filterType}", filter.GetType());
                    }

                    msg = filter.Filter(msg, msgType);
                }
            }

            var appId = msg.AppId.EmptyAlternative(settings.AppId.EmptyAlternative("na"));
            var contentType = settings.ContentType.EmptyAlternative(ContentTypeNames.Json);
            var correlationId = msg.CorrelationId.IsEmpty() ? uniqueId.New().ToString() : msg.CorrelationId;
            var version = msg.Version == 0 ? "1" : msg.Version.ToString();

            AddHeaderIfNotSet(msg, HeaderNames.AppId, appId, settings.ImplicitHeaderPrefix);
            AddHeaderIfNotSet(msg, HeaderNames.Tenant, msg.Tenant, settings.ImplicitHeaderPrefix);
            AddHeaderIfNotSet(msg, HeaderNames.CreatedAt, msg.CreatedAt?.ToUtcFormat(), settings.ImplicitHeaderPrefix);
            AddHeaderIfNotSet(msg, HeaderNames.MessageType, msgType, settings.ImplicitHeaderPrefix);
            AddHeaderIfNotSet(msg, HeaderNames.Version, version, settings.ImplicitHeaderPrefix);
            AddHeaderIfNotSet(msg, msgType, "1", string.Empty);
            AddHeaderIfNotSet(msg, HeaderNames.PublishedAt, clock.UtcNow.ToUtcFormat(), settings.ImplicitHeaderPrefix);


            var serializer = serializers.FirstOrDefault(s => s.IsApplicable(settings.ContentType.EmptyAlternative(ContentTypeNames.Json)));

            if (serializer == null) 
                throw new Exception($"No serializer available that support content type {settings.ContentType}");

            publisherWrapper.Publish(new PublishMessageWrapperDto
            {
                Content = serializer.Serialize(msg.Content),
                ContentType = contentType,
                CorrelationId = msg.CorrelationId,
                Exchange = settings.ExchangeName,
                Type = msg.Type,
                Headers = msg.Headers,
                ExpiryInSeconds = settings.DefaultTTLInSeconds,
                MessageId = msg.Id.Value,
                RoutingKey = RoutingKeyRequired() ? $"{appId}.{msgType}.{version}" : string.Empty
            });

            logger.LogTrace("Message with {msgId} {msgType} {appId} {version} published successfully.", msg.Id, msgType, appId, version);

            return Task.FromResult(msg.Id.Value);
        }

        private bool RoutingKeyRequired()
        {
            return !(settings.ExchangeType.IsSame(RabbitMQ.Client.ExchangeType.Direct)
                || settings.ExchangeType.IsSame(RabbitMQ.Client.ExchangeType.Topic));
        }

        private static void AddHeaderIfNotSet<T>(Message<T> msg, string key, string value, string keyPrefix)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            
            var finalKey = keyPrefix.HasValue() 
                ? $"{keyPrefix}{key}"
                : key;

            if(!msg.Headers.ContainsKey(finalKey))
            {
                msg.Headers[finalKey] = value;
            }
        }

        public void Dispose()
        {
            publisherWrapper?.Dispose();
        }
    }
}
