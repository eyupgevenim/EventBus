ALL Message Queue (MQ) with .NET Core
=====================================

All Message Queue (MQ) Software producer and consumer (<b>ActiveMQ, AmazonSQS, AzureStorageQueues, GoogleCloudPubSub, IBMMQ, Kafka, ServiceBus </b> ...) samples with .NET Core and deploy docker


| Event Bus | Sample Producer | Sample Consumer | deploy docker-compose |
| --------- | --------------- | --------------- | --------------------- |
|[EventBus.ActiveMQ](/src/EventBus.ActiveMQ)|[Sample.ActiveMQ.Producer](/src/Samples/Sample.ActiveMQ.Producer)|[Sample.ActiveMQ.Consumer](/src/Samples/Sample.ActiveMQ.Consumer)|[docker-compose.activemq.yml](./docker-compose.activemq.yml)|
|[EventBus.AmazonSQS](/tree/master/src/EventBus.AmazonSQS)|[Sample.AmazonSQS.Producer](/tree/master/src/Samples/Sample.AmazonSQS.Producer)|[Sample.AmazonSQS.Consumer](/tree/master/src/Samples/Sample.AmazonSQS.Consumer)|[docker-compose.amazonsqs.yml](./docker-compose.amazonsqs.yml)|
|[EventBus.AzureStorageQueues](/tree/master/src/EventBus.)|[Sample.AzureStorageQueues.Producer](/tree/master/src/Samples/Sample.AzureStorageQueues.Producer)|[Sample.AzureStorageQueues.Consumer](/tree/master/src/Samples/Sample.AzureStorageQueues.Consumer)|[docker-compose.azurestoragequeues.yml](./docker-compose.azurestoragequeues.yml)|
|[EventBus.GoogleCloudPubSub](/tree/master/src/EventBus.GoogleCloudPubSub)|[Sample.GoogleCloudPubSub.Producer](/tree/master/src/Samples/Sample.GoogleCloudPubSub.Producer)|[Sample.GoogleCloudPubSub.Consumer](/tree/master/src/Samples/Sample.GoogleCloudPubSub.Consumer)|[docker-compose.googlecloudpubsub.yml](./docker-compose.googlecloudpubsub.yml)|
|[EventBus.IBMMQ](/tree/master/src/EventBus.IBMMQ)|[Sample.IBMMQ.Producer](/tree/master/src/Samples/Sample.IBMMQ.Producer)|[Sample.IBMMQ.Consumer](/tree/master/src/Samples/Sample.IBMMQ.Consumer)|[docker-compose.ibmmq.yml](./docker-compose.ibmmq.yml)|
|[EventBus.Kafka](/tree/master/src/EventBus.Kafka)|[Sample.Kafka.Producer](/tree/master/src/Samples/Sample.Kafka.Producer)|[Sample.Kafka.Consumer](/tree/master/src/Samples/Sample.kafka.Consumer)|[docker-compose.Kafka.yml](./docker-compose.kafka.yml)|
|[EventBus.RabbitMQ](/tree/master/src/EventBus.RabbitMQ)|[Sample.RabbitMQ.Producer](/tree/master/src/Samples/Sample.RabbitMQ.Producer)|[Sample.RabbitMQ.Consumer](/tree/master/src/Samples/Sample.RabbitMQ.Consumer)|[docker-compose.rabbitmq.yml](./docker-compose.rabbitmq.yml)|
|[EventBus.ServiceBus](/tree/master/src/EventBus.ServiceBus)|[Sample.ServiceBus.Producer](/tree/master/src/Samples/Sample.ServiceBus.Producer)|[Sample.ServiceBus.Consumer](/tree/master/src/Samples/Sample.ServiceBus.Consumer)|[docker-compose.servicebus.yml](./docker-compose.servicebus.yml)|

<!--|[EventBus.](/tree/master/src/EventBus.)|[Sample..Producer](/tree/master/src/Samples/Sample..Producer)|[Sample..Consumer](/tree/master/src/Samples/Sample..Consumer)|[docker-compose..yml](./docker-compose..yml)| -->

A new Mq integration and project template
```
-- src
    |
    | -- Samples
            |
            | -- deploy
                    |
                    | -- docker-compose.newmq.yml
                    | ...
            | -- Sample.NewMq.Consumer
            | -- Sample.NewMq.Producer
            | ...
            | -- Sample.Shared
    | -- EventBus
    | -- EventBus.NewMq
    | ...


```

Usage
-----


- Event type

```csharp
public class your_event_type : IntegrationEvent
{
    //TODO:...
}
```

 - Subscribe handler

```csharp
public class your_subscribe_handler_type : IIntegrationEventHandler<your_event_type>
{
    //TODO:...
    public async Task Handle(your_event_type @event)
    {
        //TODO:....
    }
}
```


 - Add subscriber type and event subscribe
```csharp
var eventBus = serviceProvider.GetRequiredService<IEventBus>();
eventBus.Subscribe<your_event_type, your_subscribe_handler_type>();

```

 - Event publish
```csharp
var eventBus = serviceProvider.GetRequiredService<IEventBus>();
var @event = new your_event_type{ /*TODO:...*/};
eventBus.Publish(@event);

```

 - Register DI subscriber handler
```csharp
services.AddTransient<your_subscribe_handler_type>();
//TODO:...
```

 - Register DI MQ subscriptions manager
```csharp
services.AddSingleton<IEventBusSubscriptionsManager, InMemoryEventBusSubscriptionsManager>();
```

 - Register DI your MQ persistent connection
```csharp
services.AddSingleton<your_mq_PersistentConnection>(sp =>
{
    var config = ....
    var logger = sp.GetRequiredService<ILogger<Default_your_mq_PersistentConnection>>();
    return new Default_your_mq_PersistentConnection(logger, config);
});
```

 - Register DI your MQ
```csharp
services.AddSingleton<IEventBus, EventBus_your_mq>(sp =>
{
    var persistentConnection = sp.GetRequiredService<your_mq_PersistentConnection>();
    var iLifetimeScope = sp.GetRequiredService<ILifetimeScope>();
    var logger = sp.GetRequiredService<ILogger<EventBus_your_mq>>();
    var eventBusSubcriptionsManager = sp.GetRequiredService<IEventBusSubscriptionsManager>();

    return new EventBus_your_mq(persistentConnection, logger, iLifetimeScope, eventBusSubcriptionsManager);
});
```
