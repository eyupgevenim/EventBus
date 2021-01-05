using Autofac;
using EventBus.Abstractions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace EventBus
{
    public abstract class SubscribeProcessEvent
    {
        protected readonly ILogger<IEventBus> _logger;
        protected readonly IEventBusSubscriptionsManager _subsManager;
        private readonly ILifetimeScope _autofac;
        private readonly string _appName;
        private readonly string _autofacScopeName;

        protected SubscribeProcessEvent(ILogger<IEventBus> logger, 
            IEventBusSubscriptionsManager subsManager, 
            ILifetimeScope autofac,
            string appName,
            string autofacScopeName = "e_event_bus")
        {
            _logger = logger;
            _subsManager = subsManager;
            _autofac = autofac;
            _appName = appName;
            _autofacScopeName = autofacScopeName;
        }

        protected virtual async Task ProcessEvent(string eventName, string message)
        {
            _logger.LogTrace($"Processing {_appName} event: {{EventName}}", eventName);

            if (_subsManager.HasSubscriptionsForEvent(eventName))
            {
                using (var scope = _autofac.BeginLifetimeScope(_autofacScopeName))
                {
                    var subscriptions = _subsManager.GetHandlersForEvent(eventName);
                    foreach (var subscription in subscriptions)
                    {
                        if (subscription.IsDynamic)
                        {
                            var handler = scope.ResolveOptional(subscription.HandlerType) as IDynamicIntegrationEventHandler;
                            if (handler == null) continue;
                            dynamic eventData = JObject.Parse(message);

                            await Task.Yield();
                            await handler.Handle(eventData);
                        }
                        else
                        {
                            var handler = scope.ResolveOptional(subscription.HandlerType);
                            if (handler == null) continue;
                            var eventType = _subsManager.GetEventTypeByName(eventName);
                            var integrationEvent = JsonConvert.DeserializeObject(message, eventType);
                            var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);

                            await Task.Yield();
                            await (Task)concreteType.GetMethod("Handle").Invoke(handler, new object[] { integrationEvent });
                        }
                    }
                }
            }
            else
            {
                _logger.LogWarning($"No subscription for {_appName} event: {{EventName}}", eventName);
            }
        }

    }
}
