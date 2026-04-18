using Confluent.Kafka;
using System.Text.Json;

public class KafkaNotificationSender : INotificationSender
{
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;

    public KafkaNotificationSender(IConfiguration configuration)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"]
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
        _topic = configuration["Kafka:Topic"];
    }

    public async Task SendAsync(LinkUpdateDto update, CancellationToken cancellationToken)
    {
        var message = new Message<string, string>
        {
            Key = update.ChatId.ToString(),
            Value = JsonSerializer.Serialize(update)
        };

        await _producer.ProduceAsync(_topic, message, cancellationToken);
    }
}
