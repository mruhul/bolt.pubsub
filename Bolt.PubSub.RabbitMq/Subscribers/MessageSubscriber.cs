using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;

namespace Bolt.PubSub.RabbitMq.Subscribers
{
    internal sealed class MessageSubscriber : IMessageSubscriber, IDisposable
    {
        private readonly IServiceProvider serviceProvider;
        private readonly SubscriberSettings subscriberSettings;
        private readonly ILogger<RabbitMqLogger> logger;
        private readonly IEnumerable<IRabbitMqSetup> rabbitMqSetups;
        private readonly List<QueueListener> queueListeners = new List<QueueListener>();

        public MessageSubscriber(IServiceProvider serviceProvider, 
            SubscriberSettings subscriberSettings,
            ILogger<RabbitMqLogger> logger,
            IEnumerable<IRabbitMqSetup> rabbitMqSetups)
        {
            this.serviceProvider = serviceProvider;
            this.subscriberSettings = subscriberSettings;
            this.logger = logger;
            this.rabbitMqSetups = rabbitMqSetups;
        }

        public void Dispose()
        {
            if (queueListeners == null) return;

            foreach(var listener in queueListeners)
            {
                listener.Dispose();
            }
        }

        public Task Start(CancellationToken cancellationToken)
        {
            logger.LogTrace("Starting message subscriber");

            var rmqSetup = rabbitMqSetups.FirstOrDefault(x => x.IsApplicable(subscriberSettings));

            if (rmqSetup == null) throw new Exception("No rabbitmq setup defined for current subscriber settings.");

            var queueSettings = rmqSetup.Run(subscriberSettings);

            foreach(var settings in queueSettings)
            {
                var listener = serviceProvider.GetService<QueueListener>();
                listener.Listen(settings);

                queueListeners.Add(listener);
            }

            return Task.CompletedTask;
        }
    }
}
