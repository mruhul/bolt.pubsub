using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System;


namespace Bolt.PubSub.RabbitMq
{
    internal sealed class RabbitMqConnection : IDisposable
    {
        private readonly Lazy<IConnection> connection;
        private readonly IRabbitMqSettings settings;
        private readonly ILogger<RabbitMqLogger> logger;

        public RabbitMqConnection(IRabbitMqSettings settings, ILogger<RabbitMqLogger> logger)
        {
            this.settings = settings;
            this.logger = logger;
            connection = new Lazy<IConnection>(() => CreateConnection());
        }

        private IConnection CreateConnection()
        {
            if (settings == null) throw new ArgumentException($"{nameof(settings)} cannot be null.");
            if (settings.ConnectionString.IsEmpty()) throw new ArgumentException($"{nameof(settings.ConnectionString)} cannot be null or empty.");

            logger.LogTrace("Start creating rabbitmq connection.");

            var connectionFactory = new ConnectionFactory
            {
                Uri = new Uri(settings.ConnectionString),
                DispatchConsumersAsync = true
            };

            var con = connectionFactory.CreateConnection();

            logger.LogTrace("Rabbitmq connection created successfully.");

            if (settings.SkipCreateExchange is true || settings.ExchangeName.IsEmpty()) return con;

            var exchangeType = settings.ExchangeType.EmptyAlternative(RabbitMQ.Client.ExchangeType.Headers);

            logger.LogTrace("Start creating {exchangeName} of {exchangeType}.", settings.ExchangeName, exchangeType);

            using var channel = con.CreateModel();

            channel.ExchangeDeclare(settings.ExchangeName,
                exchangeType,
                true,
                false);

            logger.LogTrace("{exchangeName} of {exchangeType} created successfully", settings.ExchangeName, exchangeType);

            return con;
        }

        public IConnection GetOrCreate()
        {
            return connection.Value;
        }

        public void Dispose()
        {
            if (connection.IsValueCreated)
            {
                logger.LogTrace("Disposing rabbitmq connection");

                connection.Value?.Dispose();
            }
        }
    }
}
