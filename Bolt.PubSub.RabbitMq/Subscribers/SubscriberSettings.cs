using System.Collections.Generic;

namespace Bolt.PubSub.RabbitMq.Subscribers
{
    public class SubscriberSettings
    {
        public bool EnableDeadLetterQueue { get; set; }
        public bool EnableRetryQueue { get; set; }
        public SubscriberUnitSettings[] Settings { get; set; }
    }

    public class SubscriberUnitSettings
    {
        public string ExchangeName { get; set; }
        public string ExchangeType { get; set; } = "headers";
        public string QueueName { get; set; }
        public int ListenersCount { get; set; } = 1;
        public string RoutingKey { get; set; }
        public int ProcessCount { get; set; } = 2;
        public int? PrefetchCount { get; set; }
        public Dictionary<string, string> Bindings { get; set; }
        public int DelayOnErrorInMs { get; set; }
        /// <summary>
        /// Prefix will be prepend with message type value for [blt-msg-type] header if defined.
        /// </summary>
        public string MessageTypePrefix { get; set; }
    }
}
