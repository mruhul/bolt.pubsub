using System.Collections.Generic;

namespace Bolt.PubSub.RabbitMq.Subscribers
{
    public class SubscriberSettings
    {
        /// <summary>
        /// By default this is false. Set true if you like to push all non transient errors to error queue
        /// </summary>
        public bool EnableDeadLetterQueue { get; set; }
        public SubscriberUnitSettings[] Settings { get; set; }
    }

    public class SubscriberUnitSettings
    {
        public string ExchangeName { get; set; }
        public string ExchangeType { get; set; } = "headers";
        public string QueueName { get; set; }
        public string RoutingKey { get; set; }
        public int ProcessCount { get; set; } = 2;
        public int? PrefetchCount { get; set; }
        public Dictionary<string, string> Bindings { get; set; }

        /// <summary>
        /// Add some delay before requeue a message. The system requeue only when the error is transient error or
        /// EnableDeadLetterQueue value is false
        /// </summary>
        public int RequeueDelayInMs { get; set; }
        public string ImplicitHeaderPrefix { get; set; } = "blt-";
    }
}
