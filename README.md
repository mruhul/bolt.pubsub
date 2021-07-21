# Bolt.PubSub

A library to abstract different pubsub provider e.g RabbitMq, AWS SNS etc

At present we only have support for RabbitMq

# RabbitMQ Impl

For rabbitmq please install the latest nuget package of Bolt.PubSub.RabbitMq in your project.

## How to publish message to RabbitMq

- **First setup the library in your IOC**


 ```
  services.AddRabbitMqPublisher(configuration);
 ```

You also need to define an instance of IConfugration and pass that to `AddRabbitMqPublisher` method. By default the lib try to load settings from `Bolt:PubSub:RabbitMq` section. But you can change that if you need by defining this in options as below:

```
  var opt = new RabbitMqSetupOptions
  {
    ConfigSectionName = "App:RabbitMq"
  }

  services.AddRabbitMqPublisher(configuration, opt);
```

- **Define settings in `appsettings.json`. The section name depends on what you set during ioc setup. Default section name is `Bolt:PubSub:RabbitMq`.**

```
  Bolt: {
    "PubSub": {
      "RabbitMq": {
        "ConnectionString": "<your connection string>",
        "ExchangeName": "<name of the exchange>",
        "SkipCreateExchange" : false,
        "ExchangeType": "headers" // other values topic/direct/fanout
        "AppId": "<application id>" // (optional),
        "ImplicitHeaderPrefix": "blt-"
      }
    }
  }
```
  - **Now you can inject IMessagePublisher in your class and start publishing message**

```
  public class OrderService
  {
    private readonly IMessagePublisher publisher;

    public OrderService(IMessagePublisher publisher)
    {
      this.publisher = publisher;
    }

    public void SaveOrder(OrderRecord order)
    {
      ...

      publisher.Publish(new OrderCompleted
      {
        OrderId = order.Id
      });

      // Or you can use more verbose method
      publisher.Publish(new Message
      {
        Content = new OrderCompleted{
          OrderId = order.id
        },
        Id = Guid.NewGuid(),
        AppId = "You can define onw app id from here",
        Type = "OrderCompleted",
        Tenant = "tenant-a",
        CorrelationId = "<cid>",
        Version = 1,
        CreatedAt = DateTime.UtcNow,
        Headers = new Dictionary
        {
          ["my-header"] = "header-value"
        }
      })
    }
  }
```
## How to subscribe for RabbitMq message

- **First setup the library in your IOC**


 ```
  services.AddRabbitMqSubscriber(configuration);
 ```

You also need to define an instance of IConfugration and pass that to `AddRabbitMqSubscriber` method. By default the lib try to load settings from `Bolt:PubSub:RabbitMq` section. But you can change that if you need by defining this in options as below:

```
  var opt = new RabbitMqSubscriberOptions
  {
    ConfigSectionName = "App:RabbitMq"
  }

  services.AddRabbitMqSubscriber(configuration, opt);
```

- **Define settings in `appsettings.json`. The section name depends on what you set during ioc setup. Default section name is `Bolt:PubSub:RabbitMq`.**

```
  Bolt: {
    "PubSub": {
      "RabbitMq": {
        "ConnectionString": "<your connection string>",
        "ExchangeName": "<name of the exchange>",
        "EnableDeadLetterQueue": false,
        "Settings": [
          {
            "ExchangeName": "<exchange your queue need to bind>",
            "QueueName": "<queuename you like to listen>",
            "ProcessCount": 1, // number of consumer you like to create
            "RoutingKey": null, // use to define binding with exchange if provided,
            "PrefetchCount": null, // if you like to prefetch more than 1 msg
            "ImplicitHeaderPrefix": "blt-",
            "RequeueDelayInMs": 60000, // 1 minute
          }
        ]
      }
    }
  }
```