public class KafkaConsumerService : BackgroundService
{
    private readonly IConfiguration _configuration;

    public KafkaConsumerService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _configuration["Kafka:BootstrapServers"],
            GroupId = "bot-consumer-group",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(_configuration["Kafka:Topic"]);

        return Task.Run(() =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var result = consumer.Consume(stoppingToken);
                var update = JsonSerializer.Deserialize<LinkUpdateDto>(result.Message.Value);

                // вызываешь свою бизнес-логику отправки уведомления
            }
        }, stoppingToken);
    }
}
