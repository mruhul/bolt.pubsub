using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Bolt.PubSub.RabbitMq.Subscribers
{
    internal sealed class SimpleRabbitMqSetup : IRabbitMqSetup
    {
        private readonly RabbitMqConnection connection;
        private readonly ILogger<SimpleRabbitMqSetup> logger;
        private readonly Dictionary<string, string> exchanges = new Dictionary<string, string>();
        private readonly Dictionary<string, string> queues = new Dictionary<string, string>();

        public SimpleRabbitMqSetup(RabbitMqConnection connection, ILogger<SimpleRabbitMqSetup> logger)
        {
            this.connection = connection;
            this.logger = logger;
        }

        public bool IsApplicable(SubscriberSettings subscriberSettings)
        {
            return true;
        }

        public QueueSettings[] Run(SubscriberSettings subscriberSettings)
        {
            var result = new List<QueueSettings>(subscriberSettings.Settings.Length);

            var enableDeadLetterQueue = subscriberSettings.EnableDeadLetterQueue;

            var con = connection.GetOrCreate();

            using var channel = con.CreateModel();

            foreach (var setting in subscriberSettings.Settings)
            {
                if (setting.ExchangeName.IsEmpty())
                {
                    logger.LogError("Exchange name is empty for subscriber settings. So ignoring this invalid settings.");
                    continue;
                }
                if(setting.QueueName.IsEmpty())
                {
                    logger.LogError("Queue name is empty for subscriber settings. So ignoring this invalid settings.");
                    continue;
                }

                var bindings = new Dictionary<string, object>();
                if (setting.Bindings != null)
                {
                    foreach (var binding in setting.Bindings)
                    {
                        bindings[binding.Key] = binding.Value;
                    }
                }

                if(exchanges.ContainsKey(setting.ExchangeName) is false)
                {
                    channel.ExchangeDeclare(setting.ExchangeName, setting.ExchangeType.EmptyAlternative(RabbitMQ.Client.ExchangeType.Headers), true, false, null);

                    exchanges.Add(setting.ExchangeName, setting.ExchangeName);
                }

                if (queues.ContainsKey(setting.QueueName) is false)
                {
                    channel.QueueDeclare(setting.QueueName, true, false, false, null);
                    channel.QueueBind(setting.QueueName, setting.ExchangeName, setting.RoutingKey ?? string.Empty, bindings);

                    queues.Add(setting.QueueName, setting.QueueName);
                }

                var errorExchange = string.Empty;
                var processCount = setting.ProcessCount <= 0 ? 1 : setting.ProcessCount;
                
                if(enableDeadLetterQueue)
                {
                    errorExchange = $"{setting.ExchangeName}.DX";
                    var errorQueue = $"{setting.QueueName}.DQ";

                    if(exchanges.ContainsKey(errorExchange) is false)
                    {
                        channel.ExchangeDeclare(errorExchange, RabbitMQ.Client.ExchangeType.Direct, true, false, null);

                        exchanges.Add(errorExchange, errorExchange);
                    }

                    channel.QueueDeclare(errorQueue, true, false, false, null);
                    channel.QueueBind(errorQueue, errorExchange, setting.QueueName, null);                    
                }

                result.Add(new QueueSettings
                {
                    ExchangeName = setting.ExchangeName,
                    ErrorExchangeName = errorExchange,
                    QueueName = setting.QueueName,
                    ProcessCount = processCount,
                    PrefetchCount = setting.PrefetchCount,
                    RequeueDelayInMs = setting.RequeueDelayInMs,
                    ImplicitHeaderPrefix = setting.ImplicitHeaderPrefix
                });
            }

            return result.ToArray();
        }
    }
}
