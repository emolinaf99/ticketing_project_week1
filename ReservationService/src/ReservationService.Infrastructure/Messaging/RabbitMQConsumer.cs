using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ReservationService.Application.DTOs.ProcessReservation;
using ReservationService.Application.Interfaces;

namespace ReservationService.Infrastructure.Messaging;

public class RabbitMQConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMQSettings _settings;
    private readonly ILogger<RabbitMQConsumer> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMQConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMQSettings> settings,
        ILogger<RabbitMQConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _settings.Host,
            Port = _settings.Port,
            UserName = _settings.Username,
            Password = _settings.Password
        };

        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        _logger.LogInformation("Connected to RabbitMQ. Listening on queue: {Queue}", _settings.QueueName);

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            var json = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
            _logger.LogInformation("Message received: {Json}", json);

            try
            {
                var message = JsonSerializer.Deserialize<ProcessReservationCommand>(json, JsonOptions);

                if (message is not null)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var useCase = scope.ServiceProvider.GetRequiredService<IProcessReservationUseCase>();
                    var publisher = scope.ServiceProvider.GetRequiredService<IStatusChangedPublisher>();
                    var result = await useCase.HandleAsync(message, stoppingToken);

                    if (result.Success)
                    {
                        // Notificar cambio de estado a CrudService / SSE
                        await publisher.PublishAsync(message.TicketId, "reserved", stoppingToken);

                        // Encolar mensaje de expiración en la delay queue (TTL 5 min → dead-letter a ticket.expired)
                        var expirationPayload = JsonSerializer.SerializeToUtf8Bytes(
                            new { TicketId = message.TicketId });
                        var props = new BasicProperties { Persistent = true, ContentType = "application/json" };
                        await _channel!.BasicPublishAsync(
                            exchange: string.Empty,
                            routingKey: "q.ticket.reserved.delay",
                            mandatory: false,
                            basicProperties: props,
                            body: expirationPayload,
                            cancellationToken: stoppingToken);

                        _logger.LogInformation(
                            "Expiration scheduled for Ticket {TicketId} via delay queue",
                            message.TicketId);
                    }
                }

                await _channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message: {Json}", json);
                await _channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(_settings.QueueName, autoAck: false, consumer, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping consumer...");

        if (_channel is not null) await _channel.CloseAsync(cancellationToken);
        if (_connection is not null) await _connection.CloseAsync(cancellationToken);

        await base.StopAsync(cancellationToken);
    }
}
