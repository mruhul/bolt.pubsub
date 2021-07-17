using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bolt.PubSub.RabbitMq.Subscribers
{
    internal sealed class WorkerService : BackgroundService
    {
        private readonly IMessageSubscriber subscriber;
        private readonly ILogger<RabbitMqLogger> logger;

        public WorkerService(IMessageSubscriber subscriber, ILogger<RabbitMqLogger> logger)
        {
            this.subscriber = subscriber;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogTrace("Starting rabbitmq worker service");

            await subscriber.Start(stoppingToken);

            while(stoppingToken.IsCancellationRequested is false)
            {
                await Task.Delay(TimeSpan.FromMinutes(5));
            }

            logger.LogTrace("Exiting subscriber");
        }
    }
}
