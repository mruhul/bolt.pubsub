using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Bolt.PubSub.RabbitMq.Subscribers
{
    internal sealed class QueueListener : IDisposable
    {
        private readonly ILogger<RabbitMqLogger> logger;
        private readonly IServiceProvider serviceProvider;

        private readonly List<QueueSingleChannelListener> listeners = new List<QueueSingleChannelListener>();

        public QueueListener(ILogger<RabbitMqLogger> logger, 
            IServiceProvider serviceProvider)
        {
            this.logger = logger;
            this.serviceProvider = serviceProvider;
        }

        
        public void Listen(QueueSettings settings)
        {
            for(var i = 0; i < settings.ProcessCount; i++)
            {
                logger.LogTrace("Creating consumer {index} for {queue}", i, settings.QueueName);

                var listener = new QueueSingleChannelListener();

                listener.Listen(serviceProvider, settings);

                listeners.Add(listener);
            }
        }

        public void Dispose()
        {
            if (listeners == null) return;

            foreach(var listener in listeners)
            {
                listener.Dispose();
            }
        }
    }
}
