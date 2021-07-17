using System;

namespace Bolt.PubSub.RabbitMq.Subscribers
{
    internal interface IRabbitMqSetup
    {
        QueueSettings[] Run(SubscriberSettings subscriberSettings);
        bool IsApplicable(SubscriberSettings subscriberSettings);
    }

    public record QueueSettings
    {
        public string ExchangeName { get; init; }
        public string QueueName { get; init; }
        public string ErrorExchangeName { get; init; }
        public string RetryExchangeName { get; init; }
        public int ProcessCount { get; init; }
        public int? PrefetchCount { get; init; }
        public int RequeueDelayInMs { get; set; }
    }
}
