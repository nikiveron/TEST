у меня возникает вот такая ошибка: Unhandled exception. System.AggregateException: Some services are not able to be constructed (Error while validating the service descriptor 'ServiceType: Microsoft.Extensions.Hosting.IHostedService Lifetime: Singleton ImplementationType: LinkTracker.Bot.Application.Services.KafkaConsumerService': Cannot consume scoped service 'LinkTracker.Bot.Application.Interfaces.INotificationService' from singleton 'Microsoft.Extensions.Hosting.IHostedService'.)

 ---> System.InvalidOperationException: Error while validating the service descriptor 'ServiceType: Microsoft.Extensions.Hosting.IHostedService Lifetime: Singleton ImplementationType: LinkTracker.Bot.Application.Services.KafkaConsumerService': Cannot consume scoped service 'LinkTracker.Bot.Application.Interfaces.INotificationService' from singleton 'Microsoft.Extensions.Hosting.IHostedService'.

 ---> System.InvalidOperationException: Cannot consume scoped service 'LinkTracker.Bot.Application.Interfaces.INotificationService' from singleton 'Microsoft.Extensions.Hosting.IHostedService'.

   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteValidator.VisitCallSite(ServiceCallSite callSite, CallSiteValidatorState argument)

   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteValidator.VisitConstructor(ConstructorCallSite constructorCallSite, CallSiteValidatorState state)

   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteVisitor`2.VisitCallSite(ServiceCallSite callSite, TArgument argument)

   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteValidator.VisitCallSite(ServiceCallSite callSite, CallSiteValidatorState argument)

   at Microsoft.Extensions.DependencyInjection.ServiceProvider.ValidateService(ServiceDescriptor descriptor)

   --- End of inner exception stack trace ---

   at Microsoft.Extensions.DependencyInjection.ServiceProvider.ValidateService(ServiceDescriptor descriptor)

   at Microsoft.Extensions.DependencyInjection.ServiceProvider..ctor(ICollection`1 serviceDescriptors, ServiceProviderOptions options)

   --- End of inner exception stack trace ---

   at Microsoft.Extensions.DependencyInjection.ServiceProvider..ctor(ICollection`1 serviceDescriptors, ServiceProviderOptions options)

   at Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(IServiceCollection services, ServiceProviderOptions options)

   at Microsoft.Extensions.Hosting.HostApplicationBuilder.Build()

   at Microsoft.AspNetCore.Builder.WebApplicationBuilder.Build()

   at Program.<Main>$(String[] args) in /src/LinkTracker.Bot/Program.cs:line 83

   at Program.<Main>(String[] args)

вот мой код: using LinkTracker.Bot;
using LinkTracker.Bot.Application.Commands;
using LinkTracker.Bot.Application.Interfaces;
using LinkTracker.Bot.Application.Services;
using LinkTracker.Bot.Application.StateMachine;
using LinkTracker.Bot.Application.StateMachine.States;
using LinkTracker.Bot.Application.StateMachine.States.ListStates;
using LinkTracker.Bot.Application.StateMachine.States.TrackStates;
using LinkTracker.Bot.Application.StateMachine.States.UnTrackStates;
using LinkTracker.Bot.Infrastructure.BotExtensions;
using LinkTracker.Bot.Infrastructure.Client;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

LoadEnvs.Run();
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddHealthChecks();

builder.Services.Configure<BotOptions>(builder.Configuration.GetSection("TG"));

builder.Services.AddSingleton<ITelegramBotClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<BotOptions>>().Value;
    return new TelegramBotClient(options.Token);
});

var scrapperBaseUrl = builder.Configuration.GetValue<string>("Scrapper:BaseUrl") ?? "http://scrapper:8080";

builder.Services.AddHttpClient<IScrapperClient, ScrapperClient>(client =>
{
    client.BaseAddress = new Uri(scrapperBaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestHeaders.Add("User-Agent", "LinkTracker-Bot");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = builder.Environment.IsDevelopment() ? HttpClientHandler.DangerousAcceptAnyServerCertificateValidator : null
});

builder.Services.AddHostedService<KafkaConsumerService>();

builder.Services.AddSingleton<StartCommand>();
builder.Services.AddSingleton<HelpCommand>();
builder.Services.AddSingleton<TrackCommand>();
builder.Services.AddSingleton<UnTrackCommand>();
builder.Services.AddSingleton<ListCommand>();
builder.Services.AddSingleton<CancelCommand>();
builder.Services.AddSingleton<UnknownCommand>();
builder.Services.AddSingleton<BotCommands>();

builder.Services.AddSingleton<ICommand>(sp => sp.GetRequiredService<StartCommand>());
builder.Services.AddSingleton<ICommand>(sp => sp.GetRequiredService<HelpCommand>());
builder.Services.AddSingleton<ICommand>(sp => sp.GetRequiredService<TrackCommand>());
builder.Services.AddSingleton<ICommand>(sp => sp.GetRequiredService<UnTrackCommand>());
builder.Services.AddSingleton<ICommand>(sp => sp.GetRequiredService<ListCommand>());
builder.Services.AddSingleton<ICommand>(sp => sp.GetRequiredService<CancelCommand>());

builder.Services.AddSingleton<CommandDispatcher>();
builder.Services.AddSingleton<DefaultState>();
builder.Services.AddSingleton<WaitingForAddLinkState>();
builder.Services.AddSingleton<WaitingForTagsState>();
builder.Services.AddSingleton<WaitingForTagsConfirmationState>();
builder.Services.AddSingleton<WaitingForDeleteLinkState>();
builder.Services.AddSingleton<WaitingForFilterTagsConfirmationState>();
builder.Services.AddSingleton<WaitingForFilterTagsState>();
builder.Services.AddSingleton<StateFactory>();
builder.Services.AddSingleton<UserStateManager>();

builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IUpdateValidationService, UpdateValidationService>();

builder.Services.AddControllers();

var app = builder.Build();

app.MapHealthChecks("/health");

app.UseRouting();
app.MapControllers();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
var botClient = app.Services.GetRequiredService<ITelegramBotClient>();
var userStateManager = app.Services.GetRequiredService<UserStateManager>();

var botCommands = app.Services.GetRequiredService<BotCommands>();
await botCommands.SetCommands(botClient);

using var cts = new CancellationTokenSource();

botClient.StartReceiving(
    async (client, update, token) =>
    {
        if (update.CallbackQuery != null)
        {
            var callback = update.CallbackQuery;
            var chatId = callback.Message!.Chat.Id;
            var user = userStateManager.GetUser(chatId, token);
            await user.HandleCallback(callback, token);
            return;
        }

        if (update.Type == UpdateType.Message && update.Message?.Text != null)
        {
            var chatId = update.Message!.Chat.Id;
            var user = userStateManager.GetUser(chatId, token);
            await user.HandleMessage(update, token);
            return;
        }
    },
    (client, exception, token) =>
    {
        logger.LogError("Error: {message}", exception.Message);
    },
    cancellationToken: cts.Token);

logger.LogInformation("Bot started...");

app.Run();

using Confluent.Kafka;
using LinkTracker.Bot.Application.Interfaces;
using LinkTracker.Bot.Domain.DTOs;
using System.Text.Json;

namespace LinkTracker.Bot.Application.Services;

public class KafkaConsumerService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly INotificationService _notificationService;
    private readonly ILogger<KafkaConsumerService> _logger;

    public KafkaConsumerService(IConfiguration configuration, INotificationService notificationService, ILogger<KafkaConsumerService> logger)
    {
        _configuration = configuration;
        _notificationService = notificationService;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken ct)
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
            _logger.LogInformation("Kafka consumer запущен, слушает топик {Topic}", _configuration["Kafka:Topic"]);
            while (!ct.IsCancellationRequested)
            {
                var result = consumer.Consume(ct);
                var update = JsonSerializer.Deserialize<LinkUpdateDto>(result.Message.Value);

                _notificationService.NotifyUsersAsync(update, ct);
            }
            _logger.LogInformation("Kafka consumer остановлен");
        }, ct);
    }
}
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Notification": {
    "Mode": "Kafka"
  },
  "Kafka": {
    "BootstrapServers": "kafka-1:9092,kafka-2:9093,kafka-3:9094",
    "Topic": "link-updates"
  },
  "AllowedHosts": "*",
  "Proxy": {
    "Type": "Http",
    "Host": "68.1.210.189",
    "Port": 4145,
    "Username": "",
    "Password": ""
  },
  "Scrapper": {
    "BaseUrl": "http://scrapper:8080"
  }
}
services:
  scrapper:
    build:
      context: .
      dockerfile: src/LinkTracker.Scrapper/Dockerfile
    container_name: linktracker-scrapper
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_URLS=http://+:8080
    depends_on:
      kafka-1:
        condition: service_healthy
      kafka-2:
        condition: service_healthy
      kafka-3:
        condition: service_healthy
      postgres:
        condition: service_started
    networks:
      - linktracker-network
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3

  bot:
    build:
      context: .
      dockerfile: src/LinkTracker.Bot/Dockerfile
    container_name: linktracker-bot
    ports:
      - "5000:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_URLS=http://+:80
      - Scrapper__BaseUrl=http://scrapper:8080
    dns:
    - 8.8.8.8 
    - 8.8.4.4
    depends_on:
      - scrapper
    networks:
      - linktracker-network
    env_file:
      - src/LinkTracker.Bot/.env

  postgres:
    image: postgres:16
    container_name: postgres
    restart: always
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
      POSTGRES_DB: chats_db
    ports:
      - "5432:5432"
    volumes:
      - pg_data:/var/lib/postgresql/data
    networks:
      - linktracker-network

  pgadmin:
    image: dpage/pgadmin4:latest
    container_name: pgadmin
    restart: always
    depends_on:
      - postgres
    environment:
      PGADMIN_DEFAULT_EMAIL: postgres@localhost.com
      PGADMIN_DEFAULT_PASSWORD: postgres
    ports:
      - "5050:80"
    networks:
      - linktracker-network
    volumes:
      - pgadmin_data:/var/lib/pgadmin

  liquibase-migrations:
    image: liquibase/liquibase:4.29
    depends_on:
      - postgres
    command:
      - --searchPath=/changesets
      - --changelog-file=master.xml
      - --url=jdbc:postgresql://postgres:5432/chats_db
      - --username=postgres
      - --password=postgres
      - update
    networks:
      - linktracker-network   
    volumes:
      - ./migrations:/changesets

  zookeeper:
    image: confluentinc/cp-zookeeper:7.5.0
    container_name: zookeeper
    environment:
      ZOOKEEPER_CLIENT_PORT: 2181
    networks:
      - linktracker-network

  kafka-1:
    image: confluentinc/cp-kafka:7.5.0
    container_name: kafka-1
    depends_on:
      - zookeeper
    ports:
      - "9092:9092"
    environment:
      KAFKA_BROKER_ID: 1
      KAFKA_ZOOKEEPER_CONNECT: zookeeper:2181
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://kafka-1:9092
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 3
    healthcheck:
      test: ["CMD", "kafka-broker-api-versions", "--bootstrap-server", "localhost:9092"]
      interval: 10s
      timeout: 5s
      retries: 10
    networks:
      - linktracker-network

  kafka-2:
    image: confluentinc/cp-kafka:7.5.0
    container_name: kafka-2
    depends_on:
      - zookeeper
    ports:
      - "9093:9093"
    environment:
      KAFKA_BROKER_ID: 2
      KAFKA_ZOOKEEPER_CONNECT: zookeeper:2181
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://kafka-2:9093
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 3
    healthcheck:
      test: ["CMD", "kafka-broker-api-versions", "--bootstrap-server", "localhost:9093"]
      interval: 10s
      timeout: 5s
      retries: 10
    networks:
      - linktracker-network

  kafka-3:
    image: confluentinc/cp-kafka:7.5.0
    container_name: kafka-3
    depends_on:
      - zookeeper
    ports:
      - "9094:9094"
    environment:
      KAFKA_BROKER_ID: 3
      KAFKA_ZOOKEEPER_CONNECT: zookeeper:2181
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://kafka-3:9094
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 3
    healthcheck:
      test: ["CMD", "kafka-broker-api-versions", "--bootstrap-server", "localhost:9094"]
      interval: 10s
      timeout: 5s
      retries: 10
    networks:
      - linktracker-network

  kafka-init:
    image: confluentinc/cp-kafka:7.5.0
    depends_on:
      - kafka-1
    entrypoint: [ "/bin/sh", "-c" ]
    command: >
      "
      kafka-topics --create
      --topic link-updates
      --bootstrap-server kafka-1:9092
      --replication-factor 3
      --partitions 3
      "
    networks:
      - linktracker-network

  kafka-ui:
    image: provectuslabs/kafka-ui:latest
    container_name: kafka-ui
    ports:
      - "8090:8080"
    depends_on:
      kafka-1:
        condition: service_healthy
      kafka-2:
        condition: service_healthy
      kafka-3:
        condition: service_healthy
    environment:
      KAFKA_CLUSTERS_0_NAME: linktracker-cluster
      KAFKA_CLUSTERS_0_BOOTSTRAPSERVERS: kafka-1:9092,kafka-2:9093,kafka-3:9094
    networks:
      - linktracker-network

networks:
  linktracker-network:
    driver: bridge

volumes:
  pg_data:
  pgadmin_data:
