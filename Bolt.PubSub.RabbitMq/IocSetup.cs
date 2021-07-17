using Bolt.PubSub.RabbitMq.Subscribers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bolt.PubSub.RabbitMq
{
    public static class IocSetup
    {
        public static IServiceCollection AddRabbitMqPublisher(
            this IServiceCollection services,
            IConfiguration configuration,
            RabbitMqSetupOptions options = null)
        {
            AddCommonServices(services, configuration, options);

            services.TryAddTransient<IMessagePublisher, Publishers.Publisher>();
            services.TryAddTransient<Publishers.IRabbitMqPublisher, Publishers.RabbitMqPublisher>();

            return services;
        }

        private static void AddCommonServices(IServiceCollection services, IConfiguration configuration, RabbitMqSetupOptions options)
        {
            options ??= new RabbitMqSetupOptions();

            var settings = new RabbitMqSettings();

            if (options.ConfigSectionName.HasValue())
            {
                configuration.GetSection(options.ConfigSectionName).Bind(settings);
            }

            services.AddLogging();
            services.TryAddSingleton<IUniqueId, UniqueId>();
            services.TryAddSingleton<ISystemClock, SystemClock>();
            services.TryAddSingleton<IRabbitMqSettings>(x => settings);
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IMessageSerializer, JsonSerializer>());
            services.TryAddSingleton<RabbitMqConnection>();
        }

        public static IServiceCollection AddRabbitMqSubscriber(this IServiceCollection services,
            IConfiguration configuration,
            RabbitMqSubscriberOptions options = null)
        {
            options ??= new RabbitMqSubscriberOptions();

            AddCommonServices(services, configuration, options);

            var settings = new SubscriberSettings();

            if (options.ConfigSectionName.HasValue())
            {
                configuration.GetSection(options.ConfigSectionName).Bind(settings);
            }

            services.TryAddSingleton(c => settings);
            services.TryAddEnumerable(ServiceDescriptor.Transient<IRabbitMqSetup, SimpleRabbitMqSetup>());
            services.TryAddTransient<IMessageSubscriber, MessageSubscriber>();
            services.TryAddTransient<QueueListener>();

            if(options.RunAsHostedService)
            {
                services.AddHostedService<WorkerService>();
            }

            return services;
        }
    }

    public record RabbitMqSetupOptions
    {
        public string ConfigSectionName { get; init; } = "Bolt:PubSub:RabbitMq";
    }

    public record RabbitMqSubscriberOptions : RabbitMqSetupOptions
    {
        public bool RunAsHostedService { get; set; } = true;
    }
}
