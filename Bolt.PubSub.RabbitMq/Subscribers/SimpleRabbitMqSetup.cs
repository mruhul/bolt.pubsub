using System.Collections.Generic;

namespace Bolt.PubSub.RabbitMq.Subscribers
{
    internal sealed class SimpleRabbitMqSetup : IRabbitMqSetup
    {
        private readonly RabbitMqConnection connection;
        private readonly Dictionary<string, string> exchanges = new Dictionary<string, string>();
        private readonly Dictionary<string, string> queues = new Dictionary<string, string>();

        public SimpleRabbitMqSetup(RabbitMqConnection connection)
        {
            this.connection = connection;
        }

        public bool IsApplicable(SubscriberSettings subscriberSettings)
        {
            return subscriberSettings.EnableRetryQueue is false;
        }

        public QueueSettings[] Run(SubscriberSettings subscriberSettings)
        {
            var result = new List<QueueSettings>(subscriberSettings.Settings.Length);

            var enableDeadLetterQueue = subscriberSettings.EnableDeadLetterQueue;

            var con = connection.GetOrCreate();

            using var channel = con.CreateModel();

            foreach (var setting in subscriberSettings.Settings)
            {
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
                    channel.ExchangeDeclare(setting.ExchangeName, setting.ExchangeType, true, false, null);

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

                    channel.ExchangeDeclare(errorExchange, "direct", true, false, null);
                    channel.QueueDeclare(errorQueue, true, false, false, null);
                    channel.QueueBind(errorQueue, errorExchange, string.Empty, null);                    
                }

                result.Add(new QueueSettings
                {
                    ExchangeName = setting.ExchangeName,
                    ErrorExchangeName = errorExchange,
                    QueueName = setting.QueueName,
                    ProcessCount = processCount,
                    PrefetchCount = setting.PrefetchCount,
                    DelayOnErrorInMs = setting.DelayOnErrorInMs,
                });
            }

            return result.ToArray();
        }
    }
}
