using Autofac;
using Autofac.Extensions.DependencyInjection;
using EventBus;
using EventBus.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Threading.Tasks;

namespace Sample.Shared
{
    public static class EventBusConfigureExtensions
    {
        public static IServiceCollection AddEventBusConfigure(this IServiceCollection services)
        {
            services.AddLogging(builder =>
            {
                var serilogLogger = new LoggerConfiguration().WriteTo.File("Logs/Log.txt", rollingInterval: RollingInterval.Day).CreateLogger();
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddSerilog(logger: serilogLogger, dispose: true);
            });

            services.AddSingleton<IEventBusSubscriptionsManager, InMemoryEventBusSubscriptionsManager>();

            return services;
        }

        public static IServiceProvider ServiceProviderBuild(this IServiceCollection services)
        {
            var container = new ContainerBuilder();
            container.Populate(services);

            var serviceProvider = new AutofacServiceProvider(container.Build()) as IServiceProvider;
            return serviceProvider;
        }

        #region Producer Extensions
        public static void RunSampleProducer(this IServiceProvider serviceProvider, string appName)
        {
            var eventBus = serviceProvider.GetRequiredService<IEventBus>();
            var logger = serviceProvider.GetRequiredService<ILogger<Producer>>();

            var producer = new Producer(eventBus, logger);
            producer.RunPublisher(appName);

            //TODO:...
        }
        #endregion

        #region Consumer Extensions
        internal static string AppName;

        public static IServiceCollection AddIntegrationEventHandler(this IServiceCollection services)
        {
            services.AddTransient<ChangedStockIntegrationEventHandler>();
            //TODO:....

            return services;
        }

        public static void RunSampleConsumer(this IServiceProvider serviceProvider, string appName)
        {
            AppName = appName;
            var eventBus = serviceProvider.GetRequiredService<IEventBus>();
            eventBus.Subscribe<ChangedStockIntegrationEvent, ChangedStockIntegrationEventHandler>();

            //TODO:...
        } 
        #endregion
    }

    #region Consumer EventHandler
    class ChangedStockIntegrationEventHandler : IIntegrationEventHandler<ChangedStockIntegrationEvent>
    {
        private readonly ILogger<ChangedStockIntegrationEventHandler> _logger;
        public ChangedStockIntegrationEventHandler(ILogger<ChangedStockIntegrationEventHandler> logger)
        {
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
        }

        public async Task Handle(ChangedStockIntegrationEvent @event)
        {
            //TODO:....
            var logMessage = $"--- Subscribe -- {EventBusConfigureExtensions.AppName} - {@event.Id} - message:{JsonConvert.SerializeObject(@event)}";
            _logger.LogInformation(logMessage);
            Console.WriteLine(logMessage);
        }
    }
    #endregion

    #region Producer Publisher
    class Producer
    {
        private readonly IEventBus _eventBus;
        private readonly ILogger<Producer> _logger;
        public Producer(IEventBus eventBus, ILogger<Producer> logger)
        {
            _eventBus = eventBus;
            _logger = logger;
        }

        public void RunPublisher(string appName)
        {
            for (int i = 0; i < 20_000; i++)
            {
                var @event = new ChangedStockIntegrationEvent(i + 1000, i);
                var logMessage = $"----Publishing -- integration event: {@event.Id} from {appName} - ({JsonConvert.SerializeObject(@event)})";

                try
                {
                    _eventBus.Publish(@event);

                    Console.WriteLine(logMessage);
                    _logger.LogInformation(logMessage);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Message: {ex.Message}");
                    Console.WriteLine(logMessage);
                }

                System.Threading.Thread.Sleep(500);
            }
        }
    } 
    #endregion
}
