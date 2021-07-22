using System;
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

            channel.BasicConsume(queueSettings.QueueName, false, consumer);
        }


        private async Task Consumer_Received(object sender, BasicDeliverEventArgs evnt)
        {
            using var _ = logger.BeginScope("{traceId}{queueName}{msgId}{msgType}", 
                evnt.BasicProperties.CorrelationId ?? Guid.NewGuid().ToString(),
                queueSettings.QueueName,
                evnt.BasicProperties.MessageId,
                evnt.BasicProperties.Type);

            logger.LogTrace("New message recieved");

            using var scope = serviceProvider.CreateScope();

            var processor = scope.ServiceProvider.GetRequiredService<MessageProcessor>();

            await processor.Process(channel, evnt, queueSettings);
        }

        public void Dispose()
        {
            logger.LogInformation("Disposing channel");

            channel?.Dispose();
        }
    }
}
